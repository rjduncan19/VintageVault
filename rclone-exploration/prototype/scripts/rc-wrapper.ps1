<#
.SYNOPSIS
  Drives rclone via its remote-control HTTP API (`rclone rcd`) — the integration
  model best suited to a long-running VintageVault desktop agent. Starts the daemon,
  launches an async copy job, polls job status, then shuts the daemon down.

  This maps directly to VintageVault's command surface:
    backup   -> POST /sync/copy (async)
    status   -> POST /job/status
    (stop)   -> POST /core/quit

  Works with LOCAL folders by default so it can run with NO cloud accounts.

.PARAMETER SrcFs / DstFs
  rclone "fs" strings. Defaults create a local sandbox so the API flow is testable
  offline. Point them at real remotes (e.g. "onedrive_src:", "gdrive_dest:Vault")
  once rclone.conf is configured.
#>
[CmdletBinding()]
param(
  [string]$RcloneExe = (Join-Path $env:USERPROFILE ".rclone-bin\rclone.exe"),
  [string]$RcAddr    = "127.0.0.1:5572",
  [string]$RcUser    = "vault",
  [string]$RcPass    = "localdemo",
  [string]$SrcFs,
  [string]$DstFs,
  [string]$ConfigPath
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $RcloneExe)) { $RcloneExe = "rclone" }

# Default to a local sandbox so this runs without cloud credentials
$cleanup = $false
if (-not $SrcFs -or -not $DstFs) {
  $work = Join-Path $env:TEMP "rclone-rc-demo"
  if (Test-Path $work) { Remove-Item -Recurse -Force $work }
  $SrcFs = Join-Path $work "src"; $DstFs = Join-Path $work "dst"
  New-Item -ItemType Directory -Force -Path $SrcFs, $DstFs | Out-Null
  "hello from the rc API demo" | Set-Content (Join-Path $SrcFs "demo.txt")
  "second file"                | Set-Content (Join-Path $SrcFs "notes.md")
  $cleanup = $true
  Write-Host "No remotes supplied -> using local sandbox $work" -ForegroundColor Yellow
}

# --rc-user/--rc-pass enable Basic auth on the API
$daemonArgs = @("rcd", "--rc-addr", $RcAddr, "--rc-user", $RcUser, "--rc-pass", $RcPass)
if ($ConfigPath) { $daemonArgs += @("--config", $ConfigPath) }

Write-Host "Starting rclone rcd daemon on http://$RcAddr ..." -ForegroundColor Cyan
$daemon = Start-Process -FilePath $RcloneExe -ArgumentList $daemonArgs -PassThru -WindowStyle Hidden

try {
  $base = "http://$RcAddr"
  $pair = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${RcUser}:${RcPass}"))
  $headers = @{ Authorization = "Basic $pair" }   # send Basic auth preemptively

  # Wait for the daemon to accept requests
  $ready = $false
  for ($i = 0; $i -lt 30; $i++) {
    try { Invoke-RestMethod -Uri "$base/rc/noop" -Method Post -Headers $headers -Body '{}' -ContentType 'application/json' | Out-Null; $ready = $true; break }
    catch { Start-Sleep -Milliseconds 300 }
  }
  if (-not $ready) { throw "rcd did not become ready" }
  Write-Host "Daemon ready." -ForegroundColor Green

  # backup -> async copy job
  $body = @{ srcFs = $SrcFs; dstFs = $DstFs; _async = $true } | ConvertTo-Json
  $job  = Invoke-RestMethod -Uri "$base/sync/copy" -Method Post -Headers $headers -Body $body -ContentType 'application/json'
  Write-Host "Started copy job id=$($job.jobid) (maps to VintageVault 'backup')" -ForegroundColor Cyan

  # status -> poll job
  do {
    Start-Sleep -Milliseconds 400
    $st = Invoke-RestMethod -Uri "$base/job/status" -Method Post -Headers $headers -Body (@{ jobid = $job.jobid } | ConvertTo-Json) -ContentType 'application/json'
    Write-Host ("  status: finished={0} success={1} duration={2:n2}s" -f $st.finished, $st.success, $st.duration)
  } while (-not $st.finished)

  Write-Host "Job complete. Destination contents:" -ForegroundColor Green
  Get-ChildItem -Recurse $DstFs | Select-Object FullName
}
finally {
  Write-Host "Stopping daemon..." -ForegroundColor Cyan
  try { Invoke-RestMethod -Uri "http://$RcAddr/core/quit" -Method Post -Headers $headers -Body '{}' -ContentType 'application/json' -TimeoutSec 3 | Out-Null } catch {}
  if ($daemon -and -not $daemon.HasExited) { Stop-Process -Id $daemon.Id -Force -ErrorAction SilentlyContinue }
  if ($cleanup) { Write-Host "(local sandbox left in place for inspection)" }
}
