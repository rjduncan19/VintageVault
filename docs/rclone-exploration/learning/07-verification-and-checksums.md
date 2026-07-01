# 07 — Verification & Checksums

> **Goal:** Learn how to rigorously verify that your backup is complete, uncorrupted, and trustworthy using rclone's check commands and hash generation.

---

## Why Verification Matters

Running a backup command that exits with code 0 doesn't guarantee your data is safe. Real-world failures include:

```
Common backup failures:
├── Silent data corruption (bit-rot on disk)
├── Partial transfers (network interruption)
├── API errors masked as successes (cloud provider bugs)
├── Checksum mismatches (file modified during transfer)
├── Missing files (filter rules too aggressive)
└── Encoding issues (special characters in filenames)
```

Verification is a separate, independent step that catches these failures. **Don't trust a backup you haven't verified.**

---

## rclone's Verification Commands

### `rclone check` — Compare Source and Destination

```powershell
rclone check source:path dest:path [flags]
```

Compares every file between source and destination by:
1. Checking file exists in both locations
2. Comparing checksums (not just size/modtime)

Returns exit code 0 if everything matches, non-zero if differences found.

### Basic check after backup

```powershell
rclone check onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --progress
```

Output:
```
2026/06/30 12:00:00 INFO  : Documents/Budget.xlsx: OK
2026/06/30 12:00:00 INFO  : Documents/Resume.docx: OK
...
2026/06/30 12:02:15 NOTICE: 0 differences found
2026/06/30 12:02:15 NOTICE: 4589 matching files
```

Zero differences = ✅ backup verified.

### What check reports

| Message | Meaning |
|---------|---------|
| `OK` | File matches in both locations |
| `NOTICE: X differences found` | Summary count of mismatches |
| `ERROR: ... not found in Dst` | File missing at destination |
| `ERROR: ... not found in Src` | Extra file at destination |
| `ERROR: ... sizes differ` | File size mismatch |
| `ERROR: ... hash differ` | Checksum mismatch (data corruption!) |

---

## `rclone check` Flags

### `--one-way`

Only check that all source files exist at destination. Ignore extra files at destination:

```powershell
rclone check source: dest: --one-way
```

This is the right flag for backup verification — you don't care that destination has *extra* files (old versions), only that all current source files are present.

### `--log-file` and `--log-level`

```powershell
rclone check onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --one-way `
    --log-file C:\Backup\verify-$(Get-Date -Format 'yyyy-MM-dd').log `
    --log-level INFO `
    --progress
```

### `--download`

For storage that doesn't support server-side checksums, download and hash locally:

```powershell
rclone check source: dest: --download
```

Slower but works with any storage backend. OneDrive supports server-side hashes, so `--download` is usually unnecessary.

### `--missing-on-dst FILE`

Write a list of missing files to a file for easy review:

```powershell
rclone check onedrive-personal: E:\OneDrive-Backup `
    --one-way `
    --missing-on-dst C:\Backup\missing-files.txt

# Review what's missing
Get-Content C:\Backup\missing-files.txt
```

### `--differ FILE`

Write a list of files that differ (checksum mismatch) to a file:

```powershell
rclone check onedrive-personal: E:\OneDrive-Backup `
    --differ C:\Backup\differ-files.txt

Get-Content C:\Backup\differ-files.txt
```

---

## Hash Commands: Creating Checksums

### Generate MD5 hashes for all files

```powershell
# For a local path
rclone md5sum E:\OneDrive-Backup > C:\Backup\manifest-md5.txt

# For a remote
rclone md5sum onedrive-personal: --filter-from filters.txt > C:\Backup\onedrive-md5.txt
```

Output format:
```
a3f4e2d1b0c9876543210987654321ab  Documents/Budget.xlsx
f8e7d6c5b4a3210987654321fedcba98  Documents/Resume.docx
01234567890abcdef01234567890abcd  Photos/2025/IMG_4521.jpg
```

### Generate SHA-1 hashes

```powershell
rclone sha1sum E:\OneDrive-Backup > C:\Backup\manifest-sha1.txt
```

### Generate SHA-256 hashes (most secure)

```powershell
rclone hashsum SHA-256 E:\OneDrive-Backup > C:\Backup\manifest-sha256.txt
```

> **VintageVault uses SHA-256** for its `_snapshot.json` checksums — this matches the gold standard for backup integrity verification.

---

## Building a Verification Workflow

### Step 1: Generate pre-backup manifest

Before each backup run, snapshot what's at destination:

```powershell
$date = Get-Date -Format "yyyy-MM-dd"
rclone md5sum E:\OneDrive-Backup > "C:\Backup\manifests\before-$date.txt"
```

### Step 2: Run backup

```powershell
rclone copy onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --log-file "C:\Backup\logs\backup-$date.log"
```

### Step 3: Run check

```powershell
rclone check onedrive-personal: E:\OneDrive-Backup `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --one-way `
    --log-file "C:\Backup\logs\verify-$date.log"

$checkExit = $LASTEXITCODE
```

### Step 4: Generate post-backup manifest

```powershell
rclone md5sum E:\OneDrive-Backup > "C:\Backup\manifests\after-$date.txt"
```

### Step 5: Compare manifests

```powershell
# Files added during this backup
$before = Get-Content "C:\Backup\manifests\before-$date.txt"
$after = Get-Content "C:\Backup\manifests\after-$date.txt"

# New or changed files
Compare-Object $before $after -PassThru |
    Where-Object { $_.SideIndicator -eq "=>" }
```

---

## The Complete Verification Script

Save as `C:\Backup\verify-backup.ps1`:

```powershell
<#
.SYNOPSIS
    Verify rclone backup integrity
.DESCRIPTION
    Checks that all source files exist at destination with matching checksums.
    Generates manifests and logs results.
#>

param(
    [string]$Source = "onedrive-personal:",
    [string]$Destination = "E:\OneDrive-Backup",
    [string]$FilterFile = "C:\Backup\rclone-onedrive-filters.txt",
    [string]$ReportDir = "C:\Backup\reports"
)

$date = Get-Date -Format "yyyy-MM-dd_HH-mm"
$reportFile = "$ReportDir\verify-$date.txt"
$missingFile = "$ReportDir\missing-$date.txt"
$differFile = "$ReportDir\differ-$date.txt"

New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null

function Log($msg) {
    $line = "$(Get-Date -Format 'HH:mm:ss') $msg"
    Write-Host $line
    Add-Content $reportFile $line
}

Log "=== Backup Verification Report ==="
Log "Source: $Source"
Log "Destination: $Destination"
Log ""

# Run rclone check
Log "Running checksum verification..."

& rclone check $Source $Destination `
    --filter-from $FilterFile `
    --one-way `
    --missing-on-dst $missingFile `
    --differ $differFile `
    --log-level INFO `
    --log-file $reportFile

$exitCode = $LASTEXITCODE

# Parse results
$missing = if (Test-Path $missingFile) { (Get-Content $missingFile).Count } else { 0 }
$differ = if (Test-Path $differFile) { (Get-Content $differFile).Count } else { 0 }

Log ""
Log "=== Results ==="
Log "Missing files (at destination): $missing"
Log "Differing files (checksum mismatch): $differ"
Log "rclone exit code: $exitCode"

if ($missing -eq 0 -and $differ -eq 0) {
    Log "✅ VERIFICATION PASSED — backup is complete and uncorrupted"
} elseif ($differ -gt 0) {
    Log "❌ VERIFICATION FAILED — $differ file(s) have checksum mismatches!"
    Log "   These may be corrupted. Re-run backup and check again."
    Log "   Differing files: $differFile"
} elseif ($missing -gt 0) {
    Log "⚠️  INCOMPLETE — $missing file(s) missing at destination"
    Log "   Re-run backup to copy missing files."
    Log "   Missing files: $missingFile"
}

Log ""
Log "Full report: $reportFile"

exit $exitCode
```

---

## Understanding Checksum Support by Backend

Not all backends support server-side checksums. When checksums aren't available, rclone falls back to size+modtime comparison:

| Backend | Hash Support | Notes |
|---------|-------------|-------|
| **OneDrive** | QuickXorHash, SHA-1, SHA-256 | ✅ Full checksum support |
| **Google Drive** | MD5 | ✅ Full support |
| **S3** | MD5 (ETag) | ✅ For files < 5GB |
| **Local filesystem** | MD5, SHA-1, SHA-256 | ✅ Via file hashing |
| **SFTP** | None by default | ❌ Use `--download` flag |

For OneDrive, rclone uses **QuickXorHash** (Microsoft's fast hash) by default. This is fast and reliable.

---

## Spot-Checking Individual Files

### Verify a single file

```powershell
# Check one file specifically
rclone check onedrive-personal:Documents/important.pdf E:\OneDrive-Backup\Documents `
    --include "important.pdf"
```

### Get a file's hash

```powershell
# Hash at source
rclone hashsum QuickXorHash onedrive-personal:Documents/important.pdf

# Hash at destination
rclone hashsum MD5 E:\OneDrive-Backup\Documents\important.pdf
```

Compare them — if different, the file is corrupt or transferred incorrectly.

---

## Verifying OneDrive-to-OneDrive Backups

```powershell
rclone check onedrive-personal: onedrive-backup:VaultBackup/latest `
    --filter-from C:\Backup\rclone-onedrive-filters.txt `
    --one-way `
    --progress `
    --log-file C:\Backup\cloud-verify.log
```

For cloud-to-cloud verification, rclone compares the QuickXorHash values that OneDrive provides via the API — no downloading required. This is fast and reliable.

---

## Reading rclone's Exit Codes

| Exit Code | Meaning |
|-----------|---------|
| `0` | Success — no differences found |
| `1` | Syntax or usage error |
| `2` | Error (files failed to transfer/compare) |
| `3` | Files at source not found at destination |
| `4` | Files differ (checksum or size mismatch) |
| `5` | Extra files at destination not in source |

In PowerShell, check with `$LASTEXITCODE` after running rclone.

---

## Scheduled Verification (Weekly)

Add to your Task Scheduler alongside backup (see Tutorial 09):

```powershell
# Run verification every Sunday, report to log
C:\Backup\verify-backup.ps1 | Out-File "C:\Backup\weekly-verify.log" -Append
```

---

## What to Do When Verification Fails

### Checksum mismatch (`--differ` has entries)

```powershell
# Re-copy just the differing files
$differFiles = Get-Content C:\Backup\differ-files.txt
foreach ($f in $differFiles) {
    rclone copy "onedrive-personal:$f" "E:\OneDrive-Backup\$(Split-Path $f -Parent)"
}

# Re-verify
rclone check onedrive-personal: E:\OneDrive-Backup --one-way
```

### Files missing at destination

```powershell
# Re-run the backup to pick up missing files
rclone copy onedrive-personal: E:\OneDrive-Backup --filter-from filters.txt

# Verify again
rclone check onedrive-personal: E:\OneDrive-Backup --one-way
```

### Persistent mismatch on same file

The file may be changing between backup and check (actively edited). Check if the file is in use:
```powershell
# Add to filter file to exclude the file and re-run
- /Documents/actively-edited-file.docx
```

---

## Next Steps

Continue to → [08 — Encryption with rclone crypt](08-encryption-with-crypt.md)
