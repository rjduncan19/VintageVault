using Microsoft.Graph.Models;
using VintageVault.Cli.Config;
using VintageVault.Cli.Graph;

namespace VintageVault.Cli.Engine;

/// <summary>
/// Result of a backup run, used for CLI display.
/// </summary>
public sealed class BackupResult
{
    public bool Success { get; set; }
    public string SnapshotId { get; set; } = "";
    public string SnapshotType { get; set; } = "";
    public int FilesModified { get; set; }
    public int FilesAdded { get; set; }
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesCopied { get; set; }
    public long BytesCopied { get; set; }
    public TimeSpan Duration { get; set; }
    public AnomalyResult? AnomalyResult { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Core backup flow orchestrator.
/// Coordinates delta detection, filtering, anomaly checks, file copying, and manifest writing.
/// </summary>
public sealed class BackupOrchestrator
{
    private readonly DriveOperations _driveOps;
    private readonly ConfigStore _configStore;
    private readonly ManifestManager _manifestManager;
    private readonly AnomalyDetector _anomalyDetector;
    private const int MaxConcurrentCopies = 8;

    public BackupOrchestrator(
        DriveOperations driveOps,
        ConfigStore configStore,
        ManifestManager manifestManager,
        AnomalyDetector anomalyDetector)
    {
        _driveOps = driveOps;
        _configStore = configStore;
        _manifestManager = manifestManager;
        _anomalyDetector = anomalyDetector;
    }

    /// <summary>
    /// Progress callback: (filesCopied, totalFiles, bytesCopied)
    /// </summary>
    public event Action<int, int, long>? OnProgress;

    /// <summary>
    /// Status message callback
    /// </summary>
    public event Action<string>? OnStatus;

    public async Task<BackupResult> RunAsync(CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var config = _configStore.Load();

        if (string.IsNullOrEmpty(config.DriveId))
            return new BackupResult { Success = false, ErrorMessage = "Not authenticated. Run: vintagevault auth" };

        var driveId = config.DriveId;

        // Step 1: Ensure backup root exists
        OnStatus?.Invoke("Ensuring backup root folder exists...");
        var backupRoot = await _driveOps.EnsureBackupRootAsync(driveId, "VintageVault-Backup", ct)
            .ConfigureAwait(false);

        if (backupRoot?.Id is null)
            return new BackupResult { Success = false, ErrorMessage = "Failed to create backup root folder." };

        // Step 2: Load existing manifest
        OnStatus?.Invoke("Loading backup manifest...");
        BackupManifest? existingManifest = null;
        var manifestJson = await _driveOps.ReadFileContentAsync(driveId, backupRoot.Id, "manifest.json", ct)
            .ConfigureAwait(false);
        if (manifestJson is not null)
            existingManifest = _manifestManager.DeserializeManifest(manifestJson);

        // Step 3: Determine full vs incremental
        bool isFull = string.IsNullOrEmpty(config.LastDeltaToken);
        string snapshotType = isFull ? "full" : "incremental";
        string snapshotId = isFull
            ? $"{DateTime.UtcNow:yyyy-MM-dd}-full"
            : $"{DateTime.UtcNow:yyyy-MM-dd}";

        // Avoid duplicate snapshot IDs
        if (existingManifest?.Snapshots.Any(s => s.Id == snapshotId) == true)
        {
            int suffix = 2;
            while (existingManifest.Snapshots.Any(s => s.Id == $"{snapshotId}-{suffix}"))
                suffix++;
            snapshotId = $"{snapshotId}-{suffix}";
        }

        string? previousSnapshot = existingManifest?.Snapshots.LastOrDefault()?.Id;

        if (isFull)
            OnStatus?.Invoke("No existing backup found. Creating initial full snapshot...");
        else
            OnStatus?.Invoke($"Last snapshot: {previousSnapshot}");

        // Step 4: Call delta API
        OnStatus?.Invoke(isFull ? "Scanning OneDrive..." : "Checking for changes...");
        var (deltaChanges, newDeltaToken) = await _driveOps.GetDeltaAsync(
            driveId, isFull ? null : config.LastDeltaToken, ct).ConfigureAwait(false);

        // Filter to files only (exclude folders, the backup root, etc.)
        var filterEngine = new FilterEngine(config.Filters);
        var allFileChanges = new List<(DriveItem Item, string Path, string Action)>();
        var skippedChanges = new List<(string Path, string Reason)>();

        foreach (var item in deltaChanges)
        {
            // Build path from parentReference + name
            var path = BuildItemPath(item);
            if (string.IsNullOrEmpty(path))
                continue;

            if (item.Deleted is not null)
            {
                // Deleted items
                var skipReason = filterEngine.GetSkipReason(path);
                if (skipReason is null)
                    allFileChanges.Add((item, path, "deleted"));
                else
                    skippedChanges.Add((path, skipReason));
                continue;
            }

            if (item.Folder is not null)
                continue; // Skip folder entries

            if (item.File is null)
                continue; // Skip non-file entries

            var reason = filterEngine.GetSkipReason(path);
            if (reason is not null)
            {
                skippedChanges.Add((path, reason));
                continue;
            }

            // Determine action
            string action = isFull ? "added" : "modified";
            allFileChanges.Add((item, path, action));
        }

        int totalChanges = allFileChanges.Count + skippedChanges.Count;
        int deleteCount = allFileChanges.Count(c => c.Action == "deleted");
        int filesToCopy = allFileChanges.Count(c => c.Action != "deleted");

        OnStatus?.Invoke($"  Found {totalChanges} changes ({filesToCopy} files to copy, {deleteCount} deletions)");

        // Step 5: Anomaly detection
        var anomalyResult = _anomalyDetector.Analyze(deltaChanges, config.DetectionBaseline);
        if (anomalyResult.Detected)
        {
            OnStatus?.Invoke("⚠️  ANOMALY DETECTED");
            foreach (var warning in anomalyResult.Warnings)
                OnStatus?.Invoke($"  • {warning}");
        }
        else
        {
            OnStatus?.Invoke($"Anomaly check: OK ({totalChanges} changes vs. baseline ~{config.DetectionBaseline.AvgChangesPerCycle:F0}/cycle)");
        }

        // Step 6: Create snapshot folder
        OnStatus?.Invoke($"Creating snapshot folder: /VintageVault-Backup/{snapshotId}/");
        var snapshotFolder = await _driveOps.CreateFolderAsync(driveId, backupRoot.Id, snapshotId, ct)
            .ConfigureAwait(false);

        if (snapshotFolder?.Id is null)
            return new BackupResult { Success = false, ErrorMessage = "Failed to create snapshot folder." };

        // Step 7: Copy files with concurrency control
        var snapshotChanges = new List<SnapshotChange>();
        int copiedCount = 0;
        long totalBytesCopied = 0;
        var semaphore = new SemaphoreSlim(MaxConcurrentCopies);
        var copyTasks = new List<Task>();

        // Track folder IDs we've created in the snapshot
        var createdFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        createdFolders[""] = snapshotFolder.Id;
        createdFolders["/"] = snapshotFolder.Id;
        var folderLock = new SemaphoreSlim(1, 1);

        foreach (var (item, path, action) in allFileChanges)
        {
            if (action == "deleted")
            {
                snapshotChanges.Add(new SnapshotChange
                {
                    Path = path,
                    Action = "deleted",
                    PreviousItemId = item.Id
                });
                continue;
            }

            // Copy file
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            var capturedItem = item;
            var capturedPath = path;
            var capturedAction = action;

            var task = Task.Run(async () =>
            {
                try
                {
                    // Ensure parent folder structure exists in snapshot
                    var parentFolderId = await EnsureParentFoldersAsync(
                        driveId, snapshotFolder.Id, capturedPath, createdFolders, folderLock, ct)
                        .ConfigureAwait(false);

                    // Copy the file
                    var fileName = Path.GetFileName(capturedPath);
                    await _driveOps.CopyItemAsync(driveId, capturedItem.Id!, parentFolderId, fileName, ct)
                        .ConfigureAwait(false);

                    // Record hashes from source
                    var sourceHashes = new FileHashes();
                    if (capturedItem.File?.Hashes is not null)
                    {
                        sourceHashes.Sha1 = capturedItem.File.Hashes.Sha1Hash;
                        sourceHashes.QuickXorHash = capturedItem.File.Hashes.QuickXorHash;
                    }

                    var fileSize = capturedItem.Size ?? 0;
                    Interlocked.Add(ref totalBytesCopied, fileSize);
                    var copied = Interlocked.Increment(ref copiedCount);

                    snapshotChanges.Add(new SnapshotChange
                    {
                        Path = capturedPath,
                        Action = capturedAction,
                        Size = fileSize,
                        ItemId = capturedItem.Id,
                        LastModified = capturedItem.LastModifiedDateTime?.ToString("o"),
                        Hashes = sourceHashes,
                        CopyVerified = true // In POC, trust server-side copy integrity
                    });

                    OnProgress?.Invoke(copied, filesToCopy, Interlocked.Read(ref totalBytesCopied));
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct);

            copyTasks.Add(task);
        }

        await Task.WhenAll(copyTasks).ConfigureAwait(false);

        // Add skipped files to snapshot changes
        foreach (var (path, reason) in skippedChanges)
        {
            snapshotChanges.Add(new SnapshotChange
            {
                Path = path,
                Action = "skipped",
                SkipReason = reason
            });
        }

        // Step 8: Build and write _snapshot.json
        int filesModified = allFileChanges.Count(c => c.Action == "modified");
        int filesAdded = allFileChanges.Count(c => c.Action == "added");

        var snapshot = new SnapshotManifest
        {
            SnapshotId = snapshotId,
            Type = snapshotType,
            PreviousSnapshot = isFull ? null : previousSnapshot,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Summary = new SnapshotSummary
            {
                FilesChanged = filesModified,
                FilesAdded = filesAdded,
                FilesDeleted = deleteCount,
                TotalFilesCopied = copiedCount,
                TotalBytesCopied = totalBytesCopied
            },
            Changes = snapshotChanges
        };

        OnStatus?.Invoke("Writing snapshot metadata...");
        var snapshotJson = _manifestManager.SerializeSnapshot(snapshot);
        await _driveOps.WriteFileAsync(driveId, snapshotFolder.Id, "_snapshot.json", snapshotJson, ct)
            .ConfigureAwait(false);

        // Step 9: Write _warning.json if anomaly detected
        if (anomalyResult.Detected)
        {
            var warning = new AnomalyWarning
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                SnapshotId = snapshotId,
                Warnings = anomalyResult.Warnings,
                Details = anomalyResult.Details
            };
            var warningJson = _manifestManager.SerializeWarning(warning);
            await _driveOps.WriteFileAsync(driveId, snapshotFolder.Id, "_warning.json", warningJson, ct)
                .ConfigureAwait(false);
        }

        // Step 10: Update manifest.json
        double newAvgChanges = UpdateRunningAverage(
            config.DetectionBaseline.AvgChangesPerCycle,
            totalChanges,
            existingManifest?.Snapshots.Count ?? 0);

        double newAvgDeletions = UpdateRunningAverage(
            config.DetectionBaseline.AvgDeletionsPerCycle,
            deleteCount,
            existingManifest?.Snapshots.Count ?? 0);

        var updatedManifest = _manifestManager.CreateOrUpdateManifest(
            existingManifest, snapshot, newDeltaToken,
            newAvgChanges, newAvgDeletions, anomalyResult.Detected);

        var updatedManifestJson = _manifestManager.SerializeManifest(updatedManifest);
        await _driveOps.WriteFileAsync(driveId, backupRoot.Id, "manifest.json", updatedManifestJson, ct)
            .ConfigureAwait(false);

        // Step 11: Update config
        config.LastDeltaToken = newDeltaToken;
        config.LastBackupTimestamp = DateTime.UtcNow.ToString("o");
        config.DetectionBaseline.AvgChangesPerCycle = newAvgChanges;
        config.DetectionBaseline.AvgDeletionsPerCycle = newAvgDeletions;
        _configStore.Save(config);

        var duration = DateTime.UtcNow - startTime;

        return new BackupResult
        {
            Success = true,
            SnapshotId = snapshotId,
            SnapshotType = snapshotType,
            FilesModified = filesModified,
            FilesAdded = filesAdded,
            FilesDeleted = deleteCount,
            FilesSkipped = skippedChanges.Count,
            FilesCopied = copiedCount,
            BytesCopied = totalBytesCopied,
            Duration = duration,
            AnomalyResult = anomalyResult
        };
    }

    private async Task<string> EnsureParentFoldersAsync(
        string driveId, string snapshotFolderId, string filePath,
        Dictionary<string, string> createdFolders, SemaphoreSlim folderLock,
        CancellationToken ct)
    {
        // Extract parent path: "/Documents/Work/report.docx" -> "/Documents/Work"
        var lastSlash = filePath.LastIndexOf('/');
        var parentPath = lastSlash > 0 ? filePath[..lastSlash] : "";

        if (string.IsNullOrEmpty(parentPath) || parentPath == "/")
            return snapshotFolderId;

        await folderLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (createdFolders.TryGetValue(parentPath, out var existingId))
                return existingId;

            // Build parent folders from root down
            var parts = parentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";
            var currentParentId = snapshotFolderId;

            foreach (var part in parts)
            {
                currentPath += "/" + part;

                if (createdFolders.TryGetValue(currentPath, out var folderId))
                {
                    currentParentId = folderId;
                    continue;
                }

                var folder = await _driveOps.CreateFolderAsync(driveId, currentParentId, part, ct)
                    .ConfigureAwait(false);

                if (folder?.Id is not null)
                {
                    createdFolders[currentPath] = folder.Id;
                    currentParentId = folder.Id;
                }
            }

            return currentParentId;
        }
        finally
        {
            folderLock.Release();
        }
    }

    private static string BuildItemPath(DriveItem item)
    {
        if (item.ParentReference?.Path is not null && item.Name is not null)
        {
            var parentPath = item.ParentReference.Path;
            // The parentReference.Path looks like "/drive/root:/Documents/Work"
            // We need just the "/Documents/Work" part
            var rootPrefix = "/drive/root:";
            if (parentPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                parentPath = parentPath[rootPrefix.Length..];
            else if (parentPath == "/drive/root")
                parentPath = "";

            return parentPath + "/" + item.Name;
        }

        // For deleted items, try name only
        if (item.Name is not null)
            return "/" + item.Name;

        return "";
    }

    private static double UpdateRunningAverage(double currentAvg, int newValue, int cycleCount)
    {
        if (cycleCount <= 0)
            return newValue;

        // Exponential moving average with alpha = 0.3
        return currentAvg * 0.7 + newValue * 0.3;
    }
}
