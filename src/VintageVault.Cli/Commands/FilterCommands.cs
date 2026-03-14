using System.CommandLine;
using VintageVault.Cli.Config;

namespace VintageVault.Cli.Commands;

public static class FilterCommands
{
    public static Command CreateIncludeCommand()
    {
        var pathsArg = new Argument<string[]>("paths")
        {
            Description = "Folder paths to include (e.g., /Documents /Photos)",
            Arity = ArgumentArity.OneOrMore
        };

        var command = new Command("include", "Set include-only filter (back up ONLY listed paths)");
        command.Add(pathsArg);

        command.SetAction(parseResult =>
        {
            var paths = parseResult.GetValue(pathsArg) ?? Array.Empty<string>();
            var configStore = new ConfigStore();
            var config = configStore.Load();

            var rules = new List<FilterRule>();
            foreach (var path in paths)
            {
                try
                {
                    FilterEngine.ValidatePath(path);
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine($"❌ Invalid path '{path}': {ex.Message}");
                    return 1;
                }

                rules.Add(new FilterRule { Type = "folder", Path = path });
            }

            config.Filters = new FilterConfig
            {
                Mode = "include",
                Rules = rules
            };

            configStore.Save(config);

            Console.WriteLine();
            Console.WriteLine("VintageVault v0.1.0");
            Console.WriteLine();
            Console.WriteLine($"✅ Include filter set. Only these folders will be backed up:");
            foreach (var path in paths)
                Console.WriteLine($"   • {path}");
            Console.WriteLine();
            Console.WriteLine("Run 'vintagevault filters' to review, or 'vintagevault filters --clear' to reset.");
            return 0;
        });

        return command;
    }

    public static Command CreateExcludeCommand()
    {
        var pathsArg = new Argument<string[]>("paths")
        {
            Description = "Folder paths to exclude",
            Arity = ArgumentArity.ZeroOrMore
        };

        var patternOption = new Option<string?>("--pattern")
        {
            Description = "File pattern to exclude (e.g., *.iso)"
        };

        var command = new Command("exclude", "Add exclusion rules (skip listed paths/patterns)");
        command.Add(pathsArg);
        command.Options.Add(patternOption);

        command.SetAction(parseResult =>
        {
            var paths = parseResult.GetValue(pathsArg) ?? Array.Empty<string>();
            var pattern = parseResult.GetValue(patternOption);

            var configStore = new ConfigStore();
            var config = configStore.Load();

            // Ensure we're in exclude mode
            if (config.Filters.Mode != "exclude")
            {
                config.Filters = new FilterConfig { Mode = "exclude", Rules = new List<FilterRule>() };
            }

            // Add folder exclusions
            foreach (var path in paths)
            {
                try
                {
                    FilterEngine.ValidatePath(path);
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine($"❌ Invalid path '{path}': {ex.Message}");
                    return 1;
                }

                config.Filters.Rules.Add(new FilterRule { Type = "folder", Path = path });
            }

            // Add pattern exclusion
            if (pattern is not null)
            {
                try
                {
                    FilterEngine.ValidatePattern(pattern);
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine($"❌ Invalid pattern '{pattern}': {ex.Message}");
                    return 1;
                }

                config.Filters.Rules.Add(new FilterRule { Type = "pattern", Pattern = pattern });
            }

            if (paths.Length == 0 && pattern is null)
            {
                Console.Error.WriteLine("❌ Provide folder paths and/or --pattern to exclude.");
                return 1;
            }

            configStore.Save(config);

            Console.WriteLine();
            Console.WriteLine("VintageVault v0.1.0");
            Console.WriteLine();
            Console.WriteLine("✅ Exclusion rules updated:");
            foreach (var path in paths)
                Console.WriteLine($"   • Folder: {path}");
            if (pattern is not null)
                Console.WriteLine($"   • Pattern: {pattern}");
            Console.WriteLine();
            Console.WriteLine("Run 'vintagevault filters' to see all rules.");
            return 0;
        });

        return command;
    }

    public static Command CreateFiltersCommand()
    {
        var clearOption = new Option<bool>("--clear")
        {
            Description = "Reset all filters (back up everything)"
        };

        var command = new Command("filters", "Show or manage filter rules");
        command.Options.Add(clearOption);

        command.SetAction(parseResult =>
        {
            var clear = parseResult.GetValue(clearOption);

            var configStore = new ConfigStore();
            var config = configStore.Load();

            Console.WriteLine();
            Console.WriteLine("VintageVault v0.1.0");
            Console.WriteLine();

            if (clear)
            {
                config.Filters = new FilterConfig();
                configStore.Save(config);
                Console.WriteLine("✅ All filters cleared. Everything will be backed up.");
                Console.WriteLine();
                Console.WriteLine("Built-in exclusions still apply:");
                Console.WriteLine("   • /VintageVault-Backup/ (prevents recursion)");
                Console.WriteLine("   • ~$* files (Office temp files)");
                Console.WriteLine("   • *.tmp files");
                return 0;
            }

            // Display current filters
            if (config.Filters.Rules.Count == 0)
            {
                Console.WriteLine("No custom filters set. Everything will be backed up.");
                Console.WriteLine();
                Console.WriteLine("Built-in exclusions always apply:");
                Console.WriteLine("   • /VintageVault-Backup/ (prevents recursion)");
                Console.WriteLine("   • ~$* files (Office temp files)");
                Console.WriteLine("   • *.tmp files");
                Console.WriteLine();
                Console.WriteLine("Use 'vintagevault include' or 'vintagevault exclude' to add rules.");
                return 0;
            }

            Console.WriteLine($"Filter mode: {config.Filters.Mode.ToUpperInvariant()}");
            Console.WriteLine();

            if (config.Filters.Mode == "include")
                Console.WriteLine("Only these paths will be backed up:");
            else
                Console.WriteLine("These paths/patterns will be skipped:");

            Console.WriteLine();

            foreach (var rule in config.Filters.Rules)
            {
                if (rule.Type == "folder" && rule.Path is not null)
                    Console.WriteLine($"   • Folder: {rule.Path}");
                else if (rule.Type == "pattern" && rule.Pattern is not null)
                    Console.WriteLine($"   • Pattern: {rule.Pattern}");
            }

            Console.WriteLine();
            Console.WriteLine("Built-in exclusions always apply:");
            Console.WriteLine("   • /VintageVault-Backup/ (prevents recursion)");
            Console.WriteLine("   • ~$* files (Office temp files)");
            Console.WriteLine("   • *.tmp files");
            Console.WriteLine();
            Console.WriteLine("Use 'vintagevault filters --clear' to reset.");
            return 0;
        });

        return command;
    }
}
