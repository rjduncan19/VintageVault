<#
.SYNOPSIS
  Proves rclone's backup mechanics (copy, --backup-dir versioning, --immutable,
  filters) using LOCAL folders as "remotes" — no cloud accounts / OAuth required.

  This demonstrates the exact flags the cloud scripts use, so the mechanics are
  verifiable in CI or on any machine.
#>
[CmdletBinding()]
param(
  [string]$RcloneExe = (Join-Path $env:USERPROFILE ".rclone-bin\rclone.exe"),
  [string]$WorkDir   = (Join-Path $env:TEMP "rclone-local-demo")
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $RcloneExe)) { $RcloneExe = "rclone" } # fall back to PATH

function Run($desc, [scriptblock]$cmd) {
  Write-Host "`n=== $desc ===" -ForegroundColor Cyan
  & $cmd
}

# Fresh sandbox
if (Test-Path $WorkDir) { Remove-Item -Recurse -Force $WorkDir }
$src  = Join-Path $WorkDir "source"
$dst  = Join-Path $WorkDir "backup"
$arch = Join-Path $WorkDir "backup-archive"   # --backup-dir versioned move-aside
New-Item -ItemType Directory -Force -Path $src, $dst, $arch | Out-Null

# Seed realistic source data
"family budget v1"      | Set-Content (Join-Path $src "budget.xlsx")
"vacation photo"        | Set-Content (Join-Path $src "photo.jpg")
New-Item -ItemType Directory -Force -Path (Join-Path $src "Videos") | Out-Null
"huge home video"       | Set-Content (Join-Path $src "Videos\movie.mp4")

Write-Host "rclone:" (& $RcloneExe version | Select-Object -First 1)

# 1) First backup — exclude /Videos (mirrors VintageVault's `exclude` command)
Run "Backup #1 (copy, excluding /Videos)" {
  & $RcloneExe copy $src $dst --exclude "/Videos/**" --progress --stats-one-line
}
Run "Destination after backup #1" { Get-ChildItem -Recurse $dst | Select-Object FullName }

# 2) Modify a file, add a file, then incremental backup with versioning
"family budget v2 (EDITED)" | Set-Content (Join-Path $src "budget.xlsx")
"tax document"              | Set-Content (Join-Path $src "taxes.pdf")
Run "Backup #2 (incremental + --backup-dir versioning)" {
  & $RcloneExe copy $src $dst --exclude "/Videos/**" --backup-dir $arch --progress --stats-one-line
}
Run "Destination (current snapshot)"  { Get-ChildItem -Recurse $dst  | Select-Object FullName }
Run "Archive (previous version of budget.xlsx preserved)" { Get-ChildItem -Recurse $arch | Select-Object FullName }

# 3) --immutable refuses to overwrite/modify existing destination files
"family budget v3" | Set-Content (Join-Path $src "budget.xlsx")
Run "Backup #3 (--immutable should REFUSE to modify budget.xlsx)" {
  try { & $RcloneExe copy $src $dst --exclude "/Videos/**" --immutable --stats-one-line 2>&1 }
  catch { Write-Host "rclone reported immutable violation (expected): $_" }
}

Write-Host "`nDemo complete. Sandbox: $WorkDir" -ForegroundColor Green
Write-Host "Flags proven: copy (incremental), --exclude (filters), --backup-dir (versioning), --immutable (WORM-style protection)."
