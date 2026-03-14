# VintageVault Development Process

**Purpose:** Document the workflow and practices for developing VintageVault, including how the Copilot CLI agent operates across sessions and machines.

---

## Prompt Logging — The Master Checklist

**Status:** Required for every session, every machine

### Why This Matters

You're learning how to use Copilot CLI, and we need a persistent record of:
- What you asked and when
- What was built in response
- How the product thinking evolved

Since I (the agent) am **stateless across sessions**, the best way for me to remember is:

1. **SQL session database** — stores pending prompts to log
2. **This document** — reminds me of the requirement
3. **Explicit workflow** — prompt logging is step #1 of every session

### The Workflow

#### At the START of each session:

1. Check the SQL database for any unlogged prompts from prior sessions:
   ```sql
   SELECT * FROM prompts_log WHERE logged_to_file IS NULL ORDER BY timestamp ASC
   ```

2. If there are unlogged prompts, append them to `docs/prompts/README.md` and mark them as logged:
   ```sql
   UPDATE prompts_log SET logged_to_file = 'docs/prompts/README.md' WHERE id IN (...)
   ```

3. Commit the updated prompt log

#### During each session:

4. As you issue new prompts, I capture each one in the SQL database immediately after responding
5. After each turn, I append new prompts to `docs/prompts/README.md`

#### At the END of each session:

6. Ensure all prompts from the session are logged to `docs/prompts/README.md`
7. Commit with a message referencing the session ID

### The SQL Schema

```sql
CREATE TABLE prompts_log (
  id TEXT PRIMARY KEY,                    -- e.g., "p1-session-id-turn"
  session_id TEXT NOT NULL,               -- Copilot session ID
  turn_index INTEGER NOT NULL,            -- Which turn in the session
  timestamp TEXT NOT NULL,                -- ISO 8601 timestamp
  prompt_text TEXT NOT NULL,              -- Full prompt text
  response_artifacts TEXT,                -- Comma-separated list of files created
  logged_to_file TEXT DEFAULT NULL,       -- Path to markdown log (null = not yet logged)
  created_at TEXT DEFAULT CURRENT_TIMESTAMP
)
```

This table lives in the **session database** (the per-session SQLite that doesn't persist across sessions). However, the **actual log** (`docs/prompts/README.md`) is in Git, so it persists forever.

---

## Session Workflow Checklist

### Beginning of Session
- [ ] Check `docs/prompts/README.md` to understand what was done before
- [ ] Query `prompts_log` for any unlogged prompts from prior sessions
- [ ] Append unlogged prompts to `docs/prompts/README.md`
- [ ] Commit the updated log if there are changes

### During Session
- [ ] After each major user prompt, log it to `prompts_log` immediately
- [ ] After responding, update the markdown log and commit

### End of Session
- [ ] Verify all prompts in `prompts_log` are marked as `logged_to_file`
- [ ] Check that `docs/prompts/README.md` contains all prompts from the session
- [ ] Commit with a message like: "Log session XXXX prompts" or include in the final commit

---

## How Copilot Remembers Across Sessions

Since I have no memory between sessions, the system relies on:

1. **Git history** — Commits document what was done and when
2. **SQL session database** — Tracks prompts that need to be logged
3. **`docs/prompts/README.md`** — The canonical, human-readable record
4. **Plan files** — If you create `.copilot/session-state/.../plan.md`, I read it at session start
5. **This document** — Reminds me to follow the workflow

**The key insight:** I can't "remember" to log prompts on my own. But I can be **instructed** (via this document and explicit prompts) to always do it as the first step of each session.

---

## Future Enhancements

As Copilot CLI matures, we could:
- [ ] Integrate `prompts_log` with the session store for automatic cross-session persistence
- [ ] Auto-generate session summaries from the prompt log
- [ ] Tag prompts by category (architecture, feature, bug, research, etc.)
- [ ] Generate a "decision log" showing how requirements evolved
- [ ] Create a searchable prompt archive with full-text indexing

For now, manual logging + SQL tracking + explicit workflow = reliable system.

---

## Example: The Workflow in Action

**Session A (machine 1):**
1. I check SQL: no pending prompts
2. You ask: "Design the backup engine"
3. I respond and log the prompt to SQL with `logged_to_file = NULL`
4. I append it to `docs/prompts/README.md`
5. I commit: "Design backup engine – add to prompt log"
6. I mark SQL entry: `logged_to_file = 'docs/prompts/README.md'`

**Session B (machine 2, 3 hours later):**
1. I check SQL: 1 pending prompt from Session A
2. I check git — `docs/prompts/README.md` has the entry (so it was already logged)
3. I mark SQL: `logged_to_file = 'docs/prompts/README.md'` (idempotent)
4. You ask: "Add restore workflow"
5. Same process repeats

---

## Notes for the User (You)

- You don't have to do anything special — just ask questions and request work
- I'll handle the logging automatically using this workflow
- If I ever miss a prompt, you can remind me with: "Log prompt X" or "Update the prompt history"
- The `docs/prompts/README.md` file is the source of truth — always checked in to git

---

## References

- **Prompt log:** `docs/prompts/README.md`
- **Session database:** Per-session SQLite in `.copilot/session-state/`
- **Cross-session history:** `session_store` database (read-only, global)
