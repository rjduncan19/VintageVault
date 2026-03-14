# Contributing to VintageVault

This guide covers how to set up a development environment and contribute to VintageVault.

## Development Environment Setup

VintageVault supports three development environments. Choose based on your workflow:

### **Option 1: Local VS Code (Recommended)**

Best for: Long development sessions, full IDE features, offline work.

**Prerequisites:**
- .NET 8 SDK
- Git
- VS Code (optional but recommended)

**Setup:**
```bash
git clone https://github.com/yourusername/VintageVault.git
cd VintageVault
code .
```

**Copilot CLI in Local Environment:**
```bash
# Install GitHub Copilot CLI (if not already installed)
gh extension install github/gh-copilot

# Start development with Copilot
gh copilot run --webui  # Opens browser-based chat
# OR
gh copilot explain "your question here"  # CLI-based
```

**VS Code Settings:**
- `.vscode/settings.json` is pre-configured for markdown editing, formatting, and preview
- Recommended extensions are listed in `.vscode/EXTENSIONS.md`
- Open any `.md` file and press `Cmd+Shift+V` (Mac) or `Ctrl+Shift+V` (Windows/Linux) to preview

---

### **Option 2: GitHub Codespaces**

Best for: Quick edits, testing across machines, no local setup required.

**Setup:**
1. Open the repo on GitHub
2. Click **Code** → **Codespaces** → **Create codespace on main**
3. Wait for the container to start (2-3 minutes)
4. VS Code opens in the browser

**Codespaces Configuration:**
- `.devcontainer/devcontainer.json` automatically installs recommended extensions
- `.vscode/settings.json` applies the same settings as local VS Code
- Your changes are always backed by Git

**Important:** Codespaces will suspend after 30 minutes of inactivity. Always commit your work to Git before closing the browser tab.

**Copilot CLI in Codespaces:**
```bash
# Inside the Codespace terminal
gh copilot run --webui  # Browser-based interface
```

---

### **Option 3: Copilot CLI (Terminal-Based)**

Best for: AI-assisted development, rapid iteration, lightweight workflows.

**Prerequisites:**
- GitHub CLI (`gh`) with Copilot extension
- Terminal/command prompt

**Setup:**
```bash
# Ensure you're in the repo directory
cd /path/to/VintageVault

# Start Copilot CLI development
gh copilot run --webui
```

**Usage:**
- Copilot CLI provides browser-based chat and command suggestions
- All your prompts are stored in `docs/prompts/README.md` (tracked in Git)
- Session state is persisted via Git commits (see `docs/DEVELOPMENT_PROCESS.md`)

**Important:** This is a **stateless** environment. Always commit your work to Git. Long-form sessions may be interrupted; save prompts and code frequently.

---

## Prompt Logging & Development History

All prompts and development decisions are logged in `docs/prompts/README.md`. This creates a permanent record of:
- What was asked and when
- Which Claude model generated each response
- What was built as a result
- How thinking evolved over time

**The System:**
- Every prompt is recorded with timestamp and model information
- SQL database (`prompts_log`) tracks prompts across sessions
- Markdown log in Git is the permanent, human-readable record
- Model changes are tracked (e.g., Claude Opus vs. Haiku) because they affect reasoning quality

See `docs/DEVELOPMENT_PROCESS.md` for details on how this works and how it ensures continuity across machines.

---

## Switching Between Environments

Since all work is synced via Git, you can switch environments seamlessly:

1. **In Environment A:** Make changes, commit to Git
   ```bash
   git add .
   git commit -m "Your work here"
   git push
   ```

2. **In Environment B:** Pull the latest changes
   ```bash
   git pull
   ```

3. **Continue working** in Environment B (local, Codespaces, or CLI)

**Best Practice:** Always commit before switching environments. This ensures your work is safe and accessible everywhere.

---

## Repository Structure

```
VintageVault/
├── README.md                       # Project overview (you are here)
├── CONTRIBUTING.md                 # This file
├── .vscode/                        # VS Code workspace settings
│   ├── settings.json               # Shared editor configuration
│   └── EXTENSIONS.md               # Recommended extensions
├── .devcontainer/                  # GitHub Codespaces configuration
│   └── devcontainer.json           # Container setup & auto-install
├── docs/                           # Project documentation
│   ├── DEVELOPMENT_PROCESS.md      # How Copilot remembers across sessions
│   ├── DEVELOPMENT_SETUP.md        # This file (dev environment guide)
│   ├── prompts/README.md           # Complete prompt history & decision log
│   ├── branding/                   # Logo and brand assets
│   │   ├── logo.svg                # Official logo (VV nested monogram)
│   │   └── BRAND-GUIDE.md          # Colors, typography, usage rules
│   ├── mockups/                    # Interactive HTML wireframes
│   │   ├── index.html              # Mockup navigation page
│   │   ├── 01-landing-page.html
│   │   ├── 02-setup-wizard.html
│   │   ├── 03-dashboard.html
│   │   └── 04-restore.html
│   ├── planning/                   # Product planning docs
│   │   ├── product-strategy.md
│   │   ├── gap-analysis.md
│   │   ├── backup-fundamentals.md
│   │   ├── landscape-2026.md
│   │   ├── competitive-analysis.md
│   │   ├── monetization-strategy.md
│   │   ├── pitch.md
│   │   ├── ux-wireframes.md
│   │   ├── architecture-reassessment.md
│   │   ├── open-core-strategy.md
│   │   └── business-plan.md
│   └── architecture/               # Architecture decision records (ADRs)
│       ├── 001-system-architecture.md
│       └── 002-data-movement-costs.md
└── .gitignore                      # Git ignore rules
```

---

## Key Documentation

Before you start, read these in order:

1. **[docs/DEVELOPMENT_PROCESS.md](docs/DEVELOPMENT_PROCESS.md)** — Understand how the Copilot-assisted workflow persists state across sessions
2. **[docs/prompts/README.md](docs/prompts/README.md)** — See the complete history of prompts and decisions that led to the current architecture
3. **[docs/architecture/001-system-architecture.md](docs/architecture/001-system-architecture.md)** — Understand the chosen hybrid architecture (web dashboard + desktop agent)
4. **[docs/planning/product-strategy.md](docs/planning/product-strategy.md)** — Understand the product, target users, and key scenarios

---

## Copilot CLI Best Practices

### Session Persistence
- The CLI is **stateless across sessions**. Your prompt and response history is preserved via Git logs and `docs/prompts/README.md`.
- Always commit your work before the session ends or if you switch machines.

### Model Changes
- The Copilot CLI may use different Claude models (Opus vs. Haiku). This is tracked in `docs/prompts/README.md` because model choice affects response quality.
- If you're doing deep architectural analysis, you may want to explicitly request Claude Opus for better reasoning.

### Prompt Logging
- Every prompt is automatically logged to `docs/prompts/README.md` and committed to Git.
- This creates a searchable, version-controlled record of the entire development journey.

---

## Troubleshooting

### "Codespaces suspended and I lost my session"
- **Solution:** Always commit to Git before closing the Codespaces browser tab. The container suspends after 30 minutes of inactivity.

### "I switched machines and my prompt history is gone"
- **Solution:** Pull the latest `docs/prompts/README.md` from Git. All prompts are stored there.

### "The Copilot CLI is not remembering my previous context"
- **Solution:** This is expected (stateless). Re-read the relevant `.md` files in `docs/` to recall context, or ask Copilot to summarize what was built before.

### "VS Code extensions aren't installed"
- **Local:** Click "Install Recommended Extensions" when VS Code prompts you
- **Codespaces:** Extensions auto-install from `.devcontainer/devcontainer.json`. Wait a moment for installation to complete.

---

## Code of Conduct

Be respectful, collaborative, and kind. We're building this together.

---

## Questions?

If you hit a snag with the development environment, check:
- `docs/DEVELOPMENT_PROCESS.md` — How the system works
- `docs/prompts/README.md` — What was tried before and why
- `.vscode/settings.json` & `.devcontainer/devcontainer.json` — Configuration details
