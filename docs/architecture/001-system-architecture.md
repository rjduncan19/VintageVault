# ADR-001: System Architecture

**Status:** Accepted
**Date:** 2026-03-13
**Decision Makers:** VintageVault Core Team

## Mission

VintageVault is a **cloud-to-cloud backup tool** designed to protect families, personal projects, and very small businesses against **accidental data corruption and ransomware attacks**.

The core premise: users back up contents (or selective subsets) from one cloud drive (e.g., OneDrive) to another cloud account, potentially on a **different provider**. If one account is compromised — whether by ransomware encrypting files, accidental mass-deletion, or account takeover — the backup copy on the other provider survives.

### Target Users

Our users are **families, individuals, and very small businesses** who:
- Have important files spread across cloud drives (family photos, tax documents, small business records)
- Have **near-zero disaster recovery plans today**
- Are not technical — they don't have IT staff, backup scripts, or sysadmins
- Need something that "just works" after a simple setup

**Simplicity is the product.** If setup takes more than 10 minutes or requires technical knowledge, these users won't adopt it.

### Threat Model

| Threat | How VintageVault Protects |
|--------|--------------------------|
| **Ransomware encrypts files on source drive** | Backup copy on different provider is unaffected. Retention policy preserves pre-encryption versions. |
| **Accidental file deletion or corruption** | Point-in-time backup allows recovery of previous versions. |
| **Source account compromised / taken over** | Cross-provider backup means attacker can't reach destination account. |
| **Destination account compromised** | Source is the primary copy; user re-authorizes a new destination. |

### Critical Design Implications from Threat Model

1. **This is NOT sync — it is backup.** A naive bidirectional sync would propagate ransomware-encrypted files to the destination, destroying the backup. VintageVault must be **one-directional** with **retention/versioning** at the destination.
2. **Destination immutability matters.** Ideally, the backup copy should not be overwritable by the source account or by a compromised VintageVault agent. Write-once or append-only semantics on the destination are the gold standard.
3. **Retention windows are core, not a nice-to-have.** Users must be able to recover files from before a corruption event. This means keeping N days of history or N versions of each file on the destination.
4. **Cross-provider is the key differentiator.** Same-provider backup (OneDrive → OneDrive) offers limited protection since a compromised Microsoft account could reach both. Cross-provider (OneDrive → Google Drive) provides true isolation.
5. **Selective backup** must be supported — users should be able to choose specific folders or file types, not just full-drive mirrors.

## Context

Users provide both a source and destination cloud storage account, and VintageVault orchestrates the transfer. VintageVault never stores user data — it is a **transfer orchestrator**, not a storage provider.

This single fact drives every architectural trade-off below.

## The Critical Cost Insight: Where Does Data Flow?

```
WEB/SERVER ARCHITECTURE:
  OneDrive ──100GB──► YOUR SERVER ──100GB──► Google Drive
  You pay: ~$16-24 per user per full backup (cloud egress + ingress)
  At 1,000 users × 100GB avg = $16,000-24,000 per backup cycle

DESKTOP/CLIENT ARCHITECTURE:
  OneDrive ──100GB──► USER'S PC ──100GB──► Google Drive
  You pay: $0. User's home internet handles it.
```

In a web architecture, every byte of user data flows through your server.
In a client architecture, you serve zero data — only config and scheduling.

---

## Proposals Evaluated

### Proposal A: Desktop .NET App (System Tray)

```
User's Machine
├── .NET 8+ Background Service (Windows Service / launchd / systemd)
├── System Tray Icon (WPF NotifyIcon on Windows)
├── Minimal Settings UI (WPF window or local web UI on localhost)
├── Backup Engine
│   ├── ICloudStorageProvider abstraction
│   │   ├── OneDriveProvider (Microsoft Graph SDK)
│   │   ├── GoogleDriveProvider (Google.Apis.Drive.v3)
│   │   └── DropboxProvider (Dropbox .NET SDK)
│   ├── Delta sync engine
│   ├── Chunked transfer manager
│   └── Retry/backoff logic
├── OAuth2 tokens → OS Secure Storage (Credential Manager / Keychain)
└── SQLite local database (job history, sync state, delta tokens)
```

**Pros:**
- $0 hosting cost — all transfers happen on user's device
- Best security model — OAuth tokens in OS secure storage, never on a server
- No bandwidth costs — user's internet, not ours
- Privacy story: "Your data never touches our servers"
- All C# — Copilot-friendly codebase

**Cons:**
- Must be running to back up — if PC is off, no backup
- Cross-platform UX is hard — WPF is Windows-only
- No mobile experience
- Installation friction

**Security:** ★★★★★ | **Hosting Cost:** FREE

---

### Proposal B: Web SaaS — Blazor Frontend + ASP.NET Core Backend

```
Browser (any device)              Cloud
├── Blazor Server UI       ──►  ASP.NET Core API Server
    (MudBlazor components)       ├── User Authentication (Identity / Auth0)
                                 ├── Backup Orchestrator
                                 │   ├── Job Scheduler (Hangfire / Quartz.NET)
                                 │   ├── ICloudStorageProvider abstraction
                                 │   └── Transfer Worker (chunked, retry)
                                 ├── OAuth2 Token Vault (Azure Key Vault)
                                 ├── PostgreSQL (users, jobs, sync state)
                                 └── Hosted on: Azure App Service / Railway / Fly.io
```

**Pros:**
- Works on every device — any browser
- No install — sign up, connect accounts, done
- Always-on backups — server runs 24/7
- Easiest onboarding for non-technical users
- Single C# codebase (Blazor)

**Cons:**
- **All user data flows through your server** — enormous bandwidth cost
- You custody OAuth refresh tokens — breach exposes all users' cloud drives
- Ongoing hosting costs scale with users AND their data volume
- Blazor Server requires persistent SignalR connection

**Security:** ★★★☆☆ | **Hosting Cost:** HIGH (~$16,000-24,000/cycle at 1k users)

---

### Proposal C: Web SaaS — React/TypeScript Frontend + ASP.NET Core Backend

Same backend as Proposal B, with React + TypeScript frontend instead of Blazor.

**Pros (vs. Blazor):**
- Richest UI ecosystem (Material UI, shadcn/ui)
- Most AI training data — better Copilot output for React/TS
- Most transferable skill — easier to hire help
- Better mobile browser performance

**Cons (vs. Blazor):**
- Two languages (C# + TypeScript)
- Two build systems (dotnet + npm/vite)
- Same hosting/security/bandwidth problems as Proposal B

**Security:** ★★★☆☆ | **Hosting Cost:** HIGH

---

### Proposal D: Hybrid — Web Dashboard + Desktop Agent ⭐ RECOMMENDED

```
                    ┌─────────────────────────────────┐
                    │     Lightweight Web Service      │
                    │     (ASP.NET Core + Blazor)      │
                    │                                  │
                    │  • User account management       │
                    │  • Backup job configuration      │
                    │  • Schedule management            │
                    │  • Status dashboard               │
                    │  • Push notifications             │
                    │  • NO data transfer — config only │
                    │                                  │
                    │  Hosting cost: ~$20-50/mo total   │
                    │  (tiny — just serves config JSON) │
                    └──────────┬──────────────────────┘
                               │ HTTPS (config + status only)
                               │ (never file content)
                    ┌──────────▼──────────────────────┐
                    │     Desktop Agent (.NET 8+)      │
                    │     (runs on user's PC/Mac)      │
                    │                                  │
                    │  • Background service             │
                    │  • Pulls schedule from web        │
                    │  • Executes backups locally       │
                    │  • All data flows: Source→PC→Dest │
                    │  • OAuth tokens in OS storage     │
                    │  • Reports status back to web     │
                    │  • System tray icon               │
                    └─────────────────────────────────┘
```

**Pros:**
- **$0 bandwidth cost** — data never touches your server
- **Good security** — OAuth tokens stay on user's device in OS secure storage
- **Cross-device management** — configure from phone, agent runs on PC
- **Cheap to host** — CRUD API + Blazor dashboard; ~$20/mo handles thousands of users
- **Freemium viable** — near-zero per-user cost enables generous free tier
- **Best privacy story** — "Your files never touch our servers"
- **All C#** — backend, dashboard, and agent are all .NET/C#

**Cons:**
- Two apps to build (web dashboard + desktop agent; shared core library mitigates)
- Agent must be running — no backup if PC is off
- Agent installation friction (auto-updater needed: Squirrel.Windows / Velopack)
- No iOS/Android agent — mobile is dashboard-only

**Security:** ★★★★☆ | **Hosting Cost:** MINIMAL (~$20-50/mo)

---

### Proposal E: .NET MAUI Blazor Hybrid (Native App, Web UI)

```
Native App Shell (MAUI)
├── Blazor WebView (UI rendered in native web view)
│   └── MudBlazor components (same as web Blazor)
├── .NET 8+ runtime
├── Backup Engine (same core library)
├── OAuth tokens → native platform secure storage
├── Background tasks (platform-specific)
└── Local SQLite for sync state
```

**Pros:**
- True cross-platform native app (Windows, macOS, iOS, Android)
- iOS App Store presence
- $0 hosting/bandwidth
- 100% C#

**Cons:**
- MAUI has quality issues on non-Windows platforms (as of 2026)
- iOS severely restricts background execution (~30s via BGTaskScheduler)
- App Store review burden for every release
- Larger app size (~30-50MB bundled runtime)
- Less Copilot training data for MAUI

**Security:** ★★★★★ | **Hosting Cost:** FREE

---

## Decision

**We will implement Proposal D (Hybrid — Web Dashboard + Desktop Agent).**

### Rationale

| Factor | Desktop (A) | Web SaaS (B/C) | Hybrid (D) ⭐ | MAUI (E) |
|--------|-------------|-----------------|---------------|----------|
| Hosting cost | Free | ~$16k+/cycle | ~$20-50/mo | Free |
| Security | ★★★★★ | ★★★☆☆ | ★★★★☆ | ★★★★★ |
| Mobile access | ❌ | ✅ | ✅ (dashboard) | ✅ |
| Install friction | High | None | Medium | High |
| Freemium viable | ✅ | ❌ | ✅ | ✅ |
| GDPR burden | Minimal | Heavy | Minimal | Minimal |
| Always-on backup | ❌ | ✅ | ❌ (mitigated) | ❌ |

The hybrid approach delivers the best combination of:
1. **Economics** — near-zero per-user cost enables sustainable freemium
2. **Security** — OAuth tokens never leave the user's device
3. **User experience** — web dashboard for mobile/cross-device management
4. **Privacy** — "Your files never touch our servers" as a differentiator

---

## Security Comparison

| Concern | Desktop (A) | Web SaaS (B/C) | Hybrid (D) | MAUI (E) |
|---------|-------------|-----------------|-------------|----------|
| Token storage | OS Secure Storage | Server DB (encrypted) | OS Secure Storage | OS Secure Storage |
| If service is breached | N/A | All users' cloud drives exposed | Emails + configs only | N/A |
| If user device is breached | That user only | N/A | That user only | That user only |
| OAuth flow | PKCE (public) | Confidential client | Agent=PKCE, Web=PKCE | PKCE (public) |
| Data in transit | Device ↔ cloud (TLS) | Cloud → server → cloud | Device ↔ cloud (TLS) | Device ↔ cloud (TLS) |
| Compliance burden | Minimal | SOC 2, GDPR processor | Minimal | Minimal |

The web SaaS model (B/C) makes VintageVault a data processor under GDPR. The hybrid model avoids this entirely because user file data is never processed server-side.

---

## Mobile Strategy

**A native iOS/Android app is not required for v1.**

iOS does not allow PWAs or web apps to run background sync tasks. Even native iOS apps are severely limited (~30 seconds of background execution via BGTaskScheduler). A 100GB cloud backup simply cannot run in the background on iOS.

What mobile users actually need:
- ✅ Check backup status → web dashboard
- ✅ Get alerts if backup fails → push notifications
- ✅ Configure backup schedule → web dashboard
- ✅ Browse backed-up files → web dashboard

A responsive web dashboard covers all mobile use cases.

---

## Monetization

| Model | Per-user variable cost | Fixed cost |
|-------|----------------------|------------|
| Desktop (A) | ~$0 | Marketing site, code signing cert |
| Web SaaS (B/C) | $8-24/backup cycle | Server, DB, Key Vault |
| **Hybrid (D)** | **~$0.01 (API calls)** | **~$20-50/mo web hosting** |
| MAUI (E) | ~$0 | App Store fees |

### Planned Tiers (Hybrid)

- **Free:** 1 backup pair, weekly schedule, 30-day history
- **Pro ($3-5/mo):** Unlimited pairs, hourly schedule, email/push alerts, backup encryption, priority support
- **Family ($8-10/mo):** 5 users, shared dashboard, family admin

---

## Implementation Phases

### Phase 1: Desktop Agent (MVP)
- .NET 8+ console app / background service
- OneDrive → Google Drive, one direction
- OAuth2 via PKCE, tokens in OS secure storage
- Full backup (enumerate + transfer), then add delta sync
- **Selective backup** — user picks folders/paths to include or exclude
- **Retention policy (basic)** — keep deleted/overwritten files for N days on destination, don't propagate deletions immediately (critical for ransomware protection)
- Simple CLI or local web UI (localhost) for configuration
- Windows-only

### Phase 2: Web Dashboard
- ASP.NET Core + Blazor Server + MudBlazor
- User accounts, backup job configuration, status monitoring
- Agent phones home for config and reports status
- Push notifications for backup success/failure
- **Backup health alerts** — "last backup was N days ago," "X files failed"
- Responsive design for mobile users

### Phase 3: Polish & Expand
- macOS agent (same .NET code, different installer)
- Google Drive → OneDrive (reverse direction)
- Dropbox support
- Client-side encryption (encrypt before writing to destination)
- **Advanced retention** — N versions per file, configurable retention windows
- **Restore workflow** — guided file/folder recovery from backup history
- Freemium billing (Stripe integration)

---

## Open Questions

### Product
1. **Minimum viable feature set?** Connect OneDrive + Google Drive, select folders, click "Back Up Now," files appear in destination with basic retention. Everything else is v2+.
2. **Partial failure handling?** Resumability is critical — track per-file progress and resume from where you left off.
3. **Format incompatibilities?** Google Docs are native objects with no file. Policy needed: skip or export as .docx?
4. **Restore story?** Guided restore from backup history? Reverse the backup direction? Manual download from destination?
5. **Ransomware detection?** Should the agent detect suspicious mass-encryption (e.g., sudden change in file entropy across many files) and pause backup to avoid propagating damage?

### Technical
6. **Cloud API quotas** — Google Drive: 12,000 req/min default. Microsoft Graph has per-app throttling. Plan for quota increases and per-user rate limiting.
7. **OAuth app verification** — Both Google and Microsoft require app review before public use. Google's review for drive scopes can take weeks.
8. **Shared drives / family libraries** — OneDrive Family and Google Shared Drives have different API models.
9. **Retention implementation** — How to implement versioning on the destination? Timestamped folders? Cloud provider versioning APIs? Separate metadata DB tracking file versions?
10. **Destination immutability** — Can we leverage cloud provider features (e.g., Google Drive version history, Azure Blob immutability) to prevent backup tampering?

### Business
11. **Competitive landscape** — MultCloud, CloudHQ exist. Differentiation: privacy-first (data never touches our servers), ransomware protection with retention, dead-simple UX for non-technical users.
12. **Beta/launch strategy** — Friends/family → small community → public?
13. **Support burden** — Non-technical family users will need help at scale. In-app guidance and self-service must be excellent.

---

## References

- [Microsoft Graph API - OneDrive](https://learn.microsoft.com/en-us/graph/api/resources/onedrive)
- [Google Drive API v3](https://developers.google.com/drive/api/v3/about-sdk)
- [.NET 8 Background Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
- [MudBlazor Component Library](https://mudblazor.com/)
- [Velopack (Desktop App Updates)](https://velopack.io/)
