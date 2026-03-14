using System.Text.Json;
using System.Text.Json.Serialization;

namespace VintageVault.Cli.Config;

public sealed class FilterRule
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "folder"; // "folder" or "pattern"

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    [JsonPropertyName("pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pattern { get; set; }
}

public sealed class FilterConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "exclude"; // "include" or "exclude"

    [JsonPropertyName("rules")]
    public List<FilterRule> Rules { get; set; } = new();
}

public sealed class DetectionBaseline
{
    [JsonPropertyName("avgChangesPerCycle")]
    public double AvgChangesPerCycle { get; set; }

    [JsonPropertyName("avgDeletionsPerCycle")]
    public double AvgDeletionsPerCycle { get; set; }
}

public sealed class VintageVaultConfig
{
    [JsonPropertyName("accountId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccountId { get; set; }

    [JsonPropertyName("driveId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DriveId { get; set; }

    [JsonPropertyName("backupRoot")]
    public string BackupRoot { get; set; } = "/VintageVault-Backup";

    [JsonPropertyName("lastDeltaToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastDeltaToken { get; set; }

    [JsonPropertyName("lastBackupTimestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastBackupTimestamp { get; set; }

    [JsonPropertyName("filters")]
    public FilterConfig Filters { get; set; } = new();

    [JsonPropertyName("detectionBaseline")]
    public DetectionBaseline DetectionBaseline { get; set; } = new();
}

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    private readonly string _configDir;
    private readonly string _configPath;

    public ConfigStore() : this(null) { }

    public ConfigStore(string? configDir)
    {
        _configDir = configDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vintagevault");
        _configPath = Path.Combine(_configDir, "config.json");
    }

    public string ConfigDirectory => _configDir;
    public string ConfigPath => _configPath;

    public VintageVaultConfig Load()
    {
        if (!File.Exists(_configPath))
            return new VintageVaultConfig();

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<VintageVaultConfig>(json, s_jsonOptions)
                ?? new VintageVaultConfig();
        }
        catch (JsonException)
        {
            // Config corrupted — return defaults (safe error handling policy)
            return new VintageVaultConfig();
        }
    }

    public void Save(VintageVaultConfig config)
    {
        Directory.CreateDirectory(_configDir);
        var json = JsonSerializer.Serialize(config, s_jsonOptions);
        File.WriteAllText(_configPath, json);
    }

    public bool Exists() => File.Exists(_configPath);
}
