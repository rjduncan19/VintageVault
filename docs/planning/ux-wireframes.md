# UX Wireframes & User Flow Walkthrough

_Last updated: 2026-03-14 | Model: Claude Opus 4.6_

## Overview

This document describes VintageVault's user experience across four key screens, each implemented as an interactive HTML mockup in [`docs/mockups/`](../mockups/index.html). Open the HTML files in any browser to explore the wireframes.

---

## Screen 1: Landing Page ([01-landing-page.html](../mockups/01-landing-page.html))

**Purpose:** First impression. Convert visitors to sign-ups.

**Key sections:**
1. **Hero** — "Your files deserve a safety net" headline with clear value proposition and two CTAs (primary: "Protect My Files", secondary: "See How It Works")
2. **How It Works** — Three-box flow diagram: Your OneDrive → VintageVault Cloud API → VintageVault-Backup Folder, with transparency badge ("Your backup is plain folders you can browse, download, or share")
3. **Threat Cards** — Four threats with honest Free/Pro split: accidental deletion and overwrite (Free), ransomware and account compromise (Pro upsell)
4. **Features** — Six cards: Set & Forget, Ransomware Detection (Pro), Monthly Health Reports, Radical Transparency, Rewind & Restore, Family Dashboard
5. **Pricing** — Three-tier comparison: Free / Pro $4.99/mo / Family $9.99/mo
6. **Final CTA** — "Everyone deserves a backup plan."

**Design notes:**
- Dark hero section (trust, security feel) → light content sections → dark CTA
- Privacy badge is prominently placed — this is our key differentiator
- Pricing uses the "Most Popular" badge on Pro to anchor the decision

---

## Screen 2: Setup Wizard ([02-setup-wizard.html](../mockups/02-setup-wizard.html))

**Purpose:** Get users from install to first backup in under 5 minutes.

**Flow:**

```
Step 1: Connect OneDrive       Step 2: Confirm               Success!
┌─────────────────┐         ┌──────────────┐             ┌──────────┐
│ Connect your    │         │ Review your  │             │ You're   │
│ OneDrive        │────────►│ backup plan  │────────────►│ protected│
│                 │         │              │             │ 🎉       │
│ [OneDrive] ●    │         │ OneDrive →   │             │          │
│ [Google] soon   │         │ VintageVault │             │ Snapshot │
│ [Dropbox] soon  │         │   -Backup/   │             │ running! │
│ [iCloud] soon   │         │ Weekly sched │             │          │
└─────────────────┘         └──────────────┘             └──────────┘
```

**Step 1 — Connect OneDrive:**
- OneDrive is the only active option; Google/Dropbox/iCloud grayed with "Coming soon"
- OAuth button shows connected state with email and green check
- Single provider for MVP

**Step 2 — Confirmation:**
- Visual flow: Your OneDrive → Cloud API → VintageVault-Backup folder
- Summary table: account, what to back up ("Everything"), backup location (OneDrive/VintageVault-Backup/), schedule, retention
- Transparency card: "Your backup is just folders — browse, download, or share anytime"
- Honest warning: "Same-account backup protects against accidental deletion but not ransomware. Upgrade to Pro for cross-account protection."
- "Start My Backup" button (green, feels different from navigation blue)

**Success screen:**
- Celebration emoji (🎉) and "You're protected!" message
- Three status indicators: snapshot in progress, email notification on, "browse your backup anytime"
- "No install needed. No app to run. Snapshots happen automatically in the cloud."
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
- Logo, navigation links (Dashboard, Snapshots, Restore, Activity Log)
- Settings section (Preferences, Account, Family)
- User avatar with plan indicator ("Free Plan")

**Main content:**

```
┌─────────────────────────────────────────────────────────────┐
│  HEALTH BANNER                                               │
│  [95 Health Score ●] Your snapshots are up to date! 🎉      │
│  12,847 files · 38.4 GB · 99.8% success                    │
├─────────────────────────────────────────────────────────────┤
│  STATS:  Files Protected │ Total Backed Up │ Last     │Next │
│          12,847          │ 38.4 GB         │ Snapshot │ Sun │
├───────────────────────────────────┬──────────────────────────┤
│  BACKUP STATUS                   │  SCHEDULE                │
│  ● OneDrive → VintageVault-Backup│  Every Sunday 3:00 AM    │
│    ✓ Healthy                     │  [Upgrade for daily]     │
│  + Cross-account (Pro)           ├──────────────────────────┤
│                                  │  QUICK ACTIONS           │
│  RECENT ACTIVITY                 │  [Run Snapshot Now]      │
│  ✅ Snapshot completed (2h ago)  │  [Restore Files]         │
│  📋 Verification passed          │  [View Full Log]         │
│  ✅ Snapshot completed (Mar 9)   ├──────────────────────────┤
│  📧 Monthly report sent          │  HOW IT WORKS            │
│  ⚠️ 0 changes detected (Feb 23) │  Browse your backup at   │
└───────────────────────────────────┤  OneDrive/VintageVault-  │
                                    │  Backup/ anytime         │
                                    └──────────────────────────┘
```

**Key design decisions:**
- Health score is the dominant visual — green/circular badge makes status instantly clear
- Stats cards show the numbers users care about, using "snapshot" terminology
- Activity feed shows human-readable events, not technical logs
- "Upgrade" prompts are subtle (amber text, not blocking)
- Transparency panel replaces system tray preview — links to backup folder and DIY instructions

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
- **Rewind slider** — scrub through backup history (30 days for Free, 365 for Family). Shows selected date prominently.
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
- Rewind metaphor (browse snapshots by date)
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
