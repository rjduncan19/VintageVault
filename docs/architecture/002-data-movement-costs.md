# ADR-002: Data Movement Costs — Why Cloud-to-Cloud Transfer Requires an Intermediary

**Status:** Accepted
**Date:** 2026-03-13
**Decision Makers:** VintageVault Core Team

## The Question

> Is it really cheaper to download from cloud to device, then upload to another cloud, than to transfer directly from cloud to cloud? That seems like double the work.

This is the right question. The answer is: **direct cloud-to-cloud transfer across providers doesn't exist.** The choice isn't between "direct transfer" and "go through a device" — it's between "go through *your* server" or "go through the *user's* device." Both involve the same download-then-upload. The only question is who pays for the intermediary.

---

## Why Direct Cloud-to-Cloud Transfer Doesn't Exist

### The Fundamental Problem

Google Drive and OneDrive are separate, competing platforms. There is no API, protocol, or mechanism where you can tell Google: "Please download this file from OneDrive and store it." Or tell Microsoft: "Please send this file to Google Drive."

Each cloud provider exposes APIs for:
- **Upload** — send a file *to* their storage
- **Download** — get a file *from* their storage
- **Copy** — duplicate a file *within* their storage

No cloud provider exposes:
- **"Fetch from external URL"** — pull a file from another service
- **"Send to external URL"** — push a file to another service

This isn't a technical limitation — it's a business one. Cloud providers want to be your *only* provider. Building interoperability with competitors doesn't serve their interests.

### What About Google Transfer Service / Azure Data Factory?

These enterprise data migration tools *do* exist, but they:
- Operate within the provider's own infrastructure (e.g., moving data between GCS buckets, or from AWS S3 to Azure Blob Storage — not between consumer Google Drive and OneDrive)
- Are designed for enterprise data lake migrations, not consumer file backup
- Don't support consumer Google Drive / OneDrive as endpoints (they work with storage *services*, not productivity *apps*)
- Are prohibitively expensive for consumer use cases

### What About rclone's Server-Side Copy?

rclone, the popular open-source tool, supports "server-side copy" — but only *within* the same provider or between providers that share a backend (e.g., S3-compatible services). For Google Drive → OneDrive, rclone does exactly what VintageVault does: downloads from source, uploads to destination, with the user's machine as the intermediary.

---

## The Real Comparison: Your Server vs. User's Device

Since an intermediary is required, the architectural question is: **whose machine sits in the middle?**

```
OPTION 1: Your server as intermediary (Web SaaS model)

  OneDrive ──download──► VintageVault Server ──upload──► Google Drive
                              │
                    You pay for:
                    • Server compute time
                    • Network egress from OneDrive to your server
                    • Network egress from your server to Google Drive
                    • Server storage (temporary, during transfer)
                    • Server bandwidth allocation


OPTION 2: User's device as intermediary (Desktop agent model)

  OneDrive ──download──► User's PC ──upload──► Google Drive
                              │
                    You pay for: nothing
                    User pays for: their normal internet bill
                    (which they're already paying regardless)
```

### The Data Volume Is Identical

In both cases, the same bytes travel the same distance:
- 100 GB downloaded from OneDrive
- 100 GB uploaded to Google Drive
- **200 GB total data movement**

The difference is purely economic: who owns the pipe.

### Cost Breakdown

| Cost Factor | Your Server (SaaS) | User's Device (Agent) |
|-------------|--------------------|-----------------------|
| **Cloud egress (downloading from source)** | You pay cloud provider egress rates | Free — user's ISP handles it |
| **Upload to destination** | You pay for your server's outbound bandwidth | Free — user's ISP handles it |
| **Compute** | You pay for server CPU/RAM during transfer | User's PC does the work |
| **Temporary storage** | You may need disk during chunked transfers | User's PC disk (or stream directly) |
| **Per-user scaling** | Cost scales linearly with users × data volume | Cost is near-zero regardless of user count |

### The Numbers at Scale

Assume 1,000 users, average 100 GB each, monthly full backup (simplified):

**SaaS model (your server):**
- Egress from source cloud: ~$0.08-0.12/GB (cloud provider egress pricing varies)
- Ingress to your server: usually free
- Egress from your server to destination cloud: ~$0.08-0.12/GB
- Total: **100,000 GB × ~$0.08-0.12/GB × 2 directions ≈ $16,000-24,000/month**
- Plus: server compute, storage, monitoring, scaling infrastructure

**Desktop agent model (user's device):**
- Your cost: **~$20-50/month** (API server for config + status dashboard)
- User's cost: slightly higher internet utilization on their existing plan (most users won't notice)

The **800x cost difference** is why the hybrid architecture (ADR-001, Proposal D) is the right choice.

---

## Same-Provider Backup: OneDrive → OneDrive or Google Drive → Google Drive

_Added: 2026-03-14_

### The Question

> Does cross-provider backup need to be a P0 requirement? Most users have multiple accounts on the *same* provider (personal + work/school). Could VintageVault's MVP back up within the same ecosystem first?

This is worth exploring. Many users have two Google accounts or two Microsoft accounts. Same-provider backup could be a simpler MVP while still delivering real protection against accidental deletion, ransomware, and account compromise.

### API Research: Can Clouds Copy Between Their Own Accounts?

#### OneDrive → OneDrive (different accounts): ❌ No server-side cross-account copy

The Microsoft Graph `driveItem: copy` API copies files within the same drive or tenant context. It does **not** support direct server-side copy between different users' OneDrive accounts — even within the same Microsoft tenant.

**What you must do instead:** Download from source account, upload to destination account — the exact same flow as cross-provider backup.

```
OneDrive (Account A) ──download──► User's PC ──upload──► OneDrive (Account B)
                                      │
                        Same data flow as cross-provider.
                        No shortcut exists.
```

**Implication:** For OneDrive, same-provider backup has zero technical advantage over cross-provider. The data still flows through the user's device.

#### Google Drive → Google Drive (different accounts): ⚠️ Partial shortcut exists

The Google Drive `files.copy` API only works within the same user account. However, a **share-then-copy** workaround exists:

1. Source account **shares** the file with the destination account
2. Destination account calls `files.copy` on the shared file
3. Google creates a copy owned by the destination account — **server-side, no download/upload**

```
Google Drive (Account A) ──share──► Google Drive (Account B) ──copy──► Owned copy
                                         │
                          Server-side copy — data never
                          leaves Google's infrastructure.
                          No bandwidth cost to user.
```

**Caveats:**
- Requires managing sharing permissions at scale (share, copy, then unshare)
- Sharing creates email notifications (noisy unless suppressed via API parameter `sendNotificationEmail: false`)
- Google-native files (Docs, Sheets, Slides) copy natively; binary files also work
- API quota limits still apply (~20,000 queries/100 seconds for Workspace, less for consumer)
- Folder structure is not preserved by `files.copy` — must be recreated manually
- File metadata (modified dates, descriptions) may not transfer perfectly
- Google could change this behavior — it's a side effect of sharing, not a designed feature

**Implication:** For Google Drive, same-provider backup could be significantly more efficient — no download/upload bandwidth, faster transfers, lower user impact.

### Same-Provider vs. Cross-Provider: Trade-off Matrix

| Factor | Same-Provider | Cross-Provider |
|--------|--------------|----------------|
| **Accidental deletion protection** | ✅ Full | ✅ Full |
| **Ransomware protection** | ✅ Full (different account) | ✅ Full |
| **Account compromise protection** | ⚠️ Partial — if attacker compromises one Google/MS account, they may be able to reach the other (same password, same recovery email, same org) | ✅ Strong — completely separate provider/credentials |
| **Provider outage protection** | ❌ None — both accounts are down | ✅ Full |
| **Provider ban/suspension** | ❌ Both accounts may be affected | ✅ Protected |
| **Implementation complexity** | ✅ Simpler — one OAuth provider, one API surface | ⚠️ More complex — two different APIs, two OAuth flows |
| **Google: bandwidth efficiency** | ✅ Share-then-copy avoids download/upload | ❌ Must download+upload |
| **OneDrive: bandwidth efficiency** | ❌ Same as cross-provider (download+upload) | ❌ Same |
| **User's existing accounts** | ✅ Many users have 2+ accounts on same provider | ✅ Most users have Google + Microsoft |
| **MVP simplicity** | ✅ One provider to integrate | ⚠️ Two providers to integrate |

### Revised Phase 1 Recommendation

Same-provider backup is a viable P0 — especially for Google Drive where the share-then-copy shortcut avoids all bandwidth costs. The MVP could be:

```
PHASE 1a (Simplest MVP):
  Google Drive (Account A) → Google Drive (Account B)
  Using share-then-copy — zero bandwidth, fast transfers
  
  Protects against: accidental deletion, ransomware, account compromise (partial)
  Does NOT protect against: provider outage, provider ban

PHASE 1b (Expanded MVP):
  Add OneDrive (Account A) → OneDrive (Account B)
  Using download+upload — same architecture as cross-provider
  
PHASE 2:
  Add cross-provider backup (OneDrive ↔ Google Drive)
  Stronger protection model, but more implementation work
```

### Why This Changes the Narrative

The original pitch centered on "your cloud's backup plan" with the "safety deposit box at a different bank" metaphor. Same-provider backup weakens this metaphor — it's more like "a second safety deposit box at the *same* bank."

**However**, the protection against the three most common threats (accidental deletion, ransomware, account compromise) is still strong. Provider outage and provider ban are real but rare threats. For an MVP, protecting against the common threats may be sufficient.

**Messaging adjustment:**
- **Cross-provider:** "Your safety net is at a completely different bank"
- **Same-provider:** "Your safety net is in a completely separate vault" (still resonates for most users)

### Impact on Cost Analysis

| Scenario | Data Movement | User's Bandwidth | Our Cost |
|----------|--------------|------------------|----------|
| **Google → Google (share-then-copy)** | Zero — server-side | Zero impact | ~$0 |
| **OneDrive → OneDrive** | Download + Upload | Same as cross-provider | ~$0 |
| **OneDrive → Google Drive** | Download + Upload | Same | ~$0 |
| **SaaS relay (any direction)** | Download + Upload via our server | Zero | $16-24k/1k users/mo |

The Google share-then-copy approach is uniquely efficient and could be a strong selling point: "Zero impact on your internet speed."

---

## "But Isn't the User's Internet Slow?"

Yes — and this is a genuine trade-off. The SaaS model would use fast data center connections (typically 1-10 Gbps between cloud providers). The desktop agent uses whatever the user's home internet provides.

| Connection | 100 GB Transfer Time |
|------------|---------------------|
| Data center (1 Gbps) | ~13 minutes |
| Home fiber (100 Mbps up) | ~2.2 hours |
| Home broadband (10 Mbps up) | ~22 hours |
| Home broadband (5 Mbps up) | ~44 hours |

This is a real user experience concern. But the economics make it non-negotiable for a freemium consumer product:

1. **We can't afford $16-24 per user per backup cycle** at a $3-5/month price point. The math doesn't work.
2. **Initial backup is a one-time cost.** After the first full backup, incremental (delta) backups are much smaller — typically 1-5% of total data per cycle, depending on how much the user changes files.
3. **The user's internet is already paid for.** We're using excess capacity, not creating a new cost for them.
4. **Speed mitigations exist** — bandwidth throttling, opportunistic scheduling, priority-based backup (recent files first). See the gap analysis for details.

---

## Cloud Provider Egress Pricing (Reference)

For context, here are the egress costs that the SaaS model would incur:

| Provider | Egress Rate | Notes |
|----------|------------|-------|
| **Google Cloud** | $0.08-0.12/GB | First 1 TB: $0.12, next 9 TB: $0.08 |
| **Azure** | $0.087/GB | For first 10 TB/month |
| **AWS** | $0.09/GB | For first 10 TB/month |
| **Google Drive API** | Free (no egress charge) | But download bandwidth limited by API quotas |
| **Microsoft Graph API** | Free (no egress charge) | But download bandwidth limited by throttling |

Note: The consumer Drive/Graph APIs don't charge egress per se, but if your server is downloading 100 TB/month of user data, you're paying for the server's network ingress and the compute to process it. Cloud hosting providers charge for bandwidth to/from your VMs.

---

## What About a Future "Cloud Relay" Option?

For users who need always-on backup (PC not always running), a server-side relay could be offered as a premium tier:

```
PREMIUM TIER: Cloud relay (opt-in, higher price)

  OneDrive ──► VintageVault Cloud Relay ──► Google Drive

  User pays: $10-15/month (covers our server cost for their data volume)
  Trade-off: Data touches our servers (weaker privacy story)
  Benefit: Works 24/7 without a desktop agent
```

This would be a Phase 3+ feature and would require:
- SOC 2 compliance (we're now a data processor)
- GDPR data processing agreements
- Encryption in transit and at rest on our relay servers
- Significant infrastructure investment

The desktop agent model remains the default and the best option for the vast majority of users.

---

## Summary

| Myth | Reality |
|------|---------|
| "Just transfer directly between clouds" | Not possible cross-provider. Within Google, a share-then-copy workaround exists. Within OneDrive, no shortcut. |
| "Going through a device is extra work" | Same data volume regardless — an intermediary is always required (except Google share-then-copy) |
| "The server would be faster" | True, but costs $16k+/month at 1k users vs. ~$30/month |
| "User's internet is too slow" | Initial backup is slow; incremental backups are small. Mitigations exist. Google same-provider has zero bandwidth impact. |
| "This is a weird hack" | This is how every cross-cloud tool works (rclone, MultCloud's agent mode, etc.) |
| "Cross-provider must be P0" | Same-provider backup covers the most common threats (deletion, ransomware, compromise) and is simpler to build. Cross-provider adds outage/ban protection but can be Phase 2. |

The desktop agent architecture isn't a cost-cutting compromise — it's the only model that makes a freemium consumer backup product economically viable, while simultaneously providing the best privacy and security story. And for the Google same-provider path, the share-then-copy approach eliminates bandwidth concerns entirely — a potential MVP fast-lane.
