# rclone Exploration ("rcode")

A **parallel spike** evaluating whether the open-source [rclone](https://rclone.org)
project can serve as VintageVault's cloud-to-cloud **transfer engine**, instead of the
custom C# engine in `src/VintageVault.Cli`.

This folder is **isolated** — it does not touch the main .NET POC.

## The two questions this spike answers

1. **Does rclone support backing up from one cloud provider to another?** → see
   [`findings/feasibility.md`](findings/feasibility.md). (Short answer: yes.)
2. **How much of rclone's infrastructure could VintageVault leverage?** → see
   [`findings/feasibility.md`](findings/feasibility.md) and
   [`findings/gaps.md`](findings/gaps.md). (Short answer: most of the engine.)

## Layout

```
rclone-exploration/
  README.md                 <- you are here
  .gitignore                <- keeps rclone.conf (OAuth tokens) out of git
  findings/
    feasibility.md          <- Q1 + Q2, license, rclone-vs-C# comparison
    gaps.md                 <- where VintageVault adds value above the engine
    recommendation.md       <- draft ADR: adopt rclone? which integration model?
    results/                <- captured command output / evidence
  prototype/
    config/
      rclone.conf.template  <- redacted remote definitions (NO tokens)
    scripts/
      local-demo.ps1            <- proves copy/--backup-dir/--immutable (no cloud needed)
      backup-cross-provider.ps1 <- OneDrive -> Google Drive backup
      backup-gdrive-server-side.ps1 <- Google -> Google server-side copy
      rc-wrapper.ps1            <- drives rclone via the rcd HTTP API (agent model)
```

## Completing the live cloud runs

Two prototypes need interactive OAuth with **test accounts** (the one human step) —
see **[`SETUP.md`](SETUP.md)** for the turnkey runbook.

## Prerequisites

- rclone (installed during this spike at `~/.rclone-bin/rclone.exe`; or `winget install Rclone.Rclone`).
- For the **live** cloud scripts: fresh **test** OneDrive + Google Drive accounts
  (never personal data — see the repo's `NEXT-STEPS.md`). OAuth is interactive and
  must be run by a human.

## Quick start (no cloud accounts required)

```powershell
# Proves rclone's backup mechanics using local folders as "remotes"
./prototype/scripts/local-demo.ps1
```

## Security

- **Never commit `rclone.conf`** — it contains OAuth refresh tokens. The local
  `.gitignore` enforces this. Only the redacted `*.template` is tracked.
- Use test accounts only.
