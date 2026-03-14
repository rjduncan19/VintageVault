using VintageVault.Cli.Config;

namespace VintageVault.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigStore _store;

    public ConfigStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VintageVault-Tests-" + Guid.NewGuid().ToString("N")[..8]);
        _store = new ConfigStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Save and Load round-trip

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesConfig()
    {
        var config = new VintageVaultConfig
        {
            AccountId = "test-account-id",
            DriveId = "test-drive-id",
            BackupRoot = "/VintageVault-Backup",
            LastDeltaToken = "delta-abc-123",
            LastBackupTimestamp = "2025-01-01T12:00:00Z",
            Filters = new FilterConfig
            {
                Mode = "exclude",
                Rules = new List<FilterRule>
                {
                    new FilterRule { Type = "folder", Path = "/Photos" },
                    new FilterRule { Type = "pattern", Pattern = "*.iso" }
                }
            },
            DetectionBaseline = new DetectionBaseline
            {
                AvgChangesPerCycle = 47.5,
                AvgDeletionsPerCycle = 3.2
            }
        };

        _store.Save(config);
        var loaded = _store.Load();

        Assert.Equal(config.AccountId, loaded.AccountId);
        Assert.Equal(config.DriveId, loaded.DriveId);
        Assert.Equal(config.BackupRoot, loaded.BackupRoot);
        Assert.Equal(config.LastDeltaToken, loaded.LastDeltaToken);
        Assert.Equal(config.LastBackupTimestamp, loaded.LastBackupTimestamp);
        Assert.Equal("exclude", loaded.Filters.Mode);
        Assert.Equal(2, loaded.Filters.Rules.Count);
        Assert.Equal(47.5, loaded.DetectionBaseline.AvgChangesPerCycle);
        Assert.Equal(3.2, loaded.DetectionBaseline.AvgDeletionsPerCycle);
    }

    #endregion

    #region Default config

    [Fact]
    public void Load_NoConfigFile_ReturnsDefaults()
    {
        var loaded = _store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("/VintageVault-Backup", loaded.BackupRoot);
        Assert.NotNull(loaded.Filters);
        Assert.Equal("exclude", loaded.Filters.Mode);
        Assert.Empty(loaded.Filters.Rules);
        Assert.NotNull(loaded.DetectionBaseline);
    }

    [Fact]
    public void DefaultConfig_HasSensibleBackupRoot()
    {
        var config = new VintageVaultConfig();

        Assert.Equal("/VintageVault-Backup", config.BackupRoot);
    }

    #endregion

    #region Config file doesn't contain secrets

    [Fact]
    public void SavedConfig_DoesNotContainTokensOrSecrets()
    {
        var config = new VintageVaultConfig
        {
            AccountId = "account-id",
            DriveId = "drive-id",
            BackupRoot = "/VintageVault-Backup",
            LastDeltaToken = "delta-token-value"
        };

        _store.Save(config);

        var rawJson = File.ReadAllText(_store.ConfigPath);

        // Config should not contain OAuth tokens, passwords, or secret-like patterns
        Assert.DoesNotContain("Bearer ", rawJson);
        Assert.DoesNotContain("eyJ", rawJson); // JWT prefix (base64 of '{"')
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Exists

    [Fact]
    public void Exists_BeforeSave_ReturnsFalse()
    {
        Assert.False(_store.Exists());
    }

    [Fact]
    public void Exists_AfterSave_ReturnsTrue()
    {
        _store.Save(new VintageVaultConfig());

        Assert.True(_store.Exists());
    }

    #endregion

    #region Corrupted config

    [Fact]
    public void Load_CorruptedConfig_ReturnsDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_store.ConfigPath, "{{{{ not valid json !!");

        var loaded = _store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("/VintageVault-Backup", loaded.BackupRoot);
    }

    #endregion
}
