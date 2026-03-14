# ADR-003: Backup Storage Format

**Status:** Accepted
**Date:** 2026-03-14
**Supersedes:** Earlier draft that proposed a "living mirror" approach

## The Problem

We need a storage format for same-account OneDrive backup that is:
1. **Storage efficient** — can't explode user's quota (rules out full copies per snapshot)
2. **Corruption safe** — can't propagate ransomware/corruption to the backup (rules out "living mirror")
3. **Transparent** — users can browse and understand it (rules out proprietary formats)
4. **Server-side** — all OneDrive API operations, $0 bandwidth (rules out ZIP)

The earlier draft proposed a "living mirror" that always reflected current state. This is **fatally flawed** — it's sync, not backup. If ransomware encrypts source files, the mirror overwrites good copies with encrypted ones. This is the exact scenario we exist to prevent.

## The Solution: Immutable Incremental Snapshots

**Core principle: once a snapshot is written, it is NEVER modified.**

```
/VintageVault-Backup/
├── 2026-03-01-full/          ← Initial full copy (IMMUTABLE once written)
│   ├── Documents/
│   │   ├── Tax Returns/
│   │   └── Family Budget.xlsx
│   ├── Photos/
│   │   └── Vacation 2025/
│   └── _snapshot.json        ← Metadata: type=full, file count, checksums
│
├── 2026-03-07/               ← Incremental: ONLY changed/new files (IMMUTABLE)
│   ├── Documents/
│   │   └── Family Budget.xlsx   ← New version of this file
│   └── _snapshot.json           ← Metadata: type=incremental, changes list
│
├── 2026-03-14/               ← Incremental: ONLY changed/new files (IMMUTABLE)
│   ├── Photos/
│   │   └── Spring Break/
│   │       └── photo001.jpg  ← New file added
│   └── _snapshot.json
│
├── 2026-03-21/               ← Incremental (IMMUTABLE)
│   └── _snapshot.json        ← Records: 0 files changed, 2 files deleted
│
└── manifest.json             ← Master index of all snapshots
```

### How Each Backup Cycle Works

```
1. Query delta API → get list of changes since last snapshot
2. Create new snapshot folder: /VintageVault-Backup/{date}/
3. For each CHANGED or NEW file:
     driveItem: copy → snapshot folder (server-side, $0)
4. For each DELETED file:
     Record in _snapshot.json (no file to store)
5. Write _snapshot.json with full change manifest
6. Update master manifest.json
7. DONE. Never touch this snapshot again.
```

### Why This Survives Ransomware

```
Timeline:
  Mar 1:  Full snapshot taken (50 GB, immutable)          ← CLEAN ✅
  Mar 7:  Incremental snapshot (1 GB changes, immutable)  ← CLEAN ✅
  Mar 14: Incremental snapshot (1 GB changes, immutable)  ← CLEAN ✅
  Mar 18: ⚠️ RANSOMWARE encrypts all source files
  Mar 21: Backup cycle detects mass changes...

Two outcomes:
  A) Anomaly detection PAUSES backup → Mar 14 snapshot is last clean copy ✅
  B) No detection, snapshot runs → Mar 21 snapshot contains encrypted files ❌
     BUT: Mar 1, Mar 7, Mar 14 snapshots are UNTOUCHED ✅
     Restore from Mar 14 = full recovery
```

**Compare to living mirror:**
```
  Mar 18: Ransomware encrypts source
  Mar 21: Mirror dutifully overwrites all backup files with encrypted versions
  Result: Mirror is corrupted. Versions folder MIGHT have old copies if not pruned.
  MUCH more fragile.
```

**The immutable snapshot model means: ransomware can corrupt the LATEST snapshot at worst, but can NEVER touch older snapshots.** They're already written and we never modify them.

## Storage Math

**50 GB source, 2% weekly change rate, 30-day retention:**

| Component | Size | Notes |
|-----------|------|-------|
| Full snapshot (baseline) | 50.0 GB | One-time, refreshed periodically |
| Week 1 incremental | ~1.0 GB | Only changed files |
| Week 2 incremental | ~1.0 GB | Only changed files |
| Week 3 incremental | ~1.0 GB | Only changed files |
| Week 4 incremental | ~1.0 GB | Only changed files |
| Manifests | < 1 MB | JSON metadata |
| **Total** | **~54 GB** | **1.08x source** |

Compare:

| Approach | Total Storage | Overhead |
|----------|-------------|----------|
| Full folder copies (4 weeks) | 200 GB | 4.0x ❌ |
| Full ZIPs (4 weeks) | 140 GB | 2.8x ❌ |
| Living mirror + versions | ~53 GB | 1.06x ⚠️ corruption risk |
| **Immutable incremental snapshots** | **~54 GB** | **1.08x ✅ safe** |

Nearly identical storage to the mirror approach, but **corruption-safe**.

## Restoration

### "I accidentally deleted a file last week"

```
1. Check manifest.json → find the most recent snapshot containing the file
2. Browse to /VintageVault-Backup/2026-03-14/Documents/file.docx
3. Copy it back (user drags it, or VintageVault restores via API)
```

### "Ransomware encrypted everything — restore to March 14"

```
1. Start with full snapshot: /2026-03-01-full/
2. Apply incrementals in order: 2026-03-07, 2026-03-14
3. Skip 2026-03-21 (contains encrypted files)
4. Result: complete state as of March 14
```

The manifest tracks which files appear in which snapshots, making this lookup fast. The VintageVault restore UI can automate this. For DIY users: the full snapshot is browsable, and each incremental is labeled by date.

### "I just want to see what's in my backup"

```
Browse /VintageVault-Backup/2026-03-01-full/ ← see your initial snapshot
Browse any incremental folder ← see what changed that week
```

Slightly less convenient than a "live mirror" but much safer. The full snapshot IS browsable; incrementals show only changes.

## Transparency

Everything is still plain folders and files:

- **No proprietary format** — every file is a standard copy
- **No ZIPs** — everything browsable in File Explorer / OneDrive web
- **_snapshot.json is human-readable** — lists what changed, when, with checksums
- **manifest.json is human-readable** — master index of all snapshots
- **Users can DIY this** — "create a dated folder, copy your changed files into it"

## Periodic Re-Baseline

Over time, restoring from the initial full + many incrementals becomes complex. Periodically create a new full snapshot:

```
Free:    New full baseline every 30 days (matches retention window)
Pro:     New full baseline every 90 days
Family:  New full baseline every 90 days (365-day retention)
```

After a new baseline, older full snapshots + their incrementals can be pruned.

## API Operations (All Server-Side, $0)

| Operation | OneDrive API | Cost |
|-----------|-------------|------|
| Detect changes | `GET /me/drive/root/delta` | $0 |
| Copy changed file to snapshot | `POST driveItem: copy` | $0 |
| Create snapshot folder | `POST /me/drive/root/children` | $0 |
| Write manifest | `PUT /me/drive/root:/path:/content` | $0 |
| Prune old snapshots | `DELETE driveItem` | $0 |

## Rejected Alternatives

### Living Mirror ❌
Overwrites backup with corrupted/encrypted files. Violates core principle: "backup, not sync."

### Full Folder Copies ❌
4x storage explosion. Impractical for user quotas.

### ZIP Archives ❌
Not browsable. Requires server-side processing (download + compress + upload). Can't use OneDrive server-side copy. Costs bandwidth.

### Full ZIPs with Incremental ZIPs ❌
Better storage than full ZIPs, but still requires server-side processing and loses transparency.

## Summary

| Property | Immutable Incremental Snapshots |
|----------|-------------------------------|
| **Storage** | ~1.08x source (excellent) |
| **Corruption safety** | ✅ Old snapshots NEVER modified |
| **Ransomware survival** | ✅ At worst, latest snapshot is corrupted; older ones intact |
| **Transparency** | ✅ Plain folders and files, browsable |
| **API efficiency** | ✅ All server-side, $0 bandwidth |
| **Incremental** | ✅ Only changed files per snapshot |
| **Point-in-time restore** | ✅ Via full baseline + incrementals |
| **DIY-friendly** | ✅ Users can replicate the approach manually |
| **Mission-aligned** | ✅ Efficient, transparent, corruption-safe |

