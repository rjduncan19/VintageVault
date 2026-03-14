# VintageVault POC — Requirements & Design

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
      "lastModified": "2026-03-20T14:30:00Z"
    },
    {
      "path": "/Documents/New Project.docx",
      "action": "added",
      "size": 278528,
      "itemId": "def456",
      "lastModified": "2026-03-19T09:15:00Z"
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

  vintagevault auth          # Start OAuth flow (opens browser)
  vintagevault backup        # Run a backup cycle (full if first, incremental otherwise)
  vintagevault status        # Show last backup info from manifest.json
  vintagevault snapshots     # List all snapshots with dates and sizes
```

No web UI, no scheduler, no dashboard for POC. Just a CLI that proves the engine works.

**Acceptance criteria:** All four commands work correctly against a real OneDrive account.

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
| **Language** | TypeScript | Fastest to prototype, largest open-source community, same language front-to-back |
| **Runtime** | Node.js 20+ | LTS, excellent Graph SDK support |
| **Graph SDK** | `@microsoft/microsoft-graph-client` + `@azure/msal-node` | Official Microsoft libraries |
| **Auth** | MSAL (Microsoft Authentication Library) for Node.js | Handles OAuth2, PKCE, token refresh |
| **CLI framework** | `commander` or simple `process.argv` parsing | Lightweight, no unnecessary dependencies |
| **Config storage** | Local JSON file (`~/.vintagevault/config.json`) | Simple, portable, encrypted in production |
| **Package manager** | npm | Standard |
| **Testing** | Vitest | Fast, TypeScript-native |
| **Linting** | ESLint + Prettier | Standard TypeScript toolchain |

### Project Structure

```
vintagevault/
├── src/
│   ├── cli.ts                # CLI entry point (auth, backup, status, snapshots)
│   ├── engine/
│   │   ├── backup.ts         # Core backup orchestration
│   │   ├── delta.ts          # Delta API change detection
│   │   ├── snapshot.ts       # Snapshot folder creation + file copying
│   │   ├── manifest.ts       # Read/write manifest.json and _snapshot.json
│   │   └── detection.ts      # Anomaly detection from metadata
│   ├── graph/
│   │   ├── auth.ts           # MSAL auth, token management
│   │   ├── client.ts         # Graph API client wrapper
│   │   └── operations.ts     # driveItem: copy, create folder, write file
│   └── config/
│       └── store.ts          # Local config read/write (~/.vintagevault/)
├── tests/
│   ├── engine/
│   │   ├── backup.test.ts
│   │   ├── delta.test.ts
│   │   ├── snapshot.test.ts
│   │   └── detection.test.ts
│   └── graph/
│       └── operations.test.ts
├── package.json
├── tsconfig.json
├── .eslintrc.json
├── .prettierrc
├── README.md                  # Open source README
└── LICENSE                    # Apache 2.0
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

## Success Criteria

The POC is successful if:

1. ✅ Full snapshot captures all user files in correct folder structure
2. ✅ Incremental snapshot captures only changes (verified by modifying/adding/deleting files between runs)
3. ✅ Previous snapshots are never modified by new runs
4. ✅ Metadata manifests are accurate and parseable
5. ✅ Anomaly detection correctly flags simulated ransomware patterns
6. ✅ Total process works with $0 bandwidth (all `driveItem: copy`, no file downloads)
7. ✅ Backup of 500+ files completes in under 10 minutes
