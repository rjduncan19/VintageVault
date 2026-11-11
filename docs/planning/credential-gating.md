# Credential Gating, Control Plane, and Writer Hardening

_Created: 2026-11-11 | Source: [design chat handoff](2026-11-design-chat-handoff.md), sections 3.1–3.3_

> **Decisions are in [ADR-005](../architecture/005-credential-gating-and-isolation.md).** In short: every run is gated by a user gesture (hard decision), the v1 isolated copy is an external drive (hard decision), and the control plane is the post-v1 candidate. This doc keeps the supporting design material and the options not yet chosen.

---

## 1. Why gating

A process that holds credentials all the time is the thing ransomware wants: it can start a "backup" that overwrites or deletes the good copies. Consumer clouds have no object lock, so the protection has to come from **when credentials are usable**, not from what the storage will refuse.

Goal: **between runs, nothing on the machine can use VintageVault's credentials.** A DPAPI-encrypted cache doesn't meet this goal, because any process running as the user can decrypt it (see ADR-005, Consequences).

## 2. Gating UX (keep friction low)

| Mechanism | Effort | Why it resists malware | Status |
|---|---|---|---|
| **Windows Hello** (face, fingerprint, PIN) | ~1 second | TPM-backed; a process can't trigger it silently | **Primary (ADR-005 D2)** |
| **Phone push approval** | A tap on the phone | An attacker on the PC can't reach the phone | Alternative |
| **Hardware security key tap** | A tap | Physical presence required | Alternative |
| One approval unlocks a short window (e.g. an hour) | Lower | Weaker: anything running in the window can use it | **Needs review** before adoption |
| Approve the next run at the end of the current one | Lowest | Weaker: pre-authorizes a future run | **Needs review** before adoption |

### Reminders: nag-then-fast-confirm
Gated backups must not depend on the user remembering. When a run is due, VV shows a reminder; confirming takes one gesture. If the user ignores it, the reminders escalate gently (e.g. a dashboard banner: "Last protected backup: 12 days ago"). The aim is that the gesture is the only work involved.

### Deferred enrollment (decided in the chat)
- **First run needs zero setup.** The user connects OneDrive and gets their first snapshot.
- The harder step (enrolling an external drive, later a second account) is asked for **only after the user has seen value**, ideally right after a successful restore drill (see [adoption-and-trust.md](adoption-and-trust.md)).

## 3. Control-plane service (post-v1 candidate)

**Idea:** VintageVault brokers OAuth and keeps the only delete-capable token. For each run, the client gets a short-lived, narrow **write** token and uploads directly. VV never sees file data. Deletes inside the retention window go through VV's policy check and are refused.

| | |
|---|---|
| **Strengths** | Real retention enforcement on consumer clouds. Cheap to run (small API calls, no bulk transfer). A breach exposes tokens, not documents. |
| **Costs** | VV becomes a credential custodian and a dependency. Central operating cost. The operator (Rick) takes on its security promises. |
| **Unverified** | Whether OneDrive/Google Drive permissions can separate write from delete. One avenue: a service-account-owned folder shared to the user with write-only rights. |
| **Compare** | [ADR-002 "Cloud Relay"](../architecture/002-data-movement-costs.md#what-about-a-future-cloud-relay-option) relays data. This is a lighter variant that relays only authorization. |
| **Trigger to revisit** | Usage grows enough to justify central ops cost and fund it (see the open revenue question in ADR-005 D5). |

## 4. Hardening against a compromised writer

Assume the writer is hostile. The realistic goal is to **survive automated commodity ransomware**. A targeted attacker with kernel access wins anyway, and the docs should say so.

- **Verifier separate from the writer.** A different process (ideally different credentials) checks the snapshot after it's written: hashes against source, entropy and header spot-checks (ADR-004), and file counts. A poisoned run should fail verification, not quietly become the newest "good" snapshot.
- **Signed binaries**, and a protected process where Windows allows it.
- **Controlled Folder Access** around any local snapshot store (relevant to future local-PC scope, ADR-005 D1).
- **Enough retained versions** that one poisoned run is an inconvenience. ADR-004's keep-everything default already provides this.
- **Disconnect prompt** at the end of each external-drive run, so the air gap is real.
