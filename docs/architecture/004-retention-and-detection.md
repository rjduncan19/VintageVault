# ADR-004: Retention Strategy & Ransomware Detection

**Status:** Accepted
**Date:** 2026-03-14
**Decision Makers:** VintageVault Core Team

## The Key Insight

VintageVault's backup lives in **the user's own cloud storage**. Storage costs are on their cloud provider's balance sheet, not ours. Our infrastructure cost is API orchestration (~$0/month via Azure Functions) regardless of how much data the user stores.

This means:
- **We have no financial reason to limit retention.** Keeping snapshots forever costs us nothing.
- **The user controls their own storage.** If they want to clean up old snapshots to reclaim space, they can — it's their OneDrive folder.
- **Arbitrary retention limits (30/90/365 days) are artificial product tiers**, not technical necessities. They contradict the mission.

## Decision: Keep Everything by Default. Let Users Clean Up.

### Why This Is Right for Consumers

Research on consumer cloud storage behavior (2024-2025) overwhelmingly confirms:

- **Consumers are digital hoarders.** Emotional attachment to files drives accumulation. Most users never intentionally delete files — they buy more storage or open new accounts when quota runs low.
- **~50% of cloud accounts show zero deletion activity.** Users treat cloud storage as write-once archival, not actively managed filing systems.
- **Only a minority routinely clean up.** The typical consumer flow is: create → accumulate → ignore → hit quota → buy more storage.
- **Deletion is accidental, not intentional.** When files disappear, it's almost always unintentional (drag-and-drop error, sync conflict, shared folder mishap) — not a deliberate cleanup.

**This means:**
1. Our incremental snapshots will be very small after the initial full — most weeks, very few files change for a typical family.
2. The "deleted files" category in our snapshots will be tiny — people rarely delete.
3. A "keep N days" retention policy would delete clean backup copies that cost us nothing to keep, while providing a false sense of security against patient ransomware.
4. The real UX need isn't auto-pruning — it's helping users who DO occasionally want to clean up.

### Default Behavior

```
Every snapshot is kept indefinitely.
We never auto-delete anything.
The user's backup grows with their incremental changes.
```

With immutable incremental snapshots (ADR-003) and realistic consumer behavior (~1-2% weekly change rate on 50 GB):

```
After 1 year:   50 GB baseline + ~26-52 GB incrementals = ~76-102 GB
After 5 years:  50 GB baseline + ~130-260 GB incrementals = ~180-310 GB
```

For most Microsoft 365 users with 1 TB of storage, this is fine for years. For free-tier users (5 GB), they'll hit limits sooner — but that's between them and Microsoft, not us.

### User-Controlled Cleanup: "Manage Storage" UX

Most users will never need this. But for those hitting quota limits, we provide an explicit cleanup flow — not auto-pruning, not hidden background deletion.

**UX flow: "Manage Your Backup Storage"**

```
┌─────────────────────────────────────────────────────────┐
│  Manage Backup Storage                                   │
│                                                         │
│  Your backup is using 89 GB of your OneDrive storage    │
│  ██████████████████░░░░░░░ 89 GB / 1 TB                │
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │  Snapshots (23 total)               89 GB total │    │
│  │                                                 │    │
│  │  📦 2026-03-14-full    BASELINE     50.0 GB     │    │
│  │  📦 2026-03-21         incremental   1.2 GB     │    │
│  │  📦 2026-03-28         incremental   0.8 GB     │    │
│  │  📦 ...                                         │    │
│  │  📦 2026-08-29         incremental   1.1 GB     │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │  💡 Smart Cleanup Options                       │    │
│  │                                                 │    │
│  │  ○ Consolidate to monthly snapshots             │    │
│  │    Keep 1 snapshot per month (oldest kept)       │    │
│  │    Saves ~28 GB                                 │    │
│  │                                                 │    │
│  │  ○ Remove snapshots older than 6 months         │    │
│  │    Saves ~34 GB                                 │    │
│  │                                                 │    │
│  │  ○ Create new baseline + remove old snapshots   │    │
│  │    Fresh full copy, remove everything before it │    │
│  │    Saves ~38 GB                                 │    │
│  │                                                 │    │
│  │  ⚠️ All cleanup is permanent. Deleted snapshots │    │
│  │  cannot be recovered.                           │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  [Cancel]                        [Clean Up Selected]    │
└─────────────────────────────────────────────────────────┘
```

**Key principles:**
- **Never auto-delete.** Every cleanup action is user-initiated and confirmed.
- **Show what you'll lose.** "This will remove your ability to restore files from before June 2026."
- **Offer GFS-style consolidation** as a smart option: "Keep one snapshot per month" reduces count while maintaining long-term recovery points.
- **This is NOT a primary flow.** Most users never see this screen. It's in Settings → Storage, not on the dashboard.

### Opt-In GFS Consolidation (Smart Cleanup)

For users who want to manage storage proactively, offer an opt-in "Smart Retention" setting:

```
Smart Retention (opt-in, off by default):
  Keep all snapshots from the last 4 weeks
  After 4 weeks: keep 1 snapshot per month
  After 12 months: keep 1 snapshot per year
  
  This is GFS rotation — but:
  • Off by default (keep everything is the default)
  • User must explicitly enable it
  • Clear explanation of what it means
  • Can be disabled at any time
```

**Why opt-in, not default:** Patient ransomware that encrypts files over 3 months would survive a 4-week window. If GFS is the default, users lose their clean monthly snapshots after a year — exactly when they might need them most. Better to accumulate and let users clean up consciously.
3. **Set a retention preference** — "Auto-prune snapshots older than X months" (opt-in, never forced)

This is a settings page, not a core flow. Most users will never touch it.

### What About Periodic Re-Baselines?

Over time, restoring from a very old baseline + hundreds of incrementals becomes slow. We should periodically create a new full baseline and allow users to prune incrementals before it. But this is a **performance optimization**, not a storage constraint.

```
Suggested: New full baseline every 6-12 months.
Old baseline + its incrementals can be pruned after the new baseline is verified.
This is automatic and transparent to the user.
```

---

## Ransomware Detection

Retention solves half the problem (having clean copies to restore from). Detection solves the other half (knowing WHEN corruption started so you restore the right snapshot).

### The Problem With Retention Alone

Even with infinite retention, if ransomware encrypts files gradually over 6 months and you don't notice, you won't know which snapshot is the last clean one. You'd have to manually inspect snapshots to find it.

**Detection tells you: "Something went wrong around March 15. Restore from March 14."**

### Metadata-Based Detection (Free — All Users)

These use ONLY the metadata from the delta API. Zero file downloads. Zero additional cost. No reason to restrict to paid tiers.

**1. Mass Change Alert**
```
Normal: ~50 files change/week
Detected: 2,400 files changed this cycle

→ PAUSE backup. Alert: "Unusual activity detected. We've paused
  your backup to protect your existing snapshots."
```

**2. Extension Rename Alert**
```
Detected: 340 files gained .locked / .encrypted / .crypted extensions

→ PAUSE. Alert: "Many files had their extensions changed to
  suspicious patterns consistent with ransomware."
```

**3. Size Pattern Alert**
```
Detected: 280 files changed to sizes that are exact multiples of 16 bytes
(consistent with AES block cipher padding)

→ Flag as suspicious. Alert user.
```

**4. Mass Deletion Alert**
```
Detected: 1,200 files deleted (baseline: ~5/week)

→ Alert. (Don't pause — deletions are already captured in snapshots.)
```

### Content-Based Detection (Pro Tier)

Requires downloading file samples — small bandwidth cost justifies Pro tier.

**5. Entropy Analysis**
- Sample ~50 changed files per cycle
- Normal document: entropy ~4.5-6.0 bits/byte
- Encrypted file: entropy ~7.9-8.0 bits/byte
- Catches slow-burn ransomware that metadata detection misses

**6. File Header (Magic Number) Verification**
- Check that .docx files start with `PK`, .jpg starts with `FF D8 FF`, etc.
- Encrypted files have random headers despite keeping original extensions

---

## Tier Differentiation (Revised)

| | Free | Pro ($4.99/mo) |
|---|---|---|
| **Retention** | Everything, indefinitely | Same |
| **Schedule** | Weekly snapshots | Daily or hourly |
| **Detection** | Metadata anomaly detection (all 4 types) | + Content analysis (entropy, magic numbers) |
| **Backup scope** | Same-account snapshots | + Cross-account (true ransomware isolation) |
| **Cleanup tools** | Manual (delete folders) | + "Manage Storage" UI with smart pruning |
| **Alerts** | Email | + Push notifications |

**The free tier is genuinely protective.** Unlimited retention + metadata detection. Pro adds speed, depth, and cross-account isolation.

---

## Summary

| Decision | Rationale |
|----------|-----------|
| **Keep all snapshots by default** | Storage is on user's cloud bill, not ours. No reason to auto-delete. |
| **User controls cleanup** | It's their storage. Provide tools, don't impose limits. |
| **No artificial tier-based retention** | Mission-aligned. "Pay more for more days" is an upsell, not protection. |
| **Metadata detection for all tiers** | Costs us nothing to compute. Catches most ransomware. |
| **Content detection for Pro** | Requires downloads (bandwidth cost). Catches slow-burn ransomware. |
| **Periodic re-baselines** | Performance optimization, not storage constraint. Automatic. |
