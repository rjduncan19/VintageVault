# Optional Add-On: Local Semantic Search Over Your Files (RAG)

_Created: 2026-11-11 | Status: Idea, not scheduled | Source: [design chat handoff](2026-11-design-chat-handoff.md), section 3.6_

## The idea

A backup run already walks the user's files, so it could build a **personal semantic index** at the same time. The user could then ask questions of their own documents ("what did the plumber quote us in 2024?").

- **Nothing leaves the user's security domain.** Extraction, embedding and the index stay on the user's machine.
- **Optional add-on**, not part of the lean core. The backup engine must never depend on it.
- No competitor offers local semantic search over backups (see [competitive-analysis.md](competitive-analysis.md#gaps-noted-in-the-nov-2026-design-chat)).

## Fit with VintageVault's architecture (constraint)

VintageVault v1 is **cloud-to-cloud**, and same-account snapshots use **server-side `driveItem: copy`**. The bytes never pass through the user's machine, so **there is nothing to index during a pure server-side copy**.

Indexing is only possible where file contents actually pass through VV:
- a **desktop agent** doing cross-account or external-drive copies ([ADR-005 D3](../architecture/005-credential-gating-and-isolation.md#d3-v1-isolation-comes-from-an-external-drive-air-gap-hard-decision)), where rclone streams the bytes locally
- future **local-PC backup** ([ADR-005 D1](../architecture/005-credential-gating-and-isolation.md#d1-local-pc-backup-is-future-scope-built-on-rclone))

Otherwise the add-on would need its own download pass, which costs bandwidth and needs its own gated credentials. So this add-on naturally follows the desktop agent, not v1.

## Design sketch

### What the index stores
- Chunks, their vectors, and a pointer: **path + content hash**.
- **Not the text.** Text is re-read from the original file when a question is answered, so the index alone leaks less.
- Size: about 3 KB per 768-dimension float32 vector, under 1 KB at int8. Roughly **low single-digit GB per 10 GB of real documents**.

### v1 scope of the add-on
Documents, spreadsheets, PDFs, email archives, code. **Skip media and binaries.**

### Stack
| Part | Choice | Notes |
|---|---|---|
| Inference | **ONNX Runtime** | NPU first, then DirectML, then CPU |
| Embedding model | Sentence-transformer exported to ONNX (**BGE-small** or **MiniLM**), int8 | Small enough for laptops |
| Vector store | **LanceDB** (embedded) | Real upserts and deletes. Avoid FAISS (poor at incremental updates). |
| Text extraction | Apache Tika or `unstructured` | |

### Incremental refresh
Keyed by **path + content hash**, driven by the **backup run's change list**. The delta API (or rclone's transfer list) already says what changed, so the index re-embeds only those files and drops deleted ones.

### Windows Search integration (local files only)
- **Fast path:** consume Windows indexer change notifications via COM (`ISearchCrawlScopeManager` and related interfaces).
- **Fallback:** VV's own crawler, for NAS, mapped drives, or machines with indexing turned off.
- **Don't** embed inside an indexer filter (IFilter): timeouts and threading constraints, and it degrades system search.

### Cross-device
Options: sync the index through a working tier, or a primary machine or NAS answers queries over a private network (e.g. Tailscale). The chat's "working/index tier" from [ADR-005 D4](../architecture/005-credential-gating-and-isolation.md#d4-cadence-not-new-tiers) lands here.

## Open questions

1. **Respect Windows Search exclusions?** Users often exclude folders for privacy. Backup and search are different intents ("keep this safe" vs. "let me find this"), so this is probably an explicit wizard choice rather than a silent default.
2. **Gating:** does querying the index need a gesture, like backup runs do ([ADR-005 D2](../architecture/005-credential-gating-and-isolation.md#d2-every-backup-run-needs-a-user-gesture-hard-decision))? The index is a sensitive summary of the user's files.
3. **Packaging:** ONNX models and runtime add tens of MB. A separate download keeps the core lean.
4. **Which answer model?** Retrieval is local, but generating an answer needs a language model. A local small model keeps the "nothing leaves" promise; a cloud model would break it unless the user opts in explicitly.
