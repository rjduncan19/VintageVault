using System.Text.Json;
using System.Text.Json.Serialization;

namespace VintageVault.Cli.Engine;

#region Snapshot Models

public sealed class FileHashes
{
    [JsonPropertyName("sha1")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha1 { get; set; }

    [JsonPropertyName("quickXorHash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QuickXorHash { get; set; }
}

public sealed class SnapshotChange
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = ""; // modified, added, deleted, skipped

    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Size { get; set; }

    [JsonPropertyName("itemId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ItemId { get; set; }

    [JsonPropertyName("previousItemId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousItemId { get; set; }

    [JsonPropertyName("lastModified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModified { get; set; }

    [JsonPropertyName("hashes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FileHashes? Hashes { get; set; }

    [JsonPropertyName("copyVerified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CopyVerified { get; set; }

    [JsonPropertyName("skipReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SkipReason { get; set; }
}

public sealed class SnapshotSummary
{
    [JsonPropertyName("filesChanged")]
    public int FilesChanged { get; set; }

    [JsonPropertyName("filesAdded")]
    public int FilesAdded { get; set; }

    [JsonPropertyName("filesDeleted")]
    public int FilesDeleted { get; set; }

    [JsonPropertyName("totalFilesCopied")]
    public int TotalFilesCopied { get; set; }

    [JsonPropertyName("totalBytesCopied")]
    public long TotalBytesCopied { get; set; }
}

public sealed class SnapshotManifest
{
    [JsonPropertyName("snapshotId")]
    public string SnapshotId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "full"; // full or incremental

    [JsonPropertyName("previousSnapshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousSnapshot { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("summary")]
    public SnapshotSummary Summary { get; set; } = new();

    [JsonPropertyName("changes")]
    public List<SnapshotChange> Changes { get; set; } = new();
}

#endregion

#region Backup Manifest Models

public sealed class SnapshotEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("previousSnapshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousSnapshot { get; set; }

    [JsonPropertyName("fileCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FileCount { get; set; }

    [JsonPropertyName("totalBytes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long TotalBytes { get; set; }

    [JsonPropertyName("changesCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ChangesCount { get; set; }

    [JsonPropertyName("bytesCopied")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long BytesCopied { get; set; }

    [JsonPropertyName("hasWarning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasWarning { get; set; }
}

public sealed class ManifestDetectionBaseline
{
    [JsonPropertyName("avgChangesPerCycle")]
    public double AvgChangesPerCycle { get; set; }

    [JsonPropertyName("avgDeletionsPerCycle")]
    public double AvgDeletionsPerCycle { get; set; }
}

public sealed class BackupManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("backupRoot")]
    public string BackupRoot { get; set; } = "/VintageVault-Backup";

    [JsonPropertyName("snapshots")]
    public List<SnapshotEntry> Snapshots { get; set; } = new();

    [JsonPropertyName("lastDeltaToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastDeltaToken { get; set; }

    [JsonPropertyName("detectionBaseline")]
    public ManifestDetectionBaseline DetectionBaseline { get; set; } = new();
}

#endregion

#region Warning Model

public sealed class AnomalyWarning
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("snapshotId")]
    public string SnapshotId { get; set; } = "";

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("details")]
    public Dictionary<string, object> Details { get; set; } = new();
}

#endregion

/// <summary>
/// Reads and writes manifest.json and _snapshot.json.
/// NO accountId/email ever written to these files (PII stays local only).
/// </summary>
public sealed class ManifestManager
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public string SerializeSnapshot(SnapshotManifest snapshot)
    {
        return JsonSerializer.Serialize(snapshot, s_jsonOptions);
    }

    public SnapshotManifest? DeserializeSnapshot(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SnapshotManifest>(json, s_jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string SerializeManifest(BackupManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, s_jsonOptions);
    }

    public BackupManifest? DeserializeManifest(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupManifest>(json, s_jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string SerializeWarning(AnomalyWarning warning)
    {
        return JsonSerializer.Serialize(warning, s_jsonOptions);
    }

    public BackupManifest CreateOrUpdateManifest(
        BackupManifest? existing,
        SnapshotManifest snapshot,
        string? deltaToken,
        double avgChanges,
        double avgDeletions,
        bool hasWarning)
    {
        var manifest = existing ?? new BackupManifest();

        var entry = new SnapshotEntry
        {
            Id = snapshot.SnapshotId,
            Type = snapshot.Type,
            Timestamp = snapshot.Timestamp,
            PreviousSnapshot = snapshot.PreviousSnapshot,
            HasWarning = hasWarning
        };

        if (snapshot.Type == "full")
        {
            entry.FileCount = snapshot.Summary.TotalFilesCopied;
            entry.TotalBytes = snapshot.Summary.TotalBytesCopied;
        }
        else
        {
            entry.ChangesCount = snapshot.Changes.Count;
            entry.BytesCopied = snapshot.Summary.TotalBytesCopied;
        }

        manifest.Snapshots.Add(entry);
        manifest.LastDeltaToken = deltaToken;
        manifest.DetectionBaseline = new ManifestDetectionBaseline
        {
            AvgChangesPerCycle = avgChanges,
            AvgDeletionsPerCycle = avgDeletions
        };

        return manifest;
    }
}
