using System.CommandLine;
using VintageVault.Cli.Config;
using VintageVault.Cli.Engine;
using VintageVault.Cli.Graph;

namespace VintageVault.Cli.Commands;

public static class SnapshotsCommand
{
    public static Command Create()
    {
        var command = new Command("snapshots", "List all snapshots");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            Console.WriteLine();
            Console.WriteLine("VintageVault v0.1.0");
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
                var backupRoot = await driveOps.EnsureBackupRootAsync(config.DriveId, "VintageVault-Backup", cancellationToken);

                if (backupRoot?.Id is null)
                {
                    Console.WriteLine("📂 No backup found. Run: vintagevault backup");
                    return 0;
                }

                var manifestJson = await driveOps.ReadFileContentAsync(
                    config.DriveId, backupRoot.Id, "manifest.json", cancellationToken);

                if (manifestJson is null)
                {
                    Console.WriteLine("📂 No backup manifest found. Run: vintagevault backup");
                    return 0;
                }

                var manifestManager = new ManifestManager();
                var manifest = manifestManager.DeserializeManifest(manifestJson);

                if (manifest is null)
                {
                    Console.Error.WriteLine("❌ Config file missing or corrupted. Run: vintagevault auth");
                    return 1;
                }

                // Table header
                Console.WriteLine($"{"ID",-22} {"Type",-14} {"Files",-9} {"Size",-10} {"Status"}");
                Console.WriteLine(new string('\u2500', 65));

                long totalBytes = 0;
                string? earliestId = null;

                foreach (var snap in manifest.Snapshots)
                {
                    earliestId ??= snap.Id;

                    var sizeBytes = snap.Type == "full" ? snap.TotalBytes : snap.BytesCopied;
                    totalBytes += sizeBytes;
                    var sizeStr = BackupCommand.FormatBytes(sizeBytes);

                    var fileCount = snap.Type == "full" ? snap.FileCount : snap.ChangesCount;

                    var status = snap.HasWarning
                        ? "⚠️ Anomaly detected"
                        : "✅ Clean";

                    Console.WriteLine($"{snap.Id,-22} {snap.Type,-14} {fileCount,-9} {sizeStr,-10} {status}");
                }

                Console.WriteLine();
                Console.WriteLine($"Total snapshots: {manifest.Snapshots.Count}");
                Console.WriteLine($"Total backup size: ~{BackupCommand.FormatBytes(totalBytes)}");

                if (earliestId is not null)
                    Console.WriteLine($"Earliest recovery point: {earliestId}");

                return 0;
            }
            catch (Exception)
            {
                Console.Error.WriteLine("❌ Unexpected error. Please report at github.com/vintagevault/vintagevault");
                return 1;
            }
        });

        return command;
    }
}
