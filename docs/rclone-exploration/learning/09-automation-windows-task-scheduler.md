# 09 — Automation on Windows with Task Scheduler

> **Goal:** Schedule rclone backups to run automatically on a cadence, with logging, error handling, and optional notifications — all without any server or always-on process.

---

## Why Task Scheduler?

Windows Task Scheduler is the right tool for periodic backup jobs:

```
┌─────────────────────────────────────────────────────────────────┐
│              Why Task Scheduler for rclone                      │
│                                                                 │
│  ✅ Built into every Windows PC — no software to install        │
│  ✅ Runs whether or not you're logged in (with right settings)  │
│  ✅ Triggers on: schedule, system events, user login, idle       │
│  ✅ Automatic retry on failure                                   │
│  ✅ Email/run program on failure                                 │
│  ✅ Can run with highest privileges for network drives           │
│  ✅ Survives reboots                                             │
└─────────────────────────────────────────────────────────────────┘
```

---

## Backup Script Architecture

Before scheduling, build a solid PowerShell script that handles everything:

```
run-backup.ps1
├── Log start time and parameters
├── Check if external SSD is connected (if backing up to SSD)
├── Run rclone backup
├── Check exit code
├── Run rclone verify
├── Log summary (files transferred, errors, duration)
├── Rotate old log files
└── Exit with appropriate code
```

---

## Step 1: The Master Backup Script

Save as `C:\Backup\run-backup.ps1`:

```powershell
<#
.SYNOPSIS
    Automated rclone backup for VintageVault
.DESCRIPTION
    Runs OneDrive backup with full logging, verification, and error handling.
    Designed to be called from Windows Task Scheduler.
.PARAMETER Profile
    Which backup profile to run: "ssd", "cloud", or "both"
.PARAMETER DryRun
    If set, runs in dry-run mode (no files transferred)
#>

param(
    [ValidateSet("ssd", "cloud", "both")]
    [string]$Profile = "cloud",
    [switch]$DryRun
)

# ── Configuration ────────────────────────────────────────────────
$Config = @{
    SourceRemote   = "onedrive-personal"
    BackupRemote   = "onedrive-backup"
    SsdPath        = "E:\OneDrive-Backup"
    BackupRoot     = "VaultBackup"
    FilterFile     = "C:\Backup\rclone-onedrive-filters.txt"
    LogDir         = "C:\Backup\logs"
    MaxLogFiles    = 60      # ~2 months of daily logs
    MaxSnapshots   = 8       # Keep 8 weekly snapshots
    BwLimit        = "0"     # No limit (set to "10M" to cap at 10 MB/s)
    Transfers      = 4
    TpsLimit       = 4       # Graph API transactions per second
}

# ── Initialization ───────────────────────────────────────────────
$startTime = Get-Date
$datestamp = $startTime.ToString("yyyy-MM-dd")
$timestamp = $startTime.ToString("yyyy-MM-dd_HH-mm-ss")
$logFile = "$($Config.LogDir)\backup-$timestamp.log"
$exitCode = 0

New-Item -ItemType Directory -Path $Config.LogDir -Force | Out-Null

function Log {
    param([string]$Message, [string]$Level = "INFO")
    $line = "$((Get-Date).ToString('HH:mm:ss')) [$Level] $Message"
    Add-Content -Path $logFile -Value $line
    if ($Level -eq "ERROR") {
        Write-Host $line -ForegroundColor Red
    } elseif ($Level -eq "WARN") {
        Write-Host $line -ForegroundColor Yellow
    } else {
        Write-Host $line
    }
}

function Invoke-Rclone {
    param([string[]]$Args)
    $cmd = "rclone " + ($Args -join " ")
    Log "Running: $cmd"
    & rclone @Args
    return $LASTEXITCODE
}

# ── Header ───────────────────────────────────────────────────────
Log "══════════════════════════════════════"
Log "  VaultBackup — $(if ($DryRun) { 'DRY RUN' } else { 'LIVE' })"
Log "  Profile: $Profile"
Log "  Started: $startTime"
Log "══════════════════════════════════════"

# ── SSD Backup ───────────────────────────────────────────────────
if ($Profile -in @("ssd", "both")) {

    # Check SSD is connected
    if (-not (Test-Path $Config.SsdPath.Substring(0, 3))) {
        Log "⚠️  SSD not found at $($Config.SsdPath). Skipping SSD backup." "WARN"
    } else {
        Log "── Phase: OneDrive → SSD ──"

        $rcloneArgs = @(
            "copy", "$($Config.SourceRemote):", $Config.SsdPath,
            "--filter-from", $Config.FilterFile,
            "--log-file", $logFile,
            "--log-level", "INFO",
            "--transfers", $Config.Transfers,
            "--checkers", 8,
            "--stats", "60s",
            "--stats-log-level", "NOTICE"
        )

        if ($Config.BwLimit -ne "0") { $rcloneArgs += "--bwlimit", $Config.BwLimit }
        if ($DryRun) { $rcloneArgs += "--dry-run" }

        $result = Invoke-Rclone $rcloneArgs

        if ($result -eq 0) {
            Log "✅ SSD backup complete"
        } else {
            Log "❌ SSD backup failed (exit $result)" "ERROR"
            $exitCode = $result
        }
    }
}

# ── Cloud Backup ─────────────────────────────────────────────────
if ($Profile -in @("cloud", "both")) {
    Log "── Phase: OneDrive → OneDrive (cloud) ──"

    $latestPath = "$($Config.BackupRemote):$($Config.BackupRoot)/latest"

    $rcloneArgs = @(
        "copy", "$($Config.SourceRemote):", $latestPath,
        "--filter-from", $Config.FilterFile,
        "--log-file", $logFile,
        "--log-level", "INFO",
        "--transfers", $Config.Transfers,
        "--tpslimit", $Config.TpsLimit,
        "--stats", "60s",
        "--stats-log-level", "NOTICE"
    )

    if ($DryRun) { $rcloneArgs += "--dry-run" }

    $result = Invoke-Rclone $rcloneArgs

    if ($result -eq 0) {
        Log "✅ Cloud backup complete"

        if (-not $DryRun) {
            # Create snapshot
            $snapshotPath = "$($Config.BackupRemote):$($Config.BackupRoot)/$datestamp"
            Log "── Phase: Creating snapshot → $snapshotPath ──"
            Invoke-Rclone @("copy", $latestPath, $snapshotPath,
                "--log-file", $logFile, "--log-level", "INFO") | Out-Null
        }
    } else {
        Log "❌ Cloud backup failed (exit $result)" "ERROR"
        $exitCode = $result
    }
}

# ── Log Cleanup ──────────────────────────────────────────────────
$oldLogs = Get-ChildItem $Config.LogDir -Filter "backup-*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $Config.MaxLogFiles

foreach ($log in $oldLogs) {
    Remove-Item $log.FullName
    Log "Pruned old log: $($log.Name)"
}

# ── Summary ──────────────────────────────────────────────────────
$duration = (Get-Date) - $startTime

Log ""
Log "══════════════════════════════════════"
Log "  Backup $(if ($exitCode -eq 0) { 'SUCCEEDED' } else { 'FAILED' })"
Log "  Duration: $($duration.ToString('hh\:mm\:ss'))"
Log "  Exit code: $exitCode"
Log "══════════════════════════════════════"

exit $exitCode
```

---

## Step 2: Test the Script Manually

Before scheduling, test it works:

```powershell
# Dry run to see what would happen
& C:\Backup\run-backup.ps1 -Profile cloud -DryRun

# Real run
& C:\Backup\run-backup.ps1 -Profile cloud
```

Check the log:
```powershell
$latestLog = Get-ChildItem C:\Backup\logs -Filter "backup-*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
Get-Content $latestLog.FullName
```

---

## Step 3: Create the Task Scheduler Entry

### Method A: PowerShell (recommended, reproducible)

```powershell
# Create the scheduled task via PowerShell
$action = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\Backup\run-backup.ps1 -Profile cloud" `
    -WorkingDirectory "C:\Backup"

# Run daily at 2:00 AM
$trigger = New-ScheduledTaskTrigger -Daily -At "2:00AM"

# Run as current user, whether logged in or not
$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 4) `
    -MultipleInstances IgnoreNew `
    -RunOnlyIfNetworkAvailable `
    -StartWhenAvailable `
    -WakeToRun $false

$principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType S4U `    # Run whether logged in or not
    -RunLevel Highest   # Admin privileges if needed

Register-ScheduledTask `
    -TaskName "VaultBackup-Daily" `
    -TaskPath "\VaultBackup\" `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "Daily OneDrive → OneDrive backup via rclone"

Write-Host "Task created successfully"
```

### Method B: Task Scheduler GUI

1. Open Task Scheduler: `taskschd.msc`
2. Create Task (not "Basic Task" — use full "Create Task")
3. **General tab:**
   - Name: `VaultBackup-Daily`
   - Description: `Daily OneDrive → OneDrive backup via rclone`
   - ✅ Run whether user is logged on or not
   - ✅ Run with highest privileges
4. **Triggers tab:**
   - New → Daily → 2:00 AM
   - ✅ Enabled
5. **Actions tab:**
   - New → Start a program
   - Program: `PowerShell.exe`
   - Arguments: `-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "C:\Backup\run-backup.ps1" -Profile cloud`
6. **Settings tab:**
   - ✅ Stop task if it runs longer than: 4 hours
   - ✅ If the task is already running, don't start a new instance
   - ✅ Run task as soon as possible after a scheduled start is missed

---

## Step 4: Verify the Task is Registered

```powershell
# List VaultBackup tasks
Get-ScheduledTask -TaskPath "\VaultBackup\" | Select-Object TaskName, State, LastRunTime, LastTaskResult

# Run it manually to test
Start-ScheduledTask -TaskName "VaultBackup-Daily" -TaskPath "\VaultBackup\"

# Wait a moment then check result
Start-Sleep -Seconds 5
Get-ScheduledTask -TaskName "VaultBackup-Daily" -TaskPath "\VaultBackup\" |
    Select-Object LastRunTime, LastTaskResult
```

`LastTaskResult` of 0 = success. Non-zero = check the log.

---

## Step 5: Multiple Schedules

Set up a tiered backup strategy:

```powershell
# Weekly full verification (Sundays at 3 AM)
$verifyAction = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\Backup\verify-backup.ps1"

$weeklyTrigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At "3:00AM"

Register-ScheduledTask `
    -TaskName "VaultBackup-Verify" `
    -TaskPath "\VaultBackup\" `
    -Action $verifyAction `
    -Trigger $weeklyTrigger `
    -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 2)) `
    -Description "Weekly backup verification"

# SSD backup (if external drive present) — daily at 1 AM
$ssdAction = New-ScheduledTaskAction `
    -Execute "PowerShell.exe" `
    -Argument "-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\Backup\run-backup.ps1 -Profile ssd"

$ssdTrigger = New-ScheduledTaskTrigger -Daily -At "1:00AM"

Register-ScheduledTask `
    -TaskName "VaultBackup-SSD" `
    -TaskPath "\VaultBackup\" `
    -Action $ssdAction `
    -Trigger $ssdTrigger `
    -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 3)) `
    -Description "Daily OneDrive → SSD backup (skips if SSD not connected)"
```

---

## Step 6: Email Notification on Failure (Optional)

Add to the script after the failed backup block:

```powershell
if ($exitCode -ne 0) {
    # Using Windows built-in Send-MailMessage (requires SMTP access)
    # For Gmail, use an App Password
    $emailParams = @{
        To         = "you@example.com"
        From       = "backup-alerts@example.com"
        Subject    = "⚠️ VaultBackup FAILED on $env:COMPUTERNAME"
        Body       = @"
Backup failed at $startTime.
Duration: $duration
Exit code: $exitCode
Log file: $logFile

Last 20 lines of log:
$(Get-Content $logFile | Select-Object -Last 20 | Out-String)
"@
        SmtpServer = "smtp.gmail.com"
        Port       = 587
        UseSsl     = $true
        Credential = (Get-Credential -Message "Gmail SMTP credentials")
    }

    try {
        Send-MailMessage @emailParams
        Log "Failure notification email sent"
    } catch {
        Log "Failed to send email: $_" "WARN"
    }
}
```

> **Alternative:** Use a free webhook service like [ntfy.sh](https://ntfy.sh) for push notifications to your phone:

```powershell
if ($exitCode -ne 0) {
    $body = "VaultBackup failed on $env:COMPUTERNAME. Exit: $exitCode"
    Invoke-WebRequest -Method POST `
        -Uri "https://ntfy.sh/your-unique-topic" `
        -Body $body `
        -ContentType "text/plain" | Out-Null
}
```

---

## Step 7: Monitor Task Scheduler Logs

Task Scheduler writes its own logs in Windows Event Log:

```powershell
# View recent VaultBackup task events
Get-WinEvent -LogName "Microsoft-Windows-TaskScheduler/Operational" |
    Where-Object { $_.Message -match "VaultBackup" } |
    Select-Object TimeCreated, LevelDisplayName, Message |
    Select-Object -First 20 | Format-List
```

Or open Event Viewer → Applications and Services Logs → Microsoft → Windows → TaskScheduler → Operational.

---

## Managing the Schedule

```powershell
# Disable temporarily (e.g., when traveling with no internet)
Disable-ScheduledTask -TaskName "VaultBackup-Daily" -TaskPath "\VaultBackup\"

# Re-enable
Enable-ScheduledTask -TaskName "VaultBackup-Daily" -TaskPath "\VaultBackup\"

# Delete the task
Unregister-ScheduledTask -TaskName "VaultBackup-Daily" -TaskPath "\VaultBackup\" -Confirm:$false

# Run immediately (for testing or after a missed backup)
Start-ScheduledTask -TaskName "VaultBackup-Daily" -TaskPath "\VaultBackup\"

# List all VaultBackup tasks
Get-ScheduledTask -TaskPath "\VaultBackup\" |
    Select-Object TaskName, State, Description
```

---

## Recommended Schedule

```
Daily 1:00 AM   → SSD backup (if drive connected)
Daily 2:00 AM   → Cloud backup (OneDrive → OneDrive)
Daily 3:00 AM   → Lightweight check (file counts)
Weekly Sunday   → Full verify (checksums)
Monthly 1st     → Create long-term snapshot
```

This gives you:
- Daily protection (24-hour recovery point objective)
- Weekly verification (catch silent corruption)
- Monthly long-term snapshots for historical recovery

---

## Next Steps

Continue to → [10 — Performance Tuning](10-performance-tuning.md)
