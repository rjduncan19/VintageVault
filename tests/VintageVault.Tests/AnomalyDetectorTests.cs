using Microsoft.Graph.Models;
using VintageVault.Cli.Config;
using VintageVault.Cli.Engine;

namespace VintageVault.Tests;

public class AnomalyDetectorTests
{
    private readonly AnomalyDetector _detector = new();

    private static DriveItem CreateFileChange(string name, long? size = null)
    {
        return new DriveItem
        {
            Name = name,
            File = new FileObject(),
            Size = size
        };
    }

    private static DriveItem CreateDeletion()
    {
        return new DriveItem
        {
            Deleted = new Deleted()
        };
    }

    #region Mass Change detection

    [Fact]
    public void Analyze_MassChange_Detected()
    {
        // 250 changes with baseline of 47 → ratio ~5.3x → detected (threshold is 5x)
        var changes = Enumerable.Range(0, 250)
            .Select(i => CreateFileChange($"file{i}.docx"))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 47 };

        var result = _detector.Analyze(changes, baseline);

        Assert.True(result.Detected);
        Assert.Contains(result.Warnings, w => w.Contains("MASS CHANGE"));
    }

    [Fact]
    public void Analyze_NormalChangeVolume_NotDetected()
    {
        // 50 changes with baseline of 47 → ratio ~1.06x → not detected
        var changes = Enumerable.Range(0, 50)
            .Select(i => CreateFileChange($"file{i}.docx"))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 47 };

        var result = _detector.Analyze(changes, baseline);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("MASS CHANGE"));
    }

    #endregion

    #region Extension Swap detection

    [Fact]
    public void Analyze_ExtensionSwap_ManyLockedFiles_Detected()
    {
        // 15 files with .locked extension → detected (threshold is >10)
        var changes = Enumerable.Range(0, 15)
            .Select(i => CreateFileChange($"file{i}.locked"))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 100 };

        var result = _detector.Analyze(changes, baseline);

        Assert.True(result.Detected);
        Assert.Contains(result.Warnings, w => w.Contains("EXTENSION SWAP"));
    }

    [Fact]
    public void Analyze_ExtensionSwap_FewLockedFiles_NotDetected()
    {
        // 5 files with .locked → not detected (below threshold of 10)
        var changes = Enumerable.Range(0, 5)
            .Select(i => CreateFileChange($"file{i}.locked"))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 100 };

        var result = _detector.Analyze(changes, baseline);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("EXTENSION SWAP"));
    }

    [Fact]
    public void Analyze_ExtensionSwap_VariousSuspiciousExtensions_Detected()
    {
        var extensions = new[] { ".locked", ".encrypted", ".crypted", ".enc", ".ransom",
                                 ".crypt", ".cry", ".lock", ".locked", ".encrypted", ".crypted" };
        var changes = extensions
            .Select((ext, i) => CreateFileChange($"file{i}{ext}"))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 100 };

        var result = _detector.Analyze(changes, baseline);

        Assert.True(result.Detected);
        Assert.Contains(result.Warnings, w => w.Contains("EXTENSION SWAP"));
    }

    #endregion

    #region Size Pattern detection

    [Fact]
    public void Analyze_SizePattern_ManyAESAlignedFiles_Detected()
    {
        // 25 files with sizes that are exact multiples of 16 → detected (threshold is >20)
        var changes = Enumerable.Range(1, 25)
            .Select(i => CreateFileChange($"file{i}.dat", size: i * 16L))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 100 };

        var result = _detector.Analyze(changes, baseline);

        Assert.True(result.Detected);
        Assert.Contains(result.Warnings, w => w.Contains("SIZE PATTERN"));
    }

    [Fact]
    public void Analyze_SizePattern_FewAlignedFiles_NotDetected()
    {
        // Only 10 files with AES-aligned sizes → not detected
        var changes = Enumerable.Range(1, 10)
            .Select(i => CreateFileChange($"file{i}.dat", size: i * 16L))
            .ToList();

        var baseline = new DetectionBaseline { AvgChangesPerCycle = 100 };

        var result = _detector.Analyze(changes, baseline);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("SIZE PATTERN"));
    }

    #endregion

    #region Mass Deletion detection

    [Fact]
    public void Analyze_MassDeletion_Detected()
    {
        // 30 deletions with baseline of 3 → ratio 10x → detected (threshold is 5x)
        var changes = Enumerable.Range(0, 30)
            .Select(_ => CreateDeletion())
            .ToList();

        var baseline = new DetectionBaseline
        {
            AvgChangesPerCycle = 100,
            AvgDeletionsPerCycle = 3
        };

        var result = _detector.Analyze(changes, baseline);

        Assert.True(result.Detected);
        Assert.Contains(result.Warnings, w => w.Contains("MASS DELETION"));
    }

    [Fact]
    public void Analyze_NormalDeletionVolume_NotDetected()
    {
        // 5 deletions with baseline of 3 → ratio ~1.67x → not detected
        var changes = Enumerable.Range(0, 5)
            .Select(_ => CreateDeletion())
            .ToList();

        var baseline = new DetectionBaseline
        {
            AvgChangesPerCycle = 100,
            AvgDeletionsPerCycle = 3
        };

        var result = _detector.Analyze(changes, baseline);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("MASS DELETION"));
    }

    #endregion

    #region Combined anomalies

    [Fact]
    public void Analyze_MultipleAnomalies_AllDetected()
    {
        var changes = new List<DriveItem>();

        // Add suspicious-extension files (>10 for extension swap)
        for (int i = 0; i < 12; i++)
            changes.Add(CreateFileChange($"file{i}.locked", size: (i + 1) * 16L));

        // Add AES-aligned files to push size pattern over threshold (need >20 total)
        for (int i = 0; i < 15; i++)
            changes.Add(CreateFileChange($"data{i}.dat", size: (i + 1) * 16L));

        // Add deletions for mass deletion
        for (int i = 0; i < 20; i++)
            changes.Add(CreateDeletion());

        // Small baseline to trigger mass change too
        var baseline = new DetectionBaseline
        {
            AvgChangesPerCycle = 5,
            AvgDeletionsPerCycle = 2
        };

        var result = _detector.Analyze(changes, baseline);

        Assert.True(result.Detected);
        Assert.True(result.Warnings.Count >= 2, "Expected multiple warnings");
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Analyze_ZeroBaseline_DoesNotCrash()
    {
        var changes = Enumerable.Range(0, 10)
            .Select(i => CreateFileChange($"file{i}.txt"))
            .ToList();

        var baseline = new DetectionBaseline
        {
            AvgChangesPerCycle = 0,
            AvgDeletionsPerCycle = 0
        };

        var result = _detector.Analyze(changes, baseline);

        // Should not throw; with zero baseline, mass change/delete rules are skipped
        Assert.NotNull(result);
    }

    [Fact]
    public void Analyze_EmptyChanges_NoAnomalies()
    {
        var changes = new List<DriveItem>();
        var baseline = new DetectionBaseline { AvgChangesPerCycle = 47, AvgDeletionsPerCycle = 3 };

        var result = _detector.Analyze(changes, baseline);

        Assert.False(result.Detected);
        Assert.Empty(result.Warnings);
    }

    #endregion
}
