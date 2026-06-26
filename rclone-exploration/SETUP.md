# SETUP — Completing the live cloud prototypes

The two `blocked` todos (`prototype-cross-provider`, `prototype-same-provider-gdrive`)
need **interactive OAuth with fresh TEST accounts** — the one step that must be done by
a human with a browser. Everything else is built, validated, and turnkey.

> ⚠️ Use **fresh test accounts** only (see the repo's `NEXT-STEPS.md`). Never personal data.

## 0. Prereqs
- rclone is installed at `~/.rclone-bin/rclone.exe` (v1.74.3, validated).
- A test OneDrive account, and **two** test Google Drive accounts.

## 1. Configure remotes (interactive OAuth, ~5 min)

```powershell
$rclone = "$env:USERPROFILE\.rclone-bin\rclone.exe"
$cfg    = ".\prototype\config\rclone.conf"   # git-ignored; tokens live here

# OneDrive source  (choose: personal OneDrive)
& $rclone config --config $cfg create onedrive_src onedrive

# Google Drive destination
& $rclone config --config $cfg create gdrive_dest drive

# Google Drive source (second Google account, for same-provider test)
& $rclone config --config $cfg create gdrive_src drive
```
Each opens a browser for consent. Sign in with the corresponding **test** account.

Verify:
```powershell
& $rclone --config $cfg listremotes      # -> onedrive_src:  gdrive_dest:  gdrive_src:
```

## 2. Run prototype A — OneDrive → Google Drive (cross-provider)

```powershell
# Preview first (zero data movement):
.\prototype\scripts\backup-cross-provider.ps1 -DryRun

# Real run, capture evidence:
.\prototype\scripts\backup-cross-provider.ps1 *>&1 |
  Tee-Object .\findings\results\cross-provider.output.txt
```
Expected: rclone downloads from OneDrive and uploads to Google Drive via this machine
(no server-side transfer — matches ADR-002). Then:
```powershell
# mark done
# (run the sqlite update your tooling uses, or:)
#   UPDATE todos SET status='done' WHERE id='prototype-cross-provider';
```

## 3. Run prototype B — Google → Google (same-provider, server-side)

```powershell
.\prototype\scripts\backup-gdrive-server-side.ps1 -DryRun
.\prototype\scripts\backup-gdrive-server-side.ps1 *>&1 |
  Tee-Object .\findings\results\gdrive-server-side.output.txt
```
Expected: with `--drive-server-side-across-configs`, Google copies bytes on its own
infrastructure — **no local download/upload**. Confirm by watching that transfer stats
show server-side copies (near-zero local bandwidth).

> Note: server-side copy needs the destination account to have access to the source
> files. If a copy falls back to download+upload, share the source folder with the
> destination account (or use the same Google identity across both remotes).

## 4. (Optional) Drive a real backup via the rc API
```powershell
.\prototype\scripts\rc-wrapper.ps1 -SrcFs "onedrive_src:" -DstFs "gdrive_dest:VintageVault/agent-test" -ConfigPath .\prototype\config\rclone.conf
```

## 5. Update the todo board
```sql
UPDATE todos SET status='done' WHERE id IN ('prototype-cross-provider','prototype-same-provider-gdrive');
```

When both runs are captured in `findings/results/`, the spike is 100% complete and the
draft ADR in `findings/recommendation.md` can be promoted to `docs/architecture/`.
