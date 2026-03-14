# Decision Analysis: Backup Storage Format

_Created: 2026-03-14 | Model: Claude Opus 4.6_

## The Problem

The radical-transparency doc proposed weekly folder snapshots:

```
/VintageVault-Backup/
├── 2026-03-07/     ← Full copy (50 GB)
├── 2026-03-14/     ← Full copy (50 GB)
├── 2026-03-21/     ← Full copy (50 GB)
└── 2026-03-28/     ← Full copy (50 GB)

Total: 200 GB for 50 GB of source data. 4x storage explosion.
With 30-day retention: user's OneDrive usage QUADRUPLES.
```

**This is a dealbreaker.** OneDrive free tier is 5 GB. Even Microsoft 365's 1 TB would fill up fast for photo-heavy families. We'd be actively harming users by eating their quota.

This also violates backup fundamentals: incremental backup exists for exactly this reason.

---

## Storage Math

Assume: 50 GB source data, 2% weekly change rate, 30-day retention (4 snapshots).

| Approach | Initial | Week 2 | Week 3 | Week 4 | Total | Overhead |
|----------|---------|--------|--------|--------|-------|----------|
| **A: Full folder copies** | 50 GB | 50 GB | 50 GB | 50 GB | **200 GB** | **4.0x** |
| **B: Full ZIP copies** | ~35 GB | ~35 GB | ~35 GB | ~35 GB | **140 GB** | **2.8x** |
| **C: Full + incremental ZIPs** | ~35 GB | ~0.7 GB | ~0.7 GB | ~0.7 GB | **~37 GB** | **0.74x** |
| **D: Mirror + versions folder** | 50 GB | +1 GB | +1 GB | +1 GB | **~53 GB** | **1.06x** |
| **E: Mirror + deleted tracking** | 50 GB | ~0 | ~0 | +small | **~51 GB** | **1.02x** |

Options D and E are the clear winners on storage efficiency.

---

## Five Options Evaluated

### Option A: Full Folder Copies Per Snapshot ❌

```
/VintageVault-Backup/
├── 2026-03-07/     ← Complete copy of everything
├── 2026-03-14/     ← Complete copy of everything
└── 2026-03-21/     ← Complete copy of everything
```

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Storage efficiency | ❌ Terrible | 4x at 30-day retention |
| Transparency | ✅ Maximum | Browse any snapshot in File Explorer |
| Restore simplicity | ✅ Trivial | Just copy files back |
| Incremental support | ❌ None | Full copy every time |
| API efficiency | ❌ Terrible | Must copy ALL files every cycle |
| User quota impact | ❌ Destructive | Fills user's OneDrive rapidly |

**Verdict: Eliminated.** Storage explosion alone kills this.

### Option B: Full ZIP Per Snapshot ❌

```
/VintageVault-Backup/
├── snapshot-2026-03-07.zip    ← Everything, compressed (~35 GB)
├── snapshot-2026-03-14.zip    ← Everything, compressed (~35 GB)
└── snapshot-2026-03-21.zip    ← Everything, compressed (~35 GB)
```

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Storage efficiency | ❌ Bad | ~2.8x (compression helps but still multiplies) |
| Transparency | ❌ Poor | Can't browse without extracting |
| Restore simplicity | ⚠️ Moderate | Must download + extract entire ZIP |
| Incremental support | ❌ None | Must re-ZIP everything |
| API efficiency | ❌ Terrible | Must download all files, ZIP on server, upload |
| Portability | ✅ Great | Single file to download/share |

**Verdict: Eliminated.** Same storage problem as A (slightly better with compression), plus loses transparency and requires server-side processing.

### Option C: Full Copy + Incremental ZIPs ⚠️

```
/VintageVault-Backup/
├── Full/                       ← Initial full copy (browsable)
│   ├── Documents/
│   └── Photos/
├── Changes/
│   ├── 2026-03-14-changes.zip  ← Only changed files (~0.7 GB)
│   ├── 2026-03-21-changes.zip  ← Only changed files (~0.7 GB)
│   └── 2026-03-28-changes.zip  ← Only changed files (~0.7 GB)
└── manifest.json               ← Index of all backups
```

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Storage efficiency | ✅ Good | ~0.74x (less than source!) |
| Transparency | ⚠️ Split | Full copy is browsable; changes need extraction |
| Restore simplicity | ⚠️ Complex | Point-in-time requires Full + applying change ZIPs in order |
| Incremental support | ✅ Real incremental | Only changed files in each ZIP |
| API efficiency | ✅ Good | Only process changed files |
| Portability | ✅ Good | ZIPs are portable and standard |

**Issues:**
- The Full copy becomes stale unless periodically refreshed
- Restoring to a point-in-time is non-trivial (apply changes sequentially)
- Deleted files: how are they tracked? A change ZIP can't represent "this file was deleted"
- Requires server-side ZIP creation (download → ZIP → upload)
- Loses the "just folders" transparency for the incremental part

**Verdict: Viable but complex.** Good storage efficiency, but the split personality (browsable full + opaque ZIPs) undermines the transparency promise.

### Option D: Living Mirror + Versions Folder ⭐ RECOMMENDED

```
/VintageVault-Backup/
├── Mirror/                      ← Always matches current source state
│   ├── Documents/
│   │   ├── Tax Returns/
│   │   └── Family Budget.xlsx   ← Latest version
│   └── Photos/
│       └── Vacation 2025/
├── Versions/                    ← Previous versions of CHANGED files only
│   ├── Documents/
│   │   └── Family Budget.xlsx
│   │       ├── 2026-03-07.xlsx  ← Version from March 7
│   │       └── 2026-03-14.xlsx  ← Version from March 14
│   └── Photos/
│       └── (empty — photos didn't change)
├── Deleted/                     ← Files deleted from source
│   └── 2026-03-21/
│       └── old-document.docx    ← Removed from source on March 21
└── manifest.json                ← Complete backup index with checksums
```

**How it works:**
1. **Initial backup:** Copy everything to `/Mirror/` using `driveItem: copy` (server-side)
2. **Each subsequent cycle:**
   - Use delta API to detect changes since last run
   - **Changed file:** Move current Mirror copy → Versions/{path}/{date}.ext, then copy new version to Mirror
   - **New file:** Copy to Mirror
   - **Deleted file:** Move from Mirror → Deleted/{date}/{path}
   - **Unchanged file:** Skip entirely

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Storage efficiency | ✅ Excellent | ~1.06x (Mirror + small versions + deleted) |
| Transparency | ✅ High | Mirror is fully browsable; Versions are browsable too |
| Restore simplicity | ✅ Easy | Current state: browse Mirror. Old version: browse Versions/{file}/{date}. |
| Incremental support | ✅ Real incremental | Only changed files processed each cycle |
| API efficiency | ✅ Excellent | Delta API + copy/move only changed files |
| User understanding | ✅ Good | "Mirror is your backup. Versions has old copies. Deleted has removed files." |

**Why this is the best option:**
- **Mirror** is always browsable and always current — users can open it in File Explorer at any time
- **Versions** stores only the files that actually changed — no wasted storage on unchanged files
- **Deleted** catches files removed from source — the most common recovery scenario
- **Everything is plain folders and files** — no ZIPs, no proprietary formats
- Storage grows proportionally to actual changes, not total data size

**Storage example (50 GB source, 2% weekly change, 30 days):**
```
Mirror:     50.0 GB  (always matches source)
Versions:    3.0 GB  (4 weeks × 1 GB changed files, minus pruned old versions)
Deleted:     0.5 GB  (deleted files over 30 days)
manifest:    <1 MB
─────────────────
Total:     ~53.5 GB  (1.07x source)
```

Compare to full copies: 200 GB (4.0x source).

### Option E: Mirror-Only (No Versions) ⚠️

```
/VintageVault-Backup/
├── Mirror/                      ← Always matches current source state
│   └── ...
├── Deleted/                     ← Files deleted from source
│   └── 2026-03-21/
│       └── old-document.docx
└── manifest.json
```

Same as D but without the Versions folder. Changed files are simply overwritten in Mirror.

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Storage efficiency | ✅ Best possible | ~1.02x |
| Transparency | ✅ Maximum | Just folders |
| Restore simplicity | ✅ Trivial for current + deleted | |
| Point-in-time restore | ❌ Not supported | Only current state + deleted files |
| Incremental support | ✅ Only changed files processed | |

**Verdict: Viable for a simpler v1.** Protects against deletion and corruption (from the Mirror). Doesn't offer point-in-time "what did this look like last week?" — but that might be fine for an MVP. The Versions folder is a Phase 2 add.

---

## API Feasibility for Option D

**Using OneDrive `driveItem: copy` and `driveItem: move` — all server-side:**

| Operation | API Call | Server-Side? | Bandwidth Cost |
|-----------|---------|-------------|----------------|
| Initial full copy | `driveItem: copy` for each file | ✅ Yes | $0 |
| Detect changes | `delta` query | ✅ Yes | $0 |
| Copy new/changed file to Mirror | `driveItem: copy` | ✅ Yes | $0 |
| Move old version to Versions/ | `driveItem: move` (PATCH) | ✅ Yes | $0 |
| Move deleted file to Deleted/ | `driveItem: move` (PATCH) | ✅ Yes | $0 |
| Prune old versions (>30 days) | `driveItem: delete` | ✅ Yes | $0 |

**Every operation is server-side.** No downloads, no uploads, no bandwidth. The `move` operation (PATCH the `parentReference`) is perfect for shuffling files between Mirror/Versions/Deleted.

**Key insight:** `driveItem: move` is a metadata-only operation within the same drive — it doesn't create a new copy or use additional storage. The file content stays in the same place; only the folder reference changes. (Actually, for Versions we need a copy, not a move, since the Mirror should get the NEW version. So: copy old Mirror file → Versions, then delete old Mirror file, then copy new source file → Mirror. Or: move old Mirror → Versions, copy new source → Mirror.)

**Refined cycle for a changed file:**
```
1. Move /Mirror/Documents/Budget.xlsx → /Versions/Documents/Budget.xlsx/2026-03-14.xlsx
2. Copy source /Documents/Budget.xlsx → /Mirror/Documents/Budget.xlsx (driveItem: copy)
```
Two server-side operations per changed file. Zero bandwidth.

---

## Retention and Pruning

```
RETENTION POLICY (configurable):

Free:    Keep versions for 30 days. Prune weekly.
Pro:     Keep versions for 90 days.
Family:  Keep versions for 365 days.

PRUNING CYCLE (runs after each backup):
  For each file in /Versions/:
    If version date > retention period:
      Delete the version file
  For each folder in /Deleted/:
    If deletion date > retention period:
      Delete the folder
```

This keeps storage growth bounded even with long retention periods.

---

## Recommendation

### MVP (Phase 1): Option E — Mirror + Deleted

Simplest possible implementation. Protects against the most common scenario (accidental deletion). Low storage overhead (~1.02x).

```
/VintageVault-Backup/
├── Mirror/       ← Current state, always browsable
├── Deleted/      ← Deleted files, organized by date
└── manifest.json
```

### Phase 2: Upgrade to Option D — Add Versions

When users ask "I want the version from last week," add the Versions folder. Storage grows modestly (~1.06x).

```
/VintageVault-Backup/
├── Mirror/       ← Current state
├── Versions/     ← Previous versions of changed files
├── Deleted/      ← Deleted files
└── manifest.json
```

### What About ZIP?

**ZIP is the wrong tool for this job.** Here's why:

| | Folder-based (D/E) | ZIP-based |
|---|---|---|
| Incremental | ✅ Only changed files touched | ❌ Must rebuild ZIP or create many small ZIPs |
| Server-side | ✅ driveItem: copy/move (no bandwidth) | ❌ Must download, compress, re-upload |
| Browsable | ✅ File Explorer / OneDrive web | ❌ Must extract |
| Per-file restore | ✅ Just copy one file back | ❌ Must extract entire archive |
| Transparency | ✅ "It's just folders" | ❌ "It's a binary blob" |
| OneDrive API | ✅ Native operations | ❌ No ZIP API — must process server-side |
| Cost to us | ✅ $0 (all server-side) | ❌ Bandwidth for download/upload + compute for compression |

**ZIP only wins on portability** (single file to download/share). But users can download the Mirror folder as a ZIP from OneDrive's web UI natively — Microsoft already provides this. We don't need to create ZIPs ourselves.

---

## Impact on Radical Transparency

Option D/E is actually MORE transparent than full copies, not less:

- **Mirror is always browsable** — users see exactly what's backed up at any time
- **Versions is browsable** — users can see old versions organized by file path and date
- **Deleted is browsable** — users can see what was removed and when
- **manifest.json is human-readable** — complete index with timestamps, sizes, checksums
- **No proprietary format** — everything is plain folders, plain files, plain JSON
- **Users can DIY the same thing** — "Create a 'Backup' folder, copy your files there, move old copies to a 'Versions' subfolder"

---

## Summary

| | Full Copies (A) | Full ZIP (B) | Incremental ZIP (C) | Mirror+Versions (D) ⭐ | Mirror Only (E) |
|---|---|---|---|---|---|
| **Storage** | 4.0x ❌ | 2.8x ❌ | 0.74x ✅ | 1.06x ✅ | 1.02x ✅ |
| **Transparency** | Max ✅ | None ❌ | Split ⚠️ | High ✅ | Max ✅ |
| **Server-side** | Yes ✅ | No ❌ | No ❌ | Yes ✅ | Yes ✅ |
| **Our cost** | $0 | $$ | $$ | $0 | $0 |
| **Restore** | Trivial ✅ | Extract ⚠️ | Complex ❌ | Easy ✅ | Easy ✅ |
| **Incremental** | No ❌ | No ❌ | Yes ✅ | Yes ✅ | Yes ✅ |
| **Mission-aligned** | ⚠️ Wasteful | ❌ Opaque | ⚠️ Hybrid | ✅ Efficient + transparent | ✅ Simple + transparent |

**Decision: Start with E (Mirror+Deleted) for MVP, upgrade to D (add Versions) in Phase 2.**
