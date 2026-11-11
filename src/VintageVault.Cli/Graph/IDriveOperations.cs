using Microsoft.Graph.Models;

namespace VintageVault.Cli.Graph;

/// <summary>
/// OneDrive operations used by the backup engine and CLI commands.
/// <see cref="DriveOperations"/> is the Microsoft Graph implementation; tests use an in-memory fake
/// so the backup flow can be exercised without a live OneDrive.
/// </summary>
public interface IDriveOperations
{
    /// <summary>
    /// Get delta changes. Pass null deltaToken for full enumeration.
    /// Returns list of DriveItem changes and the new delta token.
    /// </summary>
    Task<(List<DriveItem> Changes, string? NewDeltaToken)> GetDeltaAsync(
        string driveId, string? deltaToken = null, CancellationToken ct = default);

    /// <summary>
    /// Copy a drive item to a destination folder with a new name.
    /// Returns the monitor URL for tracking copy completion, if available.
    /// </summary>
    Task<string?> CopyItemAsync(
        string driveId, string sourceItemId, string destinationFolderId, string newName,
        CancellationToken ct = default);

    /// <summary>Create a child folder under the specified parent folder.</summary>
    Task<DriveItem?> CreateFolderAsync(
        string driveId, string parentFolderId, string name, CancellationToken ct = default);

    /// <summary>Upload small (&lt; 4 MB) text content such as manifests.</summary>
    Task<DriveItem?> WriteFileAsync(
        string driveId, string parentFolderId, string name, string content, CancellationToken ct = default);

    /// <summary>Get a specific drive item by ID.</summary>
    Task<DriveItem?> GetItemAsync(string driveId, string itemId, CancellationToken ct = default);

    /// <summary>Read a child file's text content. Returns null if not found.</summary>
    Task<string?> ReadFileContentAsync(
        string driveId, string parentFolderId, string name, CancellationToken ct = default);

    /// <summary>Get drive info including owner and quota.</summary>
    Task<(string? OwnerEmail, long? QuotaTotal, long? QuotaUsed, long? QuotaRemaining)> GetDriveInfoAsync(
        CancellationToken ct = default);

    /// <summary>Find or create the backup root folder.</summary>
    Task<DriveItem?> EnsureBackupRootAsync(
        string driveId, string backupRootName = "VintageVault-Backup", CancellationToken ct = default);

    /// <summary>List children of a folder by item ID.</summary>
    Task<List<DriveItem>> ListChildrenAsync(string driveId, string folderId, CancellationToken ct = default);
}
