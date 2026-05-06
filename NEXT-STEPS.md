# 🧪 Next Step: Test Environment Setup

**You're here because the POC code is built and all 53 unit tests pass. Now we need a real OneDrive to test against.**

⚠️ **DO NOT use your personal OneDrive. Create a fresh test account.**

---

## Step 1: Create a Test Microsoft Account (~2 min)

1. Go to [outlook.com](https://outlook.com) → **Create free account**
2. Create: `vintagevault-test@outlook.com` (or similar)
3. Use a strong password, save it in your password manager
4. Complete signup (skip optional setup)
5. Go to [onedrive.live.com](https://onedrive.live.com) and sign in with the new account
6. You now have a free OneDrive with 5 GB — plenty for testing

---

## Step 2: Register an Entra App (~5 min)

1. Go to [portal.azure.com](https://portal.azure.com) — sign in with your **personal Microsoft account** (the one associated with an Azure subscription, OR the test account)
2. Search for **"App registrations"** → **New registration**
3. Fill in:
   - **Name:** `VintageVault POC`
   - **Supported account types:** "Personal Microsoft accounts only"
   - **Redirect URI:** Platform = "Public client/native (mobile & desktop)", URI = `http://localhost`
4. Click **Register**
5. On the overview page, copy the **Application (client) ID** — you'll need this
6. Under **Authentication**:
   - Enable **"Allow public client flows"** → Yes
   - Save
7. Under **API permissions**:
   - Click **Add a permission** → **Microsoft Graph** → **Delegated permissions**
   - Search and add: `Files.ReadWrite`
   - You should now have `User.Read` (default) and `Files.ReadWrite`

**Save the Application (client) ID.** You'll paste it into the code in Step 3.

---

## Step 3: Configure the Client ID in Code

Open `src/VintageVault.Cli/Graph/GraphClientFactory.cs` and replace the placeholder client ID:

```csharp
// Find this line:
private const string ClientId = "YOUR_CLIENT_ID_HERE";  // or similar placeholder

// Replace with your actual Application (client) ID:
private const string ClientId = "abcd1234-5678-9012-3456-abcdef012345";
```

---

## Step 4: Populate Test OneDrive with Data

When you start the next Copilot session, ask me to:

> "Generate test data in my test OneDrive account for POC validation"

I'll create a script that uploads a realistic file structure (~50-100 files, ~200 MB) including:
- Documents (varied sizes: 1 KB to 5 MB)
- Photos (JPEG files, 2-5 MB each)
- A large file (100 MB — tests size handling)
- Nested folder structure (3-4 levels deep)
- Files with special characters in names
- A `/Videos/` folder with a large file (to test exclusion filters)

This gives us enough variety to exercise all code paths.

---

## Step 5: Run the POC

```powershell
# Set up .NET SDK path (if not in your system PATH yet)
$env:PATH = "$HOME\.dotnet;$env:PATH"
cd <path-to>\VintageVault

# 1. Authenticate with your TEST account
dotnet run --project src\VintageVault.Cli -- auth

# 2. Set up a filter to exclude large videos
dotnet run --project src\VintageVault.Cli -- exclude /Videos

# 3. Run your first backup
dotnet run --project src\VintageVault.Cli -- backup

# 4. Check status
dotnet run --project src\VintageVault.Cli -- status

# 5. Modify some files in the test OneDrive, then run again
dotnet run --project src\VintageVault.Cli -- backup

# 6. List all snapshots
dotnet run --project src\VintageVault.Cli -- snapshots
```

---

## Step 6: Validate (Checklist from poc-spec.md)

After running, verify in the test OneDrive web UI:

- [ ] `/VintageVault-Backup/` folder was created
- [ ] Full snapshot folder exists with correct file structure
- [ ] `_snapshot.json` exists with file inventory and checksums
- [ ] `manifest.json` exists at backup root
- [ ] Incremental snapshot (after modifying files) only contains changed files
- [ ] Previous snapshots are untouched (immutable)
- [ ] Excluded `/Videos/` folder was skipped
- [ ] Skipped files are recorded in `_snapshot.json`

---

## Current POC Status

```
✅ 13 source files (2,344 lines C#)
✅ 53 unit tests (all passing)
✅ Builds clean (0 warnings, 0 errors)
✅ CLI responds to --help and --version
⏳ Needs test OneDrive account for integration testing
⏳ Needs Entra app registration for OAuth
```

---

## Quick Reference

| Item | Location |
|------|----------|
| POC spec | `docs/poc-spec.md` |
| Source code | `src/VintageVault.Cli/` |
| Unit tests | `tests/VintageVault.Tests/` |
| Full validation checklist | `docs/poc-spec.md` → "Validation Script" section |
| CLI mockups | `docs/mockups/poc/cli-mockup.html` |
| Mission statement | `MISSION.md` |
| Business plan | `docs/planning/business-plan.md` |
