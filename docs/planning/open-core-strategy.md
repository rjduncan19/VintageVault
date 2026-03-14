# Open Core Strategy: One Engine, Two Modes

_Created: 2026-03-14 | Model: Claude Opus 4.6_

## The Concern

> "Implementing two products seems far more complicated."

It would be — if they were two products. They're not. The architecture is **one shared backup engine** deployed two ways:

```
                    ┌───────────────────────────────────┐
                    │   VintageVault.Engine (open source)│
                    │                                   │
                    │  • Provider abstraction layer      │
                    │  • Delta sync / change detection   │
                    │  • Retention & versioning logic    │
                    │  • Checksum verification           │
                    │  • Anomaly detection               │
                    │  • Share-then-copy optimization    │
                    │  • File format export (Google Docs)│
                    └──────────┬────────────┬────────────┘
                               │            │
              ┌────────────────┘            └────────────────┐
              ▼                                              ▼
┌──────────────────────────┐            ┌──────────────────────────────┐
│ VintageVault.Agent       │            │ VintageVault.Web             │
│ (open source)            │            │ (proprietary SaaS)           │
│                          │            │                              │
│ • Desktop background svc │            │ • ASP.NET Core + Blazor      │
│ • System tray UI         │            │ • User accounts + billing    │
│ • Local config (JSON)    │            │ • Streaming relay            │
│ • OAuth in OS keychain   │            │   (uses Engine internally)   │
│ • CLI interface          │            │ • Push notifications         │
│ • Runs on user's PC      │            │ • Family dashboard           │
│                          │            │ • Web-based OAuth flow       │
│ Cost to us: $0           │            │                              │
│ Privacy: maximum         │            │ Cost to us: $0.02-0.74/user  │
│ User: must install       │            │ Privacy: streaming (good)    │
│                          │            │ User: 2-min web signup       │
└──────────────────────────┘            └──────────────────────────────┘
```

**The engine is identical.** Whether it runs on our server or the user's PC, it calls the same Google Drive API, uses the same delta sync logic, applies the same retention rules. The only difference is where the process executes and how auth tokens are stored.

---

## The Bitwarden Playbook

Bitwarden is the closest analogy to what we're proposing:

| Aspect | Bitwarden | VintageVault |
|--------|-----------|-------------|
| **Open source component** | Password vault engine + all clients | Backup engine + desktop agent |
| **Proprietary component** | Hosted cloud service, enterprise features | Cloud relay, web dashboard, family management |
| **Who uses open source** | Privacy enthusiasts, self-hosters, security auditors | Tech influencers, privacy advocates, power users |
| **Who uses SaaS** | Everyone else (vast majority) | Families, freelancers, non-technical users |
| **Trust signal** | "Audited by Cure53, code is public" | "Audit our code — see exactly what accesses your cloud" |
| **Revenue** | $0 from open source; $millions from SaaS | Same model |

**What made Bitwarden successful:**
1. Open source earned trust in a trust-critical category (passwords → cloud access)
2. Tech reviewers gave it glowing coverage specifically because it was open source
3. Those reviews drove mainstream adoption to the SaaS product
4. Self-hosters became evangelists: "I run it myself, but my family uses the cloud version"

**VintageVault is in the same trust-critical position.** Users must grant OAuth access to their cloud storage — their photos, documents, everything. Being open source answers the scariest question: "What does this app actually do with access to all my files?"

---

## What to Open Source (and What Not To)

### Open Source (MIT or Apache 2.0)

| Component | Why |
|-----------|-----|
| **VintageVault.Engine** | Core backup logic — this is what people want to audit |
| **VintageVault.Agent** | Desktop client — so people can build, run, and verify it themselves |
| **Provider adapters** | Google Drive, OneDrive integration — invites community contributions for new providers |

### Proprietary (Source-available or closed)

| Component | Why |
|-----------|-----|
| **VintageVault.Web** | SaaS dashboard, billing, relay infrastructure — this is the business |
| **Family management** | Premium feature |
| **Anomaly detection models** | Competitive advantage; could be reverse-engineered to bypass detection |

### Gray Area (Decide Later)

| Component | Consideration |
|-----------|--------------|
| **Anomaly detection** | Open source builds trust ("see how we detect ransomware") but could be gamed. Start open, close if abused. |
| **Relay server code** | Publishing it lets users self-host the relay. Good for trust, but reduces SaaS value. Consider source-available (viewable but not redistributable). |

---

## Why This ISN'T Twice the Work

### Shared code estimate

```
VintageVault.Engine:    ~70% of total code
  - Provider abstraction, sync logic, retention,
    verification, change detection, error handling

VintageVault.Agent:     ~10% of total code
  - Thin wrapper: system tray, local config, OAuth
  - Calls Engine methods directly

VintageVault.Web:       ~20% of total code
  - Dashboard UI, user accounts, billing, relay
  - Also calls Engine methods directly
```

The Engine is the hard part, and it's written once. The Agent and Web are thin wrappers around it. This is standard .NET dependency injection — the Engine is a NuGet package consumed by both.

### Where it adds complexity

| Area | Added Work | Mitigation |
|------|-----------|-----------|
| **OAuth flows** | Agent uses OS keychain; Web uses server-side flow | Two implementations, but standard patterns |
| **Configuration** | Agent reads local JSON; Web reads from database | Abstract behind `IConfigProvider` |
| **Transfer execution** | Agent runs locally; Web runs on relay | Same Engine; just different `ITransferHost` |
| **Testing** | Must test both deployment modes | Engine unit tests cover 70% of behavior |
| **CI/CD** | Two release channels (NuGet + web deploy) | Standard .NET build pipeline |
| **Documentation** | Two setup guides | Worth it for the trust benefit |
| **Community management** | GitHub issues, PRs, discussions | Real effort, but drives awareness |

**Estimated additional effort for open source agent vs. SaaS-only: ~15-20%.** Not 2x.

---

## The Influencer / Trust Flywheel

```
Open source agent published on GitHub
         │
         ▼
Tech influencers review it (YouTube, blogs, Reddit)
"Finally, an open source cloud backup tool!"
         │
         ▼
Coverage drives awareness
"VintageVault is like Bitwarden for cloud backup"
         │
         ▼
Privacy advocates self-host the agent
Become evangelists in their communities
         │
         ▼
Their non-technical family/friends sign up for SaaS
"My tech friend says VintageVault is legit — and I can just use the website"
         │
         ▼
SaaS subscriptions grow
Fund continued development of the open source engine
         │
         ▼
Better engine → better reviews → more awareness → repeat
```

**The open source agent is marketing that costs nothing and builds trust that money can't buy.**

---

## Licensing Recommendation

| License | Pros | Cons |
|---------|------|------|
| **MIT** | Maximum adoption, no friction for contributors | Competitors could fork and commercialize |
| **Apache 2.0** ⭐ | Same as MIT + patent protection | Slightly more complex |
| **AGPL** | Forces competitors to open source their modifications | Scares away corporate contributors |
| **BSL (Business Source License)** | Source-available but non-compete; converts to open source after time delay | Less community trust |

**Recommendation: Apache 2.0 for the engine and agent.**

- Permissive enough to attract contributors and build trust
- Patent clause protects against patent trolls
- If a competitor forks it, they still can't offer our SaaS (relay infrastructure, dashboard, billing)
- Our competitive advantage is the SaaS convenience, not the engine code

---

## Revised Product Strategy

```
PRIMARY PRODUCT (revenue):     VintageVault Cloud — SaaS web app
  • Web signup, OAuth, cloud relay, dashboard
  • $4.99/mo Pro, $9.99/mo Family
  • Target: families, freelancers, non-technical users
  • Marketing: "Set it once. Sleep easier."

SECONDARY PRODUCT (trust):     VintageVault Agent — open source desktop app
  • GitHub releases, self-serve install
  • Free forever, Apache 2.0 license
  • Target: tech influencers, privacy advocates, developers
  • Marketing: "Audit the code. Run it yourself."

SHARED FOUNDATION:             VintageVault Engine — open source .NET library
  • All backup logic lives here
  • NuGet package consumed by both products
  • Community contributions welcome
  • The real product — everything else is a wrapper
```

### Build Order

> **⚠️ Updated for same-account pivot.** Original phasing proposed Engine+Agent first. Current POR starts with web-only same-account backup, with agent as a later add-on.

```
Phase 1: Engine + Web App (same-account OneDrive backup)
  • Build the core engine as a .NET library (open source)
  • Web app with OneDrive OAuth + Azure Functions orchestration
  • Same-account immutable snapshots via Graph API
  • OneDrive-only (Google deferred due to OAuth cost)
  • Ship engine on GitHub, web app as SaaS
  • Costs: ~$0-10/month infrastructure

Phase 2: Pro tier + Agent (cross-account + open source agent)
  • Add cross-account backup (ransomware protection, Pro $4.99/mo)
  • Build and open-source the desktop agent (for privacy-conscious users)
  • Stripe billing
  • Google Drive support (pending OAuth assessment funding)

Phase 3: Grow
  • More providers, family features, anomaly detection
  • Community contributes provider adapters
  • Engine improvements benefit both web app and agent
```

**Phase 1 validates demand** with a genuinely useful free product. Phase 2 introduces revenue and the open source agent.

---

## Summary

| Question | Answer |
|----------|--------|
| Is this two products? | No — one engine, two thin wrappers |
| How much extra work? | ~15-20% over SaaS-only |
| Should it be open source? | Yes — Apache 2.0 for engine + agent |
| What stays proprietary? | SaaS dashboard, billing, relay infrastructure |
| What's the model called? | Open core (same as Bitwarden, GitLab, Grafana) |
| Does it help with reviews? | Yes — tech influencers specifically seek out open source tools to review |
| Build order? | Engine + Agent first (free), then SaaS (revenue) |
