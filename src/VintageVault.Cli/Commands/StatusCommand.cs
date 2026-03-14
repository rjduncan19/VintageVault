using System.CommandLine;
using System.Text.Json;
using VintageVault.Cli.Config;
using VintageVault.Cli.Engine;
using VintageVault.Cli.Graph;

namespace VintageVault.Cli.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Show last backup info");

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

                // Find backup root
                var backupRoot = await driveOps.EnsureBackupRootAsync(config.DriveId, "VintageVault-Backup", cancellationToken);
                if (backupRoot?.Id is null)
                {
                    Console.WriteLine("📂 No backup found. Run: vintagevault backup");
                    return 0;
                }

                // Read manifest
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

                Console.WriteLine($"Account:     {config.AccountId ?? "unknown"}");
                Console.WriteLine($"Backup root: OneDrive/VintageVault-Backup/");
                Console.WriteLine();
                Console.WriteLine("Snapshots:");

                long totalBytes = 0;
                foreach (var snap in manifest.Snapshots)
                {
                    var sizeBytes = snap.Type == "full" ? snap.TotalBytes : snap.BytesCopied;
                    totalBytes += sizeBytes;
                    var sizeStr = BackupCommand.FormatBytes(sizeBytes);
                    var typeStr = snap.Type.PadRight(13);

                    var fileInfo = snap.Type == "full"
                        ? $"{snap.FileCount} files"
                        : $"{snap.ChangesCount} changes";

                    var warningStr = snap.HasWarning ? " ⚠️ WARNING" : "";

                    Console.WriteLine($"  📦 {snap.Id,-22} {typeStr} {fileInfo,-15} {sizeStr}{warningStr}");
                }

                Console.WriteLine();
                Console.WriteLine($"Total backup size: ~{BackupCommand.FormatBytes(totalBytes)}");

                if (!string.IsNullOrEmpty(config.LastBackupTimestamp))
                {
                    if (DateTimeOffset.TryParse(config.LastBackupTimestamp, out var lastBackup))
                    {
                        var ago = DateTime.UtcNow - lastBackup.UtcDateTime;
                        var agoStr = ago.TotalDays >= 1 ? $"{(int)ago.TotalDays} days ago" : "today";
                        Console.WriteLine($"Last backup: {lastBackup:yyyy-MM-dd} ({agoStr})");
                    }
                }

                Console.WriteLine($"Detection baseline: ~{manifest.DetectionBaseline.AvgChangesPerCycle:F0} changes/cycle");
                Console.WriteLine();
                Console.WriteLine("Browse your backup: Open OneDrive → VintageVault-Backup");
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
