# Pivot Analysis: Same-Account Backup

_Created: 2026-03-14 | Model: Claude Opus 4.6_

## The Idea

Instead of copying files to a *different* account, copy them to a backup folder *within the same account*. Like an enhanced recycle bin / point-in-time snapshot.

```
CURRENT MODEL:
  OneDrive (Account A) ──► OneDrive (Account B) or Google Drive
  Requires: relay server OR desktop agent, cross-account OAuth, bandwidth

PROPOSED PIVOT:
  OneDrive ──► OneDrive:/VintageVault-Backup/{date}/
  Entirely server-side. Zero bandwidth. Zero relay.
  Just API calls.
```

## API Feasibility

### OneDrive (Same Account): ✅ This Is the Breakthrough

Microsoft Graph `driveItem: copy` works **server-side within the same drive.** No download, no upload, no intermediary.

```
POST /me/drive/items/{item-id}/copy
{
  "parentReference": { "id": "BACKUP_FOLDER_ID" },
  "name": "document.docx"
}

→ 202 Accepted (async copy, entirely within Microsoft's infrastructure)
```

- **Bandwidth cost:** $0 (data never leaves Microsoft's servers)
- **Our server cost:** $0 (Azure Function, consumption plan)
- **OAuth scope:** `Files.ReadWrite` (delegated, standard — **free verification**)
- **Speed:** Nearly instant (server-side copy, no network transfer)
- **API limit:** 30,000 items per copy operation
- **User's storage impact:** Backup consumes their quota

### Google Drive (Same Account): ⚠️ Still Has the OAuth Problem

Google Drive `files.copy` works server-side within the same account. But:

- **Requires `drive` scope** (full read/write) → **Restricted** → $15-75K security assessment
- `drive.readonly` can list/read but can't copy
- `drive.file` can only access files the app created
- 750 GB copy limit per user per 24 hours

**Workaround options:**
1. Start OneDrive-only. Add Google when assessment is funded.
2. For Google, download+reupload using `drive.readonly` + `drive.file` (sensitive scopes, free verification) — but this goes through our server, adding bandwidth cost.
3. Google Apps Script extension that runs in user's context — avoids OAuth verification entirely, but complex UX.

## What This Changes — Everything

### The $0 Infrastructure MVP

```
ENTIRE STACK FOR SAME-ACCOUNT ONEDRIVE BACKUP:

Azure Function (timer trigger)              ← FREE (1M executions/month)
  ├── Calls Graph delta API                 ← Detects changed files
  ├── Calls driveItem: copy                 ← Server-side copy
  ├── Maintains backup index in             ← Cosmos DB free tier or
  │   lightweight database                     Table Storage ($0.01/GB)
  └── Sends status email                    ← SendGrid free tier (100/day)

Total infrastructure cost: ~$0-5/month
```

Compare to previous architectures:

| Architecture | Monthly Cost | Complexity |
|-------------|-------------|-----------|
| Desktop agent | $0 (user's PC) | High (installer, system tray, background service) |
| Cloud relay (cross-account) | $200-500 | High (relay server, streaming, bandwidth) |
| **Same-account via API** | **$0-5** | **Low (Azure Function + Graph API)** |

### Protection Profile Changes

| Threat | Same-Account Backup | Cross-Account Backup |
|--------|-------------------|---------------------|
| **Accidental deletion** | ✅ Full protection | ✅ Full protection |
| **Accidental overwrite** | ✅ Full protection | ✅ Full protection |
| **File corruption** | ✅ Full protection | ✅ Full protection |
| **Ransomware** | ❌ Same account = encrypted too | ✅ Different account untouched |
| **Account compromise** | ❌ Attacker sees backup folder | ✅ Different credentials |
| **Provider outage** | ❌ Both down | ✅ Different provider |

**Honest assessment:** Same-account backup handles the **most common** data loss scenario (accidental deletion/overwrite) but not the **scariest** one (ransomware). For most families, this is actually enough — accidental deletion happens 100x more often than ransomware.

## The Product Ladder

This creates a natural upgrade path:

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  FREE: Same-Account Snapshot                                │
│  "Never lose a deleted file again"                          │
│                                                             │
│  ✓ Copies files to /VintageVault-Backup/ in your drive      │
│  ✓ Weekly snapshots, 30-day history                         │
│  ✓ Restore deleted/overwritten files                        │
│  ✓ Zero install — web signup only                           │
│  ✓ Works entirely on cloud APIs ($0 infrastructure)         │
│                                                             │
│  Protection: Accidental deletion ✓  Ransomware ✗            │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  PRO $4.99/mo: Cross-Account Backup                         │
│  "Complete protection — even from ransomware"               │
│                                                             │
│  ✓ Everything in Free                                       │
│  ✓ Backup to a DIFFERENT account (same or cross-provider)   │
│  ✓ Daily/hourly schedule                                    │
│  ✓ Anomaly detection (ransomware alert)                     │
│  ✓ 90-day history                                           │
│                                                             │
│  Protection: Accidental deletion ✓  Ransomware ✓            │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  FAMILY $9.99/mo: Family Protection                         │
│  "Everyone in the family, fully protected"                  │
│                                                             │
│  ✓ Everything in Pro for up to 5 family members             │
│  ✓ 365-day history                                          │
│  ✓ Family dashboard                                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**The upsell is natural and honest:**
> "You're protected against accidental deletion — great! But did you know ransomware can encrypt your backup folder too? Upgrade to Pro to back up to a separate account that ransomware can't reach."

This isn't a scare tactic. It's genuinely true and a genuine upgrade in protection.

## Why This Pivot Is Powerful

### 1. The Free Tier Actually Costs Nothing

Previous free tier: needed relay server ($0.02-0.74/user/month). At 100K free users, that's $2,000-74,000/month.

**New free tier:** Azure Functions + Graph API calls. At 100K users with weekly backups, that's ~$5-20/month total. The free tier is genuinely, structurally free.

### 2. No OAuth Assessment for MVP

OneDrive `Files.ReadWrite` is a standard delegated permission. Free to register, no security assessment, no annual review. **Launch tomorrow.**

### 3. No Desktop Agent Needed

The entire MVP is server-side API orchestration. No installer. No system tray. No "is my PC on?" problem. Users sign up on a web page and forget about it.

### 4. Storage Uses User's Own Quota

This sounds like a downside but it's actually fine:
- OneDrive gives 5 GB free, 1 TB with Microsoft 365
- Most users have plenty of unused quota
- Backup folder is visible and manageable by the user (transparency!)
- If they run low, they can delete old snapshots

### 5. Aligns Perfectly With the Mission

The mission is "protect everyone's digital life." Same-account backup protects against the most common threat (accidental deletion) at zero cost. **We can literally protect millions of people for free.** The Patagonia model works even better when the free tier costs nothing.

## What We Give Up

| Sacrifice | Impact | Acceptable? |
|-----------|--------|-------------|
| Ransomware protection in free tier | Real — but rare for consumers | ✅ Yes — upsell to Pro |
| Account compromise protection in free tier | Real — but rare | ✅ Yes — upsell to Pro |
| "Different bank" messaging | Weakened for free tier | ✅ Adjust messaging |
| Google Drive at launch | Can't do without $15-75K | ✅ Start OneDrive-only |
| User's storage consumed | They manage their own quota | ✅ Most have plenty |

## Revised Technical Architecture

```
PHASE 1 MVP:

┌──────────────────────────┐
│  VintageVault Web App     │     ┌─────────────────────────┐
│  (Azure Static Web App)  │     │  Azure Functions          │
│                          │     │  (Consumption Plan)       │
│  • User signup (email)   │     │                          │
│  • OAuth to OneDrive     │────►│  • Timer trigger (weekly) │
│  • View backup status    │     │  • Graph delta API        │
│  • Browse/restore files  │     │  • driveItem: copy        │
│  • Upgrade to Pro        │     │  • Status tracking        │
│                          │     │                          │
│  Cost: ~$0-1/month       │     │  Cost: ~$0-5/month       │
└──────────────────────────┘     └─────────────────────────┘

Total MVP infrastructure: ~$0-10/month
Development: 40-60 hours (much simpler than relay architecture)
```

### Phase 2: Pro Tier (Cross-Account)

Only build this after validating demand with the free tier:
- Add cross-account backup (relay or agent)
- Add anomaly detection
- Add Google Drive (fund assessment from Pro revenue)

## The Big Question: Does This Still Matter?

**"Isn't this just a fancy recycle bin?"**

Kind of. But the recycle bin:
- Empties after 30-93 days (permanently)
- Doesn't protect against overwriting (only deletion)
- Doesn't provide point-in-time browsing ("what did this folder look like last Tuesday?")
- Doesn't alert you when things go wrong
- Doesn't do anything proactively

VintageVault same-account backup:
- Keeps snapshots for 30-365 days
- Protects against both deletion AND overwriting
- Point-in-time restore ("restore this folder to March 1st")
- Proactive health reporting
- Anomaly detection (even in free tier, can warn without cross-account backup)

**It's the difference between having a recycling bin and having a time machine.** OneDrive's version history does some of this per-file, but not at the folder/account level with managed retention.

## Summary

| Question | Answer |
|----------|--------|
| Can we back up within the same account? | **Yes — server-side, zero bandwidth, for OneDrive** |
| Does it require a relay or agent? | **No — pure API orchestration** |
| What does it cost us? | **~$0-10/month total** |
| What does it protect against? | **Accidental deletion, overwrite, corruption** |
| What doesn't it protect against? | **Ransomware, account compromise** |
| Is that enough for a free tier? | **Yes — accidental deletion is the most common data loss** |
| What's the upsell? | **Cross-account backup = ransomware protection** |
| How fast can we build it? | **40-60 hours (dramatically simpler)** |
| Google Drive? | **Not at launch (still needs $15-75K assessment)** |
