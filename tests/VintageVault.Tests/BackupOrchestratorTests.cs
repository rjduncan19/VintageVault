using VintageVault.Cli.Config;
using VintageVault.Cli.Engine;
using VintageVault.Tests.Fakes;

namespace VintageVault.Tests;

public class BackupOrchestratorTests : IDisposable
{
    private const string Backup = "/VintageVault-Backup";

    private readonly string _tempDir;
    private readonly ConfigStore _configStore;
    private readonly FakeDrive _drive = new();
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 3, 14, 3, 0, 0, TimeSpan.Zero));
    private readonly ManifestManager _manifests = new();

    public BackupOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VintageVault-Tests-" + Guid.NewGuid().ToString("N")[..8]);
        _configStore = new ConfigStore(_tempDir);
        _configStore.Save(new VintageVaultConfig { DriveId = "drive-1" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private BackupOrchestrator CreateOrchestrator()
        => new(_drive, _configStore, _manifests, new AnomalyDetector(), _time);

    private Task<BackupResult> RunAsync() => CreateOrchestrator().RunAsync();

    private SnapshotManifest ReadSnapshot(string snapshotId)
    {
        var json = _drive.ReadText($"{Backup}/{snapshotId}/_snapshot.json");
        Assert.NotNull(json);
        return _manifests.DeserializeSnapshot(json!)!;
    }

    private BackupManifest ReadManifest()
    {
        var json = _drive.ReadText($"{Backup}/manifest.json");
        Assert.NotNull(json);
        return _manifests.DeserializeManifest(json!)!;
    }

    private void UpdateConfig(Action<VintageVaultConfig> change)
    {
        var config = _configStore.Load();
        change(config);
        _configStore.Save(config);
    }

    #region Preconditions and failures

    [Fact]
    public async Task Run_NotAuthenticated_FailsWithoutTouchingDrive()
    {
        _configStore.Save(new VintageVaultConfig()); // no DriveId

        var result = await RunAsync();

        Assert.False(result.Success);
        Assert.Contains("Not authenticated", result.ErrorMessage);
        Assert.Equal(0, _drive.TotalCalls);
    }

    [Fact]
    public async Task Run_BackupRootCannotBeCreated_Fails()
    {
        _drive.FailEnsureBackupRoot = true;

        var result = await RunAsync();

        Assert.False(result.Success);
        Assert.Equal("Failed to create backup root folder.", result.ErrorMessage);
        Assert.Empty(_drive.DeltaTokensRequested);
    }

    [Fact]
    public async Task Run_SnapshotFolderCannotBeCreated_FailsAndKeepsDeltaToken()
    {
        UpdateConfig(c => c.LastDeltaToken = "old-token");
        _drive.EnqueueDelta("new-token", FakeDrive.File("f1", "/a.txt"));
        _drive.FailCreateFolderNames.Add("2026-03-14");

        var result = await RunAsync();

        Assert.False(result.Success);
        Assert.Equal("Failed to create snapshot folder.", result.ErrorMessage);
        Assert.Empty(_drive.Copies);
        // The token must not advance, or the next run would silently skip these changes.
        Assert.Equal("old-token", _configStore.Load().LastDeltaToken);
    }

    #endregion

    #region Full snapshot

    [Fact]
    public async Task FirstRun_CreatesFullSnapshotMirroringFolderStructure()
    {
        _drive.EnqueueDelta("token-1",
            FakeDrive.Folder("d1", "/Documents"),
            FakeDrive.File("f1", "/Documents/Tax Returns/2025.pdf", size: 1000, sha1: "SHA-A"),
            FakeDrive.File("f2", "/Documents/Family Budget.xlsx", size: 200),
            FakeDrive.File("f3", "/Photos/Vacation/photo001.jpg", size: 3000, quickXor: "QX-C"),
            FakeDrive.File("f4", "/readme.txt", size: 5));

        var result = await RunAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("2026-03-14-full", result.SnapshotId);
        Assert.Equal("full", result.SnapshotType);
        Assert.Equal(4, result.FilesAdded);
        Assert.Equal(4, result.FilesCopied);
        Assert.Equal(4205, result.BytesCopied);

        var snap = $"{Backup}/2026-03-14-full";
        Assert.Equal("f1", _drive.Find($"{snap}/Documents/Tax Returns/2025.pdf")?.CopiedFromId);
        Assert.Equal("f2", _drive.Find($"{snap}/Documents/Family Budget.xlsx")?.CopiedFromId);
        Assert.Equal("f3", _drive.Find($"{snap}/Photos/Vacation/photo001.jpg")?.CopiedFromId);
        Assert.Equal("f4", _drive.Find($"{snap}/readme.txt")?.CopiedFromId);
        Assert.Null(_drive.Find($"{snap}/_warning.json"));

        Assert.Null(_drive.DeltaTokensRequested.Single()); // full enumeration
    }

    [Fact]
    public async Task FirstRun_WritesSnapshotMetadataWithHashes()
    {
        _drive.EnqueueDelta("token-1",
            FakeDrive.File("f1", "/a.txt", size: 10, sha1: "SHA-A", quickXor: "QX-A"),
            FakeDrive.File("f2", "/b.txt", size: 20));

        await RunAsync();

        var snapshot = ReadSnapshot("2026-03-14-full");
        Assert.Equal("full", snapshot.Type);
        Assert.Null(snapshot.PreviousSnapshot);
        Assert.Equal(2, snapshot.Summary.FilesAdded);
        Assert.Equal(2, snapshot.Summary.TotalFilesCopied);
        Assert.Equal(30, snapshot.Summary.TotalBytesCopied);

        var a = snapshot.Changes.Single(c => c.Path == "/a.txt");
        Assert.Equal("added", a.Action);
        Assert.Equal("f1", a.ItemId);
        Assert.Equal(10, a.Size);
        Assert.Equal("SHA-A", a.Hashes?.Sha1);
        Assert.Equal("QX-A", a.Hashes?.QuickXorHash);
    }

    [Fact]
    public async Task FirstRun_WritesManifestAndPersistsConfig()
    {
        _drive.EnqueueDelta("token-1", FakeDrive.File("f1", "/a.txt", size: 10));

        await RunAsync();

        var manifest = ReadManifest();
        var entry = Assert.Single(manifest.Snapshots);
        Assert.Equal("2026-03-14-full", entry.Id);
        Assert.Equal(1, entry.FileCount);
        Assert.Equal(10, entry.TotalBytes);
        Assert.Equal("token-1", manifest.LastDeltaToken);

        var config = _configStore.Load();
        Assert.Equal("token-1", config.LastDeltaToken);
        Assert.Equal(_time.Now.UtcDateTime.ToString("o"), config.LastBackupTimestamp);
    }

    [Fact]
    public async Task FirstRun_CreatesSharedParentFolderOnlyOnce()
    {
        var files = Enumerable.Range(0, 50)
            .Select(i => FakeDrive.File($"f{i}", $"/Shared/Deep/file{i}.txt"))
            .ToArray();
        _drive.EnqueueDelta("token-1", files);
        _drive.CopyDelay = TimeSpan.FromMilliseconds(2);

        await RunAsync();

        // Duplicate creates would show up as sibling folders (Graph renames on conflict).
        Assert.Equal(new[] { "Shared", "_snapshot.json" }, _drive.ChildNames($"{Backup}/2026-03-14-full"));
        Assert.Equal(new[] { "Deep" }, _drive.ChildNames($"{Backup}/2026-03-14-full/Shared"));
        Assert.Equal(50, _drive.ChildNames($"{Backup}/2026-03-14-full/Shared/Deep").Count);
    }

    #endregion

    #region Incremental snapshot

    [Fact]
    public async Task SecondRun_IsIncrementalAndUsesStoredDeltaToken()
    {
        _drive.EnqueueDelta("token-1", FakeDrive.File("f1", "/a.txt"), FakeDrive.File("f2", "/b.txt"));
        await RunAsync();

        _time.Now = _time.Now.AddDays(7);
        _drive.EnqueueDelta("token-2",
            FakeDrive.File("f1", "/a.txt", size: 999),
            FakeDrive.Deleted("f2", "b.txt"));

        var result = await RunAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("2026-03-21", result.SnapshotId);
        Assert.Equal("incremental", result.SnapshotType);
        Assert.Equal(new string?[] { null, "token-1" }, _drive.DeltaTokensRequested.ToArray());

        var snapshot = ReadSnapshot("2026-03-21");
        Assert.Equal("2026-03-14-full", snapshot.PreviousSnapshot);
        Assert.Equal(1, snapshot.Summary.FilesChanged);
        Assert.Equal(1, snapshot.Summary.FilesDeleted);
        Assert.Equal(1, snapshot.Summary.TotalFilesCopied);

        var modified = snapshot.Changes.Single(c => c.Path == "/a.txt");
        Assert.Equal("modified", modified.Action);
        var deleted = snapshot.Changes.Single(c => c.Path == "/b.txt");
        Assert.Equal("deleted", deleted.Action);
        Assert.Equal("f2", deleted.PreviousItemId);

        // Only the modified file lands in the incremental folder.
        Assert.Equal(new[] { "_snapshot.json", "a.txt" }, _drive.ChildNames($"{Backup}/2026-03-21"));
        Assert.Equal("token-2", _configStore.Load().LastDeltaToken);
    }

    [Fact]
    public async Task SecondRun_LeavesPreviousSnapshotUntouched()
    {
        _drive.EnqueueDelta("token-1", FakeDrive.File("f1", "/a.txt"));
        await RunAsync();
        var fullSnapshotJson = _drive.ReadText($"{Backup}/2026-03-14-full/_snapshot.json");
        var fullChildren = _drive.ChildNames($"{Backup}/2026-03-14-full");

        _time.Now = _time.Now.AddDays(1);
        _drive.EnqueueDelta("token-2", FakeDrive.File("f1", "/a.txt", size: 5), FakeDrive.File("f9", "/new.txt"));
        await RunAsync();

        Assert.Equal(fullSnapshotJson, _drive.ReadText($"{Backup}/2026-03-14-full/_snapshot.json"));
        Assert.Equal(fullChildren, _drive.ChildNames($"{Backup}/2026-03-14-full"));
        Assert.Equal(2, ReadManifest().Snapshots.Count);
    }

    [Fact]
    public async Task SameDayRuns_GetUniqueSnapshotIds()
    {
        _drive.EnqueueDelta("t1", FakeDrive.File("f1", "/a.txt"));
        _drive.EnqueueDelta("t2", FakeDrive.File("f1", "/a.txt"));
        _drive.EnqueueDelta("t3", FakeDrive.File("f1", "/a.txt"));
        _drive.EnqueueDelta("t4", FakeDrive.File("f1", "/a.txt"));

        var ids = new List<string>();
        for (int i = 0; i < 4; i++)
            ids.Add((await RunAsync()).SnapshotId);

        Assert.Equal(new[] { "2026-03-14-full", "2026-03-14", "2026-03-14-2", "2026-03-14-3" }, ids);
        Assert.Equal(ids, ReadManifest().Snapshots.Select(s => s.Id));
    }

    #endregion

    #region Filtering

    [Fact]
    public async Task Run_SkipsFoldersBackupRootTempFilesAndExcludedPaths()
    {
        UpdateConfig(c => c.Filters = new FilterConfig
        {
            Mode = "exclude",
            Rules = { new FilterRule { Type = "folder", Path = "/Videos" } }
        });
        _drive.EnqueueDelta("token-1",
            FakeDrive.Folder("d1", "/Docs"),
            FakeDrive.File("keep", "/Docs/keep.txt"),
            FakeDrive.File("vid", "/Videos/big.mp4", size: 1_000_000),
            FakeDrive.File("own", "/VintageVault-Backup/2026-03-01-full/old.txt"),
            FakeDrive.File("tmp", "/Docs/~$draft.docx"));

        var result = await RunAsync();

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(3, result.FilesSkipped);
        Assert.Equal("keep", Assert.Single(_drive.Copies).SourceId);

        var skipped = ReadSnapshot("2026-03-14-full").Changes
            .Where(c => c.Action == "skipped")
            .ToDictionary(c => c.Path, c => c.SkipReason);
        Assert.Equal(3, skipped.Count);
        Assert.Equal("backup folder excluded", skipped["/VintageVault-Backup/2026-03-01-full/old.txt"]);
        Assert.Equal("temporary file excluded", skipped["/Docs/~$draft.docx"]);
        Assert.NotNull(skipped["/Videos/big.mp4"]);
    }

    #endregion

    #region Anomaly detection and baseline

    [Fact]
    public async Task Run_MassDeletion_WritesWarningAndFlagsManifest()
    {
        UpdateConfig(c =>
        {
            c.LastDeltaToken = "token-0";
            c.DetectionBaseline = new DetectionBaseline { AvgChangesPerCycle = 2, AvgDeletionsPerCycle = 1 };
        });
        var deletions = Enumerable.Range(0, 20).Select(i => FakeDrive.Deleted($"d{i}", $"gone{i}.txt")).ToArray();
        _drive.EnqueueDelta("token-1", deletions);

        var result = await RunAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.AnomalyResult?.Detected);

        var warningJson = _drive.ReadText($"{Backup}/2026-03-14/_warning.json");
        Assert.NotNull(warningJson);
        Assert.Contains("MASS DELETION", warningJson);
        Assert.True(Assert.Single(ReadManifest().Snapshots).HasWarning);
    }

    [Fact]
    public async Task Run_UpdatesDetectionBaselineAsMovingAverage()
    {
        _drive.EnqueueDelta("t1", Enumerable.Range(0, 10).Select(i => FakeDrive.File($"f{i}", $"/f{i}.txt")).ToArray());
        await RunAsync();
        Assert.Equal(10, _configStore.Load().DetectionBaseline.AvgChangesPerCycle);

        _drive.EnqueueDelta("t2", FakeDrive.File("f0", "/f0.txt"), FakeDrive.Deleted("f1", "f1.txt"));
        await RunAsync();

        var baseline = _configStore.Load().DetectionBaseline;
        Assert.Equal(10 * 0.7 + 2 * 0.3, baseline.AvgChangesPerCycle, precision: 10);
        Assert.Equal(0 * 0.7 + 1 * 0.3, baseline.AvgDeletionsPerCycle, precision: 10);
        Assert.Equal(baseline.AvgChangesPerCycle, ReadManifest().DetectionBaseline.AvgChangesPerCycle, precision: 10);
    }

    #endregion

    #region Concurrency

    [Fact]
    public async Task Run_ParallelCopies_RecordEveryChangeExactlyOnce()
    {
        // Regression: copy tasks used to append to a shared List<T> without synchronization,
        // and deletion records were appended while copies were still in flight.
        const int fileCount = 400;
        var changes = new List<Microsoft.Graph.Models.DriveItem>();
        for (int i = 0; i < fileCount; i++)
        {
            changes.Add(FakeDrive.File($"f{i}", $"/dir{i % 7}/file{i}.txt", size: i + 1));
            if (i % 4 == 0)
                changes.Add(FakeDrive.Deleted($"x{i}", $"deleted{i}.txt"));
        }
        UpdateConfig(c => c.LastDeltaToken = "token-0");
        _drive.EnqueueDelta("token-1", changes.ToArray());
        _drive.CopyDelay = TimeSpan.FromMilliseconds(1);

        var result = await RunAsync();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.InRange(_drive.MaxConcurrentCopies, 2, 8);
        Assert.Equal(fileCount, result.FilesCopied);
        Assert.Equal(Enumerable.Range(1, fileCount).Sum(i => (long)i), result.BytesCopied);

        var snapshot = ReadSnapshot("2026-03-14");
        var copied = snapshot.Changes.Where(c => c.Action == "modified").Select(c => c.Path).ToList();
        var deleted = snapshot.Changes.Where(c => c.Action == "deleted").Select(c => c.Path).ToList();
        Assert.Equal(fileCount, copied.Count);
        Assert.Equal(fileCount, copied.Distinct().Count());
        Assert.Equal(fileCount / 4, deleted.Count);
        Assert.Equal(fileCount, _drive.Copies.Count);
    }

    #endregion
}
