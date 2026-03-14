using Microsoft.Graph.Models;
using VintageVault.Cli.Config;

namespace VintageVault.Cli.Engine;

public sealed class AnomalyResult
{
    public bool Detected { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Analyzes delta API results for suspicious patterns.
/// All checks are metadata-based — zero file downloads.
/// </summary>
public sealed class AnomalyDetector
{
    private static readonly HashSet<string> s_suspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".locked", ".encrypted", ".crypted", ".enc",
        ".ransom", ".crypt", ".cry", ".lock"
    };

    /// <summary>
    /// Analyze a list of delta changes against the detection baseline.
    /// </summary>
    public AnomalyResult Analyze(IReadOnlyList<DriveItem> changes, DetectionBaseline baseline)
    {
        var result = new AnomalyResult();

        int totalChanges = changes.Count;
        int deleteCount = changes.Count(c => c.Deleted is not null);
        int fileChanges = changes.Count(c => c.Deleted is null && c.File is not null);

        // Rule 1: Mass Change
        if (baseline.AvgChangesPerCycle > 0 && totalChanges > 5 * baseline.AvgChangesPerCycle)
        {
            result.Detected = true;
            var ratio = totalChanges / baseline.AvgChangesPerCycle;
            result.Warnings.Add(
                $"MASS CHANGE: {totalChanges} changes detected ({ratio:F1}x baseline of ~{baseline.AvgChangesPerCycle:F0}/cycle)");
            result.Details["massChange"] = new { totalChanges, ratio, baseline = baseline.AvgChangesPerCycle };
        }

        // Rule 2: Mass Delete
        if (baseline.AvgDeletionsPerCycle > 0 && deleteCount > 5 * baseline.AvgDeletionsPerCycle)
        {
            result.Detected = true;
            var ratio = deleteCount / baseline.AvgDeletionsPerCycle;
            result.Warnings.Add(
                $"MASS DELETION: {deleteCount} deletions detected ({ratio:F1}x baseline of ~{baseline.AvgDeletionsPerCycle:F0}/cycle)");
            result.Details["massDeletion"] = new { deleteCount, ratio, baseline = baseline.AvgDeletionsPerCycle };
        }

        // Rule 3: Extension Swap
        int suspiciousExtCount = 0;
        foreach (var item in changes)
        {
            if (item.Deleted is not null || item.File is null || item.Name is null)
                continue;

            var ext = Path.GetExtension(item.Name);
            if (!string.IsNullOrEmpty(ext) && s_suspiciousExtensions.Contains(ext))
                suspiciousExtCount++;
        }

        if (suspiciousExtCount > 10)
        {
            result.Detected = true;
            result.Warnings.Add(
                $"EXTENSION SWAP: {suspiciousExtCount} files changed to suspicious extensions");
            result.Details["extensionSwap"] = new { count = suspiciousExtCount };
        }

        // Rule 4: Size Pattern (AES block padding)
        int aesSizeCount = 0;
        foreach (var item in changes)
        {
            if (item.Deleted is not null || item.File is null)
                continue;

            if (item.Size.HasValue && item.Size.Value > 0 && item.Size.Value % 16 == 0)
                aesSizeCount++;
        }

        if (aesSizeCount > 20)
        {
            result.Detected = true;
            result.Warnings.Add(
                $"SIZE PATTERN: {aesSizeCount} files changed to sizes that are exact multiples of 16 bytes (AES block padding indicator)");
            result.Details["sizePattern"] = new { count = aesSizeCount };
        }

        return result;
    }
}
