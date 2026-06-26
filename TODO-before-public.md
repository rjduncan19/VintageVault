# TODO Before Making VintageVault Public

*Created May 5, 2026 — pick this up in a VV-specific Copilot session.*

**Context:** VintageVault is currently a private GitHub repo (`rjduncan19/VintageVault`). Making it public is the single highest-leverage move for Rick's Anthropic application — the MISSION.md reads like an Anthropic engineer wrote it (Patagonia-inspired, mission-first, open-by-default, "if we succeed we disappear"). But before flipping the visibility switch, the items below need to be addressed.

**Estimated time: ~2 hours of careful work.**

---

## 🔴 Must Do (Security & IP)

- [ ] **Scan for secrets** — `git secrets` is not a built-in git command (it's AWS Labs tooling). Use one of these instead:
  - **Option A (recommended): gitleaks** — modern, fast, well-maintained. Install via `winget install gitleaks` or `choco install gitleaks`, then run `gitleaks detect --source . --verbose`. Detects API keys, tokens, connection strings, etc. across the full git history by default.
  - **Option B: trufflehog** — also popular. Install via `pip install truffleHog` or `winget install trufflesecurity.trufflehog`, then `trufflehog git file://.`.
  - **Option C: GitHub native secret scanning** — kicks in automatically once the repo is public (free for public repos). Push first, scan happens in background, view results under repo Settings → Code security. **Risk:** if a secret IS in history, it's already public by the time GitHub finds it. Use only as a backstop, not as your primary scan.
  - **Option D (manual fallback): grep for common patterns** — `git --no-pager grep -niE "(api[_-]?key|secret|password|token|connectionstring|aws_access|client_secret|bearer)" $(git rev-list --all)` — crude but works without installing anything.
  - 💡 Run scan against **full history**, not just working tree. If you find a secret in history, you must rewrite history (e.g., `git filter-repo`) or rotate the secret before publishing — pushing a "fix" commit on top doesn't remove the secret from earlier commits.
- [ ] **Search for Microsoft references** — `git --no-pager grep -i microsoft` and `git --no-pager grep -i rjduncan19@microsoft`. Remove or anonymize anything tying VV to Microsoft work. VV is YOUR project, not a Microsoft project.
- [ ] **Verify the LICENSE file is at the repo root** — Apache 2.0 (matches MISSION.md statement). If missing, add it before public. License is what makes "open source" actually open source.
- [ ] **Verify nothing in the repo references SAL** — different project, different IP. If there's any cross-pollination, sever it now.
- [ ] **Verify .gitignore covers any local config files** that might contain secrets in normal development (settings.local.json, .env, *.pfx, etc.).
- [ ] **Verify the git author email on commits is your personal email**, not your Microsoft email. Check with `git --no-pager log --format='%ae' | sort -u`. If it's the work email, decide whether to rewrite history (loud but cleaner) or amend going forward (quieter but mixed).

## 🟡 Should Do (First Impression)

- [ ] **README pass for typos and broken links** — the README is the front door. Read it out loud. Click every link.
- [ ] **MISSION.md pass for typos and clarity** — same standard. This document is the values story; it has to be tight.
- [ ] **Add a clear "Status" line near the top of README** — explicitly state "Pre-MVP, planning & architecture phase." Honest > impressive. Anthropic engineers hate vaporware claims; honest pre-MVP is fine.
- [ ] **Verify the docs/ folder doesn't reference internal links** (microsoft.sharepoint.com, internal Teams meetings, etc.).
- [ ] **Verify branding/logo files are yours** (created by you or properly licensed). If AI-generated, that's fine — just don't claim a designer made them.
- [ ] **Check CONTRIBUTING.md** — make sure it's accurate and doesn't reference Microsoft tooling (Azure DevOps, internal services).
- [ ] **Check the .devcontainer config** — make sure it doesn't reference internal Microsoft container registries or auth.

## 🟢 Nice to Have (Discoverability)

- [ ] **Pin the repo on your GitHub profile** (after going public).
- [ ] **Add a clear topic list** (`gh repo edit --add-topic backup,ransomware,opensource,patagonia,oauth,onedrive,google-drive`) — helps GitHub discovery surface it.
- [ ] **Add a one-line repo description** in GitHub settings that matches the README headline.
- [ ] **Set up GitHub branch protection** on `main` so accidental pushes don't break things while it's in early state.

## ❌ Do NOT Do

- ❌ **Don't add features before going public.** The mission story is the asset; feature completeness is not. Rick's Anthropic application timing matters more than V1 of any feature.
- ❌ **Don't fake stars, forks, or contributors.** Anthropic recruiters can spot this and it's the worst kind of risk.
- ❌ **Don't tweet about it widely after going public.** Discoverable but not promotional. Quiet integrity > loud marketing.
- ❌ **Don't blog about VV being your "Anthropic application project."** It's a real project with a real mission. Treat it like one.
- ❌ **Don't link VV to SAL anywhere.** Different project, different IP boundaries.

---

## When You're Done

1. Verify each ✅ Must-Do is checked
2. Open `gh repo edit rjduncan19/VintageVault --visibility public --accept-visibility-change-consequences` (or do it via the GitHub web UI)
3. Confirm the URL works in an incognito window: `https://github.com/rjduncan19/VintageVault`
4. Update the running status in `C:\Users\richardd\OneDrive - Microsoft\Career\Security\00-running-status.md`:
   - Mark this TODO as done
   - Add VV link to the Anthropic application checklist
5. Update Anthropic cover letter v3 to include the values sentence referencing VintageVault (currently dropped because the repo was private — see `34-anthropic-cover-letter-draft.md`)
6. Update Anthropic resume "Public Work & Community" section with the VV link

---

## Why This Matters (Brief)

Rick is applying to Anthropic for a Staff+ Software Security Engineer role. Anthropic hires hard on values alignment (95% Glassdoor recommend, 80% retention, 95% offer acceptance — all values-driven numbers). VV's mission document maps directly to Anthropic's PBC structure, RSP, and "open by default" engineering culture.

**Public VV = verifiable values. Private VV = a story that can't be checked.**

The Anthropic application opportunity is short-window. Don't perfect this — just get it past the bar and shipped.

📄 `C:\Users\richardd\source-rjduncan19\VintageVault\TODO-before-public.md`
