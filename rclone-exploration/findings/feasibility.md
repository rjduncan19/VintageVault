# Feasibility: rclone as VintageVault's Transfer Engine

**Todo:** `research-feasibility` · **Status:** drafted
**rclone version validated:** v1.74.3 (Windows amd64)

## TL;DR

- **Q1 — Cross-provider backup?** **Yes.** rclone copies/syncs between any two of its
  70+ backends, including OneDrive ↔ Google Drive.
- **Q2 — How much infra to leverage?** **Most of the engine.** OAuth, transfers,
  retries, throttling, checksums, incremental detection, versioning, filtering,
  encryption, and a remote-control API all come for free under a permissive (MIT)
  license. VintageVault's effort shifts almost entirely to the product layer
  (see [`gaps.md`](gaps.md)).

---

## Q1 — Does rclone back up from one cloud provider to another?

Yes, with an important nuance that **exactly matches VintageVault's own ADR-002**:
there is no magic direct cloud-to-cloud transfer; an intermediary is always required
except where a provider offers server-side copy.

| Provider pair | Mechanism | Bandwidth path | Notes |
|---------------|-----------|----------------|-------|
| **OneDrive → Google Drive** (cross-provider, P0) | `rclone copy/sync` | Download from OneDrive → **this machine** → upload to Google | No server-side transfer exists between providers. `--drive-server-side-across-configs` does **not** help here. |
| **Google → Google** (same-provider, P0) | `rclone copy --drive-server-side-across-configs` | **Server-side** — bytes stay inside Google | Zero local bandwidth. This is ADR-002's "share-then-copy" efficiency win. |
| **OneDrive → OneDrive** | `rclone copy/sync` | Download + upload via this machine | No server-side cross-account copy (Graph limitation — matches ADR-002). |

### Backup-grade features rclone provides out of the box
- `--backup-dir` — move replaced/deleted files aside → **versioning / point-in-time**.
- `--immutable` — refuse to modify existing destination files → **WORM-style protection**.
- Filter/`--exclude` rules → maps to VintageVault's `exclude` command.
- `crypt` remote → **client-side encryption** (zero-knowledge destination).
- `--bwlimit`, `--transfers`, `--checkers` → polite, tunable home-connection behavior.
- `bisync` → two-way sync (use with care; can propagate deletes).

### Evidence (captured in `results/`)

`local-demo.ps1` (local folders as remotes — no cloud accounts needed) proved, against
real rclone v1.74.3:

```
Backup #1: copy with --exclude "/Videos/**"  -> Videos skipped, budget.xlsx + photo.jpg copied
Backup #2: incremental + --backup-dir        -> only changed/new files copied; PRIOR budget.xlsx preserved in archive
Backup #3: --immutable on a modified file     -> ERROR: "immutable file modified" (refused) ✅
```

This demonstrates the exact flags the cloud scripts use; only the remote endpoints
differ. The live OneDrive/Google runs require interactive OAuth with **test accounts**
(see `backup-cross-provider.ps1`, `backup-gdrive-server-side.ps1`).

---

## Q2 — How much of rclone's infrastructure can we leverage?

### Engine capabilities VintageVault would NOT have to build
| Capability | rclone | VintageVault custom C# today |
|------------|:------:|:----------------------------:|
| OneDrive + Google Drive backends | ✅ (70+ backends) | ⚠️ OneDrive only (Graph) |
| OAuth + token refresh | ✅ | ⚠️ partial (Graph) |
| Chunked / resumable uploads | ✅ | ❓ to build |
| Retries, throttling/backoff | ✅ | ❓ to build |
| Checksum verification | ✅ | ❓ to build |
| Incremental change detection | ✅ | ⚠️ manifest-based |
| Versioning (`--backup-dir`) | ✅ | ⚠️ snapshots model |
| Immutability (`--immutable`) | ✅ | ❓ |
| Client-side encryption (`crypt`) | ✅ | ❌ |
| Bandwidth limiting | ✅ | ❌ |
| Server-side Google copy | ✅ | ❌ |
| Remote-control API (`rcd`) | ✅ | ❌ |

### Integration models (pick one)
1. **Shell-out** — invoke the `rclone` binary, parse `--use-json-log` output.
   Simplest; loose coupling; great for a first integration.
2. **rc HTTP API (`rclone rcd`)** — run rclone as a managed daemon, drive via HTTP.
   **Best for the VintageVault desktop agent** (long-running, progress/status, pause).
   **Proven in this spike** — `rc-wrapper.ps1` started the daemon, launched an async
   `/sync/copy` job, polled `/job/status` to completion, and shut it down. The mapping:
   `backup → /sync/copy`, `status → /job/status`, `stop → /core/quit`.
3. **Go library embedding** — import rclone packages directly. Maximum control, but the
   internal API is **not** a stable public contract → highest maintenance cost.

### What VintageVault still owns (the product / the moat)
Consumer UX, ransomware/anomaly detection, retention policy, monitoring/alerting,
guided restore, account management — see [`gaps.md`](gaps.md). rclone does **none** of
these.

---

## License compatibility

- **rclone:** MIT (permissive).
- **VintageVault:** Apache-2.0.
- MIT is **compatible** with Apache-2.0 and with shipping rclone as a bundled binary or
  dependency. Requirements: preserve rclone's copyright/license notice. **Action:** add
  an attribution entry to the repo `NOTICE` when rclone is bundled or distributed.

---

## Risks / caveats
- **rclone internal Go API is not stable** → prefer shell-out or `rcd` over embedding.
- **OneDrive Graph throttling** on large initial backups → tune `--tpslimit`, retries.
- **`bisync` can propagate deletions** → use one-way `copy` with `--backup-dir` for
  backup semantics; avoid `sync`/`bisync` destructive modes by default.
- **Google server-side copy** requires the destination account to have access to source
  files (sharing) and quota limits apply → orchestration logic needed.
- **OAuth app verification** at consumer scale is a product concern regardless of engine.

## See also
- [`gaps.md`](gaps.md) — where VintageVault adds value above the engine.
- [`recommendation.md`](recommendation.md) — draft ADR with the decision.
- `../prototype/scripts/` — runnable scripts; `results/` — captured evidence.
