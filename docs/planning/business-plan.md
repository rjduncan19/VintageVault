# VintageVault — Business Plan

_Prepared: March 2026 | For internal review_

---

## Executive Summary

VintageVault is a mission-driven, open-source cloud-to-cloud backup service that protects families, freelancers, and small businesses against ransomware, accidental deletion, and account compromise by automatically copying files from one cloud account to another.

**The mission:** Ransomware attacks are up 34% year-over-year. Enterprise IT has solutions. Everyone else has nothing. VintageVault exists to change that — not to build a unicorn, but to make the world's digital life safer.

**The model:** Open core. An open-source backup engine (Apache 2.0) anyone can use for free. A sustainable SaaS web app for people who want convenience. Revenue exists to sustain the mission, not the other way around. Think Patagonia, not Uber.

**The ask:** ~$2,000-4,000 startup costs, 10-15 hours/week of evening/weekend development, with AI (GitHub Copilot) handling the bulk of code generation. Break-even at ~50 paid subscribers.

**What success looks like:** People protected, not revenue maximized. If the free open-source agent protects 10,000 families and the SaaS breaks even, that's a win.

---

## The Problem

Your cloud drive is not a backup. It's the thing that needs backing up.

| Threat | Frequency | Impact | Current consumer solution |
|--------|-----------|--------|--------------------------|
| Ransomware encrypts cloud files | 69-72% of businesses hit; consumer attacks rising | Total loss of all files | None |
| Accidental deletion | Common (especially families with shared drives) | Lost files, trash expires in 30 days | Hope you notice in time |
| Account compromise (phishing) | Rising; #1 attack vector | Attacker can delete everything | Change password (too late) |
| Provider outage | Rare but real (Google, Microsoft have had multi-hour outages) | Temporary loss of access | Wait |

**The gap:** Enterprise IT spends billions on backup. Consumers spend $0. Not because the threats don't exist — because no product exists for them.

---

## The Solution

**Simple version:** VintageVault automatically creates immutable snapshots of your OneDrive files on a schedule. If something goes wrong, browse your backup folder and restore.

**How it works:**

```
PRIMARY EXPERIENCE (Web-only, no install):
  User signs up → authorizes OneDrive → snapshots run automatically via cloud API
  2-minute setup. No app to install. Backup is plain folders in your own OneDrive.

PHASE 2 ADD-ON (Cross-account, Pro tier):
  Backup to a SEPARATE account for ransomware protection.
  Requires relay server or optional desktop agent.

PHASE 2 ADD-ON (Open Source Agent — optional):
  User downloads desktop app → runs backups locally → maximum privacy
  Free forever. Apache 2.0 license.
```

---

## Market

### Target Audience

| Segment | Size | Willingness to pay | Priority |
|---------|------|-------------------|----------|
| **Families** (protecting photos, documents) | Billions | Low individually, but volume play | P0 |
| **Freelancers** (client files, contracts) | ~60M globally | Moderate ($5-10/mo is acceptable) | P0 |
| **Micro-businesses** (1-10 employees) | ~400M globally | Higher ($5-15/user/mo) | P1 |

### Total Addressable Market

```
Conservative TAM:
  100M households with 2+ cloud accounts × 2% awareness × 4% conversion
  = 80,000 potential paid users × $60/yr average
  = $4.8M annual revenue opportunity

This is before any marketing spend. The real TAM is much larger.
```

### Competition

| Competitor | Pricing | Why they're not a threat |
|-----------|---------|------------------------|
| MultCloud | $9.99/mo | Sync (not backup), data through their servers, no anomaly detection |
| CloudHQ | $9.90/mo | Business-focused, sync not backup, expensive |
| CBackup | ~$10/mo | Closest competitor — basic UI, no anomaly detection, limited |
| rclone | Free | CLI tool for developers — not our audience |
| Doing nothing | Free | The real competitor. Marketing must overcome apathy. |

**Our advantages:** Privacy-first architecture, 50-75% lower price, ransomware detection, consumer UX, open source trust.

See `docs/planning/competitive-analysis.md` for detailed analysis.

---

## Business Model

### Open Core (Bitwarden Model)

```
OPEN SOURCE (Apache 2.0):              PROPRIETARY (SaaS):
  VintageVault.Engine                    VintageVault.Web
  VintageVault.Agent                     Dashboard, billing, relay
  
  Purpose: Trust & awareness             Purpose: Revenue
  Cost: $0                               Revenue: Subscriptions
  Users: Influencers, developers         Users: Families, mainstream
```

### Pricing

| | Free | Pro ($4.99/mo) | Family ($9.99/mo) |
|---|---|---|---|
| Backup pairs | 1 | Unlimited | Unlimited |
| Schedule | Weekly | Daily/hourly | Daily/hourly |
| Retention | 30 days | 90 days | 365 days |
| Data limit | 15 GB | 500 GB | 2 TB |
| Privacy mode (desktop agent) | — | ✅ | ✅ |
| Anomaly detection | — | ✅ | ✅ |
| Family members | — | — | Up to 5 |

### Why These Prices Work

- **$4.99/mo** is half of MultCloud ($9.99) and CloudHQ ($9.90)
- Less than a coffee. Framed as insurance, not software.
- Our relay cost is $0.02-0.74/user/month → **85-94% gross margin**
- Google → Google route (share-then-copy) costs us ~$0/user

---

## Financial Projections

### Startup Costs (One-Time)

| Item | Cost | Notes |
|------|------|-------|
| LLC formation | $100-500 | State filing + registered agent |
| Domain name (vintagevault.com or similar) | $12-50/yr | |
| **Google OAuth security assessment** | **$15,000-75,000** | **Required for restricted Drive scopes. Annual re-verification. This is the single largest cost.** |
| Microsoft Entra app registration | $0 | Free |
| Logo / branding | $0-300 | DIY with Canva/Figma or Fiverr |
| Legal (privacy policy, ToS) | $0-500 | Templates + AI-assisted drafting |
| Stripe setup | $0 | Free (2.9% + $0.30 per transaction) |
| **Total (with Google assessment)** | **$15,600-76,350** | |
| **Total (without — MVP with OneDrive only or < 100 test users)** | **$600-1,350** | Google allows 100 test users without verification |

#### The Google OAuth Problem

Google requires a third-party security assessment ($15,000-75,000) for apps requesting restricted Drive scopes (full read/write access). This is the single biggest financial barrier.

**Mitigation strategies:**
1. **Start with < 100 test users** — Google allows unverified apps for up to 100 explicitly-added test users. This is enough for beta.
2. **Start with OneDrive → OneDrive only** — Microsoft's OAuth verification is free. Launch, prove the model, then invest in Google verification.
3. **Use `drive.file` scope** — Only access files the app creates. Avoids restricted scope. But limits what we can back up.
4. **Apply for Google Cloud startup credits** — Google offers up to $100,000 in credits for startups through various programs.
5. **Defer Google until revenue justifies it** — Launch with Microsoft-only, generate revenue, use revenue to fund Google assessment.

**Recommended approach:** Launch MVP with OneDrive → OneDrive (free OAuth). Beta-test Google with <100 users. Fund Google assessment from early revenue or startup credits.

### Monthly Operating Costs

| Item | Pre-Revenue | 100 Paid Users | 1,000 Paid Users |
|------|-------------|----------------|-------------------|
| Azure hosting (App Service / VM) | $20-50 | $50-100 | $200-500 |
| Database (Azure SQL / Cosmos DB) | $5-20 | $20-50 | $50-200 |
| Egress / relay bandwidth | $0 | $20-75 | $200-750 |
| Monitoring / logging | $0 | $10-20 | $50-100 |
| Transactional email (SendGrid) | $0 | $0-10 | $10-25 |
| Domain + DNS | $1 | $1 | $1 |
| GitHub (repos, actions) | $0 | $0 | $4-20 |
| GitHub Copilot subscription | $19 | $19 | $19 |
| **Total monthly infrastructure** | **$45-90** | **$120-275** | **$535-1,615** |
| **Per-user cost** | — | **$1.20-2.75** | **$0.54-1.62** |

### Revenue Projections

| Milestone | Timeline | Paid Users | Monthly Revenue | Monthly Cost | Net |
|-----------|----------|-----------|-----------------|-------------|-----|
| Beta launch | Month 1-3 | 0 | $0 | $45-90 | -$45-90 |
| Early adopters | Month 4-6 | 20-50 | $100-350 | $80-150 | +$0-200 |
| **Break-even** | **Month 6-9** | **~50** | **~$250** | **~$150** | **~+$100** |
| Growing | Month 12 | 200-500 | $1,000-3,500 | $200-500 | +$800-3,000 |
| Established | Month 24 | 1,000-2,000 | $5,000-14,000 | $535-1,615 | +$4,000-12,000 |
| Scaled | Month 36+ | 5,000+ | $25,000+ | $2,500-5,000 | +$20,000+ |

### Break-Even Analysis

```
Fixed monthly costs:             ~$90 (pre-scale)
Variable cost per paid user:     ~$0.50-1.50/month
Average revenue per paid user:   ~$5.50/month (blended Pro + Family)
Contribution margin per user:    ~$4.00-5.00/month

Break-even users:                ~20-25 paid subscribers
                                 (at 8% conversion = ~250-300 free users)
```

**Break-even is achievable at very low scale.** This is a viable side project.

---

## Development Plan

### The Copilot Advantage

Development is done primarily by one developer (Richard) using GitHub Copilot CLI as an AI pair programmer. This dramatically reduces development cost:

| Factor | Traditional Solo Dev | With Copilot |
|--------|---------------------|-------------|
| Lines of code/hour | 50-100 | 200-500 |
| Boilerplate time | High | Near-zero |
| API integration research | Hours | Minutes |
| Test generation | Manual | Largely automated |
| Documentation | Separate effort | Generated alongside code |
| Effective dev-hours/week | 10-15 hrs of weeknight/weekend time | Equivalent to 30-50 hrs of traditional output |

**Cost of development labor: $0** (side project, own time)
**Cost of Copilot: $19/month** (GitHub Copilot Individual)

### Development Timeline

| Phase | Deliverable | Effort | Calendar Time |
|-------|------------|--------|---------------|
| **1a: Engine + CLI** | Core backup engine, Google→Google share-then-copy, CLI interface | 80-120 hrs | 2-3 months |
| **1b: Open source launch** | GitHub release, README, contribution guide | 10-15 hrs | 2 weeks |
| **2a: Web dashboard MVP** | ASP.NET Core + Blazor, user accounts, job config, status | 60-80 hrs | 2 months |
| **2b: Relay + billing** | Streaming relay service, Stripe integration | 40-60 hrs | 1-2 months |
| **3: Polish + grow** | OneDrive support, family tier, anomaly detection, restore UX | Ongoing | Ongoing |

**Total to revenue-generating product: ~200-280 hours over 6-8 months**

At 10-15 hours/week of evenings and weekends, this is a realistic timeline for a side project.

### Technology Stack

| Component | Technology | Why |
|-----------|-----------|-----|
| Engine | .NET 8, C# | Best cloud API support, strong typing, Copilot excels at C# |
| Web app | ASP.NET Core + Blazor Server | All C# (no second language), MudBlazor for UI components |
| Database | SQLite (early) → Azure SQL (scale) | Start simple, migrate when needed |
| Hosting | Azure App Service | $20-50/month, built-in CI/CD, auto-scaling |
| Payments | Stripe | Industry standard, 2.9% + $0.30, excellent .NET SDK |
| Auth | ASP.NET Identity + OAuth | Well-documented, Copilot knows it deeply |
| Source control | GitHub (public repo for engine, private for web) | Open core model |

---

## Risk Assessment

### Critical Risks

| Risk | Severity | Likelihood | Mitigation |
|------|----------|-----------|-----------|
| **Google changes/revokes API access** | Critical | Low-Medium | Start with OneDrive (Microsoft is more developer-friendly). Diversify providers. Monitor API deprecation notices. |
| **Google OAuth assessment cost** | High | Certain | Start with OneDrive-only or <100 test users. Apply for startup credits. Fund from early revenue. |
| **Nobody wants this** (no product-market fit) | Critical | Medium | Validate with beta users before investing in Google assessment. Free open-source agent tests demand at zero cost. |
| **Google/Microsoft builds it natively** | High | Low | They won't — cross-provider backup conflicts with their lock-in strategy. Same-provider backup is more likely but would validate the market for us. |
| **Data breach / security incident** | Critical | Low | Open source engine for audit. Streaming relay (no persistent storage). LLC protects personal assets. Consider E&O insurance. |
| **Copilot-generated code has bugs** | Medium | Medium | Comprehensive testing. Open source community review. Gradual rollout. |
| **Support burden unsustainable** | Medium | Medium | Self-service first. Community forums (GitHub Discussions). FAQ/docs. Only add paid support at scale. |
| **API rate limiting at scale** | Medium | Medium | Google allows 12,000 queries/60s. Implement backoff. Multiple API projects if needed. |

### Legal / Compliance

| Requirement | When Needed | Cost |
|-------------|-------------|------|
| LLC formation | Before launch | $100-500 |
| Privacy policy | Before launch | $0-500 (template + AI) |
| Terms of service | Before launch | $0-500 |
| GDPR compliance (if EU users) | Before launch | Operational (document data flows, implement deletion) |
| SOC 2 | At significant scale (1,000+ users) | $20,000-50,000 |
| Google OAuth security assessment | Before >100 Google Drive users | $15,000-75,000 |

---

## Go-To-Market Strategy

### Phase 1: Credibility (Months 1-3)

```
Open source engine + agent → GitHub
  ↓
Tech bloggers, YouTubers, Reddit discover it
  ↓
"Finally, an open source cloud backup tool" reviews
  ↓
Developer/privacy community adoption (hundreds of agent users)
  ↓
GitHub stars, word-of-mouth, backlinks
```

**Cost: $0.** The open source agent IS the marketing.

### Phase 2: Conversion (Months 4-8)

```
SaaS web app launches
  ↓
Agent users see: "Want it easier? Try VintageVault Cloud"
  ↓
Non-technical friends/family of agent users sign up
  ↓
"My tech friend recommended this" → 2-minute web signup
  ↓
Free tier → Pro/Family conversion
```

### Phase 3: Growth (Month 9+)

- SEO content: "How to back up Google Drive" articles
- Comparison pages: "VintageVault vs MultCloud"
- Reddit/forum presence (genuine, not spammy)
- Product Hunt launch
- Partner with tech YouTubers for reviews
- Referral program: "Protect a friend, get a free month"

### Channels (Ranked by Expected ROI)

| Channel | Cost | Expected Impact |
|---------|------|----------------|
| GitHub / open source community | $0 | High — tech influencers amplify |
| SEO content marketing | $0 (own time) | High — "how to backup Google Drive" has volume |
| Reddit, Hacker News | $0 | Medium-High — our audience lives here |
| Product Hunt launch | $0 | Medium — one-time spike, good for credibility |
| Tech YouTuber partnerships | $0-500 (free product) | Medium — if they review it organically |
| Paid ads (Google, Facebook) | $500+/month | Low initially — save for when unit economics are proven |

---

## Questions She Will Ask

_(And answers)_

### "Is there actually demand for this?"

**Honest answer:** Unproven. The market is unserved, which could mean massive opportunity or no willingness to pay. The open source agent tests demand at zero cost before we invest in the SaaS. If nobody uses the free agent, we don't build the paid product.

**Validation plan:** Launch open source agent → measure GitHub stars, downloads, feedback. If <500 stars in 3 months, demand signal is weak.

### "What if Google or Microsoft just builds this?"

**Cross-provider** backup conflicts with their lock-in strategy — they want you dependent on their ecosystem, not backing up to a competitor. **Same-provider** backup is plausible (Google could add "backup to another Google account"), but that would validate our market and we'd still offer cross-provider + anomaly detection as differentiators.

### "What's the opportunity cost of your time?"

At 10-15 hours/week for 6-8 months: ~500-700 hours total. At a nominal consulting rate, that's $50,000-100,000 of opportunity cost. The question is whether this becomes worth more than the alternative use of those hours (rest, other projects, family time).

**Counter:** This is also a learning investment — deep experience with cloud APIs, SaaS architecture, open source community building, and AI-assisted development. These skills compound regardless of VintageVault's commercial outcome.

### "What about the $15-75K Google OAuth assessment?"

This is the biggest financial risk. Mitigations:
1. Start with OneDrive-only (Microsoft OAuth is free)
2. Beta test Google with <100 users (allowed without verification)
3. Apply for Google Cloud startup credits ($100K available)
4. Fund from early revenue
5. If all else fails, the desktop agent can use the user's own Google Cloud project (rclone does this)

**Worst case:** We operate OneDrive-only for the first year while saving for the assessment.

### "Who handles support?"

Self-service first: comprehensive docs, FAQ, GitHub Discussions. At <500 users, support volume is ~2-5 questions/week — manageable in evenings. At scale, hire part-time support or use community moderators.

### "What if there's a security breach?"

- LLC protects personal assets
- Open source engine means security experts can audit the code
- Streaming relay stores zero user data (nothing to breach)
- E&O insurance available (~$500-2,000/year when revenue justifies it)
- For the desktop agent: data never leaves the user's device

### "Is AI-written code reliable enough for production?"

Copilot generates code; the developer reviews, tests, and maintains it. The open source model adds another safety layer — community review. This is no different from using any code generation tool; the human is still responsible. Comprehensive integration tests cover the critical paths (file sync, OAuth, data integrity).

### "What's the endgame?"

**This is not a startup. It's a sustainable mission.**

Inspired by Patagonia's model: the mission is protecting people's digital lives. Revenue sustains the work. We're not optimizing for an exit.

**Option A: Sustainable mission (primary goal)** — Break-even or modest income ($3-5K/month). Open source engine protects thousands for free. SaaS sustains development. 1% of revenue goes to digital safety organizations. The project is alive, useful, and self-sustaining.

**Option B: Growing impact** — If demand is strong, invest more to reach more people. Not for shareholder return — because more people protected is better.

**Option C: Acqui-hire / IP donation** — If the project can't sustain itself, open-source everything and donate the IP to a digital safety nonprofit. The code lives on even if the business doesn't.

**Option D: Shut it down gracefully** — If demand doesn't materialize within 6-12 months, total financial loss is $2,000-4,000. The code remains open source. Educational value is retained.

### "What's the maximum you could lose?"

| Scenario | Financial Loss | Time Loss |
|----------|---------------|-----------|
| **Kill after beta (3 months)** | $300-500 | 150 hours |
| **Kill after SaaS launch (8 months)** | $2,000-4,000 | 500 hours |
| **Kill after Google OAuth assessment** | $17,000-80,000 | 700 hours |
| **Never do the Google assessment** | $2,000-4,000 max | 500 hours |

**Key insight:** The Google assessment is the only decision that creates significant financial risk. Every other cost is pocket change for a side project. **Don't pay for the assessment until the product is proven.**

### "How is this different from your last side project?"

_(She'll ask this.)_ This one has:
1. Clear market gap (not a crowded space)
2. Near-zero marginal costs (sustainable economics)
3. Built-in validation mechanism (open source → measure demand before investing)
4. AI-assisted development (10x faster than traditional side project)
5. Kill switch at every phase (escalating investment only if metrics warrant)

---

## Decision Framework

```
PHASE 1: Invest time only ($0-500)
  Build engine + open source agent
  ↓
  Signals positive?                    Signals negative?
  (500+ GitHub stars,                  (<100 stars, no
   active community,                   engagement, no
   user testimonials)                  interest)
  ↓                                    ↓
PHASE 2: Small financial              STOP. Total loss:
  investment ($1,000-2,000)            ~$300 + 150 hours.
  Build SaaS, launch free tier         Educational value
  ↓                                    retained.
  Paying users?
  (50+ Pro subscribers,
   positive unit economics)
  ↓                                    
PHASE 3: Google assessment?            NO → Stay OneDrive-only.
  Only if revenue or credits               Still a viable business.
  cover the cost.
```

**The beauty of this plan: every phase has a clear go/no-go signal, and losses are capped at each stage.**

---

## One-Page Summary

| | |
|---|---|
| **What** | Cloud-to-cloud backup for families |
| **Mission** | Protect everyone's digital life from ransomware |
| **Why now** | 2.3B cloud users, zero consumer backup products, APIs are mature |
| **Model** | Open core: free agent (mission) + paid SaaS (sustainability) |
| **Price** | Free / $4.99 / $9.99 per month |
| **Cost to start** | $500-2,000 (no Google assessment) |
| **Break-even** | ~50 paid subscribers (~$250/month revenue) |
| **Dev approach** | Solo + GitHub Copilot, evenings/weekends, 10-15 hrs/week |
| **Timeline to revenue** | 6-8 months |
| **Max downside** | $2,000-4,000 if killed before Google assessment |
| **Success metric** | People protected, not revenue maximized |
| **Biggest risk** | Nobody wants this (mitigated by free open source validation) |
| **Endgame** | Sustainable mission, not exit |
