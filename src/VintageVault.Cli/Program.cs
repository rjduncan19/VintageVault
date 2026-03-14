using System.CommandLine;
using VintageVault.Cli.Commands;

var rootCommand = new RootCommand("VintageVault — Immutable incremental OneDrive backup engine");
rootCommand.Add(AuthCommand.Create());
rootCommand.Add(BackupCommand.Create());
rootCommand.Add(StatusCommand.Create());
rootCommand.Add(SnapshotsCommand.Create());
rootCommand.Add(FilterCommands.CreateIncludeCommand());
rootCommand.Add(FilterCommands.CreateExcludeCommand());
rootCommand.Add(FilterCommands.CreateFiltersCommand());

return await rootCommand.Parse(args).InvokeAsync();
