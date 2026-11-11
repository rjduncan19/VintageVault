# Design Chat Handoff — Sept to Nov 2026

_Source: a long voice design conversation with Claude (claude.ai), Sept 30 to Nov 11, 2026. Written for a Claude Code session to fold into the repo._

> **Read this first.** That conversation happened without access to this repo. Much of it assumed VintageVault is a **PC-to-cloud** backup tool (local disk, Volume Shadow Copy, a local snapshot engine). The repo is a **cloud-to-cloud** design (OneDrive to OneDrive/Google, same-account snapshots first, rclone as the engine, MSAL tokens). Do **not** paste these notes into existing docs as-is. Reconcile them using section 2, and ask Rick about every "Decision needed" item before changing an ADR.

---

## 1. Kick-off prompt for Claude Code

Paste this into the Claude Code session in `C:\Users\rjdun\source\VintageVault`:

```
Read docs/planning/2026-11-design-chat-handoff.md. Then:
1. Read the docs it names in section 2 and confirm or correct my comparison table.
2. Ask me about each "Decision needed" item, one at a time. Do not edit ADRs until I answer.
3. Draft the new docs in section 4 and the edits in section 5 on a branch named design-chat-nov-2026.
4. Create a CLAUDE.md at the repo root (section 6).
5. Do not touch the 7 modified files under src/ and tests/ (see section 7).
Show me a summary of changes before committing.
```

---

## 2. Reconciling the chat with the repo

### Where they agree (just reinforce)
| Topic | Repo | Chat |
|---|---|---|
| Engine | rclone adopted (`rclone-exploration/findings/recommendation.md`) | rclone as base |
| Backup, not sync | ADR-001 implication 1, ADR-003 rejects living mirror | Snapshot-based, never a mirror |
| Immutable snapshots | ADR-003 immutable incremental snapshots | Snapshots so ransomware is "just another version" |
| Anomaly detection | ADR-004 metadata + content detection | Entropy and change-rate spikes, ideally checked by a separate verifier |
| Mission / donation model | `MISSION.md`, Patagonia-style | Shareware or donation-based |
| Nonprofits | `MISSION.md` mentions free backup for nonprofits | New detailed segment (section 3.4) |
| External SSD | rclone learning tutorial 03 | External drive as an air-gapped immutable tier |

### Where they diverge (Decision needed)
1. **Source of data: cloud or local PC?** The repo backs up cloud accounts. The chat assumed local files, with Volume Shadow Copy for open files and a local Restic/Kopia snapshot engine. _Decision needed:_ is local-PC backup an added scope (a desktop agent is already Phase 2 in ADR-001 / architecture-reassessment Option C), or out of scope? If added, Restic/Kopia vs. the repo's own `--backup-dir` snapshot layout also needs a decision.
2. **Credential model.** The repo (poc-spec R1) uses MSAL with a persistent DPAPI-encrypted token cache, so the tool can run unattended. The chat's core principle is the opposite: **no always-running process holding credentials**; each protected run needs a user gesture (Windows Hello preferred). _Decision needed:_ likely answer is "both, by tier": unattended for the same-account/convenience tier, gated for the protected/immutable tier. Confirm before rewriting.
3. **What provides ransomware isolation.** ADR-001 says **cross-provider** is the isolation (attacker can't reach the other account). The chat noted a compromised machine or agent holding tokens for both accounts can still delete in both; consumer OneDrive/Google Drive lack true object lock (versioning and ~30-day recycle bins only). The chat's candidates: (a) a **control-plane service** that alone holds delete-capable tokens and enforces retention, (b) an external drive air gap, (c) S3-style object lock (B2/Wasabi), judged too much friction for target users. _Decision needed:_ which goes into v1, and whether ADR-001's threat model gets a "compromised agent" row.
4. **Two-tier cadence.** The chat landed on frequent snapshots plus a rarer (roughly monthly) gated immutable copy, with separate credentials per tier, plus a separate frequently synced working/index tier for cross-device use. Map this onto the repo's product ladder (free same-account, Pro cross-account) rather than introducing a parallel ladder.

---

## 3. New material from the chat (not in the repo)

### 3.1 Credential-gating UX (low friction)
- **Windows Hello** as the primary unlock: TPM-backed, about a second of effort, malware can't trigger it silently.
- Alternatives: hardware security key tap; one approval unlocking a short window (e.g. an hour); **phone push approval** (attacker on the PC can't reach the phone); approving the next run at the end of the current one.
- Nag-then-fast-confirm reminder so gated backups don't depend on memory.
- **Deferred enrollment** (decided): first run works with zero setup. The harder step (second account, external drive) is asked for only after the user has seen value, ideally right after a successful restore drill.

### 3.2 Control-plane service (leading candidate, undecided)
- VV brokers OAuth and holds the delete-capable token. The client gets short-lived, narrow write tokens per run and uploads directly; VV never sees file data.
- Deletes inside the retention window go through VV's policy check and are refused.
- Cheap to run (small API calls, no bulk transfer); a breach exposes tokens, not documents.
- Costs: VV becomes a credential custodian and a dependency; server costs in a donation model.
- **To verify:** whether OneDrive/Google Drive permissions can separate write from delete (possibly a service-account-owned folder shared to the user). Compare with ADR-002's "Cloud Relay" section; this is a lighter variant that relays no data.

### 3.3 Hardening against a compromised writer
- Assume the writer is hostile. Realistic goal: survive automated commodity ransomware; a targeted attacker with kernel access wins anyway.
- Verifier separate from the writer; signed binaries; protected process where possible; Controlled Folder Access around any local snapshot store; enough retained versions that one poisoned run is an inconvenience.

### 3.4 Segment: churches, nonprofits, charities
- Motivating case (Oct 2026): a medium-sized, tech-forward and progressive congregation in Rick's region (it relies on technology for accessibility: Zoom, simulcast services and so on) had two compromises in one year. A part-time bookkeeper's personal (BYOD) device, also used for other clients, was hacked; separately the minister's email was compromised. Both were used to phish the whole membership. The congregation is unsure whether it is fully clean.
- Honest framing: VV would not have prevented those account/device compromises. Its value is **recoverability** (books, donor records, member lists) and known-good snapshots from before an incident.
- Pair VV with **remedial cybersecurity education** for staff, volunteers and members (MFA, phishing-resistant sign-in, separating contractor devices).
- **Two-way referrals:** point to CISA guidance for faith-based organizations, Global Cyber Alliance small-org toolkit, TechSoup, NTEN, denominational bodies, and earn links back. Open source and free/donation tiers help; a pilot case study helps most.
- Distribution: TechSoup and denominational networks reach many congregations at once.
- Possible pilot: the motivating congregation (not yet asked).
- Overlaps with Rick's [Harbor](https://github.com/rjduncan19/Harbor) project (elder device management) on the BYOD-contractor problem.

### 3.5 Adoption for the never-backed-up population
- About 20% of computer owners have never backed up (Backblaze survey, flat for years); other surveys put rare-or-never at ~40%. ~97% of recent ransomware attacks also target backup storage (secondary-source stat; verify before publishing).
- Make value visible before disaster: concrete, personal first-run results ("4,200 photos protected, back to 2009").
- Be honest about gaps: show what is synced but not protected (many think OneDrive sync is a backup).
- Spontaneous **restore drills**: restore a random file and show it worked.
- Zero-question setup with sensible defaults.

### 3.6 Optional local RAG add-on (new idea)
- Build a personal semantic index during the backup scan (files are already being read). Nothing leaves the user's security domain. Ships as an optional add-on, not the lean core.
- Index stores chunks, vectors, and a path/content-hash pointer; text is re-read from the original at answer time. ~3 KB per 768-dim float32 vector, under 1 KB int8; roughly low single-digit GB per 10 GB of real documents.
- v1 scope: documents, spreadsheets, PDFs, email archives, code. Skip media/binaries.
- Stack: **ONNX Runtime** (NPU, then DirectML, then CPU), a sentence-transformer exported to ONNX (BGE-small or MiniLM), int8; **LanceDB** (embedded, real upserts/deletes; avoid FAISS); Tika or unstructured for extraction.
- Incremental refresh keyed by path + content hash, driven by the backup run's change list.
- Windows Search: consume indexer change notifications via COM (ISearchCrawlScopeManager etc.) as a fast path, own crawler as fallback (NAS, mapped drives, indexing off). Don't embed inside an indexer filter (timeouts, threading, degrades system search).
- **Open:** respect Windows Search exclusions (often privacy-motivated) when choosing what to embed? Backup and search may be different intents; probably an explicit wizard choice.
- Cross-device: sync the index via the working tier; or a primary machine/NAS serves queries over something like Tailscale.
- **Fit with the repo:** for cloud-to-cloud backups, indexing happens wherever the bytes pass (desktop agent) or not at all for pure server-side copies. Note this constraint in the doc.

### 3.7 Competitive notes (Sept 2026, check against `competitive-analysis.md` and `landscape-2026.md`)
- Consumer: Backblaze, Carbonite, IDrive — simple, provider holds credentials.
- Acronis — heaviest ransomware marketing, heavy suite.
- Pro-sumer: Veeam Community, Retrospect, Duplicati — real object lock, too complex for non-specialists.
- Gap: people who need immutable backups can't operate the tools that provide them; nobody offers local semantic search over backups.

### 3.8 rclone vs. OneDrive client observations
- Rick's OneDrive desktop client crashed 6+ times syncing 300+ GB of photos/video on a strong PC (26H2, 64 GB RAM) over ~10 Mbps upload.
- rclone uses the API directly: retries with backoff, chunked uploads, skips completed files on rerun. Weakness: an interrupted large file restarts from zero.
- Keep concurrency modest; Microsoft throttles bulk transfers. 300 GB at 10 Mbps is ~67 hours.
- Don't trust a green sync tick; verify with `rclone check` (hash comparison). Good input for rclone learning doc 07 and for a VV "verify" feature.

---

## 4. Suggested new docs
- `docs/planning/nonprofit-segment.md` — section 3.4.
- `docs/planning/rag-add-on.md` — section 3.6, including the cloud-to-cloud fit constraint.
- `docs/planning/credential-gating.md` — sections 3.1 to 3.3; becomes an ADR once decisions 2 and 3 are settled.
- `docs/planning/adoption-and-trust.md` — section 3.5 (or merge into `product-strategy.md`).

## 5. Suggested edits to existing docs (after decisions)
- `product-strategy.md` Audience Segments: add churches/nonprofits/charities.
- `001-system-architecture.md` Threat Model: add "VV agent or user machine compromised" and how each tier handles it.
- `competitive-analysis.md`: merge 3.7 if missing; add the "no local semantic search" gap.
- `docs/rclone-exploration/learning/07-verification-and-checksums.md`: add the OneDrive-client crash observation as motivation.
- `MISSION.md`: expand the nonprofit line to include the education partnership idea.
- `docs/prompts/README.md`: add a session entry pointing to this handoff.

## 6. CLAUDE.md for the repo root
Keep it short and point to existing docs rather than restating them: project one-liner, status (pre-MVP; rclone adopted; same-account POC with 53 passing tests awaiting a real test OneDrive per `NEXT-STEPS.md`), where decisions live (`docs/architecture/` ADRs, `docs/planning/`), build/test commands from `CONTRIBUTING.md`, developer profile (30 years software, 25 at Microsoft on Windows, expert C++, comfortable with COM/IPC), and the rule that ADR changes need Rick's sign-off.

## 7. Repo state note
Seven files under `src/` and `tests/` show as modified, but `git diff --ignore-all-space` is empty: it is line endings only (CRLF/LF), and `core.autocrlf` is unset. Don't commit them with the doc changes. Consider adding a `.gitattributes`.
