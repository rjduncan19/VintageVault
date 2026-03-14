# UX Wireframes & User Flow Walkthrough

_Last updated: 2026-03-14 | Model: Claude Opus 4.6_

## Overview

This document describes VintageVault's user experience across four key screens, each implemented as an interactive HTML mockup in [`docs/mockups/`](../mockups/index.html). Open the HTML files in any browser to explore the wireframes.

---

## Screen 1: Landing Page ([01-landing-page.html](../mockups/01-landing-page.html))

**Purpose:** First impression. Convert visitors to sign-ups.

**Key sections:**
1. **Hero** — "Your cloud's backup plan" headline with clear value proposition and two CTAs (primary: "Start Free Backup", secondary: "See How It Works")
2. **How It Works** — Three-box flow diagram: Source Cloud → Your Computer → Backup Cloud, with privacy badge ("Your data never touches our servers")
3. **Threat Cards** — Four threats with protection explanations: ransomware, accidental deletion, account compromise, provider outage
4. **Features** — Six cards: Set & Forget, Ransomware Detection, Monthly Health Reports, Privacy First, Time Travel Restore, Family Dashboard
5. **Pricing** — Three-tier comparison: Free / Pro $3.99/mo / Family $7.99/mo
6. **Final CTA** — "Your files deserve a backup plan"

**Design notes:**
- Dark hero section (trust, security feel) → light content sections → dark CTA
- Privacy badge is prominently placed — this is our key differentiator
- Pricing uses the "Most Popular" badge on Pro to anchor the decision

---

## Screen 2: Setup Wizard ([02-setup-wizard.html](../mockups/02-setup-wizard.html))

**Purpose:** Get users from install to first backup in under 5 minutes.

**Flow:**

```
Step 1: Choose Source     Step 2: Choose Destination     Step 3: Confirm     Success!
┌─────────────────┐     ┌──────────────────────┐     ┌──────────────┐     ┌──────────┐
│ Where are your  │     │ Where should we      │     │ Review your  │     │ You're   │
│ files?          │────►│ back up to?          │────►│ backup plan  │────►│ protected│
│                 │     │                      │     │              │     │ 🎉       │
│ [OneDrive] ●    │     │ [OneDrive] (source)  │     │ OneDrive →   │     │          │
│ [Google Drive]  │     │ [Google Drive] ●     │     │ Google Drive │     │ Backup   │
│ [Dropbox] soon  │     │ [Dropbox] soon       │     │ Weekly sched │     │ running! │
│ [iCloud] soon   │     │ [iCloud] soon        │     │ ~4,200 files │     │          │
└─────────────────┘     └──────────────────────┘     └──────────────┘     └──────────┘
```

**Step 1 — Source selection:**
- Provider grid with clear icons (OneDrive, Google Drive active; Dropbox, iCloud grayed with "Coming soon")
- OAuth button shows connected state with email and green check
- Single selection — one source per pair

**Step 2 — Destination selection:**
- Source provider is grayed out ("Already your source") to prevent same-account backup
- OAuth button for destination with connected state
- Shows destination path: `Google Drive / VintageVault / OneDrive /`

**Step 3 — Confirmation:**
- Visual flow: Source → Your PC → Destination
- Summary table: accounts, what to back up ("Everything"), schedule, retention, estimated time
- Tip card about first backup duration
- "Start My Backup" button (green, feels different from navigation blue)

**Success screen:**
- Celebration emoji (🎉) and "You're protected!" message
- Three status indicators: backup in progress, notifications on, system tray active
- "You can close this window" reassurance
- Link to open full dashboard

**UX principles applied:**
- Progress bar (1-2-3) reduces uncertainty
- "Coming soon" providers set expectations without blocking flow
- OAuth is shown as already connected (mockup assumes success state)
- Estimated backup time is shown before commitment (managing expectations)

---

## Screen 3: Dashboard ([03-dashboard.html](../mockups/03-dashboard.html))

**Purpose:** At-a-glance backup health. The screen users see most (but not often — backup should be invisible).

**Layout:** Sidebar navigation + main content area

**Sidebar:**
- Logo, navigation links (Dashboard, Backup Pairs, Restore, Activity Log)
- Settings section (Preferences, Account, Family)
- User avatar with plan indicator ("Free Plan")

**Main content:**

```
┌─────────────────────────────────────────────────────────────┐
│  HEALTH BANNER                                               │
│  [95 Health Score ●] Everything looks great! 🎉              │
│  12,847 files · 38.4 GB · 99.8% success                    │
├─────────────────────────────────────────────────────────────┤
│  STATS:  Files Protected │ Total Backed Up │ Last │ Next    │
│          12,847          │ 38.4 GB         │ 2h   │ Sun 3am │
├───────────────────────────────────┬──────────────────────────┤
│  BACKUP PAIRS                    │  SCHEDULE                │
│  ● OneDrive → Google Drive       │  Every Sunday 3:00 AM    │
│    ✓ Healthy                     │  [Upgrade for daily]     │
│  + Add pair (Pro)                ├──────────────────────────┤
│                                  │  QUICK ACTIONS           │
│  RECENT ACTIVITY                 │  [Run Backup Now]        │
│  ✅ Backup completed (2h ago)    │  [Restore Files]         │
│  📋 Verification passed          │  [View Full Log]         │
│  ✅ Backup completed (Mar 9)     ├──────────────────────────┤
│  📧 Monthly report sent          │  SYSTEM TRAY PREVIEW     │
│  ⚠️ PC sleep interrupted (Feb 23)│  🔒 Protected            │
└───────────────────────────────────┴──────────────────────────┘
```

**Key design decisions:**
- Health score is the dominant visual — green/circular badge makes status instantly clear
- Stats cards show the numbers users care about
- Activity feed shows human-readable events, not technical logs
- "Upgrade" prompts are subtle (amber text, not blocking)
- System tray preview shows what the desktop agent looks like

---

## Screen 4: Restore Flow ([04-restore.html](../mockups/04-restore.html))

**Purpose:** File recovery during a stressful moment. Must be calm, guided, and confidence-inspiring.

**Flow:**

```
Browse & Select    →    Confirm Restore    →    Restoring...    →    Complete!
┌──────────────┐     ┌────────────────┐     ┌──────────────┐     ┌──────────┐
│ Time slider  │     │ 3 items to     │     │ 🔄 67%       │     │ 🎉       │
│ File browser │     │ restore        │     │ 2.3/3.4 GB   │     │ 261 files│
│ [✓] Select   │────►│ Restore to:    │────►│ ~8 min left   │────►│ restored │
│ [✓] Select   │     │ ○ Original     │     │              │     │ All ✓    │
│ [3 selected] │     │ ○ New folder   │     │ Progress list │     │          │
└──────────────┘     └────────────────┘     └──────────────┘     └──────────┘
```

**Browse & Select:**
- **Time Travel slider** — scrub through backup history (30 days for Free, 365 for Family). Shows selected date prominently.
- **Breadcrumb navigation** — familiar folder browsing experience
- **File browser** with status indicators:
  - ✓ Current (green) — file is unchanged
  - Modified since (blue) — file exists but has changed
  - ⚠️ Deleted from source (red background) — file was deleted, only exists in backup
- Checkbox selection for multi-file restore
- Bottom bar shows selection count, size estimate, and "Restore Selected" button

**Confirm Restore:**
- Lists each file/folder being restored with what will happen (overwrite, re-create)
- Four destination options: Original location, Restored Files folder, Download to PC, Custom location
- Safety message: "Existing files won't be deleted" (reduces anxiety)
- Clear summary of total items and size

**Restoring (Progress):**
- Large progress indicator with percentage and ETA
- Currently restoring file shown
- Completed items with green checks
- "Please keep VintageVault running" notice (but allows continued computer use)

**Complete (Success):**
- Celebration with itemized confirmation
- Summary stats: 261 files, 3.4 GB, 12 minutes, all checksums verified
- Options to browse more files or return to dashboard

**UX principles applied:**
- Time Travel metaphor inspired by Apple Time Machine
- Deleted files highlighted prominently (the most common restore scenario)
- Multiple restore destination options reduce decision anxiety
- Progress screen is informative but not overwhelming
- Checksum verification confirmation builds trust

---

## User Journey Summary

```
FIRST TIME USER:
  Landing Page → Download Agent → Setup Wizard (5 min) → Dashboard → [done]
                                                                      ↓
ONGOING USE:                                                    [runs silently]
  Monthly email: "12,847 files protected" → (optional) Dashboard check
                                                                      
RESTORE EVENT:
  Dashboard → Restore → Time Travel Browse → Select Files → Confirm → Done
```

**The ideal user journey has very few touchpoints.** After setup, VintageVault should be invisible — the less users interact with the dashboard, the better the product is doing its job.
