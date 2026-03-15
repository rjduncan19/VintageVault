# Backup Fundamentals — Standing on the Shoulders of Decades

**Date:** 2026-03-13
**Purpose:** Capture the essential concepts, patterns, algorithms, and user metaphors from the backup industry so VintageVault doesn't reinvent the wheel. This document is a reference for the team — not a textbook, but a curated summary of what matters for our product.

---

## Why This Document Exists

Backup is a solved problem in many ways. Enterprises have been doing it since the 1960s (tape reels). Consumers have had Time Machine since 2007. The fundamental patterns — full, incremental, differential, retention, verification — are well-established and battle-tested.

VintageVault's innovation is not in *how* to back up. It's in *where* (cloud-to-cloud), *for whom* (non-technical consumers), and *at what cost* (near-zero). But the backup engine itself should use proven patterns. This document captures them.

---

## Part 1: Backup Types

### Full Backup

**What it does:** Copies every selected file in its entirety. The result is a complete, self-contained snapshot of the data at a point in time.

**Metaphor for users:** "A complete photocopy of every page in a filing cabinet."

**Properties:**
- Slowest to create, largest storage footprint
- Fastest and simplest to restore — just one backup set needed
- The baseline that all other backup types depend on

**When to use:** As the initial backup (every user's first run), and periodically to create a fresh baseline (e.g., monthly).

**VintageVault relevance:** Every user's first backup is a full backup. The question is whether subsequent backups are also full (simple but wasteful) or incremental (efficient but more complex).

---

### Incremental Backup

**What it does:** Copies only the files that have changed since the *last backup of any type* (full or incremental).

**Metaphor for users:** "Only photocopying the pages that changed since yesterday."

**Properties:**
- Fastest to create, smallest storage footprint per cycle
- Restore requires the last full backup PLUS every incremental since then, replayed in order
- If any link in the chain is corrupted, all subsequent incrementals may be unusable

```
Day 0: Full backup (100 GB)
Day 1: Incremental (2 GB — changes since Day 0)
Day 2: Incremental (1.5 GB — changes since Day 1)
Day 3: Incremental (3 GB — changes since Day 2)
...
To restore Day 3: Need Full + Inc1 + Inc2 + Inc3
```

**VintageVault relevance:** This is the natural model for cloud-to-cloud backup. Both Microsoft Graph and Google Drive APIs provide delta/change feeds that enumerate exactly what changed since the last sync token. The API itself gives us incremental semantics for free.

---

### Differential Backup

**What it does:** Copies all files that have changed since the *last full backup* (not the last incremental — the last full).

**Metaphor for users:** "Photocopying every page that changed since the last complete copy, even if we already copied some of them yesterday."

**Properties:**
- Larger than incremental (cumulative changes since last full), but smaller than a full
- Restore requires only the last full backup PLUS the latest differential — much simpler than chaining incrementals
- Gets progressively larger as time passes since the last full

```
Day 0: Full backup (100 GB)
Day 1: Differential (2 GB — changes since Day 0)
Day 2: Differential (3.5 GB — changes since Day 0, includes Day 1 changes again)
Day 3: Differential (6.5 GB — changes since Day 0, includes Days 1+2 again)
...
To restore Day 3: Need Full + Diff3 only
```

**VintageVault relevance:** Less natural fit for cloud-to-cloud. The cloud APIs give us "changes since last token" (incremental), not "changes since last full." We'd have to synthesize differential behavior by resetting the delta token periodically. Probably not worth the complexity for v1.

---

### Synthetic Full Backup

**What it does:** Constructs a new "full backup" by merging the previous full with all subsequent incrementals — without re-reading the source data. The result is a complete point-in-time snapshot, built from existing backup data.

**Metaphor for users:** "Assembling a complete copy from the pieces we already have, without bothering the original."

**Properties:**
- Avoids the bandwidth cost of a fresh full backup
- The destination must have enough storage and compute to merge
- Common in enterprise backup products (Veeam, Commvault)

**VintageVault relevance:** Very relevant for our use case. If a user has been doing incremental backups for months, their "current state" on the destination is the sum of the initial full plus all deltas. If we maintain a metadata index of what's on the destination, we effectively have a synthetic full at all times. The destination *is* the synthetic full — we just need to keep our metadata accurate.

---

### Continuous Data Protection (CDP)

**What it does:** Captures every change as it happens, in real time. Every write is logged and backed up immediately.

**Metaphor for users:** "A security camera for your files — recording every change the moment it happens."

**Properties:**
- Enables recovery to any point in time, not just scheduled snapshots
- Requires constant monitoring and bandwidth
- Extremely resource-intensive

**VintageVault relevance:** Not our model. CDP requires either a file system driver (which is invasive and platform-specific) or real-time API subscriptions. Google Drive supports push notifications via webhooks (changes.watch), and Microsoft Graph supports subscriptions for change notifications. In theory, we could approach near-CDP, but it's complexity we don't need in v1. Our users don't need minute-by-minute protection — they need to survive ransomware and accidental deletion, which daily or weekly backups handle.

---

## Part 2: Key Concepts

### The 3-2-1 Rule

The gold standard of backup strategy, established decades ago and still cited universally:

- **3** copies of your data (the original + 2 backups)
- **2** different storage media (or providers/locations)
- **1** copy offsite (or on a different provider)

**VintageVault directly implements 3-2-1** for its users:
- Copy 1: The original files in the source cloud drive
- Copy 2: The backup on the destination cloud drive (different provider = different "media" and "offsite")
- The user's local machine may have a synced copy, creating copy 3 naturally

This is a powerful marketing message: "VintageVault gives you a 3-2-1 backup strategy without you having to think about it."

---

### RPO and RTO

Two metrics that the backup industry uses to define requirements:

**RPO — Recovery Point Objective:** How much data can you afford to lose? Measured in time.
- RPO of 24 hours = "I can tolerate losing up to 1 day of changes"
- RPO of 1 hour = "I can tolerate losing up to 1 hour of changes"

**RTO — Recovery Time Objective:** How quickly must you be operational again after a disaster? Measured in time.
- RTO of 4 hours = "I need my files back within 4 hours"
- RTO of 1 day = "I can wait up to a day to get my files back"

**VintageVault's defaults should target:**
- **RPO: 24 hours (free) / 1 hour (pro).** Daily backups for free tier, hourly for paid.
- **RTO: "As fast as the user's internet allows."** Since restoration means downloading from the destination cloud, the RTO is limited by internet speed, not by our infrastructure. For a 50 GB restore on a 25 Mbps connection, RTO is roughly 4.5 hours.

**User-facing language:** Don't say RPO/RTO. Say: "If something goes wrong, you might lose up to a day of changes (free) or an hour (Pro). Getting your files back depends on your internet speed."

---

### Retention Policies

Retention answers: "How far back can I recover?"

**Common patterns:**

| Policy | Description | Storage Cost | Use Case |
|--------|-------------|-------------|----------|
| **Keep N versions** | Store the last N versions of each file | Moderate, predictable | Simple, easy to understand |
| **Keep N days** | Keep all versions from the last N days, then purge | Variable | Time-based recovery window |
| **GFS (Grandfather-Father-Son)** | Keep daily for N days, weekly for N weeks, monthly for N months | Moderate | Enterprise standard for layered retention |
| **Keep everything** | Never delete any backup version | Unlimited growth | Only viable if storage is truly cheap |

**VintageVault's model:**

> **⚠️ Updated 2026-03-15:** The original "keep N days" recommendation below has been superseded. Research shows consumers are overwhelmingly accumulators — most people never delete files intentionally. The "keep N days" model was designed for enterprise environments with high churn. For our consumer audience, it creates a false sense of security against patient ransomware.
>
> **Current POR (ADR-004):** Keep all snapshots indefinitely by default. Storage is on the user's cloud quota, not ours. User can opt into cleanup via a "Manage Storage" UI. No artificial tier-based retention limits.
>
> GFS rotation (weekly/monthly/yearly promotion) is available as an opt-in space-saving strategy, not a default. See [ADR-004](../architecture/004-retention-and-detection.md).

~~For v1, "keep N days" is the simplest and most intuitive for consumers:~~
~~- Free: 30-day retention~~
~~- Pro: 90-day or 365-day retention~~

~~GFS is more storage-efficient but harder to explain to non-technical users. Consider it for v2 if storage costs become a concern.~~

**Critical retention rule for ransomware protection:** When the backup engine detects that a source file has been deleted or modified, it must NOT immediately delete or overwrite the backed-up version. Instead, it should mark the destination copy for retention-policy-governed expiry. This is the fundamental protection against ransomware: the encrypted/deleted source files don't immediately propagate to destroy the backup.

---

### Deduplication

**What it is:** Identifying and eliminating duplicate copies of data to reduce storage consumption. Can operate at file level (skip files that haven't changed) or block/chunk level (skip identical segments within files).

**Types:**
- **File-level dedup:** If the file hasn't changed (same hash), don't copy it again. Simple, effective for most consumer workloads.
- **Block-level dedup:** Split files into chunks (e.g., 4 KB - 64 KB), hash each chunk, only store unique chunks. More complex, saves more space, essential for large files that change partially (databases, VM images).

**VintageVault relevance:** File-level deduplication is sufficient and comes almost for free with our incremental model — the delta APIs tell us exactly which files changed, so we never re-transfer unchanged files. Block-level dedup is overkill for consumer cloud files (documents, photos) and would require us to manage chunk storage on the destination, which adds enormous complexity. Skip it.

---

### Checksums and Verification

**What it is:** Using cryptographic hashes (MD5, SHA-1, SHA-256) to verify that the data written to the backup matches the source data. Essential for detecting silent corruption, truncated transfers, and bit rot.

**Industry practice:**
1. Compute checksum of source file before/during transfer
2. After writing to destination, retrieve the destination's checksum
3. Compare. If mismatch, flag and retry.
4. Periodically re-verify: read back backed-up files and confirm checksums still match (catches destination-side corruption).

**Cloud API support:**
- **Google Drive:** Returns MD5 checksum for uploaded binary files (not for Google Docs exports). Available via `files.get` with `fields=md5Checksum`.
- **OneDrive/Microsoft Graph:** Returns SHA-1 hash and QuickXorHash via `file.hashes` property.

**VintageVault must:**
- Store source checksum at time of backup in local metadata DB
- Compare with destination-reported checksum after upload
- Run periodic spot-check verification on a sample of backed-up files

---

### Backup Windows and Scheduling

**What it is:** The time period allocated for backup operations. In enterprise, this is typically overnight or during low-usage periods. The "backup window" must be long enough to complete the backup, or the job must be able to pause and resume.

**Traditional assumption:** There's a predictable window (e.g., 10pm-6am) when the system is idle and the network is available.

**VintageVault's reality:** Consumer laptops have no predictable backup window. The machine may be awake for 2 hours, asleep for 10, awake for 30 minutes, asleep again. The backup engine must be:
- **Opportunistic:** Start whenever conditions are favorable (awake, on power, on Wi-Fi)
- **Interruptible:** Gracefully pause when conditions change (lid closes, battery, metered network)
- **Resumable:** Pick up exactly where it left off on next wake

This is a departure from traditional scheduled backup. It's closer to how mobile email sync works: "sync whenever you can, stop when you can't, resume when you can again."

---

## Part 3: User Metaphors That Work

The backup industry has decades of user education. Here are the metaphors that have proven effective for non-technical users:

### The Insurance Metaphor
> "Backup is like insurance. You pay a small cost now and hope you never need it. But when disaster strikes, it's the only thing that matters."

**Why it works for VintageVault:** Our target users understand insurance. They don't understand "incremental cloud-to-cloud backup with configurable retention." The insurance metaphor sets the right expectations: low ongoing cost, invisible when working, invaluable when needed.

### The Time Machine Metaphor (Apple)
> "Go back in time and retrieve any file from any point in the past."

**Why it works:** Apple's Time Machine is arguably the most successful consumer backup product ever. Its genius is the metaphor — a visual timeline that users can scrub through to find their files. VintageVault's restore experience should aspire to this simplicity: browse your backup like a file explorer, optionally filter by date range.

### The Safety Deposit Box Metaphor
> "Your files are in a vault at a completely separate location. Even if your house burns down (your main cloud is compromised), the vault is untouched."

**Why it works for VintageVault:** The cross-provider aspect maps perfectly to a geographically separate safety deposit box. "Your OneDrive is your house. Your Google Drive backup is a safety deposit box at a different bank."

### The Photo Album Metaphor
> "Imagine you have one photo album at home. If there's a fire, it's gone. Now imagine you made a copy and left it at your parents' house."

**Why it works:** For the family/photos segment (our primary audience), this is instantly relatable. The "copy at your parents' house" is the cross-provider backup.

### Metaphors to AVOID

| Metaphor | Why It Fails for Our Audience |
|----------|-------------------------------|
| "Mirror" / "Replica" | Implies real-time sync, which we don't do. Also implies bidirectional, which is dangerous. |
| "Archive" | Implies cold storage, long-term, and "put away." Our backup is active and current. |
| "Snapshot" | Technical term. Users don't know what this means. |
| "Disaster recovery" | Sounds enterprise and scary. Families don't think about "disasters." |
| "Redundancy" | Technical jargon. Nobody wants "redundancy" — they want "safety." |

---

## Part 4: Common Backup Failure Modes (Lessons from History)

These are well-known failure patterns that have bitten backup products for decades. We should learn from them:

### 1. "The Backup That Was Never Tested"
**Pattern:** User sets up backup, forgets about it. Months later, disaster strikes. Backup is corrupted, incomplete, or can't be restored.
**Lesson:** Verification is not optional. VintageVault must periodically verify backup integrity AND surface the results to the user. A green checkmark should mean "verified working," not just "last job completed."

### 2. "The Backup That Backed Up the Corruption"
**Pattern:** Ransomware encrypts files. Backup agent runs on schedule. Encrypted files overwrite the clean backup. The backup is now as useless as the source.
**Lesson:** This is VintageVault's core threat model. Retention policies (don't immediately overwrite/delete old versions) and anomaly detection (pause if mass changes detected) are the defenses.

### 3. "The Backup That Ran Out of Space"
**Pattern:** Destination fills up. Backup starts failing silently. User doesn't notice until they need to restore.
**Lesson:** The agent must check destination capacity before each backup. Alert loudly if space is running low. Never silently fail.

### 4. "The Backup That Was Too Slow to Finish"
**Pattern:** Backup window isn't long enough. Job runs out of time, aborts, and starts over next cycle. Backup never completes.
**Lesson:** Our backup engine must support true resume. Each file transfer should be checkpointed independently. An interrupted backup should continue from the last completed file, not restart.

### 5. "The Backup Nobody Knew How to Restore"
**Pattern:** IT team set up enterprise backup. When disaster hits, nobody knows how to use the restore tools. Documentation is outdated. Recovery takes days instead of hours.
**Lesson:** Apple understood this with Time Machine: the restore UX is as important as the backup UX. VintageVault must invest equally in recovery. A guided, step-by-step restore wizard, not a command-line tool.

### 6. "The Backup With the Wrong Scope"
**Pattern:** User thought "everything" was backed up. But the backup only covered one folder, or skipped cloud-native files (Google Docs), or missed a new subfolder created after setup.
**Lesson:** Default to "back up everything." When a user adds a new folder to their source drive, the backup should automatically include it without reconfiguration. New content should be opted-in by default, not opted-out.

---

## Part 5: Algorithms and Implementation Patterns

### Change Detection

How does the backup engine know what changed?

| Method | How It Works | Pros | Cons |
|--------|-------------|------|------|
| **Timestamp comparison** | Compare file modified dates between source and last backup | Fast, simple | Timestamps can be unreliable (copied files, timezone issues) |
| **Checksum comparison** | Hash each source file, compare with stored hash from last backup | Most accurate | Expensive — requires reading every file to compute hash |
| **Cloud delta API** | Use provider's change feed (Microsoft Graph delta, Google Drive changes.list) | Native, efficient, maintained by provider | Provider-dependent; token can expire requiring full re-scan |
| **File system watchers** | OS-level notifications when files change (inotify, FSEvents, ReadDirectoryChangesW) | Real-time, no polling | Only works for local files; irrelevant for cloud API source |

**VintageVault's approach:** Cloud delta APIs are the clear winner. Both Microsoft Graph and Google Drive provide robust change tracking:

- **Microsoft Graph:** `GET /drives/{drive-id}/root/delta` returns all changes since a delta token. Returns `@odata.deltaLink` for next call. Supports pagination with `@odata.nextLink`. Token can expire (returns 410 Gone), requiring full re-enumeration.

- **Google Drive:** `changes.list` with a `pageToken` returns changes since that token. Supports `startPageToken` for initial state. Also supports push notifications via `changes.watch` (webhooks) for near-real-time change awareness.

Both APIs handle the hard parts: detecting new files, modified files, renamed files, moved files, and deleted files. We should use them rather than implementing our own change detection.

**Fallback for expired tokens:** If the delta/change token expires (Microsoft Graph returns 410; Google returns an invalid token error), the agent must perform a full re-enumeration of the source drive and reconcile against the local metadata database. This is equivalent to a "full backup" comparison pass but shouldn't require re-transferring files that are already on the destination with matching checksums.

---

### Transfer Optimization

**Chunked / resumable uploads:** Both Google Drive and OneDrive support resumable uploads for files over a threshold (Google: >5 MB recommended; OneDrive: >4 MB). The pattern:
1. Initiate an upload session (get a resumable upload URI)
2. Upload the file in chunks (e.g., 5-10 MB each)
3. If interrupted, resume from the last successful chunk
4. Session URI typically expires after 7 days (Google) or a few days (OneDrive)

**Bandwidth throttling:** Limit transfer rate to a configurable fraction of available bandwidth. Implementation: introduce a delay between chunks, or limit the number of concurrent transfers. Expose to users as "Backup speed: Slow / Normal / Fast / Maximum."

**Concurrency:** Transfer multiple small files in parallel (e.g., 4-8 concurrent uploads) to maximize throughput. Serialize large file uploads to avoid memory pressure.

**Priority ordering:** Process the most recently modified files first. Rationale: if the backup is interrupted, the most recent (and presumably most valuable) files are protected first.

---

### Conflict and Edge Case Handling

| Scenario | What Happens | What the Engine Should Do |
|----------|-------------|--------------------------|
| File modified during transfer | Source file changes while being uploaded to destination | Finish current transfer, then re-transfer on next cycle (file will be in delta again) |
| File deleted after enumeration | Delta lists a file, but it's gone when we try to download | Log as "deleted during backup," skip. Delta will reflect the deletion on next cycle. |
| File too large for destination quota | Destination drive runs out of space mid-upload | Abort upload, alert user: "Destination drive is full. Free up space or upgrade your plan." |
| Rate limit hit | Cloud API returns 429 Too Many Requests | Exponential backoff with jitter. Respect `Retry-After` header. Log the throttling. |
| Token expired | Delta/change token is no longer valid | Perform full re-enumeration. Log event. Notify user if this takes a long time. |
| Permission revoked | OAuth token is invalid or revoked | Alert user immediately: "VintageVault can no longer access your [OneDrive/Google Drive]. Please reconnect." |
| Network interrupted | Wi-Fi drops, VPN disconnects, etc. | Pause gracefully. Retry with backoff. Resume from last checkpoint on next network availability. |

---

## Part 6: What VintageVault Should Borrow vs. Invent

### Borrow (proven, well-understood)
- **Incremental backup** via cloud delta APIs — the pattern is decades old, the APIs are mature
- **The 3-2-1 rule** as a marketing and architecture principle
- **Retention policies** — time-based for v1 (N days), GFS for v2 if needed
- **Checksum verification** — post-upload comparison, periodic spot checks
- **Resumable transfers** — chunked uploads with checkpoint
- **The insurance/safety deposit box metaphors** — proven to resonate with non-technical users
- **Alert-on-failure, silent-on-success** — the enterprise backup standard that Apple perfected for consumers

### Invent (new for our context)
- **Cross-provider backup as a first-class concept** — traditional backup doesn't cross provider boundaries because it predates multi-cloud consumer usage
- **Condition-based scheduling** — traditional backup has fixed windows; we need power-aware, network-aware, opportunistic scheduling because our "server" is a laptop
- **Anomaly detection for ransomware** — traditional backup doesn't watch for mass-encryption patterns; it just runs on schedule. We should pause and protect if the source looks compromised
- **Zero-infrastructure user experience** — traditional backup requires the user to own the destination (a NAS, an external drive, tape). VintageVault's destination is the user's *other* cloud account, which they already have
- **Privacy-first architecture** — traditional cloud backup (Backblaze, Carbonite, Acronis) stores your data on their servers. Our "data never touches our servers" model is genuinely new for this product category
