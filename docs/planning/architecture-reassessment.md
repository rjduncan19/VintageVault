# Architecture Reassessment: Desktop Agent vs. Cloud Relay vs. Both

_Created: 2026-03-14 | Model: Claude Opus 4.6_

## The Core Tension

ADR-001 chose the desktop agent (Proposal D) primarily because server-side backup was assumed to cost **$16,000-24,000/month** for 1,000 users. But that number was based on **monthly full backups** — which nobody actually does. The backup fundamentals doc (written in the same planning session) recommends incremental backup after the initial full.

**This means ADR-002's cost comparison is misleading.** Let's redo it with realistic numbers.

---

## Corrected Cost Analysis

### The Original (Flawed) Calculation

```
ADR-002 assumed: 1,000 users × 100 GB each × monthly FULL backup
Egress: 100,000 GB × $0.08-0.12/GB × 2 directions = $16,000-24,000/month

This made server-side look impossibly expensive.
```

### The Corrected Calculation

**Step 1: Initial full backup (one-time cost)**

Only ONE direction of egress matters — downloading from source is ingress (free on Azure/GCP/AWS), uploading to destination is egress:

```
1,000 users × 100 GB × $0.087/GB egress = $8,700 one-time
Per user: ~$8.70 (amortized over first year = ~$0.73/user/month)
```

**Step 2: Ongoing incremental backups**

After the first full backup, only changed files are transferred. Industry standard: 1-5% of data changes per backup cycle.

```
Weekly incremental (conservative 5%):
  1,000 users × 5 GB × $0.087/GB = $435/month
  Per user: $0.44/month

Weekly incremental (realistic 2%):
  1,000 users × 2 GB × $0.087/GB = $174/month
  Per user: $0.17/month
```

**Step 3: Server compute (streaming relay — no storage)**

A server that streams data from source API → destination API without storing it:

```
Compute (2-4 vCPU VM):                    $100-200/month
Temporary memory/buffer:                   included
Database (user configs, job state):        $20-50/month
Monitoring, logging:                       $20-50/month
Total server infrastructure:               ~$150-300/month
```

**Step 4: Total cost for server-side relay**

```
                           Year 1          Year 2+
                           (with initial)  (incremental only)
Egress (initial full):     $8,700          $0
Egress (incremental):     $2,088-5,220     $2,088-5,220
Server infrastructure:    $1,800-3,600     $1,800-3,600
─────────────────────────────────────────────────────────
Total (1,000 users):      $12,588-17,520   $3,888-8,820
Per user/month:            $1.05-1.46       $0.32-0.74
```

### Google → Google Exception

For Google-to-Google backup using share-then-copy:

```
Egress:     $0 (server-side copy within Google)
Compute:    ~$150-300/month (API orchestration only)
Per user:   $0.015-0.03/month (essentially free)
```

### Revised Cost Comparison

| Model | Per-user/month (Year 2+) | Notes |
|-------|--------------------------|-------|
| **Desktop agent** | ~$0 | But: installation friction, PC must be on |
| **Server relay (OneDrive routes)** | $0.32-0.74 | Streaming, no data stored |
| **Server relay (Google → Google)** | ~$0.02 | Share-then-copy, zero bandwidth |
| **Old ADR-002 estimate** | ~~$16-24~~ | ❌ Based on monthly full backups |

**At $5/month subscription price, server relay has 85-94% gross margin.** The economics work.

---

## The Real Problem with Desktop Agent

The cost advantage of the desktop agent is real but small ($0.32-0.74/user/month savings). Meanwhile, the desktop agent has **massive friction problems** for our target audience:

### Friction Analysis

| Factor | Desktop Agent | Web-Only SaaS |
|--------|--------------|---------------|
| **Onboarding time** | 10-15 min (download, install, configure) | 2-3 min (sign up, OAuth, done) |
| **Installation barrier** | "Download this .exe" — scary for non-technical users | Zero — works in browser |
| **Reliability** | PC must be on, awake, and connected | Always on, 24/7 |
| **Platform support** | Windows-only initially | Any device with a browser |
| **Updates** | User must install updates | Automatic, transparent |
| **IT/parental friction** | May require admin permissions | No admin needed |
| **"Does it work?"** | User has no idea unless they check | We know, and can tell them |

### Who Actually Installs Desktop Software in 2026?

Our target audience is **families, freelancers, and non-technical users.** This is exactly the demographic that:
- Doesn't install desktop software anymore (everything is web/mobile)
- Doesn't understand "system tray" or "background service"
- Won't notice if the agent stops running
- May have managed devices (work laptops, school Chromebooks) that block installs

The people who WOULD install a desktop agent are developers and power users — the same people who'd use rclone for free.

**We're optimizing the hardest path for the wrong audience.**

### SaaS Adoption Data (2025-2026)

- 99% of organizations use at least one SaaS solution
- SaaS free-to-paid median conversion: **8%** (vs. our assumed 4% for desktop)
- Desktop install-to-completion drop-off is significant for consumer products
- Consumers overwhelmingly prefer "try before you buy" web experiences

---

## Three Strategic Options

### Option A: Stay with Desktop Agent (Current Plan)

```
User: Downloads .exe → installs → authorizes OAuth → backup runs when PC is on

Cost to us:     ~$0/user
Reliability:    Low (PC sleep, user closes app, uninstalls)
Friction:       High
Privacy story:  "Your data never touches our servers" ← strongest
Target market:  Power users, privacy enthusiasts
```

**Risk:** Low adoption among target audience. The product nobody installs.

### Option B: Pure Cloud Relay (SaaS)

```
User: Signs up on web → authorizes source + destination OAuth → done

Cost to us:     $0.02-0.74/user/month (depends on route)
Reliability:    High (runs 24/7 on our servers)
Friction:       Minimal
Privacy story:  "Data streams through but is never stored" ← moderate
Target market:  Families, freelancers, non-technical users
```

**Privacy details for streaming relay:**
- Data encrypted in transit (TLS)
- Streamed directly from source API → destination API
- Processed in memory only — never written to disk
- No persistent storage of user files on our servers
- For Google → Google: data never leaves Google's infrastructure at all

**Risk:** Must handle initial backup cost ($8.70/user one-time). Server infrastructure complexity.

### Option C: Cloud-First with Optional Desktop Agent ⭐ RECOMMENDED

```
DEFAULT: Web signup → OAuth → cloud relay handles backup
OPTIONAL: "Want more privacy? Download our desktop agent"

Google → Google:    Share-then-copy (zero cost, zero bandwidth)
OneDrive → *:       Cloud relay streaming ($0.32-0.74/user/month)
Privacy mode:       Desktop agent ($0/user, PC must be on)
```

**Why this is the best model:**

1. **Lowest friction for the default path** — web signup takes 2 minutes
2. **Always works** — no dependency on user's PC being on
3. **Economically viable** — 85-94% gross margin at $5/month
4. **Privacy option exists** — desktop agent for users who want it
5. **Google → Google is nearly free** — share-then-copy is a competitive advantage
6. **Phased build** — start with cloud relay (simpler), add agent later

---

## How Option C Changes the Product

### Phase 1 (MVP) — Cloud Relay

```
┌─────────────────────────────┐
│     VintageVault Web App    │
│     (ASP.NET Core + Blazor) │
│                             │
│  • User signup + OAuth      │
│  • Backup job config        │     Source Cloud API
│  • Streaming relay ────────────►  ↓ download
│    (no persistent storage)  │     Relay server (memory)
│  • Status dashboard         │     ↓ upload
│  • Push notifications       │     Destination Cloud API
│                             │
│  Google→Google: share+copy  │
│  OneDrive→*: stream relay   │
│                             │
│  Hosting: ~$200-500/mo      │
│  Per-user relay: $0.02-0.74 │
└─────────────────────────────┘
```

- Start with Google → Google (cheapest, simplest — share-then-copy)
- Add OneDrive → Google Drive (streaming relay)
- Web-only, no desktop install required
- Works 24/7

### Phase 2 — Desktop Agent (Optional Add-On)

- For privacy-conscious users who don't want data streaming through our servers
- "Privacy Mode" toggle in settings
- Desktop agent handles transfers locally
- Same dashboard, same config — just different transfer path

### Phase 3 — Expand

- More providers (Dropbox, iCloud)
- Client-side encryption (works with both relay and agent)
- Family tier, restore workflows, etc.

---

## Impact on Business Model

### Pricing Adjustment

The cloud relay has real per-user costs, so the free tier needs boundaries:

| | Free | Pro ($4.99/mo) | Family ($9.99/mo) |
|---|---|---|---|
| **Backup pairs** | 1 pair | Unlimited | Unlimited |
| **Schedule** | Weekly | Daily/hourly | Daily/hourly |
| **Retention** | 30 days | 90 days | 365 days |
| **Data limit** | 15 GB total | 500 GB | 2 TB |
| **Transfer method** | Cloud relay | Cloud relay | Cloud relay |
| **Privacy mode (agent)** | — | ✅ Optional | ✅ Optional |
| **Anomaly detection** | — | ✅ | ✅ |

**Why 15 GB free limit:** Prevents abuse of the relay. At $0.087/GB egress, a 15 GB initial backup costs us ~$1.30 per free user one-time, then ~$0.02-0.07/month ongoing. Affordable as a growth investment.

**Price increase from $3.99 to $4.99/mo:** Accounts for relay costs while maintaining competitive positioning. Still well below MultCloud ($9.99) and CloudHQ ($9.90).

### Revenue Projection (Revised)

| Metric | Desktop Agent Model | Cloud Relay Model |
|--------|-------------------|-------------------|
| Free users | 100,000 | 100,000 |
| Free tier relay cost | $0 | ~$2,000-7,000/mo |
| Conversion rate | 4% (optimistic for desktop) | 8% (SaaS median) |
| Paid users | 4,000 | 8,000 |
| ARPU | $39-79/yr | $49.99-99.99/yr |
| **Revenue** | **$156k-316k/yr** | **$400k-800k/yr** |
| Relay costs (paid users) | $0 | $3,840-7,100/mo |
| **Gross margin** | ~95% | ~85-92% |

The cloud relay model generates **2-3x more revenue** due to higher conversion, despite higher per-user costs.

---

## What We're Giving Up

Switching to cloud-first means:

| Lost Advantage | Mitigation |
|---------------|-----------|
| "Data never touches our servers" | "Data streams through but is never stored." For Google→Google: "Data never leaves Google." Desktop agent available as privacy option. |
| $0/user cost | Relay cost is $0.02-0.74/user/month — viable at $5/month |
| Simplest infrastructure | More complex, but standard web infrastructure |

**What we gain is far more valuable:**
- 2x+ conversion (web vs. desktop install)
- 24/7 reliability (no PC dependency)
- 2-minute onboarding (vs. 10-15 minutes)
- Works on any device
- We control the experience end-to-end

---

## Decision Point

**The original ADR-001 decision was based on flawed cost data.** The $16-24k/month figure assumed monthly full backups, which contradicts our own backup fundamentals document recommending incremental sync. With corrected numbers, server-side relay costs $0.02-0.74/user/month — well within viable margins for a $5/month product.

**Recommendation: Adopt Option C (Cloud-First with Optional Desktop Agent).**

The desktop agent can still exist as a "privacy mode" add-on, but the default experience should be web-only, zero-install, always-on cloud relay. This matches our target audience (non-technical families), maximizes conversion, and is economically viable.

The Google → Google share-then-copy path should be the MVP launch route — it's the cheapest and simplest to implement.
