using VintageVault.Cli.Config;

namespace VintageVault.Tests;

public class FilterEngineTests
{
    #region ValidatePath — security-critical input validation

    [Fact]
    public void ValidatePath_PathTraversal_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePath("/folder/../secret"));
        Assert.Contains("..", ex.Message);
    }

    [Fact]
    public void ValidatePath_NullByte_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePath("/folder/file\0.txt"));
        Assert.Contains("invalid characters", ex.Message);
    }

    [Fact]
    public void ValidatePath_MissingLeadingSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePath("folder/file.txt"));
    }

    [Fact]
    public void ValidatePath_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePath(""));
    }

    [Fact]
    public void ValidatePath_WhitespaceOnly_Throws()
    {
        Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePath("   "));
    }

    [Fact]
    public void ValidatePath_ValidPath_DoesNotThrow()
    {
        FilterEngine.ValidatePath("/Documents/report.docx");
    }

    #endregion

    #region ValidatePattern

    [Fact]
    public void ValidatePattern_NullByte_Throws()
    {
        Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePattern("*.tx\0t"));
    }

    [Fact]
    public void ValidatePattern_PathTraversal_Throws()
    {
        Assert.Throws<ArgumentException>(() => FilterEngine.ValidatePattern("../*.txt"));
    }

    [Fact]
    public void ValidatePattern_ValidGlob_DoesNotThrow()
    {
        FilterEngine.ValidatePattern("*.iso");
    }

    #endregion

    #region Built-in exclusions

    [Fact]
    public void ShouldInclude_BackupFolder_AlwaysExcluded()
    {
        var engine = new FilterEngine(new FilterConfig());

        Assert.False(engine.ShouldInclude("/VintageVault-Backup"));
        Assert.False(engine.ShouldInclude("/VintageVault-Backup/snapshot1/file.txt"));
    }

    [Fact]
    public void ShouldInclude_BackupFolder_CaseInsensitive()
    {
        var engine = new FilterEngine(new FilterConfig());

        Assert.False(engine.ShouldInclude("/VINTAGEVAULT-BACKUP"));
        Assert.False(engine.ShouldInclude("/vintagevault-backup/data.json"));
    }

    [Fact]
    public void ShouldInclude_TempFiles_AlwaysExcluded()
    {
        var engine = new FilterEngine(new FilterConfig());

        Assert.False(engine.ShouldInclude("/Documents/~$report.docx"));
        Assert.False(engine.ShouldInclude("/Documents/data.tmp"));
    }

    [Fact]
    public void ShouldInclude_TempExtension_CaseInsensitive()
    {
        var engine = new FilterEngine(new FilterConfig());

        Assert.False(engine.ShouldInclude("/folder/file.TMP"));
    }

    [Fact]
    public void ShouldInclude_EmptyPath_ReturnsFalse()
    {
        var engine = new FilterEngine(new FilterConfig());

        Assert.False(engine.ShouldInclude(""));
        Assert.False(engine.ShouldInclude(null!));
    }

    #endregion

    #region Exclude mode

    [Fact]
    public void ExcludeMode_ExcludedFolder_IsFilteredOut()
    {
        var config = new FilterConfig
        {
            Mode = "exclude",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "folder", Path = "/Photos" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.False(engine.ShouldInclude("/Photos/vacation.jpg"));
        Assert.False(engine.ShouldInclude("/Photos"));
    }

    [Fact]
    public void ExcludeMode_NonExcludedPath_PassesThrough()
    {
        var config = new FilterConfig
        {
            Mode = "exclude",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "folder", Path = "/Photos" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.True(engine.ShouldInclude("/Documents/report.docx"));
    }

    [Fact]
    public void ExcludeMode_PatternRule_ExcludesMatching()
    {
        var config = new FilterConfig
        {
            Mode = "exclude",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "pattern", Pattern = "*.iso" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.False(engine.ShouldInclude("/Downloads/windows.iso"));
        Assert.True(engine.ShouldInclude("/Downloads/readme.txt"));
    }

    #endregion

    #region Include mode

    [Fact]
    public void IncludeMode_OnlyIncludedPathsPassThrough()
    {
        var config = new FilterConfig
        {
            Mode = "include",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "folder", Path = "/Documents" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.True(engine.ShouldInclude("/Documents/report.docx"));
        Assert.False(engine.ShouldInclude("/Photos/vacation.jpg"));
    }

    [Fact]
    public void IncludeMode_BuiltInExclusions_StillApply()
    {
        var config = new FilterConfig
        {
            Mode = "include",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "folder", Path = "/VintageVault-Backup" }
            }
        };
        var engine = new FilterEngine(config);

        // Built-in exclusions take priority even in include mode
        Assert.False(engine.ShouldInclude("/VintageVault-Backup/data.json"));
    }

    #endregion

    #region Glob pattern matching (tested through ShouldInclude)

    [Fact]
    public void GlobPattern_StarWildcard_MatchesCorrectExtension()
    {
        var config = new FilterConfig
        {
            Mode = "exclude",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "pattern", Pattern = "*.iso" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.False(engine.ShouldInclude("/Downloads/windows.iso"));
        Assert.True(engine.ShouldInclude("/Downloads/readme.txt"));
    }

    [Fact]
    public void GlobPattern_QuestionMark_MatchesSingleChar()
    {
        var config = new FilterConfig
        {
            Mode = "exclude",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "pattern", Pattern = "file?.txt" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.False(engine.ShouldInclude("/docs/file1.txt"));
        Assert.True(engine.ShouldInclude("/docs/file12.txt"));
    }

    [Fact]
    public void GlobPattern_CaseInsensitive()
    {
        var config = new FilterConfig
        {
            Mode = "exclude",
            Rules = new List<FilterRule>
            {
                new FilterRule { Type = "pattern", Pattern = "*.iso" }
            }
        };
        var engine = new FilterEngine(config);

        Assert.False(engine.ShouldInclude("/Downloads/FILE.ISO"));
        Assert.False(engine.ShouldInclude("/Downloads/file.Iso"));
    }

    #endregion

    #region GetSkipReason

    [Fact]
    public void GetSkipReason_IncludedFile_ReturnsNull()
    {
        var engine = new FilterEngine(new FilterConfig());

        Assert.Null(engine.GetSkipReason("/Documents/file.docx"));
    }

    [Fact]
    public void GetSkipReason_TempFile_ReturnsReason()
    {
        var engine = new FilterEngine(new FilterConfig());

        var reason = engine.GetSkipReason("/Documents/file.tmp");
        Assert.NotNull(reason);
        Assert.Contains("temporary", reason);
    }

    [Fact]
    public void GetSkipReason_BackupFolder_ReturnsReason()
    {
        var engine = new FilterEngine(new FilterConfig());

        var reason = engine.GetSkipReason("/VintageVault-Backup/file.json");
        Assert.NotNull(reason);
        Assert.Contains("backup", reason);
    }

    #endregion
}
