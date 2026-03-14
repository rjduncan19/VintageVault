# VintageVault — The Pitch

_Last updated: 2026-03-14 | Model: Claude Opus 4.6_

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
┌──────────────┐                              ┌──────────────┐
│              │         Your PC              │              │
│   OneDrive   │───────► (running  ──────────►│ Google Drive  │
│   (source)   │         VintageVault)        │  (backup)    │
│              │                              │              │
└──────────────┘                              └──────────────┘

                     Your data never
                     touches our servers.
```

**How it works:**
1. **Connect** your source cloud (e.g., OneDrive) and destination cloud (e.g., Google Drive)
2. VintageVault's lightweight desktop agent **automatically copies** your files on a schedule
3. If something goes wrong, **browse your backup** and restore any file from the past 30-365 days

**That's it.** No servers to manage. No external drives to buy. No technical knowledge required.

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
- **$3.99/mo Pro** pricing that undercuts every competitor
- **~95% gross margins** on paid subscriptions

### Pricing

| Free | Pro ($3.99/mo) | Family ($7.99/mo) |
|------|----------------|-------------------|
| 1 backup pair | Unlimited pairs | Everything in Pro |
| Weekly schedule | Daily/hourly | Up to 5 family members |
| 30-day retention | 90-day retention | 365-day retention |
| Basic alerts | Anomaly detection | Family dashboard |
| | Encryption | Shared management |

### Revenue model

At **100,000 free users** with **4% paid conversion:**
- **$204,000 ARR** with ~95% gross margin
- Scales linearly — infrastructure costs grow minimally

---

## Technology

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Desktop agent** | .NET 8 background service | Runs backups locally; manages OAuth; system tray |
| **Web dashboard** | ASP.NET Core + Blazor + MudBlazor | User accounts, backup config, status monitoring |
| **Cloud APIs** | Microsoft Graph + Google Drive API | Delta sync, file transfer, change detection |
| **Security** | OAuth2 PKCE, OS secure credential storage | Tokens never leave user's device |

### Architecture

```
┌─────────────────────────────┐     ┌──────────────────────────────┐
│     Web Dashboard           │     │     Desktop Agent             │
│     (ASP.NET Core + Blazor) │     │     (.NET 8 Background Svc)  │
│                             │     │                              │
│  • User accounts            │◄───►│  • Pulls config from web     │
│  • Backup configuration     │     │  • Executes backups locally  │
│  • Status monitoring        │     │  • Reports status to web     │
│  • Push notifications       │     │  • OAuth in OS secure storage│
│  • NO data transfer         │     │  • System tray presence      │
│                             │     │  • Anomaly detection         │
│  Hosting: ~$20-50/mo total  │     │  Per-user cost: ~$0          │
└─────────────────────────────┘     └──────────────────────────────┘
```

---

## Roadmap

### Phase 1 — MVP (Desktop Agent)
- Windows desktop agent
- OneDrive → Google Drive backup
- Full + incremental sync via delta APIs
- Selective folder backup
- Basic retention policy
- Local configuration (CLI or localhost UI)

### Phase 2 — Web Dashboard
- User accounts and authentication
- Remote backup configuration and monitoring
- Push notifications (success/failure/anomaly)
- Responsive design for mobile monitoring
- Pro tier billing (Stripe)

### Phase 3 — Growth
- macOS agent
- Additional providers (Dropbox, iCloud)
- Reverse direction (Google Drive → OneDrive)
- Client-side encryption (E2EE)
- Family tier with shared dashboard
- Restore workflow ("Time Machine for your cloud")

---

## The Ask

VintageVault is currently in the planning and architecture phase, with comprehensive documentation covering product strategy, gap analysis, competitive landscape, monetization model, and technical architecture.

**What's next:**
1. Build the Phase 1 MVP (desktop agent, OneDrive → Google Drive)
2. Private beta with 50-100 families
3. Iterate based on feedback
4. Launch free tier publicly
5. Introduce Pro tier with web dashboard

---

## The Vision

> **Every family deserves a backup plan they don't have to think about.**
>
> Cloud storage changed how we store files. VintageVault changes how we protect them.
> One-directional. Privacy-first. Set it once. Sleep easier.

---

_"Your cloud's backup plan."_
