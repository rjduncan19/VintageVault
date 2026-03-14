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

### Default Behavior

```
Every snapshot is kept indefinitely.
We never auto-delete anything.
The user's backup grows with their incremental changes.
```

With immutable incremental snapshots (ADR-003) and a 2% weekly change rate on 50 GB of source data:

```
After 1 year:   50 GB baseline + ~52 GB incrementals = ~102 GB (2.0x)
After 5 years:  50 GB baseline + ~260 GB incrementals = ~310 GB (6.2x)
```

For most Microsoft 365 users with 1 TB of storage, this is fine for years. For free-tier users (5 GB), they'll hit limits sooner — but that's between them and Microsoft, not us.

### User-Controlled Cleanup

Users who want to reclaim space can:

1. **Delete old snapshot folders directly** — it's their OneDrive, they own it
2. **Use our "Manage Storage" UI** — shows space used by snapshots, lets them prune by date range
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
