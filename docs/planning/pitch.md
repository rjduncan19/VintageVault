# VintageVault — The Pitch

<p align="center">
  <img src="../branding/logo.svg" alt="VintageVault" width="120">
</p>

_Last updated: 2026-03-14 | Model: Claude Opus 4.6_

---

## The Mission

**We're in business to protect everyone's digital life.**

Not to build a unicorn. Not to maximize shareholder value. To make sure that when ransomware hits — and it will — regular people have a backup they didn't have to think about.

See [MISSION.md](../../MISSION.md) for our full principles.

---

## The Problem

**2.3 billion people store their most important files in the cloud — and none of them have a backup.**

Your family photos. Your freelance contracts. Your tax documents. Your kid's school projects. They're all in Google Drive or OneDrive, and you assume they're safe.

They're not.

- **Ransomware** encrypts your files and the encryption syncs to your cloud instantly
- **Accidental deletion** can wipe a folder before you notice (and trash empties in 30 days)
- **Account compromise** — one phished password and everything is gone
- **Provider outage** — Google has had multi-hour outages affecting millions of users

**Your cloud drive is not a backup. It's the thing that needs backing up.**

Enterprise IT departments know this. They spend billions on cloud backup solutions from Veeam, Spanning, and Druva. But for the 2+ billion consumers and small businesses? **There's nothing.**

---

## The Solution

**VintageVault automatically backs up your cloud to a second cloud. Set it once. Sleep easier.**

```
┌──────────────┐         ┌──────────────┐         ┌─────────────────────────┐
│              │         │              │         │                         │
│  Your        │────────►│ VintageVault │────────►│  VintageVault-Backup/   │
│  OneDrive    │         │ (cloud API)  │         │  (in YOUR OneDrive)     │
│              │         │              │         │                         │
└──────────────┘         └──────────────┘         └─────────────────────────┘

               No install. No desktop app. No data on our servers.
               Backup is plain folders you can browse anytime.
```

**How it works:**
1. **Connect** your OneDrive account on our website (2-minute signup)
2. VintageVault's cloud service **automatically creates snapshots** of your files on a schedule
3. If something goes wrong, **browse your backup folder** and restore any file from the past 30-365 days

**That's it.** No app to install. No desktop agent to keep running. Your backup is plain folders in your own OneDrive — browse them in File Explorer anytime.

---

## Why Now

### The market is ready

| Signal | Evidence |
|--------|----------|
| **Cloud adoption is universal** | 2.3B+ cloud storage users worldwide; 55% use 3+ providers |
| **Threats are accelerating** | Ransomware attacks up 34% from 2025; consumer phishing rising |
| **The tools exist** | Cloud APIs (Microsoft Graph, Google Drive API) are mature and free |
| **Users already have destinations** | Most people already have both Google and Microsoft accounts |
| **Nobody is serving consumers** | Enterprise backup is a $12B market; consumer cloud backup is ~$0 |

### The gap is clear

```
Enterprise backup market:     $12B+ and growing
                              Veeam, Spanning, Druva, Commvault, etc.
                              ───────────────────────────────────

Consumer cloud backup market: ≈ $0
                              Nobody.
                              ───────

               ★ VintageVault goes here
```

---

## How It's Different

### vs. MultCloud / CloudHQ ($10+/mo)
- **They sync.** Sync propagates ransomware. VintageVault is one-directional backup with retention.
- **Their data flows through their servers.** VintageVault keeps data on your device — privacy-first.
- **They charge per-GB.** VintageVault's agent costs us $0/user, enabling a real free tier.

### vs. IDrive / Backblaze ($7-9/mo)
- **They back up your device.** VintageVault backs up your **cloud account.** Complementary, not competitive.

### vs. rclone (free)
- **rclone is a terminal tool for developers.** VintageVault is for your parents. Same core capability, completely different audience.

### vs. doing nothing (most people)
- **"It won't happen to me"** — until it does. VintageVault is insurance: invisible when everything is fine, invaluable when disaster strikes.

---

## The Product

### For families

> _"My daughter accidentally deleted her entire college applications folder from Google Drive. Trash had already emptied. With VintageVault, I browsed back to last week's backup and restored everything in 3 clicks."_

### For freelancers

> _"A client's shared folder got hit by ransomware and the encrypted files synced to my OneDrive. My VintageVault backup on Google Drive was untouched — it even paused the backup when it detected the mass changes."_

### For peace of mind

> _"I get a monthly email: '12,847 files protected. Last backup: 2 hours ago. Everything is healthy.' I never think about it. That's the point."_

---

## Business Model

### Why our economics work

**The key insight:** VintageVault's desktop agent runs on the user's own computer. Data flows directly from source cloud → user's PC → destination cloud. **Our servers never touch the data.**

| Model | Server cost per 1,000 users |
|-------|----------------------------|
| Traditional SaaS (data through servers) | $16,000-24,000/month |
| **VintageVault (agent on user's device)** | **$20-50/month total** |

This **800× cost advantage** enables:
- A genuinely useful **free tier** (free users cost us nothing)
- **$4.99/mo Pro** pricing that undercuts every competitor
- **~95% gross margins** on paid subscriptions

### Pricing

| Free ($0) | Pro ($4.99/mo) | Family ($9.99/mo) |
|------|----------------|-------------------|
| 1 OneDrive snapshot | Everything in Free | Everything in Pro |
| Weekly schedule | Daily/hourly snapshots | Up to 5 family members |
| 30-day retention | Cross-account backup (ransomware-safe) | 365-day retention |
| Basic alerts | Anomaly detection | Family dashboard |
| | 90-day retention | Shared management |

### Revenue model

At **100,000 free users** with **8% paid conversion** (SaaS median):
- **~$400,000 ARR** with ~90% gross margin
- Free tier infrastructure: ~$5-20/month (Azure Functions)
- Scales linearly — infrastructure costs grow minimally

---

## Technology

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Backup engine** | .NET 8 library (open source) | Delta sync, snapshot management, change detection |
| **Web app** | ASP.NET Core + Blazor + MudBlazor | User accounts, backup config, status, restore UI |
| **Backup orchestration** | Azure Functions (timer trigger) | Scheduled snapshot execution |
| **Cloud APIs** | Microsoft Graph (OneDrive) | Delta queries, server-side `driveItem: copy` |
| **Security** | OAuth2 PKCE, Azure Key Vault | Secure token management |

### Architecture

```
┌──────────────────────────────┐     ┌──────────────────────────────┐
│  VintageVault Web App        │     │  Azure Functions              │
│  (ASP.NET Core + Blazor)     │     │  (Consumption Plan)           │
│                              │     │                              │
│  • User signup + OAuth       │────►│  • Timer trigger (weekly)     │
│  • Backup configuration      │     │  • Graph delta API            │
│  • Status dashboard          │     │  • driveItem: copy/move       │
│  • Restore file browser      │     │  • Immutable snapshot mgmt    │
│  • Stripe billing            │     │  • Anomaly detection (Pro)    │
│                              │     │                              │
│  Hosting: ~$0-10/month       │     │  Cost: ~$0-5/month           │
└──────────────────────────────┘     └──────────────────────────────┘

  All operations are server-side OneDrive API calls.
  No desktop agent. No relay server. $0 bandwidth.
  Backup is plain folders in user's own OneDrive.
```

---

## Roadmap

### Phase 1 — MVP (Same-Account OneDrive Snapshots)
- Web signup + OneDrive OAuth
- Same-account immutable incremental snapshots via Graph API
- Weekly schedule, 30-day retention
- Browsable backup (plain folders in user's OneDrive)
- Email status notifications
- Open source engine on GitHub

### Phase 2 — Pro Tier + Cross-Account
- Cross-account backup (separate OneDrive or Google Drive) for ransomware protection
- Daily/hourly schedules
- Anomaly detection
- Stripe billing
- 90-day retention

### Phase 3 — Growth
- Google Drive support (pending OAuth assessment)
- Family tier with shared dashboard
- Additional providers (Dropbox)
- Desktop agent as optional "privacy mode"
- Client-side encryption (E2EE)
- Restore workflow UX

---

## The Ask

VintageVault is a mission-driven project in the planning and architecture phase.

**What's next:**
1. Build the Phase 1 MVP (same-account OneDrive snapshots, web-only)
2. Open source the backup engine on GitHub
3. Private beta with 50-100 families
4. Launch free tier publicly
5. Introduce Pro tier with cross-account backup

---

## The Vision

> **Every family deserves a backup plan they don't have to think about.**
>
> Cloud storage changed how we store files. VintageVault changes how we protect them.
> Open source. Mission-driven. Sustainable, not growth-at-all-costs.
>
> We'd rather protect 100,000 people for free than sell 1,000 subscriptions.

---

_"We're in business to protect everyone's digital life."_
