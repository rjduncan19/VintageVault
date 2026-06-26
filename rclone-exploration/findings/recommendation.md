# Draft ADR: Adopt rclone as VintageVault's Transfer Engine

**Status:** Proposed (output of the `rclone-exploration` spike)
**Date:** 2026-06-25
**Supersedes / informs:** ADR-002 (data movement), the custom C# engine in
`src/VintageVault.Cli` (Graph client, `BackupOrchestrator`, `ManifestManager`)
**Todo:** `recommendation-adr`

> This is a **draft** for discussion. If accepted, promote a cleaned-up version into
> `docs/architecture/` as a numbered ADR.

## Context

VintageVault is building a cloud-to-cloud backup engine from scratch in C#. In parallel,
this spike evaluated **rclone** (MIT-licensed, 70+ backends) as the transfer engine. The
spike installed rclone v1.74.3, proved core backup mechanics locally, and proved the
remote-control API integration model (see [`feasibility.md`](feasibility.md),
[`gaps.md`](gaps.md), and `../prototype/`).

Key facts established:
- rclone **does** back up cross-provider (OneDrive ↔ Google Drive) and same-provider
  (Google server-side copy), consistent with ADR-002.
- rclone covers ~the entire transfer engine; the custom C# engine duplicates a fraction
  of it and is OneDrive-only today.
- rclone covers **none** of VintageVault's differentiators (ransomware detection,
  consumer UX, retention policy, guided restore, alerting).
- MIT is license-compatible with VintageVault's Apache-2.0.

## Decision

**Adopt rclone as the data-movement engine.** De-prioritize / plan to retire the custom
C# transfer engine (`BackupOrchestrator` + Graph download/upload). Re-focus VintageVault
engineering on the product layer above the engine.

**Integration model:** start with **shell-out** for the simplest path, and use the
**`rclone rcd` remote-control HTTP API** for the long-running **desktop agent** (proven
in `rc-wrapper.ps1`). **Do not** embed rclone as a Go library (unstable internal API).

**Backup semantics:** one-way `rclone copy` with `--backup-dir` (versioning) and
`--immutable` (WORM-style protection); never default to destructive `sync`/`bisync`.

## Consequences

### Positive
- Massive scope reduction: OAuth, transfers, retries, throttling, checksums, versioning,
  encryption, bandwidth limiting, and a control API are no longer ours to build/maintain.
- Instant multi-provider support (70+ backends) — cross-provider becomes config, not code.
- Google same-provider server-side copy (zero-bandwidth) available immediately.
- Engineering can spend ~100% of effort on the **moat**: anomaly detection, consumer UX,
  retention, restore, alerting (see `gaps.md`).
- Aligns with stated strategy: *"rclone is the floor; compete on UX, not features."*

### Negative / costs
- New runtime dependency (bundle the rclone binary; track version; add `NOTICE` entry).
- Less low-level control than owning the engine; behavior tuned via flags, not code.
- Need an orchestration layer to manage the `rcd` daemon lifecycle, config, and secrets.
- Google server-side copy needs sharing/permission orchestration.

## Impact on the existing POC
- **Retire / freeze:** custom Graph transfer path (`BackupOrchestrator`, Graph up/down).
- **Keep & elevate:** `AnomalyDetector` (ransomware), the manifest/snapshot + retention
  model, the CLI command surface (re-pointed at rclone), and all UX/onboarding work.
- The `backup` / `status` / `snapshots` / `exclude` commands map cleanly onto rclone
  (`/sync/copy`, `/job/status`, `--backup-dir` snapshots, `--exclude`).

## Alternatives considered
1. **Keep building the custom C# engine.** Rejected: high cost to reach rclone parity,
   OneDrive-only today, reinvents a mature, audited tool, and burns the runway that
   should go to the moat.
2. **Embed rclone as a Go library.** Rejected for now: internal API is not a stable
   public contract → high maintenance; shell-out / `rcd` give the same capability with
   far less coupling.
3. **SaaS relay engine.** Out of scope here and rejected by ADR-002 economics
   ($16–24k/1k users/mo vs ~$30) and the privacy story.

## Risks & mitigations
| Risk | Mitigation |
|------|-----------|
| rclone version/behavior drift | Pin a known-good version; smoke-test on upgrade. |
| OneDrive Graph throttling on big initial backups | `--tpslimit`, `--bwlimit`, retries, opportunistic scheduling. |
| Destructive sync/bisync data loss | Default to one-way `copy` + `--backup-dir`; gate `sync` behind guardrails. |
| Secret handling (OAuth tokens in `rclone.conf`) | Keep config out of git (done via `.gitignore`); encrypt at rest; never log tokens. |
| License attribution | Add rclone (MIT) to `NOTICE` on bundling/distribution. |

## Open questions / next steps
1. **Live validation with test accounts** (the one human step): run
   `backup-cross-provider.ps1` (OneDrive→Google) and `backup-gdrive-server-side.ps1`
   (Google→Google) end-to-end and capture timings.
2. Decide bundling strategy (ship rclone binary vs. require install).
3. Prototype the anomaly-detection hook around an rclone run (pre-flight mass-change
   scan → pause/alert before copy).
4. Define the snapshot/retention policy on top of `--backup-dir` directories.
5. If accepted, write the numbered ADR in `docs/architecture/` and update ADR-002's
   status to reference this decision.
