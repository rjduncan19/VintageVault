# VintageVault — Reviewer Guide

<p align="center">
  <img src="docs/branding/logo.svg" alt="VintageVault" width="160">
</p>

<p align="center"><em>Protecting everyone's digital life from ransomware and data loss.</em></p>

---

## What Is This?

VintageVault is a side project idea for an open-source, mission-driven cloud backup tool. It automatically creates snapshots of your cloud storage files so you can recover from accidental deletion, file corruption, or ransomware.

**Think of it as:** A "time machine" for your OneDrive — except it's free, open source, and your backup is just plain folders you can browse anytime.

**This repo contains:** The complete planning phase — product strategy, competitive analysis, business plan, UX mockups, architecture decisions, and a working proof-of-concept CLI tool.

---

## Recommended Reading Order

### 1. The Mission (~3 min)
**[MISSION.md](MISSION.md)** — Why this exists. Patagonia-inspired model: revenue sustains the work, not the other way around. Includes "If We Succeed, We Disappear" — we'd celebrate if Google/Microsoft built this natively.

### 2. The Pitch (~8 min)
**[docs/planning/pitch.md](docs/planning/pitch.md)** — The full value proposition: problem, solution, market, competitive advantage, technology, and roadmap.

### 3. The Business Plan (~12 min)
**[docs/planning/business-plan.md](docs/planning/business-plan.md)** — Financials, startup costs, revenue projections, risk assessment, and a "Questions She Will Ask" section written specifically for a senior PMM reviewer. Includes the Google OAuth cost surprise ($15-75K) and how we mitigate it.

### 4. The Devil's Advocate (~10 min)
**[docs/planning/devils-advocate.md](docs/planning/devils-advocate.md)** — Brutally honest "Shark Tank" critique. 10 reasons this could fail. Spoiler: it's a viable lifestyle business, not venture-scale. And we're OK with that.

### 5. The UX (~5 min, interactive)
Open these HTML files in a browser (click tabs to navigate):
- **[docs/mockups/01-landing-page.html](docs/mockups/01-landing-page.html)** — Marketing landing page
- **[docs/mockups/02-setup-wizard.html](docs/mockups/02-setup-wizard.html)** — 3-step setup flow
- **[docs/mockups/03-dashboard.html](docs/mockups/03-dashboard.html)** — Backup dashboard
- **[docs/mockups/04-restore.html](docs/mockups/04-restore.html)** — File restore flow
- **[docs/mockups/poc/cli-mockup.html](docs/mockups/poc/cli-mockup.html)** — CLI tool mockup (what was actually built)

### 6. The Competitive Landscape (~5 min)
**[docs/planning/competitive-analysis.md](docs/planning/competitive-analysis.md)** — Who else is doing this, their pricing, and why the consumer market is essentially empty.

---

## Optional Deep-Dives

| Document | What it covers |
|----------|---------------|
| [docs/planning/monetization-strategy.md](docs/planning/monetization-strategy.md) | Why freemium subscription ($4.99 Pro / $9.99 Family) |
| [docs/planning/same-account-pivot.md](docs/planning/same-account-pivot.md) | Key architecture pivot: backup within the same OneDrive ($0 infrastructure) |
| [docs/planning/radical-transparency.md](docs/planning/radical-transparency.md) | "DIY-first" model — teach users to do it themselves |
| [docs/planning/open-core-strategy.md](docs/planning/open-core-strategy.md) | Bitwarden model: open source engine + proprietary SaaS |
| [docs/architecture/003-backup-storage-format.md](docs/architecture/003-backup-storage-format.md) | Immutable incremental snapshots (not sync) |
| [docs/architecture/004-retention-and-detection.md](docs/architecture/004-retention-and-detection.md) | Ransomware detection from metadata alone |

---

## Key Numbers (TL;DR)

| | |
|---|---|
| **Startup cost** | $500-2,000 (no Google OAuth assessment) |
| **Monthly infrastructure** | ~$0-10 (backup runs on user's own OneDrive) |
| **Break-even** | ~50 paid subscribers |
| **Max financial risk** | $2,000-4,000 |
| **Time investment** | 10-15 hrs/week, 6-8 months to revenue |
| **Revenue model** | Free (snapshots) / $4.99 Pro (ransomware protection) / $9.99 Family |
| **Dev approach** | Solo + GitHub Copilot, evenings/weekends |
| **Success metric** | People protected, not revenue maximized |

---

## Current Status

The POC (proof-of-concept) CLI tool is built:
- 13 source files, 2,344 lines of C#
- 53 unit tests, all passing
- Builds clean, ready for integration testing
- Not yet tested against a live OneDrive (needs test account setup)

---

## Questions? Feedback?

This is a planning-phase project. Nothing is launched, no money has been spent, no commitments have been made. All feedback welcome — especially the hard questions.
