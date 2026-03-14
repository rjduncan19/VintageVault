using System.CommandLine;
using VintageVault.Cli.Config;
using VintageVault.Cli.Engine;
using VintageVault.Cli.Graph;

namespace VintageVault.Cli.Commands;

public static class BackupCommand
{
    public static Command Create()
    {
        var command = new Command("backup", "Run a backup cycle (full or incremental)");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            Console.WriteLine();
            Console.WriteLine("VintageVault v0.1.0 — Backup Engine");
            Console.WriteLine();

            var configStore = new ConfigStore();
            var config = configStore.Load();

            if (string.IsNullOrEmpty(config.DriveId))
            {
                Console.Error.WriteLine("❌ Not authenticated. Run: vintagevault auth");
                return 1;
            }

            try
            {
                var factory = new GraphClientFactory();
                var client = await factory.CreateAuthenticatedClientAsync(dcr =>
                {
                    Console.WriteLine(dcr.Message);
                    return Task.CompletedTask;
                });

                var driveOps = new DriveOperations(client);
                var manifestManager = new ManifestManager();
                var anomalyDetector = new AnomalyDetector();

                var orchestrator = new BackupOrchestrator(
                    driveOps, configStore, manifestManager, anomalyDetector);

                // Wire up progress reporting
                orchestrator.OnStatus += msg => Console.WriteLine(msg);
                orchestrator.OnProgress += (copied, total, bytes) =>
                {
                    var pct = total > 0 ? (double)copied / total : 0;
                    var barLen = 40;
                    var filled = (int)(pct * barLen);
                    var bar = new string('\u2588', filled) + new string(' ', barLen - filled);
                    var sizeStr = FormatBytes(bytes);
                    Console.Write($"\r  [{bar}] {copied}/{total} files ({sizeStr})");
                    if (copied == total)
                        Console.WriteLine();
                };

                var result = await orchestrator.RunAsync(cancellationToken);

                Console.WriteLine();

                if (!result.Success)
                {
                    Console.Error.WriteLine($"❌ {result.ErrorMessage}");
                    return 1;
                }

                // Display results
                if (result.AnomalyResult?.Detected == true)
                {
                    Console.WriteLine("⚠️  Snapshot complete WITH WARNINGS");
                    Console.WriteLine("   Recommend reviewing files before relying on this snapshot.");
                }
                else
                {
                    Console.WriteLine($"✅ {(result.SnapshotType == "full" ? "Full" : "Incremental")} snapshot complete!");
                }

                Console.WriteLine($"   Snapshot: {result.SnapshotId}");

                if (result.SnapshotType == "incremental")
                {
                    Console.WriteLine($"   Changed:  {result.FilesModified} modified, {result.FilesAdded} added, {result.FilesDeleted} deleted");
                }

                Console.WriteLine($"   Copied:   {result.FilesCopied} files ({FormatBytes(result.BytesCopied)})");

                if (result.FilesSkipped > 0)
                    Console.WriteLine($"   Skipped:  {result.FilesSkipped} files (filtered)");

                Console.WriteLine($"   Duration: {FormatDuration(result.Duration)}");
                Console.WriteLine($"   Location: OneDrive/VintageVault-Backup/{result.SnapshotId}/");
                return 0;
            }
            catch (Exception)
            {
                // Safe error handling — never expose raw exception messages
                Console.Error.WriteLine("❌ Unexpected error. Please report at github.com/vintagevault/vintagevault");
                return 1;
            }
        });

        return command;
    }

    internal static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    internal static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{ts.TotalSeconds:F0} sec";
        if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes} min {ts.Seconds} sec";
        return $"{(int)ts.TotalHours} hr {ts.Minutes} min";
    }
}
