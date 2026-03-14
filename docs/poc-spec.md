# VintageVault POC — Requirements & Design

<p align="center">
  <img src="docs/branding/logo.svg" alt="VintageVault" width="120">
</p>

_Created: 2026-03-14 | Status: Draft — awaiting review before implementation_

---

## Goal

Prove that same-account immutable incremental snapshots work end-to-end via OneDrive APIs, with zero infrastructure cost. This is not a product launch — it's a technical proof-of-concept that validates the core architecture.

---

## Functional Requirements

### R1: OAuth Authentication

The user authenticates with their Microsoft account and grants VintageVault access to their OneDrive.

- OAuth2 Authorization Code flow with PKCE
- Scope: `Files.ReadWrite` (delegated, standard — no security assessment required)
- Store refresh token securely (local config file for POC; Azure Key Vault for production)
- Token refresh on expiry (Graph SDK handles this)

**Acceptance criteria:** User can authorize, and VintageVault can call Graph API on their behalf.

### R2: Initial Full Snapshot

On first run, copy all user files to a full snapshot folder.

```
User's OneDrive:
├── Documents/
│   ├── Tax Returns/
│   │   └── 2025.pdf
│   └── Family Budget.xlsx
└── Photos/
    └── Vacation/
        ├── photo001.jpg
        └── photo002.jpg

After initial snapshot:
├── Documents/              ← untouched
├── Photos/                 ← untouched
└── VintageVault-Backup/
    └── 2026-03-14-full/    ← new, immutable
        ├── Documents/
        │   ├── Tax Returns/
        │   │   └── 2025.pdf
        │   └── Family Budget.xlsx
        ├── Photos/
        │   └── Vacation/
        │       ├── photo001.jpg
        │       └── photo002.jpg
        └── _snapshot.json
```

- Create `/VintageVault-Backup/` root folder if it doesn't exist
- Create `/{date}-full/` subfolder
- Recursively copy all files via `driveItem: copy` (server-side, async)
- Wait for all copy operations to complete (poll monitor URLs)
- Write `_snapshot.json` with file inventory, sizes, and item IDs
- Write/update `manifest.json` at backup root

**Acceptance criteria:** All user files appear in the snapshot folder. `_snapshot.json` is accurate.

### R3: Incremental Snapshot

On subsequent runs, detect changes since the last snapshot and copy only changed/new files.

```
Changes since last snapshot:
  - Modified: Documents/Family Budget.xlsx
  - Added:    Documents/New Project.docx
  - Deleted:  Photos/Vacation/photo002.jpg

After incremental snapshot:
└── VintageVault-Backup/
    ├── 2026-03-14-full/        ← untouched (immutable)
    ├── 2026-03-21/             ← new incremental
    │   ├── Documents/
    │   │   ├── Family Budget.xlsx    ← modified file (new version)
    │   │   └── New Project.docx      ← new file
    │   └── _snapshot.json            ← records changes + deletions
    └── manifest.json                 ← updated master index
```

- Use `GET /me/drive/root/delta` to get changes since last run
- Store delta token between runs for efficient change detection
- Create `/{date}/` subfolder for this snapshot
- Copy only changed/new files via `driveItem: copy`
- Record deleted files in `_snapshot.json` (no file to copy, just metadata)
- Never touch previous snapshot folders
- Update `manifest.json`

**Acceptance criteria:** Only changed files appear in the new snapshot. Previous snapshots are untouched. Deleted files are recorded in metadata.

### R4: Snapshot Metadata

Each snapshot includes machine-readable metadata.

**`_snapshot.json` (per snapshot):**
```json
{
  "snapshotId": "2026-03-21",
  "type": "incremental",
  "previousSnapshot": "2026-03-14-full",
  "timestamp": "2026-03-21T03:00:00Z",
  "deltaToken": "aGVsbG8gd29ybGQ...",
  "summary": {
    "filesChanged": 1,
    "filesAdded": 1,
    "filesDeleted": 1,
    "totalFilesCopied": 2,
    "totalBytesCopied": 524288
  },
  "changes": [
    {
      "path": "/Documents/Family Budget.xlsx",
      "action": "modified",
      "size": 245760,
      "itemId": "abc123",
      "lastModified": "2026-03-20T14:30:00Z",
      "hashes": {
        "sha1": "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3",
        "quickXorHash": "base64encodedvalue=="
      },
      "copyVerified": true
    },
    {
      "path": "/Documents/New Project.docx",
      "action": "added",
      "size": 278528,
      "itemId": "def456",
      "lastModified": "2026-03-19T09:15:00Z",
      "hashes": {
        "sha1": "b6589fc6ab0dc82cf12099d1c2d40ab994e8410c",
        "quickXorHash": "anotherbase64value=="
      },
      "copyVerified": true
    },
    {
      "path": "/Photos/Vacation/photo002.jpg",
      "action": "deleted",
      "previousItemId": "ghi789"
    }
  ]
}
```

**`manifest.json` (master index at backup root):**
```json
{
  "version": 1,
  "accountId": "user@example.com",
  "backupRoot": "/VintageVault-Backup",
  "snapshots": [
    {
      "id": "2026-03-14-full",
      "type": "full",
      "timestamp": "2026-03-14T03:00:00Z",
      "fileCount": 847,
      "totalBytes": 12884901888
    },
    {
      "id": "2026-03-21",
      "type": "incremental",
      "timestamp": "2026-03-21T03:00:00Z",
      "previousSnapshot": "2026-03-14-full",
      "changesCount": 3,
      "bytesCopied": 524288
    }
  ],
  "lastDeltaToken": "aGVsbG8gd29ybGQ...",
  "detectionBaseline": {
    "avgChangesPerCycle": 47,
    "avgDeletionsPerCycle": 3
  }
}
```

**Acceptance criteria:** Both JSON files are written correctly and can be parsed by future restore tools.

### R5: Basic Anomaly Detection

Analyze the delta API response metadata to flag suspicious patterns.

```
Detection rules (all metadata-based, zero file downloads):

1. MASS CHANGE:    changesCount > 5 × avgChangesPerCycle
2. MASS DELETION:  deletionsCount > 5 × avgDeletionsPerCycle
3. EXTENSION SWAP: >10 files changed to known ransomware extensions
                   (.locked, .encrypted, .crypted, .enc, .ransom)
4. SIZE PATTERN:   >20 files changed to sizes that are exact multiples
                   of 16 bytes (AES block padding indicator)
```

POC behavior on detection:
- Log a warning to console with details
- Write a `_warning.json` file in the snapshot folder
- Continue the backup (don't pause — pausing is a production feature)

**Acceptance criteria:** When test data triggers a rule, a warning is logged and `_warning.json` is written.

### R6: CLI Interface

A command-line tool to run backup operations manually.

```
Commands:

  vintagevault auth                         # Start OAuth flow (opens browser)
  vintagevault backup                       # Run a backup cycle (full or incremental)
  vintagevault status                       # Show last backup info
  vintagevault snapshots                    # List all snapshots
  vintagevault include /Documents /Photos   # Set include-only filter
  vintagevault exclude /Videos/Raw          # Add exclusion rule
  vintagevault exclude --pattern "*.iso"    # Exclude by pattern
  vintagevault filters                      # Show current filter rules
  vintagevault filters --clear              # Reset to back up everything
```

No web UI, no scheduler, no dashboard for POC. Just a CLI that proves the engine works.

**Acceptance criteria:** All four commands work correctly against a real OneDrive account.

### R7: Checksum Integrity Verification

Record and verify file integrity using checksums provided by the OneDrive API — no file downloads needed.

**How it works:** OneDrive returns SHA-1 hash and QuickXorHash in the `file.hashes` property of every `driveItem`. This is free metadata — we don't need to download files to get it.

```
For each file in the backup:
  1. Read source file's hashes from delta API response (file.hashes.sha1Hash, file.hashes.quickXorHash)
  2. Record in _snapshot.json alongside the file entry
  3. After driveItem: copy completes, read the COPY's hashes from the copied driveItem
  4. Compare: source hash == copy hash → verified ✅
  5. If mismatch: log error, flag in _snapshot.json → integrity failure ❌
```

**What this catches:**
- Silent corruption during copy (rare but possible)
- Truncated transfers
- API bugs that produce incomplete copies
- Bit rot in older snapshots (via periodic spot-check verification in future phases)

**Snapshot metadata with checksums:**
```json
{
  "path": "/Documents/Family Budget.xlsx",
  "action": "modified",
  "size": 245760,
  "itemId": "abc123",
  "lastModified": "2026-03-20T14:30:00Z",
  "hashes": {
    "sha1": "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3",
    "quickXorHash": "base64encodedvalue=="
  },
  "copyVerified": true
}
```

**Acceptance criteria:** Every file in every snapshot includes source hashes. Post-copy verification confirms hash match. Mismatches are logged and flagged.

### R8: Include/Exclude File Filtering

Users can specify which folders/files to back up or skip. Essential for users with very large files (VMs, ISOs, video projects) that would consume excessive quota.

**CLI interface:**

```
Commands:

  vintagevault include /Documents /Photos   # Only back up these folders
  vintagevault exclude /Videos/Raw          # Skip this folder
  vintagevault exclude --pattern "*.iso"    # Skip files matching pattern
  vintagevault filters                      # Show current include/exclude rules
  vintagevault filters --clear              # Reset to "back up everything"
```

**Default behavior:** Back up everything (no filters). This is the simplest and safest default.

**Filter rules (stored in `~/.vintagevault/config.json`):**

```json
{
  "filters": {
    "mode": "exclude",
    "rules": [
      { "type": "folder", "path": "/Videos/Raw" },
      { "type": "pattern", "pattern": "*.iso" },
      { "type": "pattern", "pattern": "*.vmdk" }
    ]
  }
}
```

**Two modes:**
- **`exclude` mode (default):** Back up everything EXCEPT listed paths/patterns
- **`include` mode:** Back up ONLY listed paths (everything else skipped)

**Filter application:** During delta processing, check each changed file against filter rules before copying. Filtered files are recorded in `_snapshot.json` as `"action": "skipped"` with reason, so the manifest is honest about what's not backed up.

**Built-in exclusions (always skipped):**
- `/VintageVault-Backup/` (prevent infinite recursion)
- `.tmp` files, `~$` lock files (Office temp files)

**Acceptance criteria:** Excluded files are not copied. Included-only mode restricts to specified paths. `vintagevault filters` shows current rules. Skipped files are recorded in snapshot metadata.

---

## What the POC Does NOT Include

| Feature | Why deferred |
|---------|-------------|
| Web dashboard | POC validates the engine, not the UX |
| Scheduled backups | Manual trigger sufficient for POC |
| Email notifications | Console output sufficient for POC |
| Restore automation | Users browse backup folders manually |
| Cross-account backup | Phase 2 (Pro tier) |
| Azure deployment | Runs locally first |
| Content-based detection | Requires file downloads; metadata detection proves the concept |
| Multiple users | Single-user CLI tool for POC |
| Google Drive | Deferred ($15-75K OAuth assessment) |

---

## Technical Design

### Technology Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Language** | C# / .NET 8 | Best Graph SDK, best Azure integration, strongly typed, single-binary publishing, already configured in devcontainer |
| **Runtime** | .NET 8 (LTS) | Cross-platform (Windows, macOS, Linux), self-contained publish option |
| **Graph SDK** | `Microsoft.Graph` + `Azure.Identity` | Official Microsoft SDK, strongly typed, excellent async/batch support |
| **Auth** | MSAL (`Microsoft.Identity.Client`) | Handles OAuth2, PKCE, token refresh, device code flow |
| **CLI framework** | `System.CommandLine` | Official .NET CLI framework, subcommand routing, help generation |
| **Config storage** | Local JSON file (`~/.vintagevault/config.json`) | Simple, portable; encrypted in production |
| **Testing** | xUnit + Moq | .NET standard, strong async test support |
| **Linting** | `dotnet format` + .editorconfig | Built-in, zero additional tooling |
| **Publishing** | `dotnet publish --self-contained` | Single native binary, no runtime dependency for users |

### Project Structure

```
vintagevault/
├── src/
│   └── VintageVault.Cli/            # CLI application
│       ├── Program.cs                # Entry point + command routing
│       ├── Commands/
│       │   ├── AuthCommand.cs        # OAuth flow
│       │   ├── BackupCommand.cs      # Run backup cycle
│       │   ├── StatusCommand.cs      # Show backup status
│       │   └── SnapshotsCommand.cs   # List snapshots
│       ├── Engine/
│       │   ├── BackupOrchestrator.cs # Core backup flow coordination
│       │   ├── DeltaTracker.cs       # Delta API change detection
│       │   ├── SnapshotWriter.cs     # Create snapshot folders + copy files
│       │   ├── ManifestManager.cs    # Read/write manifest.json + _snapshot.json
│       │   ├── IntegrityChecker.cs   # Checksum verification (see R7)
│       │   └── AnomalyDetector.cs    # Metadata-based anomaly detection
│       ├── Graph/
│       │   ├── GraphClientFactory.cs # Authenticated Graph client setup
│       │   └── DriveOperations.cs    # Copy, create folder, write file, read hashes
│       └── Config/
│           └── ConfigStore.cs        # Local config read/write
├── tests/
│   └── VintageVault.Tests/
│       ├── Engine/
│       │   ├── BackupOrchestratorTests.cs
│       │   ├── DeltaTrackerTests.cs
│       │   ├── IntegrityCheckerTests.cs
│       │   └── AnomalyDetectorTests.cs
│       └── Graph/
│           └── DriveOperationsTests.cs
├── VintageVault.sln
├── README.md                         # Open source README
├── LICENSE                           # Apache 2.0
└── .editorconfig
```

### Backup Flow (Sequence)

```
vintagevault backup
       │
       ▼
  ┌─────────────┐
  │ Load config  │  Read ~/.vintagevault/config.json
  │ + tokens     │  Refresh OAuth token if expired
  └──────┬──────┘
         │
         ▼
  ┌─────────────┐
  │ Check for    │  Does /VintageVault-Backup/ exist?
  │ existing     │  Does manifest.json exist?
  │ backup root  │  Is there a delta token from last run?
  └──────┬──────┘
         │
    ┌────┴────┐
    │         │
  NO delta  HAS delta
  token     token
    │         │
    ▼         ▼
  ┌─────┐  ┌──────────┐
  │FULL │  │INCREMENTAL│
  │SNAP │  │SNAPSHOT   │
  └──┬──┘  └────┬─────┘
     │          │
     ▼          ▼
  ┌─────────────────┐
  │ Enumerate files  │  Full: list all via /me/drive/root/delta (no token)
  │ or get delta     │  Incremental: /me/drive/root/delta?token=xxx
  └──────┬──────────┘
         │
         ▼
  ┌─────────────────┐
  │ Anomaly check   │  Compare change counts to baseline
  │ (detection.ts)  │  Flag if suspicious, log warning
  └──────┬──────────┘
         │
         ▼
  ┌─────────────────┐
  │ Create snapshot  │  POST /me/drive/root/children
  │ folder           │  Name: {date} or {date}-full
  └──────┬──────────┘
         │
         ▼
  ┌─────────────────┐
  │ Copy files       │  For each changed/new file:
  │ (batch)          │    POST /drives/{id}/items/{id}/copy
  │                  │  Preserve folder structure in snapshot
  │                  │  Poll monitor URLs for completion
  └──────┬──────────┘
         │
         ▼
  ┌─────────────────┐
  │ Write metadata   │  _snapshot.json (this snapshot's details)
  │                  │  manifest.json (master index, updated)
  │                  │  _warning.json (if anomaly detected)
  └──────┬──────────┘
         │
         ▼
  ┌─────────────────┐
  │ Save state       │  Store new delta token in config
  │                  │  Update detection baseline averages
  └─────────────────┘
```

### Key Implementation Notes

**Folder structure in snapshots:** Snapshots must preserve the user's folder hierarchy. If the user has `/Documents/Work/report.docx`, the snapshot should contain `/{date}/Documents/Work/report.docx`. The `driveItem: copy` API requires creating parent folders first, then copying files into them.

**Async copy operations:** `driveItem: copy` returns `202 Accepted` with a monitor URL. We must poll this URL to confirm completion. For many files, batch copies and poll in parallel with reasonable concurrency (e.g., 10 concurrent copies).

**Delta API pagination:** The delta response may be paginated (multiple pages of changes). Must follow `@odata.nextLink` until we get a `@odata.deltaLink` (which contains the token for next run).

**Rate limiting:** Graph API has per-app and per-user limits. Implement exponential backoff on 429 responses. For POC with a single user, unlikely to hit limits.

**Excluded folders:** Don't back up the `/VintageVault-Backup/` folder itself (infinite recursion). Filter it from delta results.

---

## POC UX Mockup

The POC is CLI-only. See the [interactive mockup](poc/cli-mockup.html) for the expected terminal output.

### Auth Flow
```
$ vintagevault auth

VintageVault v0.1.0

Opening browser for Microsoft sign-in...
  → https://login.microsoftonline.com/common/oauth2/v2.0/authorize?...

Waiting for authorization...

✅ Authenticated as richard@example.com
   OneDrive: 847 files, 12.3 GB used of 1 TB
   Saved credentials to ~/.vintagevault/config.json
```

### First Backup (Full Snapshot)
```
$ vintagevault backup

VintageVault v0.1.0 — Backup Engine

📂 No existing backup found. Creating initial full snapshot...

Scanning OneDrive...
  Found 847 files (12.3 GB) across 43 folders

Creating snapshot folder: /VintageVault-Backup/2026-03-14-full/
Copying files...
  [████████████████████████████████████████] 847/847 files (12.3 GB)
  ├── Documents/          284 files   (2.1 GB)
  ├── Photos/             512 files   (9.8 GB)
  └── Other/               51 files   (0.4 GB)

Writing snapshot metadata...
  ├── _snapshot.json      (type: full, 847 files)
  └── manifest.json       (1 snapshot indexed)

✅ Full snapshot complete!
   Snapshot: 2026-03-14-full
   Files:    847
   Size:     12.3 GB
   Duration: 4 min 23 sec
   Location: OneDrive/VintageVault-Backup/2026-03-14-full/
```

### Incremental Backup
```
$ vintagevault backup

VintageVault v0.1.0 — Backup Engine

📂 Last snapshot: 2026-03-14-full (3 days ago)

Checking for changes...
  Delta API: 23 changes detected

  Modified:  8 files
  Added:     12 files
  Deleted:   3 files

🔍 Anomaly check: OK (23 changes vs. baseline ~47/week)

Creating snapshot folder: /VintageVault-Backup/2026-03-17/
Copying changed files...
  [████████████████████████████████████████] 20/20 files (148 MB)

Writing snapshot metadata...
  ├── _snapshot.json      (type: incremental, 23 changes)
  └── manifest.json       (2 snapshots indexed)

✅ Incremental snapshot complete!
   Snapshot: 2026-03-17
   Changed:  8 modified, 12 added, 3 deleted
   Copied:   20 files (148 MB)
   Duration: 12 sec
   Location: OneDrive/VintageVault-Backup/2026-03-17/
```

### Anomaly Detection Warning
```
$ vintagevault backup

VintageVault v0.1.0 — Backup Engine

📂 Last snapshot: 2026-03-17 (7 days ago)

Checking for changes...
  Delta API: 2,847 changes detected

⚠️  ANOMALY DETECTED
  ────────────────────────────────────────────
  Changes detected:    2,847
  Your normal average: ~47/week
  Ratio:               60.6× baseline

  Suspicious patterns found:
    • 2,340 files changed extension to .locked
    • 412 files changed to sizes that are exact
      multiples of 16 bytes (encryption padding)

  This is consistent with ransomware activity.
  ────────────────────────────────────────────

  ⚠️  Warning written to _warning.json
  📂 Backup will proceed (POC mode — production would pause)

Creating snapshot folder: /VintageVault-Backup/2026-03-24/
Copying changed files...
  [████████████████████████████████████████] 2847/2847 files (8.2 GB)

Writing snapshot metadata...
  ├── _snapshot.json      (type: incremental, 2847 changes)
  ├── _warning.json       (anomaly: mass extension change + size pattern)
  └── manifest.json       (3 snapshots indexed)

⚠️  Snapshot complete WITH WARNINGS
   Previous clean snapshot: 2026-03-17
   Recommend reviewing files before relying on this snapshot.
```

### Status Check
```
$ vintagevault status

VintageVault v0.1.0

Account:    richard@example.com
Backup root: OneDrive/VintageVault-Backup/

Snapshots:
  📦 2026-03-14-full     FULL          847 files   12.3 GB
  📦 2026-03-17          incremental   20 copied   148 MB
  📦 2026-03-24          incremental   2847 copied  8.2 GB  ⚠️ WARNING

Total backup size: ~20.6 GB
Last backup: 2026-03-24 (today)
Detection baseline: ~47 changes/week

Browse your backup: Open OneDrive → VintageVault-Backup
```

### Snapshots List
```
$ vintagevault snapshots

VintageVault v0.1.0

ID                    Type          Files    Size     Status
───────────────────────────────────────────────────────────────
2026-03-14-full       full          847      12.3 GB  ✅ Clean
2026-03-17            incremental   20       148 MB   ✅ Clean
2026-03-24            incremental   2847     8.2 GB   ⚠️ Anomaly detected

Total snapshots: 3
Total backup size: ~20.6 GB
Earliest recovery point: 2026-03-14
```

---

## What This POC Proves

| Question | How POC Answers It |
|----------|-------------------|
| Does `driveItem: copy` work for same-account backup? | Run a full snapshot, verify files appear in backup folder |
| Does the delta API reliably detect changes? | Modify files, run incremental, verify only changes are captured |
| Is immutable incremental storage-efficient? | Compare backup size to source size after several snapshots |
| Can we detect ransomware from metadata alone? | Simulate mass file changes, verify anomaly detection fires |
| Is the API fast enough? | Measure time for full and incremental snapshots |
| Does this work within OneDrive quotas? | Run against a real account and observe quota impact |

---

## Open Questions (Resolve During POC)

1. **Copy concurrency** — How many parallel `driveItem: copy` operations can we run before hitting rate limits? Start with 10, adjust based on 429 responses.
2. **Folder creation** — Does `driveItem: copy` auto-create parent folders, or must we create them first? Test and document.
3. **Delta API for initial full** — Can we use delta with no token to get a full file listing, or do we need `GET /me/drive/root/children` recursively?
4. **Large files** — Any special handling for files > 4 GB? (OneDrive supports up to 250 GB files)
5. **Special files** — How does `driveItem: copy` handle OneNote notebooks, SharePoint-linked files, or shortcut files?

---

## Development & Validation Flow

### Test Account Setup

**Do NOT test against your real OneDrive.** The POC will create folders and copy files — use a separate test account.

**Option A: New personal Microsoft account (recommended for POC)**
1. Create a free Microsoft account at outlook.com (e.g., `vintagevault-test@outlook.com`)
2. Sign in to OneDrive (free 5 GB)
3. Populate with test data (see below)
4. Register a Microsoft Entra app registration for the POC

**Option B: Microsoft 365 Developer Program**
1. Join at [developer.microsoft.com/microsoft-365/dev-program](https://developer.microsoft.com/en-us/microsoft-365/dev-program)
2. Get a free E5 sandbox with 25 user licenses and 1 TB OneDrive per user
3. Better for testing at scale, but heavier setup

**Recommended for POC:** Option A. Quick, free, sufficient for validating the architecture.

### App Registration (Microsoft Entra ID)

Required before any Graph API calls work:

1. Go to [portal.azure.com](https://portal.azure.com) → Azure Active Directory → App registrations → New
2. Name: `VintageVault POC`
3. Supported account types: "Personal Microsoft accounts only" (for POC)
4. Redirect URI: `http://localhost:3000/callback` (Web)
5. Under API permissions, add: `Microsoft Graph` → `Files.ReadWrite` (delegated)
6. Under Authentication, enable "Allow public client flows" (for device code flow fallback)
7. Record the **Application (client) ID** — needed in config
8. No client secret needed (public client with PKCE)

### Test Data

Populate the test OneDrive with representative data:

```
Test OneDrive structure:
├── Documents/
│   ├── Work/
│   │   ├── report.docx          (50 KB)
│   │   ├── spreadsheet.xlsx     (120 KB)
│   │   └── presentation.pptx   (2 MB)
│   ├── Personal/
│   │   ├── tax-return-2025.pdf  (500 KB)
│   │   └── notes.txt            (1 KB)
│   └── big-file.zip             (100 MB — test large file handling)
├── Photos/
│   ├── Vacation/
│   │   ├── photo001.jpg         (3 MB)
│   │   ├── photo002.jpg         (4 MB)
│   │   └── photo003.jpg         (3.5 MB)
│   └── Family/
│       └── portrait.png         (8 MB)
└── Videos/
    └── birthday.mp4             (500 MB — test exclusion filter)
```

Total: ~15-20 files, ~600 MB. Small enough to run quickly, varied enough to test folder structures, file types, and size ranges.

### Validation Script (Manual Test Checklist)

```
PHASE 1: AUTH
  [ ] Run `vintagevault auth`
  [ ] Browser opens, sign in with test account
  [ ] CLI shows "Authenticated as ..."
  [ ] Config file created at ~/.vintagevault/config.json

PHASE 2: FILTERS
  [ ] Run `vintagevault exclude /Videos`
  [ ] Run `vintagevault filters` — shows exclusion rule
  [ ] Verify Videos folder will be skipped

PHASE 3: FULL SNAPSHOT
  [ ] Run `vintagevault backup`
  [ ] CLI shows "Creating initial full snapshot..."
  [ ] Progress bar advances, all files copied
  [ ] Check OneDrive: /VintageVault-Backup/{date}-full/ exists
  [ ] Folder structure matches source (minus excluded /Videos)
  [ ] _snapshot.json exists with correct file inventory
  [ ] manifest.json exists at backup root
  [ ] Checksum verification: all files show copyVerified: true

PHASE 4: INCREMENTAL SNAPSHOT
  [ ] Modify Documents/Work/report.docx (change content)
  [ ] Add a new file: Documents/new-file.txt
  [ ] Delete Photos/Vacation/photo003.jpg
  [ ] Wait a few minutes (for OneDrive to sync changes)
  [ ] Run `vintagevault backup`
  [ ] CLI shows "incremental" with correct change count (3)
  [ ] Check OneDrive: new snapshot folder with only changed/added files
  [ ] Deleted file recorded in _snapshot.json
  [ ] Previous full snapshot is UNTOUCHED (verify timestamps)

PHASE 5: ANOMALY DETECTION
  [ ] Rename many files to .locked extension (or create a script to do this)
  [ ] Run `vintagevault backup`
  [ ] CLI shows anomaly warning with change count and patterns
  [ ] _warning.json written in snapshot folder

PHASE 6: STATUS COMMANDS
  [ ] Run `vintagevault status` — shows all snapshots
  [ ] Run `vintagevault snapshots` — shows table with sizes and status
  [ ] Anomaly snapshot shows warning indicator

PHASE 7: IMMUTABILITY VERIFICATION
  [ ] Run backup 3+ times with changes between each
  [ ] Verify EVERY previous snapshot folder has unchanged timestamps
  [ ] Verify no files were added/modified/deleted in old snapshots
```

### Development Workflow

```
1. SETUP
   - Create test Microsoft account + OneDrive
   - Register Entra app
   - Install .NET 8 SDK (if not present)
   - `dotnet new sln -n VintageVault`
   - `dotnet new console -n VintageVault.Cli`
   - Add NuGet packages: Microsoft.Graph, Azure.Identity,
     Microsoft.Identity.Client, System.CommandLine

2. BUILD ITERATIVELY
   - R1 (Auth) first — get a working Graph client
   - R8 (Filters) — simple config, needed before first backup
   - R2 (Full snapshot) — the core operation
   - R7 (Checksums) — integrate into the copy loop
   - R4 (Metadata) — write manifests
   - R3 (Incremental) — add delta API support
   - R5 (Anomaly detection) — analyze delta results
   - R6 (CLI polish) — wire up all commands

3. TEST EACH REQUIREMENT
   - Run against test OneDrive after each R is implemented
   - Verify results by browsing OneDrive web UI
   - Check manifests are parseable and accurate

4. VALIDATE END-TO-END
   - Run full validation checklist above
   - Document any open questions resolved
   - Note API quirks discovered
```

---

## Success Criteria

The POC is successful if:

1. ✅ Full snapshot captures all user files in correct folder structure
2. ✅ Incremental snapshot captures only changes (verified by modifying/adding/deleting files between runs)
3. ✅ Previous snapshots are never modified by new runs
4. ✅ Metadata manifests are accurate and parseable
5. ✅ File checksums are recorded from source and verified against copies
6. ✅ Anomaly detection correctly flags simulated ransomware patterns
7. ✅ Total process works with $0 bandwidth (all `driveItem: copy`, no file downloads)
8. ✅ Backup of 500+ files completes in under 10 minutes
9. ✅ Include/exclude filters correctly skip specified files
10. ✅ Skipped files are recorded in snapshot metadata
