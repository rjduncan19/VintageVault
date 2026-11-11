# ADR-005: Credential Gating and v1 Ransomware Isolation

**Status:** Proposed. Rick made the decisions on 2026-11-11; this text needs his review before it becomes Accepted.
**Date:** 2026-11-11
**Decision Makers:** Rick Duncan
**Informs:** [ADR-001 Threat Model](001-system-architecture.md#threat-model), [Same-Account Pivot](../planning/same-account-pivot.md), [Cross-Account Strategy: Open Decision on Auth Model](../planning/cross-account-strategy.md#open-decision-auth-model-for-cross-account-transfer)
**Background:** [Credential Gating (planning)](../planning/credential-gating.md), [Design chat handoff](../planning/2026-11-design-chat-handoff.md)

> ⚠️ **Two of these are hard decisions** (marked below). Either could sink the ransomware-protection promise if it's done poorly. They are written down so they get revisited on purpose, not changed in passing.

## Context

The earlier plan assumed backups would run unattended:
- The CLI POC (poc-spec R1) keeps MSAL tokens in a DPAPI-encrypted cache so it can run without the user.
- The Phase 1 plan (same-account pivot) runs backups from an Azure Function on a weekly timer. That means VintageVault's server holds each user's refresh token.

ADR-001 treated **cross-provider** backup as the isolation: an attacker in one account can't reach the other. That only holds if the attacker doesn't also control whatever holds tokens for **both** accounts. A compromised PC or VV agent with both tokens can delete or encrypt in both.

Consumer OneDrive and Google Drive offer no true object lock either, only versioning and recycle bins of roughly 30–93 days. VintageVault's "immutable" snapshots (ADR-003) are a convention VV follows. The same `Files.ReadWrite` token that writes them can delete them.

A design conversation (Sept–Nov 2026) focused on this gap. This ADR records the decisions that came out of it.

## Decisions

### D1. Local-PC backup is future scope, built on rclone

Backing up local PC files (not just cloud accounts) is **post-MVP** scope. It would arrive through an optional desktop agent and use **rclone plus VintageVault's own browsable snapshot layout** (ADR-003). Restic and Kopia were considered and rejected for now: their repository formats are opaque, which breaks ADR-003's promise that a backup is "just folders you can browse", and they would add a second snapshot format. v1 stays cloud-to-cloud.

### D2. Every backup run needs a user gesture (hard decision)

**No VintageVault process holds usable credentials between runs, in any tier.** Each run is unlocked by a user gesture:
- **Primary:** Windows Hello. It's TPM-backed, takes about a second, and malware can't trigger it silently.
- **Alternatives:** phone push approval, or a hardware security key tap.

*Rationale (Rick):* if this is done poorly, the whole ransomware-protection promise fails. It's easy to make a secure flow more convenient later and hard to add security afterwards. So start gated everywhere and relax only with evidence.

*Not decided here:* conveniences that would weaken the gate, such as one approval unlocking a short window or approving the next run at the end of this one. Each needs its own review before adoption. See [credential-gating.md](../planning/credential-gating.md).

### D3. v1 isolation comes from an external drive air gap (hard decision)

v1's isolated copy is an **external drive**, written by a gated run and then disconnected. rclone already supports this (see [learning tutorial 03](../rclone-exploration/learning/03-backup-onedrive-to-external-ssd.md)). It needs no VintageVault server.

- **Control-plane service** (VV alone holds delete-capable tokens and refuses deletes inside the retention window): the **strongest post-v1 candidate**, but **not v1**. It brings central operating cost and makes Rick, as operator, responsible for its security promises. Reconsider it if usage grows. Credential gating (D2) is v1's cheap stand-in.
- **S3-style object lock (B2/Wasabi):** real WORM retention, but rejected for v1 because setting up a storage account and keys is too much friction for the target users.

### D4. Cadence, not new tiers

The chat's two-tier cadence maps onto the existing product ladder as **two cadences, not new paid tiers**:

| Cadence | What | Where |
|---|---|---|
| Frequent (e.g. weekly) | Gated same-account snapshots (ADR-003), with nag-then-fast-confirm reminders | Every user |
| Rare (about monthly) | Gated copy to the external drive, then disconnect | Every user who enrolls a drive (deferred enrollment, after first value) |

The chat's third idea, a separate synced working/index tier for cross-device use, is deferred to the [RAG add-on](../planning/rag-add-on.md).

### D5. Revenue model: open question (not decided)

The repo assumes freemium subscriptions ([monetization-strategy.md](../planning/monetization-strategy.md)). The design chat assumed shareware or donations. This is left open on purpose; no monetization docs changed. It matters here because a control plane (D3) would need recurring revenue to run.

## Threat model additions

These rows extend [ADR-001's threat model](001-system-architecture.md#threat-model):

| Threat | How VintageVault responds (v1) |
|---|---|
| **User's machine or VV agent compromised (commodity ransomware)** | No usable credentials at rest between runs (D2), so malware can't start a run or reuse a token silently. The external-drive copy is out of reach while disconnected (D3). Enough snapshots are retained that one poisoned run is an inconvenience, not a loss. |
| **Compromise during a gated run** | The attacker can act with that run's tokens while the run lasts. Same-account snapshots and the connected drive are exposed during the run; earlier disconnected drive copies are not. Mitigations: short-lived tokens, disconnect prompt at end of run, verifier separate from the writer (planning). |
| **Targeted attacker with kernel/admin access** | Out of scope. Stated honestly: VV aims to survive automated commodity ransomware, not a targeted attacker who owns the machine. |

## Consequences

**Positive**
- Malware that lands between runs finds no usable token, which closes the main gap in the ADR-001 isolation argument.
- No VintageVault server holds user credentials in v1, matching the privacy story ("your files never touch our servers") and avoiding operator custody.
- Runs entirely on the user's side, so v1 needs no new infrastructure.

**Negative / costs**
- **The free tier is no longer zero-effort.** Backups depend on the user responding to a prompt. Reminder UX (nag-then-fast-confirm) becomes core, not polish.
- **The Phase 1 plan in [same-account-pivot.md](../planning/same-account-pivot.md) no longer works as designed.** An Azure Function on a timer can't run without a user gesture. Phase 1 must be user-initiated (CLI or app prompt), and the "$0 server" claim stays true but for a different reason.
- **DPAPI alone doesn't satisfy D2.** Any process running as the user can decrypt a DPAPI-protected cache. The token cache (or the key that protects it) must be released only by Windows Hello, or refresh tokens must not be persisted at all. The CLI POC's current MSAL cache must change before it can be called "protected".
- The air gap depends on the user actually connecting the drive for the monthly run and disconnecting it afterwards.

## Open questions

1. **Gating mechanism:** use a Windows Hello-protected key (e.g. `KeyCredentialManager`, TPM-bound) to wrap the MSAL cache, or keep no refresh token and do an interactive sign-in each run? Check the effect on Graph sign-in prompts.
2. **Non-Windows / browser:** what's the equivalent gesture on macOS (Touch ID) and in a browser (WebAuthn/passkeys)? Relevant to the [auth model decision](../planning/cross-account-strategy.md#open-decision-auth-model-for-cross-account-transfer).
3. **Separate credentials for the drive tier:** encrypt the drive (BitLocker To Go or rclone `crypt`) with a secret that's separate from the cloud tokens?
4. **Control-plane feasibility (post-v1):** can OneDrive/Google Drive permissions separate write from delete (for example, a service-account-owned folder shared to the user)? A time-boxed spike when the trigger is met.
5. **Revenue model** (D5).
