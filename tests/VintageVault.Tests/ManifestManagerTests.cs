using System.Text.RegularExpressions;
using VintageVault.Cli.Engine;

namespace VintageVault.Tests;

public class ManifestManagerTests
{
    private readonly ManifestManager _manager = new();

    #region SnapshotManifest round-trip

    [Fact]
    public void SnapshotManifest_RoundTrip_PreservesAllFields()
    {
        var original = new SnapshotManifest
        {
            SnapshotId = "snap-20250101-120000",
            Type = "incremental",
            PreviousSnapshot = "snap-20241231-120000",
            Timestamp = "2025-01-01T12:00:00Z",
            Summary = new SnapshotSummary
            {
                FilesChanged = 10,
                FilesAdded = 3,
                FilesDeleted = 2,
                TotalFilesCopied = 13,
                TotalBytesCopied = 1048576
            },
            Changes = new List<SnapshotChange>
            {
                new SnapshotChange
                {
                    Path = "/Documents/report.docx",
                    Action = "modified",
                    Size = 2048,
                    ItemId = "item-001",
                    LastModified = "2025-01-01T11:30:00Z",
                    Hashes = new FileHashes
                    {
                        Sha1 = "abc123",
                        QuickXorHash = "def456"
                    },
                    CopyVerified = true
                }
            }
        };

        var json = _manager.SerializeSnapshot(original);
        var deserialized = _manager.DeserializeSnapshot(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.SnapshotId, deserialized.SnapshotId);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.PreviousSnapshot, deserialized.PreviousSnapshot);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
        Assert.Equal(original.Summary.FilesChanged, deserialized.Summary.FilesChanged);
        Assert.Equal(original.Summary.FilesAdded, deserialized.Summary.FilesAdded);
        Assert.Equal(original.Summary.FilesDeleted, deserialized.Summary.FilesDeleted);
        Assert.Equal(original.Summary.TotalFilesCopied, deserialized.Summary.TotalFilesCopied);
        Assert.Equal(original.Summary.TotalBytesCopied, deserialized.Summary.TotalBytesCopied);

        Assert.Single(deserialized.Changes);
        var change = deserialized.Changes[0];
        Assert.Equal("/Documents/report.docx", change.Path);
        Assert.Equal("modified", change.Action);
        Assert.Equal(2048, change.Size);
        Assert.Equal("item-001", change.ItemId);
        Assert.True(change.CopyVerified);
        Assert.NotNull(change.Hashes);
        Assert.Equal("abc123", change.Hashes.Sha1);
        Assert.Equal("def456", change.Hashes.QuickXorHash);
    }

    #endregion

    #region No PII in serialized output

    [Fact]
    public void SnapshotManifest_Serialized_ContainsNoPII()
    {
        var manifest = new SnapshotManifest
        {
            SnapshotId = "snap-001",
            Type = "full",
            Timestamp = "2025-01-01T00:00:00Z",
            Changes = new List<SnapshotChange>
            {
                new SnapshotChange
                {
                    Path = "/Documents/file.txt",
                    Action = "added",
                    Size = 100,
                    ItemId = "item-abc"
                }
            }
        };

        var json = _manager.SerializeSnapshot(manifest);

        // Email pattern: something@something.something
        Assert.DoesNotMatch(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", json);
    }

    [Fact]
    public void BackupManifest_Serialized_ContainsNoPII()
    {
        var manifest = new BackupManifest
        {
            Version = 1,
            BackupRoot = "/VintageVault-Backup",
            Snapshots = new List<SnapshotEntry>
            {
                new SnapshotEntry
                {
                    Id = "snap-001",
                    Type = "full",
                    Timestamp = "2025-01-01T00:00:00Z",
                    FileCount = 50,
                    TotalBytes = 1024000
                }
            },
            DetectionBaseline = new ManifestDetectionBaseline
            {
                AvgChangesPerCycle = 47,
                AvgDeletionsPerCycle = 3
            }
        };

        var json = _manager.SerializeManifest(manifest);

        Assert.DoesNotMatch(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", json);
    }

    #endregion

    #region BackupManifest round-trip

    [Fact]
    public void BackupManifest_RoundTrip_PreservesSnapshots()
    {
        var original = new BackupManifest
        {
            Version = 1,
            BackupRoot = "/VintageVault-Backup",
            LastDeltaToken = "delta-token-abc",
            Snapshots = new List<SnapshotEntry>
            {
                new SnapshotEntry
                {
                    Id = "snap-001",
                    Type = "full",
                    Timestamp = "2025-01-01T00:00:00Z",
                    FileCount = 100,
                    TotalBytes = 5242880
                },
                new SnapshotEntry
                {
                    Id = "snap-002",
                    Type = "incremental",
                    Timestamp = "2025-01-02T00:00:00Z",
                    PreviousSnapshot = "snap-001",
                    ChangesCount = 15,
                    BytesCopied = 102400
                }
            },
            DetectionBaseline = new ManifestDetectionBaseline
            {
                AvgChangesPerCycle = 47.5,
                AvgDeletionsPerCycle = 3.2
            }
        };

        var json = _manager.SerializeManifest(original);
        var deserialized = _manager.DeserializeManifest(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Version, deserialized.Version);
        Assert.Equal(original.BackupRoot, deserialized.BackupRoot);
        Assert.Equal(original.LastDeltaToken, deserialized.LastDeltaToken);
        Assert.Equal(2, deserialized.Snapshots.Count);
        Assert.Equal("snap-001", deserialized.Snapshots[0].Id);
        Assert.Equal("snap-002", deserialized.Snapshots[1].Id);
        Assert.Equal("snap-001", deserialized.Snapshots[1].PreviousSnapshot);
        Assert.Equal(47.5, deserialized.DetectionBaseline.AvgChangesPerCycle);
        Assert.Equal(3.2, deserialized.DetectionBaseline.AvgDeletionsPerCycle);
    }

    #endregion

    #region Change entry serialization

    [Fact]
    public void SnapshotChange_CopyVerified_RoundTrips()
    {
        var snapshot = new SnapshotManifest
        {
            SnapshotId = "snap-verify",
            Type = "incremental",
            Timestamp = "2025-06-01T00:00:00Z",
            Changes = new List<SnapshotChange>
            {
                new SnapshotChange
                {
                    Path = "/file1.txt",
                    Action = "modified",
                    CopyVerified = true,
                    Hashes = new FileHashes { Sha1 = "aaa", QuickXorHash = "bbb" }
                },
                new SnapshotChange
                {
                    Path = "/file2.txt",
                    Action = "added",
                    CopyVerified = false
                }
            }
        };

        var json = _manager.SerializeSnapshot(snapshot);
        var deserialized = _manager.DeserializeSnapshot(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.Changes[0].CopyVerified);
        Assert.False(deserialized.Changes[1].CopyVerified);
    }

    #endregion

    #region Deserialization of invalid JSON

    [Fact]
    public void DeserializeSnapshot_InvalidJson_ReturnsNull()
    {
        var result = _manager.DeserializeSnapshot("not valid json {{{");

        Assert.Null(result);
    }

    [Fact]
    public void DeserializeManifest_InvalidJson_ReturnsNull()
    {
        var result = _manager.DeserializeManifest("<<<invalid>>>");

        Assert.Null(result);
    }

    #endregion

    #region CreateOrUpdateManifest

    [Fact]
    public void CreateOrUpdateManifest_NewManifest_AddsEntry()
    {
        var snapshot = new SnapshotManifest
        {
            SnapshotId = "snap-new",
            Type = "full",
            Timestamp = "2025-01-01T00:00:00Z",
            Summary = new SnapshotSummary
            {
                TotalFilesCopied = 50,
                TotalBytesCopied = 1024000
            }
        };

        var result = _manager.CreateOrUpdateManifest(null, snapshot, "delta-123", 47, 3, false);

        Assert.Single(result.Snapshots);
        Assert.Equal("snap-new", result.Snapshots[0].Id);
        Assert.Equal("full", result.Snapshots[0].Type);
        Assert.Equal(50, result.Snapshots[0].FileCount);
        Assert.Equal("delta-123", result.LastDeltaToken);
        Assert.Equal(47, result.DetectionBaseline.AvgChangesPerCycle);
    }

    [Fact]
    public void CreateOrUpdateManifest_ExistingManifest_AppendsEntry()
    {
        var existing = new BackupManifest
        {
            Snapshots = new List<SnapshotEntry>
            {
                new SnapshotEntry { Id = "snap-001", Type = "full", Timestamp = "2025-01-01T00:00:00Z" }
            }
        };

        var snapshot = new SnapshotManifest
        {
            SnapshotId = "snap-002",
            Type = "incremental",
            PreviousSnapshot = "snap-001",
            Timestamp = "2025-01-02T00:00:00Z",
            Summary = new SnapshotSummary { TotalBytesCopied = 5000 },
            Changes = new List<SnapshotChange>
            {
                new SnapshotChange { Path = "/f1.txt", Action = "modified" },
                new SnapshotChange { Path = "/f2.txt", Action = "added" }
            }
        };

        var result = _manager.CreateOrUpdateManifest(existing, snapshot, "delta-456", 50, 4, true);

        Assert.Equal(2, result.Snapshots.Count);
        Assert.Equal("snap-002", result.Snapshots[1].Id);
        Assert.True(result.Snapshots[1].HasWarning);
        Assert.Equal(2, result.Snapshots[1].ChangesCount);
    }

    #endregion
}
