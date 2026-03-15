# Cross-Account Backup: Provider-Specific Strategies

_Created: 2026-03-15 | Model: Claude Opus 4.6_

## The Tension

The mission requires robust ransomware protection. Same-account snapshots protect against accidental deletion but NOT ransomware (attacker can encrypt the backup folder too). True protection requires a copy on a **separate account** that ransomware can't reach.

But cross-account backup has a cost problem:

| Approach | Bandwidth | Reliability | Our Cost |
|----------|-----------|-------------|----------|
| **Server relay** (we download + upload) | User's data through our servers | High (always on) | $0.50-0.74/user/month |
| **Desktop agent** (user downloads + uploads) | User's bandwidth | Low (PC must be running) | $0 |
| **Google share-then-copy** | Zero (server-side) | High | $0 |
| **OneDrive share-then-copy** | Not possible via API | N/A | N/A |

**Google and OneDrive have fundamentally different capabilities here.** We should treat them separately.

---

## Google Drive: The Zero-Cost Cross-Account Path ⭐

Google's `files.copy` API works on shared files across accounts. This is the one provider where true cross-account backup is free:

```
FLOW: Google → Google cross-account (zero bandwidth, zero cost)

Account A (source):
  1. VintageVault enumerates files via changes API
  2. For each file to back up:
     a. Share file with Account B (sendNotificationEmail: false)
     b. Record share in manifest

Account B (destination, authorized separately):
  3. For each newly shared file:
     a. files.copy → creates owned copy in /VintageVault-Backup/
     b. Unshare the original (remove Account B's access)
     c. Record copy in snapshot manifest

Result: Account B has a full copy. Data never left Google's servers.
Account A's owner can't touch Account B's copies.
```

**This is the holy grail:** true cross-account ransomware isolation at zero bandwidth cost, zero server cost. The only cost is API calls (within Google's free quotas).

**Caveats:**
- Requires OAuth for BOTH accounts (user authorizes A and B)
- Google's OAuth verification ($15-75K) is still a barrier — BUT for <100 test users it's free
- 750 GB/user/day copy limit
- Folder structure must be recreated manually (`files.copy` doesn't copy folders)
- Google Docs/Sheets copy natively; binary files also work

**Recommended for:** Free tier cross-account backup. This is the mission-aligned play — true ransomware protection at zero cost to us AND zero cost to the user.

---

## OneDrive: The Resumable Client Approach

OneDrive has no server-side cross-account copy API. The data must physically move through an intermediary. Two options:

### Option A: Server Relay ($0.50-0.74/user/month)
Traditional approach — our server downloads from Account A, uploads to Account B. Works but costs bandwidth. Reserved for Pro tier.

### Option B: Browser-Based Resumable Transfer (Novel) ⭐

**The insight:** We don't need a desktop agent. A **web page left open in a browser tab** can orchestrate the transfer using the Graph API directly from the browser via JavaScript + MSAL.js. The browser IS the client.

```
USER EXPERIENCE:

1. User opens app.vintagevault.com/transfer
2. Signs into Account A (source) and Account B (destination)
3. Page shows: "Transferring 847 files (12.3 GB)..."
4. User leaves the tab open while they work on other things
5. Transfer happens file-by-file: download from A → upload to B
   (data flows through the browser, NOT our servers)
6. If user closes the tab or PC sleeps:
   → Progress is saved to Account A's VintageVault-Backup/transfer-state.json
   → Next time user opens the page, it resumes from where it left off
7. When complete: "✅ Cross-account backup finished!"
```

**Why this works better than a desktop agent:**
- No install required — it's just a web page
- Resumable — state is saved per-file, not per-session
- Uses the user's bandwidth (not ours)
- Runs in any browser on any device
- OAuth tokens stay in the browser (MSAL.js) — never touch our servers

**The resume mechanism:**

```json
// transfer-state.json (saved in Account A's VintageVault-Backup/)
{
  "transferId": "2026-03-15-cross-account",
  "sourceAccount": "richard@example.com",
  "destinationDriveId": "b!xyz...",
  "snapshotId": "2026-03-14-full",
  "status": "in_progress",
  "totalFiles": 847,
  "completedFiles": 423,
  "completedBytes": 6442450944,
  "lastCompletedItem": "/Documents/Work/report.docx",
  "fileManifest": [
    { "path": "/Documents/file1.docx", "itemId": "abc", "status": "completed" },
    { "path": "/Documents/file2.xlsx", "itemId": "def", "status": "completed" },
    { "path": "/Photos/photo1.jpg", "itemId": "ghi", "status": "pending" }
  ]
}
```

When the user reopens the page:
1. Load `transfer-state.json` from Account A
2. Skip all `"completed"` files
3. Resume from the first `"pending"` file
4. Continue transferring

**Bandwidth optimization:**
- Files are chunked (5-10 MB chunks via Graph's resumable upload)
- Each chunk is independently resumable
- Progress updates to `transfer-state.json` every 10 files (not every file — reduce API calls)
- Browser throttles transfer to ~50% of available bandwidth (configurable)

**Failure modes and handling:**

| Failure | What Happens | Resume Behavior |
|---------|-------------|-----------------|
| Tab closed | Transfer stops | Resumes from last saved checkpoint |
| PC sleeps | Transfer stops | Same — resumes on wake/reopen |
| Browser crashes | Transfer stops | Same — state file has checkpoint |
| Network drops | Transfer pauses, retries with backoff | Resumes automatically when online |
| OAuth token expires | Transfer pauses | User re-authenticates, continues |
| PC shuts down | Transfer stops | Resumes next time user opens page |

**The key insight:** Unlike a desktop agent that must be "installed" and "running," a browser tab is something users already understand. "Leave this tab open while your backup runs" is dramatically simpler than "install this .exe and make sure it's in your system tray."

### Option C: Scheduled Micro-Transfers (Future)

For users who can't leave a tab open long enough:

```
Instead of one big transfer session:
  Monday:    Transfer 50 files (user opens page for 10 minutes)
  Tuesday:   Transfer 50 more (user opens page for 10 minutes)
  Wednesday: Transfer 50 more
  ...
  After 2 weeks: All 847 files are cross-account backed up

Each session picks up exactly where the last left off.
"Open this page whenever you have a few minutes. We'll chip away at it."
```

This turns the "PC must be on" problem into micro-sessions that fit naturally into how people use computers.

---

## UX Flow: Cross-Account Setup

### Google → Google (Free Tier — Recommended First)

```
STEP 1: "Upgrade your protection"
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  🛡️ Your files are protected against accidental deletion.  │
│                                                             │
│  Want protection against ransomware too?                    │
│  Add a second Google account as a safety vault.             │
│                                                             │
│  How it works:                                              │
│  📂 Google Drive (yours)                                    │
│       ↓ share-then-copy (data never leaves Google)          │
│  📂 Google Drive (backup account)                           │
│                                                             │
│  ✓ Zero impact on your internet speed                       │
│  ✓ Ransomware on Account A can't reach Account B           │
│  ✓ Free — no subscription required                          │
│                                                             │
│  [Connect Second Google Account →]                          │
│                                                             │
│  Don't have a second account?                               │
│  Create a free one at accounts.google.com                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘

STEP 2: Authorize both accounts
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  Connect your accounts:                                     │
│                                                             │
│  Source:      richard@gmail.com           ✅ Connected      │
│  Backup:     richard.backup@gmail.com    [Connect →]        │
│                                                             │
│  We need read access to your source and write access to     │
│  your backup account. Your data stays on Google's servers.  │
│                                                             │
└─────────────────────────────────────────────────────────────┘

STEP 3: Confirm
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  Cross-account backup plan:                                 │
│                                                             │
│  📂 richard@gmail.com                                       │
│       ↓ share → copy → unshare (server-side)               │
│  📂 richard.backup@gmail.com / VintageVault-Backup/         │
│                                                             │
│  Schedule: Weekly (after your regular snapshot)              │
│  First backup: ~847 files, ~12 GB                          │
│  Estimated time: Instant (server-side copy)                 │
│                                                             │
│  [✓ Start Cross-Account Backup]                             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### OneDrive → OneDrive (Browser Transfer)

```
STEP 1: Same upgrade prompt as Google, but different messaging:
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  🛡️ Your files are protected against accidental deletion.  │
│                                                             │
│  Want protection against ransomware too?                    │
│  Add a second OneDrive account as a safety vault.           │
│                                                             │
│  How it works:                                              │
│  📁 OneDrive (yours)                                        │
│       ↓ transfers through your browser (not our servers)    │
│  📁 OneDrive (backup account)                               │
│                                                             │
│  ✓ Your data never touches our servers                      │
│  ✓ Ransomware on Account A can't reach Account B           │
│  ✓ Free — uses your own internet connection                 │
│  ✓ Resumable — close and reopen anytime                     │
│                                                             │
│  [Connect Second OneDrive Account →]                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘

STEP 2: Same dual-account authorization

STEP 3: Transfer page
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  Cross-Account Transfer                                     │
│                                                             │
│  📁 richard@example.com → 📁 backup@outlook.com            │
│                                                             │
│  [████████████████████░░░░░░░░░░] 423 / 847 files          │
│  6.2 GB / 12.3 GB · ~45 min remaining                      │
│                                                             │
│  ⏸️ Transfer speed: Normal                                  │
│  [Slower — save bandwidth] [Faster — use more bandwidth]    │
│                                                             │
│  ────────────────────────────────────────────                │
│  💡 Keep this tab open. If you need to close it,            │
│  just reopen this page later — we'll pick up                │
│  right where we left off.                                   │
│                                                             │
│  You can use other tabs and apps normally.                   │
│  ────────────────────────────────────────────                │
│                                                             │
│  Currently transferring:                                    │
│  📄 Documents/Work/Q1-Report.xlsx (2.3 MB)                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘

STEP 4: Resumed session (after closing and reopening)
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  Welcome back! Resuming your transfer...                    │
│                                                             │
│  📁 richard@example.com → 📁 backup@outlook.com            │
│                                                             │
│  Previously completed: 423 / 847 files (6.2 GB)            │
│  Remaining: 424 files (6.1 GB)                              │
│                                                             │
│  [Resume Transfer →]                                        │
│                                                             │
│  Last session: March 14, 2:30 PM (closed after 45 minutes)  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Provider Comparison: Cross-Account Capabilities

| Capability | Google Drive | OneDrive |
|-----------|-------------|----------|
| **Server-side cross-account copy** | ✅ share-then-copy (free, instant) | ❌ Not available via API |
| **Our bandwidth cost** | $0 | $0 (browser transfer) or $0.50/user (relay) |
| **User bandwidth cost** | $0 | Uses their connection |
| **Reliability** | High (server-side, always completes) | Moderate (browser must stay open) |
| **Resume support** | Not needed (instant) | ✅ Per-file checkpoint in transfer-state.json |
| **Initial backup speed** | Minutes (server-side) | Hours (limited by upload speed) |
| **Incremental updates** | Minutes | Minutes (only changed files) |
| **OAuth cost barrier** | $15-75K assessment (or <100 test users) | Free |
| **Free tier viable?** | ✅ Yes — zero cost to us | ✅ Yes — zero cost to us |

**Strategic implication:** Google cross-account is the superior path technically, but has the OAuth cost barrier. OneDrive is available now but requires the browser transfer approach. Both can be offered in the free tier.

---

## Revised Tier Strategy

Given that cross-account backup CAN be free (Google) or near-free (OneDrive browser transfer):

| | Free | Pro ($4.99/mo) |
|---|---|---|
| **Same-account snapshots** | ✅ | ✅ |
| **Cross-account (Google → Google)** | ✅ Server-side, zero cost | ✅ |
| **Cross-account (OneDrive → OneDrive)** | ✅ Browser transfer (resumable) | ✅ Server relay (always-on, no tab needed) |
| **Schedule** | Weekly | Daily/hourly |
| **Detection** | Metadata | + Content analysis |
| **What Pro adds** | — | Speed, convenience (relay instead of browser tab), deeper detection |

**This is the mission-aligned play:** Real ransomware protection in the free tier, for both Google and OneDrive users. Pro upgrades convenience and speed, not fundamental protection.

---

---

## Open Decision: Auth Model for Cross-Account Transfer

**Status:** Needs decision before Phase 2 implementation

Cross-account backup requires the user to authenticate with TWO accounts simultaneously. How we handle this affects security, UX, and architecture. The options have real trade-offs:

### Option 1: CLI Tool (Most Secure)

The CLI we already built uses MSAL + OS keychain (DPAPI/Keychain/libsecret). Adding dual-account support is straightforward — MSAL natively supports `getAllAccounts()`. Tokens are encrypted at the OS level, zero XSS risk.

| Pro | Con |
|-----|-----|
| OS-encrypted token storage | Requires install (CLI binary) |
| No web attack surface | Less accessible for non-technical users |
| Already built (Phase 1 POC) | No web-based resume UX |
| Supports resumable transfers natively | |

### Option 2: Browser SPA (Most Accessible)

MSAL.js in a web app — the approach described in the browser transfer UX above. Standard for Microsoft 365 web apps. Dual-account works via separate `PublicClientApplication` instances.

| Pro | Con |
|-----|-----|
| No install — just a URL | `sessionStorage`: re-auth every tab close |
| Works on any device | `localStorage`: persists but XSS-vulnerable |
| Best for non-technical users | Dual-account flow is awkward (two popups) |
| Resume UX is natural ("reopen this page") | Requires strict CSP to mitigate XSS |

**The token storage dilemma:**
- `sessionStorage` (safer) = user must re-authenticate every time they reopen the tab. Terrible for resumable transfers.
- `localStorage` (convenient) = tokens persist across sessions but any JS on the page can read them. Requires zero third-party scripts and strict Content Security Policy.

### Option 3: PWA — Progressive Web App (Best of Both)

Install from the browser → gets OS-level storage and background capabilities. Bridges the gap between web accessibility and native security.

| Pro | Con |
|-----|-----|
| Installable from browser (no app store) | PWA storage APIs still evolving |
| OS-level credential storage on some platforms | Background execution limited on iOS/some browsers |
| Works offline | Adds complexity to build/test |
| "Install" feels lighter than "download .exe" | |

### Option 4: Phased Approach (Recommended?)

1. **Phase 1 (now):** CLI tool with OS keychain — already built, most secure
2. **Phase 2:** Web SPA with `localStorage` + strict CSP — for the browser transfer UX
3. **Phase 3:** PWA wrapper — adds install prompt, OS storage, background capabilities

This lets us ship cross-account backup securely (CLI) while building toward the browser experience. No single decision blocks progress.

### Decision Criteria

- How important is "no install" for the cross-account flow specifically?
- Is `localStorage` + strict CSP acceptable risk for a first-party SPA?
- Do we expect users to do cross-account setup once (tolerate friction) or repeatedly (need smooth UX)?
- Should the Phase 1 CLI get a `--cross-account` flag before we build any web transfer UI?

**This decision should be made before Phase 2 implementation begins.**

---

## Impact on Mission & Brand

```
OLD MESSAGE:
  "Free protects against deletion. Pay $4.99/mo for ransomware protection."
  → Feels like holding safety hostage behind a paywall.

NEW MESSAGE:
  "Everyone gets ransomware protection. Free. Open source.
   Pro makes it faster and more convenient."
  → Mission-aligned. Builds trust. Earns recommendations.
```

This is the Patagonia move: give away the critical safety feature, charge for convenience. It's counterintuitive for business but powerful for brand trust — and with infrastructure costs near $0, we can afford to do it.
