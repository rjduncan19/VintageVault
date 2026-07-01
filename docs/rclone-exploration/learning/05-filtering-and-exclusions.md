# 05 — Filtering & Exclusions

> **Goal:** Master rclone's powerful filter system to precisely control which files are included in your backup — excluding large media, temp files, system artifacts, and more.

---

## Why Filters Matter

Without filters, rclone copies *everything* in the source remote. For backup purposes, this is usually wrong:

```
What you have in OneDrive        What you actually want to back up
────────────────────────         ────────────────────────────────
📁 Documents/          ✅        📁 Documents/
📁 Photos/             ✅        📁 Photos/
📁 Videos/             ❌        (100 GB of movies you can re-download)
📁 Downloads/          ❌        (temp files, installers)
📄 desktop.ini         ❌        (Windows system file)
📄 ~$budget.xlsx       ❌        (temp lock file from Excel)
📄 huge-file.zip (8GB) ❌        (larger than your backup quota)
```

Filters let you define precisely what gets included.

---

## Filter Rule Syntax

```
# Comment
+ pattern   → include matching files/dirs
- pattern   → exclude matching files/dirs
```

**Rules are evaluated top-to-bottom. First match wins.**

### Pattern wildcards

| Wildcard | Matches | Example |
|----------|---------|---------|
| `*` | Any characters within one path segment | `*.jpg` matches `photo.jpg` but not `a/photo.jpg` |
| `**` | Any characters including path separators | `**.jpg` matches `photo.jpg` and `a/b/photo.jpg` |
| `?` | Any single character | `file?.txt` matches `file1.txt` |
| `[abc]` | Any character in brackets | `file[123].txt` |
| `/` at start | Anchors to root of remote | `/Videos/**` only matches top-level Videos |

---

## Three Ways to Apply Filters

### Method 1: Command-line flags (for quick, simple exclusions)

```powershell
# Exclude by extension
rclone copy onedrive-personal: dest: --exclude "*.tmp"

# Exclude a directory
rclone copy onedrive-personal: dest: --exclude "/Videos/**"

# Include only specific types
rclone copy onedrive-personal: dest: --include "*.{docx,xlsx,pdf}"
```

**Limitation:** Multiple `--include`/`--exclude` flags get complex. Use a filter file instead.

### Method 2: Filter file (recommended for production)

```powershell
rclone copy onedrive-personal: dest: --filter-from filters.txt
```

The file can have many rules, comments, and can be version-controlled.

### Method 3: Combine both

```powershell
rclone copy onedrive-personal: dest: `
    --filter-from filters.txt `
    --exclude "*.tmp"       # Additional exclusion on top of file
```

---

## Building a Production Filter File

Here's a comprehensive filter file for personal OneDrive backup, with explanations:

```
# ═══════════════════════════════════════════════════════════════
# OneDrive Backup Filter Rules
# Last updated: 2026-06-30
# Apply with: --filter-from this-file.txt
# ═══════════════════════════════════════════════════════════════


# ── PHASE 1: Explicit includes (override later excludes) ─────────

# Always include these critical folders regardless of later rules
+ /Documents/**
+ /Desktop/**
+ /Attachments/**


# ── PHASE 2: Large media exclusions ─────────────────────────────

# Exclude entire Videos folder (re-downloadable content)
- /Videos/**
- /Movies/**
- /TV Shows/**
- /Recordings/**
- /Camera Roll/**

# But wait — keep SMALL videos (screen recordings under 100MB)
# can't do this with path rules alone, use --max-size separately


# ── PHASE 3: Junk and temp exclusions ────────────────────────────

# OS system files
- desktop.ini
- Thumbs.db
- .DS_Store
- .Spotlight-V100/**
- .fseventsd/**
- .TemporaryItems/**
- ehthumbs.db
- ehthumbs_vista.db

# Application temp files
- *.tmp
- *.temp
- *.bak
- *.swp
- *.swo
- ~$*              # Office lock files (e.g., ~$document.docx)
- *.~*             # Various temp patterns

# Windows shortcuts (usually not useful in backups)
- *.lnk

# Python/Node.js build artifacts (if you dev in OneDrive)
- __pycache__/**
- .pytest_cache/**
- node_modules/**
- .npm/**
- *.pyc
- *.pyo
- dist/**
- build/**
- .next/**

# IDE metadata
- .vscode/**
- .idea/**
- *.suo
- *.user
- .vs/**

# Git internals (objects are binary, huge, not needed in backup)
# Note: keep .git folder presence but not the objects
- .git/objects/**
- .git/refs/**


# ── PHASE 4: Downloads folder ────────────────────────────────────

# Downloads usually has installers, not documents
# If you store documents there, move them first
- /Downloads/**


# ── PHASE 5: Include everything else ─────────────────────────────

# Catch-all include (without this, unmatched files default to include,
# but making it explicit is clearer)
+ **
```

---

## The `--max-size` and `--min-size` Flags

These work alongside filter files:

```powershell
# Exclude files larger than 500 MB
rclone copy onedrive-personal: dest: `
    --filter-from filters.txt `
    --max-size 500M

# Only back up files larger than 1 KB (skip near-empty files)
rclone copy onedrive-personal: dest: `
    --filter-from filters.txt `
    --min-size 1k

# Combine both: back up files between 1K and 1G
rclone copy onedrive-personal: dest: `
    --filter-from filters.txt `
    --min-size 1k `
    --max-size 1G
```

Size suffixes: `b`, `k`/`K`, `M`, `G`, `T`, `P`

---

## Testing Your Filter Rules

Before committing to a filter file, test it:

### See what would be transferred

```powershell
rclone copy onedrive-personal: dest: `
    --filter-from filters.txt `
    --dry-run `
    --log-level INFO 2>&1 | Where-Object { $_ -match "Not copying" }
```

### See what would be excluded

```powershell
rclone ls onedrive-personal: `
    --filter-from filters.txt `
    --log-level DEBUG 2>&1 | Select-String "excluded"
```

### Count what would be included vs. excluded

```powershell
# Total files
$total = (& rclone ls onedrive-personal:).Count
Write-Host "Total: $total"

# Files after filter
$filtered = (& rclone ls onedrive-personal: --filter-from filters.txt).Count
Write-Host "After filter: $filtered"
Write-Host "Excluded: $($total - $filtered)"
```

### Compare sizes

```powershell
# Size without filters
rclone size onedrive-personal:

# Size with filters
rclone size onedrive-personal: --filter-from filters.txt
```

---

## Filter Ordering Gotchas

This is the most common source of confusion. Rules are evaluated in order, and the **first match wins**.

### ❌ Wrong order (directory excluded before file included)

```
- /Work/**
+ /Work/Critical-Contract.pdf
```

Result: `Critical-Contract.pdf` is **excluded** because `/Work/**` matches first.

### ✅ Correct order (specific include before general exclude)

```
+ /Work/Critical-Contract.pdf
- /Work/**
```

Result: `Critical-Contract.pdf` is **included**, rest of Work is excluded.

### The directory problem

If you exclude a directory, rclone won't descend into it, even if a later rule would include files inside it. Fix: exclude individual files, or restructure your folders.

```
# If you want to exclude all of /Archive/ but keep /Archive/Important/:
# This DOESN'T work:
- /Archive/**
+ /Archive/Important/**   # Too late — Archive was already excluded

# This DOES work:
+ /Archive/Important/**   # Include first
- /Archive/**             # Then exclude the rest
```

---

## Filters for Common Scenarios

### Back up only documents (not media)

```
+ *.{doc,docx,xls,xlsx,ppt,pptx,pdf,txt,md,rtf}
+ *.{csv,json,xml,yaml,yml}
- **
```

### Back up photos but not videos

```
+ *.{jpg,jpeg,png,gif,heic,heif,raw,cr2,nef,dng}
+ *.{tif,tiff,bmp,webp}
- *.{mp4,mov,avi,mkv,wmv,flv,webm}
+ **
```

### Back up only recent files (using `--max-age`)

```powershell
# Only files modified in the last 30 days
rclone copy onedrive-personal: dest: --max-age 30d

# Only files modified since a specific date
rclone copy onedrive-personal: dest: --max-age 2026-01-01
```

### Back up a specific subfolder only

```powershell
# Only Documents
rclone copy onedrive-personal:Documents dest:Documents-Backup
```

---

## The `--filter` Flag vs. `--filter-from`

```powershell
# Inline filter rule (can be repeated)
rclone copy source: dest: --filter "- *.tmp" --filter "- /Videos/**"

# Load from file (recommended for complex rules)
rclone copy source: dest: --filter-from C:\Backup\filters.txt
```

You can combine both — inline filters are appended to file-based filters.

---

## Debugging Filters

Enable debug logging to see every filter decision:

```powershell
rclone ls onedrive-personal: `
    --filter-from filters.txt `
    --log-level DEBUG 2>&1 |
    Select-String "filter|include|exclude" |
    Select-Object -First 50
```

Output shows each file's filter decision:
```
DEBUG: Photos/2025/party.jpg: included
DEBUG: Videos/movie.mp4: excluded
DEBUG: desktop.ini: excluded
DEBUG: Documents/Resume.docx: included
```

---

## Filters in the VintageVault Context

VintageVault implements filters through its `ExclusionManager.cs`. When a user adds an exclusion via:

```
vintagevault exclude /Videos
```

Under the hood, this adds a rule equivalent to `- /Videos/**` in an rclone filter file. The UX is simpler (a path picker), but the underlying logic is the same.

Understanding rclone's filter system helps you:
1. Design VintageVault's exclusion rule syntax
2. Know which edge cases to test (directory exclusions, wildcard ordering)
3. Translate user-friendly paths into correct filter rules

---

## Next Steps

Continue to → [06 — Sync vs Copy vs Move Operations](06-sync-copy-move-operations.md)
