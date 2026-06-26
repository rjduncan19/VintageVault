<#
.SYNOPSIS
  Same-provider backup: Google Drive -> Google Drive across two accounts.
  Uses --drive-server-side-across-configs so Google copies the bytes on its OWN
  infrastructure (zero download/upload on this machine) — the ADR-002 "share-then-copy"
  efficiency win. NOT available for OneDrive.

.NOTES
  Requires [gdrive_src] and [gdrive_dest] remotes in rclone.conf (TEST accounts).
  Server-side copy requires the destination account to have access to the source
  files (sharing); see rclone Google Drive docs.
#>
[CmdletBinding()]
param(
  [string]$RcloneExe  = (Join-Path $env:USERPROFILE ".rclone-bin\rclone.exe"),
  [string]$ConfigPath = (Join-Path $PSScriptRoot "..\config\rclone.conf"),
  [string]$Source     = "gdrive_src:",
  [string]$Dest       = "gdrive_dest:VintageVault/gdrive-backup",
  [switch]$DryRun
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $RcloneExe)) { $RcloneExe = "rclone" }

$args = @(
  "copy", $Source, $Dest,
  "--config", $ConfigPath,
  "--drive-server-side-across-configs",   # the key flag: server-side, no local bandwidth
  "--progress",
  "--stats-one-line"
)
if ($DryRun) { $args += "--dry-run" }

Write-Host "Google Drive -> Google Drive (same-provider, SERVER-SIDE copy)" -ForegroundColor Cyan
Write-Host "rclone $($args -join ' ')"
& $RcloneExe @args
