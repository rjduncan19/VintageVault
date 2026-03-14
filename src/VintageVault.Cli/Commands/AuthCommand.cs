using System.CommandLine;
using VintageVault.Cli.Config;
using VintageVault.Cli.Graph;

namespace VintageVault.Cli.Commands;

public static class AuthCommand
{
    public static Command Create()
    {
        var command = new Command("auth", "Authenticate with Microsoft OneDrive");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            Console.WriteLine();
            Console.WriteLine("VintageVault v0.1.0");
            Console.WriteLine();

            var configStore = new ConfigStore();
            var factory = new GraphClientFactory();

            try
            {
                Console.WriteLine("Starting device code authentication...");
                Console.WriteLine();

                var authResult = await factory.AuthenticateWithDeviceCodeAsync(dcr =>
                {
                    Console.WriteLine(dcr.Message);
                    return Task.CompletedTask;
                });

                Console.WriteLine();
                Console.WriteLine("Waiting for authorization...");

                // Get drive info
                var client = await factory.CreateAuthenticatedClientAsync();
                var driveOps = new DriveOperations(client);
                var (ownerEmail, quotaTotal, quotaUsed, _) = await driveOps.GetDriveInfoAsync(cancellationToken);

                // Get drive ID
                var drive = await client.Me.Drive.GetAsync(cancellationToken: cancellationToken);
                var driveId = drive?.Id;

                // Save config (email stays local only — never written to OneDrive manifests)
                var config = configStore.Load();
                config.AccountId = authResult.Account?.Username ?? ownerEmail ?? "unknown";
                config.DriveId = driveId;
                configStore.Save(config);

                var email = config.AccountId;
                var usedGB = quotaUsed.HasValue ? quotaUsed.Value / (1024.0 * 1024 * 1024) : 0;
                var totalGB = quotaTotal.HasValue ? quotaTotal.Value / (1024.0 * 1024 * 1024) : 0;

                Console.WriteLine($"✅ Authenticated as {email}");
                Console.WriteLine($"   OneDrive: {usedGB:F1} GB used of {totalGB:F0} GB");
                Console.WriteLine($"   Saved credentials to {configStore.ConfigPath}");
                return 0;
            }
            catch (Exception)
            {
                // Safe error handling — never expose raw exception messages (may contain tokens)
                Console.Error.WriteLine("❌ Authentication failed. Please try again.");
                return 1;
            }
        });

        return command;
    }
}
