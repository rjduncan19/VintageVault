# 03 — Backup OneDrive to an External SSD

> **Goal:** Create a complete, production-quality backup workflow that copies your OneDrive to a local external SSD — with smart filtering, bandwidth control, verification, and automation hooks.

---

## Concept: What This Backup Does

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│   OneDrive (cloud)              External SSD (local)                │
│   ─────────────────             ────────────────────                │
│   📁 Documents/         ──────▶  📁 OneDrive-Backup/               │
│   📁 Photos/            ──────▶     📁 Documents/                  │
│   📁 Desktop/           ──────▶     📁 Photos/                     │
│   📁 Videos/  ✗ (excluded)          📁 Desktop/                    │
│   📁 Downloads/ ✗ (excluded)        📁 .rclone-last-backup         │
│                                                                     │
│   rclone copy (not sync)                                           │
│   → New and changed files are copied                               │
│   → Files deleted from OneDrive are NOT deleted from backup        │
│   → Safe for ransomware protection                                  │
└─────────────────────────────────────────────────────────────────────┘
```

**Why `copy` not `sync` for backup?**

With ransomware protection in mind, you want your backup to be *additive*, not a perfect mirror. If ransomware encrypts your OneDrive and you run `sync`, it would delete all your good backup files and replace them with encrypted ones. Using `copy` means backup files are never deleted automatically.

---

## Prerequisites

- rclone installed (Tutorial 01)
- `onedrive-personal` remote configured with your own app (Tutorial 02)
- An external SSD connected (e.g., `E:\` drive)

---

## Step 1: Understand Your OneDrive Layout

Before setting up filters, explore what's in your OneDrive:

```powershell
# List top-level directories
rclone lsd onedrive-personal:

# See how much data is in each folder
rclone size onedrive-personal: --include "Documents/**"
rclone size onedrive-personal: --include "Photos/**"
rclone size onedrive-personal: --include "Videos/**"

# Get total size
rclone size onedrive-personal:
```

Example output:
```
Total objects: 4,823
Total size: 47.234 GiB (50,723,456,123 Bytes)
```

This helps you plan:
- How long the first backup will take
- Which folders to exclude (large ones you don't need backed up)
- How much SSD space you need

---

## Step 2: Create a Filter File

Filter files let you specify exactly what to include or exclude. They're more powerful than individual `--include`/`--exclude` flags because they can be versioned and reused.

Create the file at `C:\Backup\rclone-onedrive-filters.txt`:

```powershell
New-Item -ItemType Directory -Path "C:\Backup" -Force
New-Item -ItemType File -Path "C:\Backup\rclone-onedrive-filters.txt" -Force
notepad "C:\Backup\rclone-onedrive-filters.txt"
```

Add these contents:

```
# rclone filter file for OneDrive → SSD backup
# Lines starting with # are comments
# - prefix = exclude
# + prefix = include (takes precedence over later excludes)
# Rules are evaluated top-to-bottom; first match wins

# ── Exclude large media you don't need backed up locally ──────────
- /Videos/**
- /Recordings/**

# ── Exclude system and temp folders ───────────────────────────────
- /Downloads/**
- /.trash/**
- /.Trash-*/**

# ── Exclude OneDrive system files ─────────────────────────────────
- desktop.ini
- Thumbs.db
- .DS_Store

# ── Exclude large binary/archive files over 2GB ───────────────────
--max-size 2G

# ── Exclude temporary files by extension ──────────────────────────
- *.tmp
- *.temp
- ~$*
- *.lnk

# ── Include everything else ───────────────────────────────────────
+ **
```

> **How filter rules work:**
> - Rules are checked top-to-bottom for each file
> - First matching rule wins
> - `-` = exclude, `+` = include
> - `**` = match any path including slashes
> - `/` at the start = anchored to the root of the remote
> - A trailing `/**` = everything inside that directory

---

## Step 3: Dry Run First

Always simulate before running for real. This shows you exactly what would be copied:

```powershell
rclone copy onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --dry-run `
    --progress `
    --log-file C:\Backup\rclone-dry-run.log `
    --log-level INFO
```

Review the log:
```powershell
Get-Content C:\Backup\rclone-dry-run.log | Select-String "NOTICE"
```

Check for anything unexpected — files you meant to exclude but didn't, or important files missing.

---

## Step 4: Run the First Full Backup

The first backup copies everything matching your filters. It will take a while depending on your data size and internet speed.

```powershell
rclone copy onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --progress `
    --log-file C:\Backup\rclone-backup.log `
    --log-level INFO `
    --transfers 4 `
    --checkers 8 `
    --bwlimit 20M
```

### Flag explanations:

| Flag | Value | Why |
|------|-------|-----|
| `--progress` | (none) | Show real-time dashboard |
| `--log-file` | path | Write log for later review |
| `--log-level INFO` | INFO | Log each file transferred |
| `--transfers` | 4 | Parallel file transfers (4 is a good default) |
| `--checkers` | 8 | Parallel checksum workers |
| `--bwlimit` | 20M | Cap at 20 MB/s (adjust to your connection) |

### Progress output looks like:

```
Transferred:   12.345 GiB / 31.456 GiB, 39%, 18.234 MiB/s, ETA 16m22s
Checks:           1234 / 4823, 26%
Transferred:       234 / 4823, 5%
Elapsed time: 11m23s
Transferring:
 * Documents/Work/Q2-Report.xlsx: 67% /2.34 MiB, 1.234 MiB/s, 1s
 * Photos/2025/IMG_4521.jpg:      45% /4.56 MiB, 2.345 MiB/s, 2s
 * Documents/Personal/Taxes.pdf: 100% /890 KiB, 890 KiB/s, 0s
 * Desktop/notes.txt:             100% /12 B, 12 B/s, 0s
```

---

## Step 5: Verify the Backup

After the backup completes, verify that what's on the SSD matches what was in OneDrive.

### 5.1 Quick file count comparison

```powershell
# Count files at source (apply same filters)
rclone ls onedrive-personal: --filter-from C:\Backup\rclone-onedrive-filters.txt | Measure-Object -Line

# Count files at destination
rclone ls E:\OneDrive-Backup | Measure-Object -Line
```

The counts should match (or destination may have more if you had previous backups).

### 5.2 Deep checksum verification

rclone's `check` command compares checksums between source and destination:

```powershell
rclone check onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --log-file C:\Backup\rclone-check.log `
    --log-level INFO `
    --progress
```

Successful output:
```
2026/06/30 11:30:00 INFO  : Documents/Work/Q2-Report.xlsx: OK
2026/06/30 11:30:00 INFO  : Photos/2025/IMG_4521.jpg: OK
...
2026/06/30 11:32:15 NOTICE: 4589 differences found
```

Wait — "differences found" doesn't mean errors! Check what kind:

```powershell
# Check for actual mismatches (not just missing files)
Get-Content C:\Backup\rclone-check.log | Select-String "ERROR"
```

If no ERRORs, just NOTICE lines about missing files, the backup is good. Missing files at destination that exist at source indicate files that need to be backed up.

### 5.3 Generate a manifest

Create a list of all files in the backup with checksums:

```powershell
rclone md5sum E:\OneDrive-Backup > C:\Backup\manifest-$(Get-Date -Format 'yyyy-MM-dd').txt
```

This creates a file like `manifest-2026-06-30.txt`:
```
a3f4e2d1b0c9...  Documents/Work/Q2-Report.xlsx
f8e7d6c5b4a3...  Photos/2025/IMG_4521.jpg
...
```

Store this manifest file somewhere safe (even email it to yourself). If you ever need to verify data integrity, you can compare against it.

---

## Step 6: Incremental Backups

After the first full backup, subsequent runs only copy new and changed files — much faster.

```powershell
# Same command as before — rclone automatically skips unchanged files
rclone copy onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --progress `
    --log-file C:\Backup\rclone-backup-$(Get-Date -Format 'yyyy-MM-dd').log `
    --log-level INFO `
    --transfers 4 `
    --bwlimit 20M
```

**How rclone knows what to skip:**
1. Checks if file exists at destination
2. Compares file size
3. Compares modification time
4. If both match → skip (no download needed)

For a typical incremental run where only a few files changed, this completes in seconds.

---

## Step 7: Create a Backup Script

Save this as `C:\Backup\run-onedrive-backup.ps1`:

```powershell
<#
.SYNOPSIS
    rclone OneDrive → External SSD backup script
.DESCRIPTION
    Backs up personal OneDrive to E:\OneDrive-Backup with filtering,
    logging, and basic error notification.
#>

param(
    [string]$Remote = "onedrive-personal",
    [string]$Destination = "E:\OneDrive-Backup",
    [string]$FilterFile = "C:\Backup\rclone-onedrive-filters.txt",
    [string]$LogDir = "C:\Backup\logs",
    [string]$BwLimit = "20M"
)

$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$logFile = "$LogDir\backup-$timestamp.log"

# Ensure log directory exists
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

Write-Host "Starting OneDrive backup at $(Get-Date)"
Write-Host "Source: $Remote:"
Write-Host "Destination: $Destination"
Write-Host "Log: $logFile"

# Run the backup
& rclone copy "${Remote}:" $Destination `
    --filter-from $FilterFile `
    --log-file $logFile `
    --log-level INFO `
    --transfers 4 `
    --checkers 8 `
    --bwlimit $BwLimit `
    --stats 30s `
    --stats-log-level NOTICE

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "✅ Backup completed successfully at $(Get-Date)"

    # Keep only last 30 log files
    Get-ChildItem $LogDir -Filter "backup-*.log" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 30 |
        Remove-Item

} else {
    Write-Host "❌ Backup failed with exit code $exitCode" -ForegroundColor Red
    Write-Host "Check log: $logFile" -ForegroundColor Red

    # Optional: send email notification (uncomment and configure)
    # Send-MailMessage -To "you@example.com" -From "backup@example.com" `
    #     -Subject "OneDrive Backup FAILED" `
    #     -Body "Exit code: $exitCode. Check $logFile" `
    #     -SmtpServer "smtp.example.com"

    exit $exitCode
}
```

Test the script:

```powershell
# Dry run first
& C:\Backup\run-onedrive-backup.ps1 -BwLimit "5M"

# Run for real
& C:\Backup\run-onedrive-backup.ps1
```

---

## Step 8: Verify the SSD Has What You Expect

### Browse the backup

```powershell
# Open Windows Explorer at the backup location
explorer E:\OneDrive-Backup

# Or list with PowerShell
Get-ChildItem E:\OneDrive-Backup -Recurse | Measure-Object -Property Length -Sum
```

### Compare sizes

```powershell
# Source size (applying filters)
rclone size onedrive-personal: --filter-from C:\Backup\rclone-onedrive-filters.txt

# Destination size
rclone size E:\OneDrive-Backup
```

They should be identical (or destination slightly larger if you had previous runs).

### Spot-check specific files

```powershell
# Verify a specific important file exists and has the right size
rclone ls onedrive-personal:Documents/important-contracts.pdf
rclone ls E:\OneDrive-Backup\Documents\important-contracts.pdf

# Open a backed-up file to confirm it's readable
Start-Process "E:\OneDrive-Backup\Documents\important-contracts.pdf"
```

---

## Understanding Backup Safety

This setup protects you from:

| Threat | How Protected |
|--------|--------------|
| Ransomware encrypts OneDrive | SSD has pre-encryption versions |
| Accidental file deletion | `copy` never deletes from SSD |
| Corrupt file upload | rclone checksum catches mismatches |
| SSD failure | Back up the SSD to a second location |

**What it does NOT protect against:**
- SSD failure (use two SSDs or add Tutorial 04 cross-account backup)
- Physical disaster (fire/flood) — the SSD is local
- Files deleted from OneDrive more than 30 days ago that you never backed up

**Defense in depth:** Use this SSD backup alongside the OneDrive-to-OneDrive backup (Tutorial 04) for multiple recovery options.

---

## Common Problems & Solutions

### "Error: directory not found" on the SSD destination
```powershell
New-Item -ItemType Directory -Path "E:\OneDrive-Backup" -Force
```

### Backup is too slow
Increase transfers: `--transfers 8 --checkers 16` (only helps on fast SSDs)
Remove the bandwidth limit temporarily: drop `--bwlimit`

### "Quota exceeded" error from OneDrive
This is a Graph API rate limit. rclone will retry automatically with exponential backoff. If it keeps failing, add `--tpslimit 4` to limit transactions per second.

### File modified times don't match
Add `--use-server-modtime` to use OneDrive's reported modification time instead of the upload time.

---

## Next Steps

Continue to → [04 — Backup OneDrive to OneDrive](04-backup-onedrive-to-onedrive.md)
