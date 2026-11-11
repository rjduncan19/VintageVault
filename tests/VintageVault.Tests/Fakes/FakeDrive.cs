using System.Collections.Concurrent;
using Microsoft.Graph.Models;
using VintageVault.Cli.Graph;

namespace VintageVault.Tests.Fakes;

/// <summary>
/// In-memory OneDrive used to exercise the backup engine without Graph.
/// Models a folder tree (folders, copied files, written text files), scripted delta responses,
/// and records every call so tests can assert on what the engine did.
/// Thread-safe: the orchestrator copies files concurrently.
/// </summary>
public sealed class FakeDrive : IDriveOperations
{
    public const string RootId = "root";

    public sealed class Node
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string? ParentId { get; init; }
        public bool IsFolder { get; init; }
        public string? Content { get; set; }
        /// <summary>For copied files, the source item they were copied from.</summary>
        public string? CopiedFromId { get; init; }
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, Node> _nodes = new();
    private readonly Queue<(List<DriveItem> Changes, string? Token)> _deltaResponses = new();
    private int _nextId;
    private int _activeCopies;

    public FakeDrive()
    {
        _nodes[RootId] = new Node { Id = RootId, Name = "", ParentId = null, IsFolder = true };
    }

    // ---- Configuration knobs ----

    /// <summary>Delay inside each copy, to force copies to overlap. Default: none.</summary>
    public TimeSpan CopyDelay { get; set; } = TimeSpan.Zero;

    /// <summary>When true, EnsureBackupRootAsync returns null (simulates a failure).</summary>
    public bool FailEnsureBackupRoot { get; set; }

    /// <summary>Folder names for which CreateFolderAsync returns null.</summary>
    public HashSet<string> FailCreateFolderNames { get; } = new();

    // ---- Recorded calls ----

    public ConcurrentQueue<string?> DeltaTokensRequested { get; } = new();
    public ConcurrentQueue<(string SourceId, string DestFolderId, string Name)> Copies { get; } = new();
    public int MaxConcurrentCopies { get; private set; }
    public int TotalCalls;

    /// <summary>Script the next delta response.</summary>
    public void EnqueueDelta(string? newToken, params DriveItem[] changes)
        => _deltaResponses.Enqueue((changes.ToList(), newToken));

    // ---- IDriveOperations ----

    public Task<(List<DriveItem> Changes, string? NewDeltaToken)> GetDeltaAsync(
        string driveId, string? deltaToken = null, CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        DeltaTokensRequested.Enqueue(deltaToken);
        lock (_lock)
        {
            if (_deltaResponses.Count == 0)
                return Task.FromResult((new List<DriveItem>(), (string?)"token-empty"));
            return Task.FromResult(_deltaResponses.Dequeue());
        }
    }

    public async Task<string?> CopyItemAsync(
        string driveId, string sourceItemId, string destinationFolderId, string newName,
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        var active = Interlocked.Increment(ref _activeCopies);
        lock (_lock)
            MaxConcurrentCopies = Math.Max(MaxConcurrentCopies, active);
        try
        {
            if (CopyDelay > TimeSpan.Zero)
                await Task.Delay(CopyDelay, ct);
            else
                await Task.Yield();

            lock (_lock)
            {
                if (!_nodes.TryGetValue(destinationFolderId, out var dest) || !dest.IsFolder)
                    throw new InvalidOperationException($"Copy destination {destinationFolderId} is not a folder");
                AddNode(newName, destinationFolderId, isFolder: false, copiedFrom: sourceItemId);
            }
            Copies.Enqueue((sourceItemId, destinationFolderId, newName));
            return null;
        }
        finally
        {
            Interlocked.Decrement(ref _activeCopies);
        }
    }

    public Task<DriveItem?> CreateFolderAsync(
        string driveId, string parentFolderId, string name, CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        if (FailCreateFolderNames.Contains(name))
            return Task.FromResult<DriveItem?>(null);
        lock (_lock)
        {
            // Real Graph uses conflictBehavior=rename, so duplicates become siblings, not reuse.
            var node = AddNode(name, parentFolderId, isFolder: true);
            return Task.FromResult<DriveItem?>(ToDriveItem(node));
        }
    }

    public Task<DriveItem?> WriteFileAsync(
        string driveId, string parentFolderId, string name, string content, CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        lock (_lock)
        {
            var existing = FindChild(parentFolderId, name);
            if (existing is not null)
                existing.Content = content;
            else
                existing = AddNode(name, parentFolderId, isFolder: false, content: content);
            return Task.FromResult<DriveItem?>(ToDriveItem(existing));
        }
    }

    public Task<DriveItem?> GetItemAsync(string driveId, string itemId, CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        lock (_lock)
            return Task.FromResult(_nodes.TryGetValue(itemId, out var n) ? ToDriveItem(n) : null);
    }

    public Task<string?> ReadFileContentAsync(
        string driveId, string parentFolderId, string name, CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        lock (_lock)
            return Task.FromResult(FindChild(parentFolderId, name)?.Content);
    }

    public Task<(string? OwnerEmail, long? QuotaTotal, long? QuotaUsed, long? QuotaRemaining)> GetDriveInfoAsync(
        CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        return Task.FromResult<(string?, long?, long?, long?)>(("Test User", 5_000_000_000, 1_000, 4_999_999_000));
    }

    public Task<DriveItem?> EnsureBackupRootAsync(
        string driveId, string backupRootName = "VintageVault-Backup", CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        if (FailEnsureBackupRoot)
            return Task.FromResult<DriveItem?>(null);
        lock (_lock)
        {
            var node = FindChild(RootId, backupRootName) ?? AddNode(backupRootName, RootId, isFolder: true);
            return Task.FromResult<DriveItem?>(ToDriveItem(node));
        }
    }

    public Task<List<DriveItem>> ListChildrenAsync(string driveId, string folderId, CancellationToken ct = default)
    {
        Interlocked.Increment(ref TotalCalls);
        lock (_lock)
            return Task.FromResult(_nodes.Values.Where(n => n.ParentId == folderId).Select(ToDriveItem).ToList());
    }

    // ---- Inspection helpers ----

    /// <summary>Resolve a slash path like "/VintageVault-Backup/2026-03-14-full/Docs/a.txt".</summary>
    public Node? Find(string path)
    {
        lock (_lock)
        {
            var current = _nodes[RootId];
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = FindChild(current.Id, part);
                if (next is null)
                    return null;
                current = next;
            }
            return current;
        }
    }

    public string? ReadText(string path) => Find(path)?.Content;

    /// <summary>Names of the direct children of the folder at <paramref name="path"/>.</summary>
    public List<string> ChildNames(string path)
    {
        lock (_lock)
        {
            var folder = Find(path) ?? throw new InvalidOperationException($"No folder at {path}");
            return _nodes.Values.Where(n => n.ParentId == folder.Id).Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        }
    }

    // ---- DriveItem builders (shapes match what Graph's delta API returns) ----

    public static DriveItem File(string id, string path, long size = 100, string? sha1 = null, string? quickXor = null)
    {
        var (parent, name) = SplitPath(path);
        return new DriveItem
        {
            Id = id,
            Name = name,
            Size = size,
            File = new FileObject { Hashes = new Hashes { Sha1Hash = sha1, QuickXorHash = quickXor } },
            ParentReference = new ItemReference { Path = parent.Length == 0 ? "/drive/root:" : "/drive/root:" + parent },
            LastModifiedDateTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)
        };
    }

    public static DriveItem Folder(string id, string path)
    {
        var (parent, name) = SplitPath(path);
        return new DriveItem
        {
            Id = id,
            Name = name,
            Folder = new Folder(),
            ParentReference = new ItemReference { Path = "/drive/root:" + parent }
        };
    }

    /// <summary>Delta tombstone. Graph often omits parentReference.path for deleted items.</summary>
    public static DriveItem Deleted(string id, string name)
        => new() { Id = id, Name = name, Deleted = new Deleted { State = "deleted" } };

    private static (string Parent, string Name) SplitPath(string path)
    {
        var idx = path.LastIndexOf('/');
        return (path[..idx], path[(idx + 1)..]);
    }

    private Node AddNode(string name, string parentId, bool isFolder, string? content = null, string? copiedFrom = null)
    {
        if (!_nodes.ContainsKey(parentId))
            throw new InvalidOperationException($"Parent {parentId} does not exist");
        var node = new Node
        {
            Id = "fake-" + Interlocked.Increment(ref _nextId),
            Name = name,
            ParentId = parentId,
            IsFolder = isFolder,
            Content = content,
            CopiedFromId = copiedFrom
        };
        _nodes[node.Id] = node;
        return node;
    }

    private Node? FindChild(string parentId, string name)
        => _nodes.Values.FirstOrDefault(n => n.ParentId == parentId && n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static DriveItem ToDriveItem(Node n) => new()
    {
        Id = n.Id,
        Name = n.Name,
        Folder = n.IsFolder ? new Folder() : null,
        File = n.IsFolder ? null : new FileObject()
    };
}

/// <summary>Fixed clock for deterministic snapshot IDs.</summary>
public sealed class FakeTime : TimeProvider
{
    public DateTimeOffset Now { get; set; }
    public FakeTime(DateTimeOffset now) => Now = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
