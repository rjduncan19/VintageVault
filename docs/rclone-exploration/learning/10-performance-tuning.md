# 10 — Performance Tuning

> **Goal:** Make your rclone backups as fast, efficient, and reliable as possible — through parallelism, bandwidth control, transfer optimization, and smart scheduling.

---

## The Performance Levers

rclone has three primary performance dimensions:

```
┌─────────────────────────────────────────────────────────────────┐
│                rclone Performance Model                         │
│                                                                 │
│   SPEED           ────────────────────────────────────          │
│                                                                 │
│   1. Parallel Transfers    (--transfers N)                      │
│      How many files are copied simultaneously                   │
│                                                                 │
│   2. Parallel Checkers     (--checkers N)                       │
│      How many checksum comparisons run simultaneously           │
│                                                                 │
│   3. Chunk size            (--onedrive-chunk-size)              │
│      For large files: size of each upload chunk                 │
│                                                                 │
│   CONTROL         ────────────────────────────────────          │
│                                                                 │
│   4. Bandwidth limit       (--bwlimit)                          │
│      Cap total transfer speed                                   │
│                                                                 │
│   5. API rate limit        (--tpslimit)                         │
│      Cap API calls per second (avoid 429 errors)               │
│                                                                 │
│   6. Retry settings        (--retries, --low-level-retries)     │
│      How aggressively to retry on failure                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Understanding Your Bottleneck

Before tuning, identify what's limiting your backup speed:

### Is it your internet connection?

```powershell
# Test upload speed (to OneDrive)
rclone test memory --drive-chunk-size 256M 2>&1
# Or simply run a backup and watch the MiB/s in --progress output
```

If speed is close to your ISP's upload limit → you're bandwidth-limited. Adding more `--transfers` won't help.

### Is it the API (too many small files)?

Lots of small files (< 1 MB) can be slower than a few large ones because each file requires multiple API calls:
1. Check if file exists
2. Request upload session
3. Upload chunk(s)
4. Finalize

If you're transferring 10,000 small files and seeing < 50% of your bandwidth used → you're API-limited. Increase `--transfers` and `--checkers`.

### Is it the SSD write speed?

For OneDrive → SSD backups, if you're maxing out your SSD's write speed:
```powershell
# Check write speed of SSD
winsat disk -drive E 2>&1 | Select-String "Disk Sequential"
```

If SSD is the bottleneck, adding more `--transfers` won't help and may make things worse.

---

## Key Flags and Their Effects

### `--transfers N` (default: 4)

How many files are uploaded/downloaded simultaneously.

```powershell
--transfers 4    # Good default
--transfers 8    # Good for many small files + fast connection
--transfers 16   # High-bandwidth, low-latency connections
--transfers 32   # Rarely helpful; may hit API limits
```

**Increase when:**
- Many small files (< 1 MB each)
- Fast internet (100+ Mbps)
- Low per-request latency to OneDrive

**Decrease when:**
- Getting 429 rate limit errors
- Uploading very large files (chunked upload is already parallel)
- Limited RAM (each transfer uses memory)

### `--checkers N` (default: 8)

How many files are checked (compared to see if they need copying) simultaneously.

```powershell
--checkers 8     # Default — usually fine
--checkers 16    # Better for large remote catalogs
--checkers 32    # For very large OneDrives (100k+ files)
```

Checkers are fast — each check is just an API call to get metadata. Set checkers 2x your transfers count:

```
--transfers 8 --checkers 16
--transfers 16 --checkers 32
```

### `--bwlimit SPEED` (default: off)

Caps total bandwidth usage.

```powershell
--bwlimit 5M      # 5 MB/s (good for background operation on slow connection)
--bwlimit 20M     # 20 MB/s
--bwlimit 100M    # 100 MB/s
--bwlimit 0       # No limit (use all available bandwidth)
```

**Time-based bandwidth scheduling:**
```powershell
# Use 10 MB/s during business hours, unlimited at night
--bwlimit "08:00,10M 23:00,0"

# Use 5 MB/s Mon-Fri, unlimited on weekends
--bwlimit "Mon-Fri 08:00,5M Mon-Fri 22:00,0 Sat,0 Sun,0"
```

> **VintageVault application:** Users should be able to configure "backup bandwidth limit" in the UI. The underlying behavior maps directly to `--bwlimit`.

### `--tpslimit N` (default: off)

Limits transactions per second to OneDrive's API. Helps avoid 429 rate limit errors:

```powershell
--tpslimit 4     # 4 API calls/second (conservative, safe)
--tpslimit 10    # More aggressive
--tpslimit 0     # No limit (default)
```

### `--retries N` (default: 3)

How many times to retry a failed transfer before giving up:

```powershell
--retries 3      # Default
--retries 5      # More resilient on flaky connections
--retries 10     # Very aggressive retry (slow connections)
```

### `--low-level-retries N` (default: 10)

How many times to retry a single chunk of a large file upload:

```powershell
--low-level-retries 10   # Default
--low-level-retries 20   # For very large files on unstable connections
```

---

## OneDrive-Specific Tuning

### Chunk size for large files

OneDrive requires chunked uploads for files > 4 MB. The chunk size affects:
- Memory usage (each chunk is held in RAM while uploading)
- Number of API calls (fewer, larger chunks = fewer calls but slower error recovery)

```powershell
--onedrive-chunk-size 10M    # Default (10 MB chunks)
--onedrive-chunk-size 50M    # Fewer API calls, higher memory
--onedrive-chunk-size 100M   # For very large files on reliable connections
--onedrive-chunk-size 250M   # Maximum chunk size (OneDrive limit)
```

For backing up many large files (videos, disk images):
```powershell
--onedrive-chunk-size 100M --transfers 2 --checkers 4
```

For many small documents:
```powershell
--onedrive-chunk-size 10M --transfers 8 --checkers 16
```

### Enable server-side hashing

OneDrive's QuickXorHash is faster than downloading files for checksums:

```powershell
# Verify rclone is using QuickXorHash (it should by default for OneDrive)
rclone hashsum QuickXorHash onedrive-personal:Documents/small-file.txt
```

### Delta API for incremental performance

For subsequent backups, enable delta queries to get only changed files:

```powershell
# Use OneDrive delta (automatic in rclone if supported)
# Check rclone docs for --onedrive-delta flag support in your version
rclone copy onedrive-personal: dest: --fast-list
```

`--fast-list` uses fewer API calls by fetching directory listings in bulk, but uses more memory.

---

## Performance Profiles

### Profile: Background (low-impact, always running)

```powershell
rclone copy onedrive-personal: onedrive-backup:VaultBackup/latest `
    --filter-from filters.txt `
    --transfers 2 `
    --checkers 4 `
    --bwlimit 5M `
    --tpslimit 2 `
    --stats 120s
```

Good for: continuous/incremental backup running all day without disrupting other network usage.

### Profile: Daily overnight (fast, full speed)

```powershell
rclone copy onedrive-personal: onedrive-backup:VaultBackup/latest `
    --filter-from filters.txt `
    --transfers 8 `
    --checkers 16 `
    --tpslimit 8 `
    --stats 30s `
    --progress
```

Good for: scheduled at 2 AM when nobody's using the network.

### Profile: Initial backup (maximum speed, monitored)

```powershell
rclone copy onedrive-personal: onedrive-backup:VaultBackup/latest `
    --filter-from filters.txt `
    --transfers 16 `
    --checkers 32 `
    --onedrive-chunk-size 50M `
    --stats 10s `
    --progress
```

Good for: one-time initial backup where you're watching it and want maximum speed.

### Profile: Recovery (focused, prioritized)

```powershell
# When restoring specific files — single-threaded for reliability
rclone copy onedrive-backup:VaultBackup/latest/Documents/ C:\Restored\Documents\ `
    --transfers 1 `
    --checksum `
    --progress `
    --log-level INFO
```

---

## Measuring Performance

### Benchmark your connection

```powershell
# Upload speed to OneDrive (create a 1 GB test file and copy it)
$null = fsutil file createnew C:\temp\rclone-bench.bin 1073741824
rclone copy C:\temp\rclone-bench.bin onedrive-personal:benchmark-test/ --progress
rclone deletefile onedrive-personal:benchmark-test/rclone-bench.bin
Remove-Item C:\temp\rclone-bench.bin
```

### Read stats from logs

rclone's `--stats 60s` adds periodic stats to the log:

```
2026/06/30 02:30:00 NOTICE: Transferred:   5.234 GiB / 31.456 GiB, 17%, 8.456 MiB/s, ETA 52m35s
2026/06/30 02:31:00 NOTICE: Transferred:   5.789 GiB / 31.456 GiB, 18%, 9.123 MiB/s, ETA 48m20s
```

Parse speed from logs:
```powershell
Get-Content C:\Backup\logs\backup-latest.log |
    Select-String "MiB/s" |
    ForEach-Object { ($_ -split ',')[2].Trim() }
```

---

## Reducing API Calls (OneDrive Efficiency)

OneDrive's Graph API has soft rate limits (~1,000-3,000 requests per minute per application). To stay within limits:

### Use `--fast-list`

```powershell
# Lists directories with fewer API calls (one call per dir instead of paginated)
rclone copy ... --fast-list
```

Trade-off: uses more memory (holds entire directory listing in RAM).

### Use `--no-traverse`

For operations copying known files (not scanning for changes):

```powershell
# Skip scanning destination — just copy everything from source
rclone copy ... --no-traverse
```

Faster when destination is known empty or you want to force re-copy.

### Reduce checksum calls

If you trust size+modtime comparison (faster, less safe):

```powershell
# Skip checksum; use size+modtime only
rclone copy ... --size-only
```

---

## Memory Usage

Each transfer goroutine uses memory proportional to chunk size:

```
Memory ≈ (--transfers + --checkers) × --onedrive-chunk-size

Example:
  --transfers 8 --checkers 16 --onedrive-chunk-size 50M
  = (8 + 16) × 50M = 1.2 GB RAM
```

For machines with limited RAM (4 GB total):
```powershell
--transfers 4 --checkers 8 --onedrive-chunk-size 10M
# ≈ 120 MB RAM
```

---

## Diagnosing Slowness

### Check for 429 errors (rate limiting)

```powershell
Get-Content C:\Backup\logs\backup-latest.log |
    Select-String "429|rate limit|too many"
```

If found: add `--tpslimit 2` and reduce `--transfers`.

### Check for retries (connection issues)

```powershell
Get-Content C:\Backup\logs\backup-latest.log |
    Select-String "retry|Retrying"
```

Many retries indicate network instability. Consider running at off-peak hours.

### Check for errors

```powershell
Get-Content C:\Backup\logs\backup-latest.log |
    Select-String "ERROR|CRITICAL"
```

---

## Summary: Recommended Settings by Scenario

| Scenario | `--transfers` | `--checkers` | `--bwlimit` | `--tpslimit` | Chunk Size |
|----------|--------------|-------------|------------|-------------|-----------|
| Background (day) | 2 | 4 | 5M | 2 | 10M |
| Daily overnight | 8 | 16 | none | 4 | 10M |
| Initial backup | 16 | 32 | none | 8 | 50M |
| Large files only | 2 | 4 | none | 4 | 100M |
| Slow/mobile connection | 2 | 4 | 1M | 1 | 5M |
| Verification only | 0 | 32 | none | 4 | N/A |

---

## Connecting This to VintageVault

VintageVault's backup engine should expose these settings as user-configurable options:

```json
{
  "backup": {
    "parallelTransfers": 4,
    "bandwidthLimitMBps": 0,
    "scheduleHour": 2,
    "chunkSizeMB": 10
  }
}
```

The UI surfaces these as:
- "Backup speed" slider (maps to `--bwlimit`)
- "Run at" time picker (maps to Task Scheduler trigger)
- Advanced: parallel transfers (for power users)

Sensible defaults (low transfers, limited bandwidth) mean backups run respectfully in the background. Power users can unlock full speed.

---

## You're Done! 🎉

You've completed the rclone learning series. Here's what you've covered:

| Tutorial | Key Takeaway |
|----------|-------------|
| 00 | rclone's architecture and how it informs VintageVault |
| 01 | Installation and first remote configuration |
| 02 | Custom Azure app — never share API quota with all rclone users |
| 03 | OneDrive → SSD backup with filters and verification |
| 04 | OneDrive → OneDrive cross-account ransomware protection |
| 05 | Filter rules — precisely control what gets backed up |
| 06 | copy vs. sync vs. move — use copy for backup, always |
| 07 | Checksum verification — don't trust backups you haven't checked |
| 08 | rclone crypt — zero-knowledge encrypted backup |
| 09 | Windows Task Scheduler — automated, unattended backups |
| 10 | Performance tuning — make it fast without breaking things |

Return to → [rclone Exploration README](../README.md)
