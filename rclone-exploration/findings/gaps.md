# Gap Analysis: rclone vs. the VintageVault Value Proposition

**Todo:** `evaluate-gaps` · **Status:** drafted

## Core insight

rclone provides a world-class **transfer engine**. Every meaningful gap lives in the
**product layer above the engine** — which is precisely VintageVault's stated strategy:

> *"rclone is the floor. Any technical user can replicate core features for free.
> VintageVault must be dramatically easier to justify any price."* — `competitive-analysis.md`

rclone is a **developer CLI**. VintageVault's value prop is **grandparent-simple,
trustworthy, privacy-first, ransomware-aware, family-focused**. The gaps are the
distance between those two things.

## Gap table

| # | Gap | rclone today | VintageVault value-add | Tier |
|---|-----|--------------|------------------------|------|
| 1 | **Ransomware / anomaly detection** | None. Will sync encrypted-over-good files; `--backup-dir` keeps versions but no detection/pause/alert. | Flagship moat: detect mass modify/delete/entropy spikes → pause + alert (`AnomalyDetector.cs`). | 1 |
| 2 | **Consumer onboarding / UX** | No consumer GUI; manual OAuth app registration; text config file. | 2-minute guided setup, pre-registered OAuth apps, one-click connect, family defaults. | 1 |
| 3 | **Guided restore** | Restore = additional CLI commands. | Browsable, point-in-time restore ("my photos as of last Tuesday") for non-technical victims. | 1 |
| 4 | **Retention / snapshot policy** | `--backup-dir` (flat move-aside) + `--immutable`; no managed GFS retention, pruning, or point-in-time model. | `ManifestManager`/snapshot model: keep N daily/weekly, auto-prune, immutable versioned snapshots. | 2 |
| 5 | **Monitoring & alerting** | Logs to console/file only. | "Backup hasn't run in 7 days" emails, success/failure notifications, health dashboard. | 2 |
| 6 | **Reliable scheduling agent** | Relies on external cron / Task Scheduler. | Managed desktop agent: sleep/wake handling, retries, opportunistic scheduling, bandwidth windows. | 2 |
| 7 | **Safety rails** | Sharp tool — `sync`/`bisync` can propagate deletions / cause data loss. | Safe-by-default backup semantics; never destructive without versioning. | 2 |
| 8 | **Account / token lifecycle** | Plaintext config; manual re-auth. | Friendly multi-account management, re-auth prompts, OAuth verification handling at consumer scale. | 3 |
| 9 | **Integrity / trust reporting** | Hash-checks during transfer only. | Periodic verification + test-restore + public trust/transparency report (mission alignment). | 3 |
| 10 | **Encryption UX** | `crypt` exists but manual setup. | Optional client-side encryption with key-management UX + the trust story. | 3 |
| 11 | **Server-side optimization orchestration** | Has `--drive-server-side-across-configs`; share→copy→unshare dance is the caller's job. | Implements ADR-002's Google share-then-copy zero-bandwidth path with permission management. | 3 |

## Prioritized: where to add value

- **Tier 1 — the moat (high value, rclone = zero coverage):**
  ransomware/anomaly detection + pause/alert · consumer onboarding · guided restore.
- **Tier 2 — reliability & trust:**
  retention/snapshot policy + immutability orchestration · monitoring/alerting ·
  dependable scheduling agent.
- **Tier 3 — differentiators:**
  integrity/trust reports · encryption UX · account management.

## The inverse — do NOT rebuild (reuse rclone)

Backends + OAuth plumbing, chunked/resumable transfers, retries, throttling/backoff,
checksums, filters, `--bwlimit`, `--backup-dir`, the `crypt` primitive, and the `rcd`
remote-control API. Adopting rclone for these lets the team spend ~100% of effort on
Tier 1–3 — the actual product and the actual moat.

## Strategic implication

If rclone owns the engine, VintageVault's roadmap becomes almost entirely the
differentiators it already lists in `MISSION.md` and `competitive-analysis.md`. The
custom C# transfer engine (`BackupOrchestrator`, Graph client) becomes a candidate for
retirement, while `AnomalyDetector`, the manifest/retention model, and the UX layer
become the product's center of gravity.
