# 01 — Installation & First Config

> **Goal:** Install rclone on Windows, verify it works, and walk through your first remote configuration interactively.

---

## Installing rclone on Windows

### Option A: winget (Recommended)

Windows Package Manager is the simplest path:

```powershell
winget install Rclone.Rclone
```

After installation, open a **new** terminal and verify:

```powershell
rclone version
```

Expected output:
```
rclone v1.68.2
- os/version: windows (amd64)
- os/kernel: 10.0.26100.0 (x86_64)
- os/type: windows
- os/arch: amd64
- go/version: go1.23.3
- go/linking: static
- go/tags: none
```

### Option B: Manual Download

1. Go to [https://rclone.org/downloads/](https://rclone.org/downloads/)
2. Download the **Windows - AMD64** zip
3. Extract to `C:\Tools\rclone\`
4. Add `C:\Tools\rclone\` to your `PATH`:

```powershell
# Add to user PATH permanently
[Environment]::SetEnvironmentVariable(
    "PATH",
    "$env:PATH;C:\Tools\rclone",
    "User"
)
```

5. Restart your terminal and verify with `rclone version`

### Option C: Chocolatey

```powershell
choco install rclone
```

---

## Understanding the Config File Location

On Windows, rclone stores its configuration at:

```
%APPDATA%\rclone\rclone.conf
```

Which resolves to something like:
```
C:\Users\YourName\AppData\Roaming\rclone\rclone.conf
```

You can always check where rclone is looking:

```powershell
rclone config file
```

Output:
```
Configuration file is stored at:
C:\Users\richardd\AppData\Roaming\rclone\rclone.conf
```

> **Security note:** This file contains OAuth tokens equivalent to passwords. It's created with restricted permissions automatically. Never share or commit this file.

---

## Running `rclone config` for the First Time

The `rclone config` command launches an interactive wizard. Let's walk through adding a generic OneDrive remote using rclone's **built-in shared app** (we'll replace this with a custom app in the next tutorial — but let's see the full flow first).

```powershell
rclone config
```

You'll see:

```
No remotes found, make a new one?
n) New remote
s) Set configuration password
q) Quit config
n/s/q> n
```

Type `n` and press Enter.

```
name> onedrive-personal
```

Enter a memorable name (no spaces, use hyphens).

```
Type of storage to configure.
...
XX / Microsoft OneDrive
   \ (onedrive)
...
Storage> onedrive
```

Type `onedrive` (or the number next to it).

```
Option client_id.
OAuth Client Id.
Leave blank normally.
Enter a value. Press Enter to leave empty.
client_id>
```

**Press Enter** to use rclone's shared client ID for now. (We'll change this in Tutorial 02.)

```
Option client_secret.
OAuth Client Secret.
Leave blank normally.
Enter a value. Press Enter to leave empty.
client_secret>
```

**Press Enter** again.

```
Edit advanced config?
y) Yes
n) No (default)
y/n> n
```

Press Enter for No.

```
Use web browser to automatically authenticate rclone with remote?
 * Say Y if the machine running rclone has a web browser you can use
 * Say N if running rclone on a headless machine
y) Yes (default)
n) No
y/n> y
```

Type `y`. Your browser will open and prompt you to sign in to Microsoft.

Sign in with your OneDrive account. After authorization, your browser shows:
```
Success!
All done. Please go back to rclone.
```

Back in the terminal:

```
Choose a number from below, or type in your own string value.
 1 / OneDrive Personal or Business
   \ (onedrive)
 2 / Root Sharepoint site
   ...
Your choice> 1
```

Choose option 1 for a personal OneDrive.

```
Found 1 drives, please select the one you want to use:
0: OneDrive (personal) id=b!abc123...
Chose drive to use:> 0
```

Select `0`.

```
Is this okay?
y) Yes (default)
n) No
y/n> y
```

Press Enter. Your remote is configured.

```
Current remotes:

Name                 Type
====                 ====
onedrive-personal    onedrive

e) Edit existing remote
n) New remote
d) Delete remote
r) Rename remote
c) Copy remote
s) Set configuration password
q) Quit config
e/n/d/r/c/s/q> q
```

Type `q` to exit.

---

## Verifying Your First Remote

Now test that rclone can see your OneDrive:

### List the root of your OneDrive

```powershell
rclone lsd onedrive-personal:
```

Output (example):
```
          -1 2024-01-15 09:23:00        -1 Documents
          -1 2024-03-20 14:11:00        -1 Photos
          -1 2024-06-01 08:00:00        -1 Desktop
          -1 2025-01-10 16:45:00        -1 Videos
```

`lsd` lists **directories** only (no files). This is a quick sanity check.

### List files in a folder

```powershell
rclone ls onedrive-personal:Documents
```

Shows files with sizes:
```
    12345 Budget.xlsx
    98765 Resume.docx
  1234567 ProjectReport.pdf
```

### Check your storage quota

```powershell
rclone about onedrive-personal:
```

Output:
```
Total:   5.368 GiB
Used:    2.134 GiB
Free:    3.234 GiB
```

### List files with modification times

```powershell
rclone lsl onedrive-personal:Documents
```

```
    12345 2025-06-15 10:22:33.000000000 Budget.xlsx
    98765 2025-05-01 09:10:00.000000000 Resume.docx
```

---

## Dry-Run Your First Copy

Before copying anything for real, always use `--dry-run` to see what rclone *would* do:

```powershell
# Preview what would be copied from Documents to a local folder
rclone copy onedrive-personal:Documents C:\temp\test-backup --dry-run --progress
```

Output:
```
2026/06/30 10:00:00 NOTICE: Budget.xlsx: Not copying as --dry-run is set (size 12.1 KiB)
2026/06/30 10:00:00 NOTICE: Resume.docx: Not copying as --dry-run is set (size 96.4 KiB)
2026/06/30 10:00:00 NOTICE: ProjectReport.pdf: Not copying as --dry-run is set (size 1.177 GiB)
```

No files are touched. This is safe to run any time.

---

## Understanding `--progress` Output

The `--progress` flag shows a real-time dashboard. Here's what each field means:

```
Transferred:   1.234 GiB / 5.678 GiB, 22%, 45.678 MiB/s, ETA 1m45s
Checks:            42 / 150, 28%
Transferred:        8 / 150, 5%
Elapsed time:  45.0s
Transferring:
 *                      Budget.xlsx:  45% /12.1 KiB, 0/s, -
 *                      Resume.docx:  78% /96.4 KiB, 0/s, -
```

| Field | Meaning |
|-------|---------|
| `Transferred` (top) | Bytes transferred / total bytes |
| `Checks` | Files compared (checksums) |
| `Transferred` (bottom) | Files fully transferred |
| `Transferring` | Currently active transfers |
| `ETA` | Estimated time remaining |

---

## Viewing and Editing the Config Directly

The config file is plain text — you can view and edit it:

```powershell
# Open in Notepad
notepad "$env:APPDATA\rclone\rclone.conf"

# Or view in terminal
Get-Content "$env:APPDATA\rclone\rclone.conf"
```

Example content after setup:

```ini
[onedrive-personal]
type = onedrive
token = {"access_token":"eyJ0eXAiOiJKV1Qi...","token_type":"Bearer","refresh_token":"0.AQIA...","expiry":"2026-07-01T10:00:00.00000-07:00"}
drive_id = b!Abc123XYZ...
drive_type = personal
```

Notice: No `client_id` or `client_secret` here — it's using rclone's built-in shared app credentials. **We'll change this in the next tutorial.**

---

## Common First-Time Problems

### "rclone: command not found"
The install directory isn't in your PATH. Restart your terminal, or manually add the folder to PATH.

### Browser doesn't open for OAuth
Run with `--auth-no-open-browser` and it will print a URL for you to paste manually:
```powershell
rclone config reconnect onedrive-personal: --auth-no-open-browser
```

### Token expired / unauthorized error
Re-authorize the remote:
```powershell
rclone config reconnect onedrive-personal:
```

### Config file location is wrong
Override with environment variable:
```powershell
$env:RCLONE_CONFIG = "C:\MyBackup\rclone.conf"
rclone config
```

---

## What's in the Config That Matters

| Key | Purpose |
|-----|---------|
| `type` | Backend type (`onedrive`, `drive`, `s3`, etc.) |
| `token` | OAuth access + refresh tokens (treat like a password) |
| `drive_id` | The specific OneDrive drive being accessed |
| `drive_type` | `personal` or `business` |
| `client_id` | Your Azure app ID (blank = rclone shared app) |
| `client_secret` | Your Azure app secret (blank = rclone shared app) |

---

## Next Steps

Using rclone's shared client app is fine for learning, but for production use you should register your own Azure application. This gives you:
- Your own API quota (not shared with all rclone users)
- Full control over permissions
- No dependency on rclone's Microsoft partnership continuing

Continue to → [02 — OneDrive Custom App Setup](02-onedrive-custom-app-setup.md)
