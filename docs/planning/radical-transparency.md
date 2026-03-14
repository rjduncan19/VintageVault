# Radical Transparency Model: DIY-First Backup

_Created: 2026-03-14 | Model: Claude Opus 4.6_

## The Idea

Instead of building a proprietary backup system, build a **transparent tool that does something simple and teaches users what it's doing** — so they could do it themselves if they wanted to. Then charge for the convenience of automation.

This is the Patagonia version of backup: _"Here's how to protect your files. Do it yourself for free, or pay us to do it automatically."_

---

## What the Free Tool Does

```
VintageVault creates a /VintageVault-Backup/ folder in your OneDrive.
Inside, it creates dated snapshot folders.
Each snapshot contains copies of your files.

Your OneDrive:
├── Documents/
│   ├── Tax Returns/
│   └── Family Budget.xlsx
├── Photos/
│   └── Vacation 2025/
└── VintageVault-Backup/           ← We create this
    ├── 2026-03-07/                ← Weekly snapshot
    │   ├── Documents/
    │   │   ├── Tax Returns/
    │   │   └── Family Budget.xlsx
    │   └── Photos/
    │       └── Vacation 2025/
    ├── 2026-03-14/                ← This week's snapshot
    │   └── ...
    └── backup-manifest.json       ← What was backed up, when, checksums
```

**That's it.** Regular folders. Regular files. No proprietary format. No lock-in. You can browse your backup in File Explorer. You can download it. You can delete it. You can share the folder with another account manually.

### Why NOT a ZIP

| ZIP | Plain folder copies |
|-----|-------------------|
| ❌ Can't browse without extracting | ✅ Browse in File Explorer / web |
| ❌ Can't restore single files easily | ✅ Just copy the file back |
| ❌ Must re-ZIP everything (no incremental) | ✅ Only copy changed files |
| ❌ Huge files (100GB+ ZIP is unwieldy) | ✅ Individual files are manageable |
| ❌ Proprietary-feeling | ✅ Completely transparent |
| ❌ OneDrive 250GB upload limit | ✅ No single-file limit concern |
| ✅ Single file to download | ⚠️ Multiple files to download |

**Plain folder copies are more transparent, more useful, and more aligned with the mission.** The user can see exactly what's backed up, restore individual files by drag-and-drop, and share/move the backup folder without any special tools.

A `backup-manifest.json` file in the root provides metadata (what was backed up, when, file checksums) for the tool to work efficiently, but it's also human-readable.

---

## The OneDrive Cross-Account Sharing Question

> "If Account A shares EVERYTHING with Account B, can B use cloud APIs to copy?"

**Answer: Technically yes, but the API is being killed.**

- The `/me/drive/sharedWithMe` endpoint is **deprecated** as of 2025 and will be **fully removed November 2026**
- It's already broken — returning only one result in many cases
- Microsoft's replacement (`/shares/` API) requires encoding sharing URLs per-item — there's no "list everything shared with me" equivalent
- `driveItem: copy` CAN copy shared items to your own drive, but discovering what's shared is the broken part

**This means the "share everything → B copies via API" approach is a dead end for a new product.**

### What DOES Work for Cross-Account

For the Pro tier (cross-account backup), the options remain:
1. **Same-account copy + manual share:** User shares their `/VintageVault-Backup/` folder with Account B. Done manually by the user. We just create the backup — they decide where to share it.
2. **Download + upload via relay:** Our server downloads from A, uploads to B. Works but costs bandwidth.
3. **Desktop agent:** Runs on user's PC. Downloads from A, uploads to B locally.

Option 1 is the most mission-aligned: **we create the backup, you decide what to do with it.**

---

## The Radical Transparency Product

### Free: "We'll Make the Backup. You Own It."

```
What VintageVault does:
  1. Connects to your OneDrive (OAuth)
  2. Creates /VintageVault-Backup/ folder
  3. Copies changed files on a schedule (weekly)
  4. Keeps 30 days of snapshots
  5. Sends you a health email

What YOU can do with your backup:
  • Browse it in File Explorer (it's just folders)
  • Download any file (it's your OneDrive)
  • Share the folder with another account (you decide)
  • Copy it to a USB drive (download + drag)
  • Delete old snapshots to save space
  • Stop using VintageVault anytime (your files stay)

We don't lock anything. We don't use proprietary formats.
We don't store anything on our servers.
Your backup is yours.
```

### Paid: "We'll Do More, Transparently"

| What you pay for | Why it costs money | What it does |
|-----------------|-------------------|-------------|
| **Daily/hourly backups** ($4.99/mo) | More API calls, more server orchestration | Shorter gap between "oops" and your last backup |
| **Cross-account copy** ($4.99/mo) | Requires relay server bandwidth | Copies to a completely separate account (ransomware protection) |
| **Anomaly detection** ($4.99/mo) | ML processing, alerting infrastructure | Warns you if your files are being mass-encrypted |
| **Family dashboard** ($9.99/mo) | Multi-user management, shared UI | See backup status for 5 family members |

**Every paid feature explains WHY it costs money.** No hidden margins. No "Pro features" that are artificially restricted. If something costs us $0 to provide, it's free.

---

## How This Aligns With the Mission

### Patagonia Parallel

Patagonia teaches you to repair your jacket instead of buying a new one. They sell you a new jacket only when repair isn't an option.

VintageVault teaches you to protect your files. It does it for you automatically for free. It charges only for things that genuinely cost money to provide (server bandwidth, advanced processing).

### The "Worn Wear" Equivalent

Patagonia's **Worn Wear** program: "Buy used, trade in, repair."

VintageVault's equivalent: **"Here's how to back up your files yourself. Here's a free tool that does it automatically. Here are paid features that genuinely require infrastructure we must pay for."**

### What We Publish

Following Buffer's radical transparency:

```
MONTHLY TRANSPARENCY REPORT:
  Users protected (free): 12,847
  Users protected (paid): 423
  Files backed up this month: 2.4 million
  Infrastructure cost: $8.43
  Revenue: $2,115
  Margin: $2,106.57
  1% donated to: Digital Safety Alliance ($21.15)
```

---

## DIY Instructions (Included in Product)

Part of the product is teaching people to do it themselves. The web app includes a page:

### "How to Back Up Your OneDrive — With or Without Us"

**Option 1: Let VintageVault do it (free)**
→ Sign up, authorize, we handle everything.

**Option 2: Do it yourself (also free, no account needed)**
1. Open OneDrive in your browser
2. Create a folder called `My Backup`
3. Select all your important folders
4. Right-click → Copy to → My Backup
5. Set a recurring calendar reminder to do this monthly
6. Congratulations, you have a backup!

**Option 3: Extra protection (DIY)**
1. Do Option 2
2. Right-click your `My Backup` folder → Share
3. Share with a family member's Microsoft account
4. They now have access to your backup files
5. Even if your account is compromised, theirs has a copy

**This is what VintageVault automates.** There's no magic. No proprietary technology. Just disciplined, scheduled copying that most people won't do manually.

---

## Revised Messaging

**Old:** "Your cloud's backup plan" (product-centric)

**New:** "Protecting your digital life — with or without us" (mission-centric)

**Taglines:**
- _"The backup you can see, touch, and take with you"_
- _"No proprietary formats. No lock-in. Just copies of your files."_
- _"We automate what you could do yourself — so you actually do it"_

---

## Impact on Devil's Advocate Concerns

| Concern | How This Helps |
|---------|---------------|
| "Nobody will pay for this" | They don't have to. Free tier costs us ~$0. Paid features have genuine cost justification. |
| "It's just a fancy recycle bin" | Yes, and we say so! Honesty builds trust. The upsell (cross-account) is genuinely different. |
| "Trust gap for solo developer" | Maximum transparency: open source, plain files, DIY instructions, no lock-in |
| "Feature, not a product" | We know. We're a mission, not a company. If OneDrive adds this natively, we celebrate. |
| "Google can kill you" | We're starting OneDrive-only. And if Google adds backup, more people are protected. Win. |

---

## Summary

| | Traditional SaaS Approach | Radical Transparency Approach |
|---|---|---|
| Backup format | Proprietary database | Plain folders you can browse |
| Lock-in | Need our tool to restore | Drag and drop in File Explorer |
| DIY option | Not mentioned | Actively taught and encouraged |
| Pricing justification | "Pro features" | "This costs us $X to provide" |
| If user leaves | Loses access to backups | Keeps everything (it's their OneDrive) |
| If we shut down | Users lose backups | Nothing changes (files are in their drive) |
| Mission alignment | Moderate | Complete |
