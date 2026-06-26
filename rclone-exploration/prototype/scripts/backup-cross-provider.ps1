<#
.SYNOPSIS
  Cross-provider backup: OneDrive -> Google Drive.
  Cross-provider has NO server-side transfer: rclone downloads from OneDrive and
  uploads to Google Drive through THIS machine (matches VintageVault ADR-002).

.NOTES
  Requires a populated rclone.conf with [onedrive_src] and [gdrive_dest] remotes
  (run `rclone config` first — interactive OAuth, use TEST accounts only).
  Start with -DryRun to preview with zero data movement.
#>
[CmdletBinding()]
param(
  [string]$RcloneExe   = (Join-Path $env:USERPROFILE ".rclone-bin\rclone.exe"),
  [string]$ConfigPath  = (Join-Path $PSScriptRoot "..\config\rclone.conf"),
  [string]$Source      = "onedrive_src:",
  [string]$Dest        = "gdrive_dest:VintageVault/onedrive-backup",
  [string]$ArchiveDir  = "gdrive_dest:VintageVault/_archive",
  [switch]$DryRun,
  [switch]$Immutable
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $RcloneExe)) { $RcloneExe = "rclone" }

$args = @(
  "copy", $Source, $Dest,
  "--config", $ConfigPath,
  "--backup-dir", $ArchiveDir,
  "--exclude", "/Videos/**",          # mirrors VintageVault exclude filters
  "--transfers", "4",
  "--checkers", "8",
  "--bwlimit", "8M",                  # be polite on a home connection
  "--progress",
  "--stats-one-line"
)
if ($Immutable) { $args += "--immutable" }
if ($DryRun)    { $args += "--dry-run" }

Write-Host "OneDrive -> Google Drive (cross-provider, download+upload via this host)" -ForegroundColor Cyan
Write-Host "rclone $($args -join ' ')"
& $RcloneExe @args
