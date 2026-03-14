namespace VintageVault.Cli.Config;

public sealed class FilterEngine
{
    // Extensions and prefixes always excluded regardless of user rules
    private static readonly string[] s_tempExtensions = { ".tmp" };
    private static readonly string[] s_tempPrefixes = { "~$" };
    private const string BackupRootPrefix = "/VintageVault-Backup/";
    private const string BackupRootExact = "/VintageVault-Backup";

    private readonly FilterConfig _filters;

    public FilterEngine(FilterConfig filters)
    {
        _filters = filters ?? new FilterConfig();
    }

    /// <summary>
    /// Returns true if the file should be included in the backup (i.e., NOT filtered out).
    /// </summary>
    public bool ShouldInclude(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Always exclude the backup folder itself
        if (path.Equals(BackupRootExact, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(BackupRootPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        // Always exclude temp files
        var fileName = GetFileName(path);
        if (IsTempFile(fileName))
            return false;

        if (_filters.Rules.Count == 0)
            return true;

        if (_filters.Mode.Equals("include", StringComparison.OrdinalIgnoreCase))
            return MatchesAnyRule(path);

        // Exclude mode: include unless it matches a rule
        return !MatchesAnyRule(path);
    }

    /// <summary>
    /// Returns a skip reason if the file is filtered out, or null if it should be included.
    /// </summary>
    public string? GetSkipReason(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "empty path";

        if (path.Equals(BackupRootExact, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(BackupRootPrefix, StringComparison.OrdinalIgnoreCase))
            return "backup folder excluded";

        var fileName = GetFileName(path);
        if (IsTempFile(fileName))
            return "temporary file excluded";

        if (_filters.Rules.Count == 0)
            return null;

        if (_filters.Mode.Equals("include", StringComparison.OrdinalIgnoreCase))
            return MatchesAnyRule(path) ? null : "not in include list";

        return MatchesAnyRule(path) ? "matched exclude rule" : null;
    }

    /// <summary>
    /// Validates a user-provided filter path. Throws ArgumentException if invalid.
    /// </summary>
    public static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.");

        if (path.Contains('\0'))
            throw new ArgumentException("Path contains invalid characters.");

        if (path.Contains(".."))
            throw new ArgumentException("Path traversal (..) is not allowed.");

        if (!path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.");
    }

    /// <summary>
    /// Validates a user-provided glob pattern. Throws ArgumentException if invalid.
    /// </summary>
    public static void ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern cannot be empty.");

        if (pattern.Contains('\0'))
            throw new ArgumentException("Pattern contains invalid characters.");

        if (pattern.Contains(".."))
            throw new ArgumentException("Path traversal (..) is not allowed in patterns.");
    }

    private bool MatchesAnyRule(string path)
    {
        foreach (var rule in _filters.Rules)
        {
            if (rule.Type.Equals("folder", StringComparison.OrdinalIgnoreCase) && rule.Path is not null)
            {
                if (path.Equals(rule.Path, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(rule.Path + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (rule.Type.Equals("pattern", StringComparison.OrdinalIgnoreCase) && rule.Pattern is not null)
            {
                if (MatchesGlob(GetFileName(path), rule.Pattern))
                    return true;
            }
        }
        return false;
    }

    private static bool IsTempFile(string fileName)
    {
        foreach (var ext in s_tempExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (var prefix in s_tempPrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string GetFileName(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    /// <summary>
    /// Simple glob matching supporting * and ? wildcards only.
    /// </summary>
    internal static bool MatchesGlob(string input, string pattern)
    {
        int i = 0, j = 0;
        int starI = -1, starJ = -1;

        while (i < input.Length)
        {
            if (j < pattern.Length && (pattern[j] == '?' || char.ToLowerInvariant(pattern[j]) == char.ToLowerInvariant(input[i])))
            {
                i++;
                j++;
            }
            else if (j < pattern.Length && pattern[j] == '*')
            {
                starI = i;
                starJ = j;
                j++;
            }
            else if (starJ >= 0)
            {
                j = starJ + 1;
                starI++;
                i = starI;
            }
            else
            {
                return false;
            }
        }

        while (j < pattern.Length && pattern[j] == '*')
            j++;

        return j == pattern.Length;
    }
}
