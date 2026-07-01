# rclone Exploration

This folder contains research and learning materials on **rclone** — the open-source cloud storage synchronization tool that serves as a key reference implementation for VintageVault's backup engine design.

## Why We Study rclone

VintageVault's mission is to make cloud backup simple, reliable, and trustworthy. rclone is the gold standard for cloud-to-cloud data movement, supporting 70+ storage providers. Understanding how rclone solves the hard problems of cloud sync (checksums, resumable transfers, rate limiting, filtering, encryption) directly informs our design decisions.

From our MISSION.md:
> Open-source contributions — improvements to rclone, cloud API documentation, security research

rclone is also one of the tools our users may already know. VintageVault wraps concepts familiar to rclone users in a simpler interface, while learning from rclone's battle-tested approach.

## What rclone Does That We Care About

| rclone Capability | VintageVault Relevance |
|---|---|
| OneDrive OAuth with custom app | We need our own client ID/secret per user |
| Cross-account sync | Core feature: OneDrive A → OneDrive B |
| Filter rules and exclusions | Users exclude large folders (Videos, Downloads) |
| Checksum verification | Trust-but-verify after every backup |
| rclone crypt overlay | Encrypted backup option for advanced users |
| Bandwidth throttling | Respectful background operation |
| Retry and resume logic | Reliability on flaky connections |

## Learning Modules

Work through these in order if you're new to rclone:

| # | File | What You'll Learn |
|---|------|-------------------|
| 00 | [Overview & Concepts](learning/00-overview-and-concepts.md) | What rclone is, how it works, key terminology |
| 01 | [Installation & First Config](learning/01-installation-and-first-config.md) | Install on Windows, run `rclone config` |
| 02 | [OneDrive Custom App Setup](learning/02-onedrive-custom-app-setup.md) | Register your own Azure app instead of using rclone's shared key |
| 03 | [Backup OneDrive → External SSD](learning/03-backup-onedrive-to-external-ssd.md) | Full tutorial: cloud to local backup with filters and scheduling |
| 04 | [Backup OneDrive → OneDrive](learning/04-backup-onedrive-to-onedrive.md) | Full tutorial: cross-account backup, ransomware isolation |
| 05 | [Filtering & Exclusions](learning/05-filtering-and-exclusions.md) | Exclude patterns, filter files, size limits |
| 06 | [Sync vs Copy vs Move](learning/06-sync-copy-move-operations.md) | Operation semantics and when to use each |
| 07 | [Verification & Checksums](learning/07-verification-and-checksums.md) | Confirming your backup is complete and uncorrupted |
| 08 | [Encryption with rclone crypt](learning/08-encryption-with-crypt.md) | Zero-knowledge encrypted backup overlay |
| 09 | [Automation on Windows](learning/09-automation-windows-task-scheduler.md) | Scheduled backups, logging, alerting |
| 10 | [Performance Tuning](learning/10-performance-tuning.md) | Bandwidth limits, parallelism, chunk size optimization |

## Quick Reference

```
rclone [command] source:path dest:path [flags]

Commands:
  copy      Copy files (no deletions at destination)
  sync      Mirror source to destination (deletes extras at dest)
  check     Verify checksums without transferring
  ls        List files
  lsl       List with modification times
  about     Show storage quota
  config    Interactive configuration

Flags (commonly used):
  --dry-run               Simulate without transferring
  --progress              Show real-time progress
  --transfers N           Parallel transfers (default: 4)
  --checkers N            Parallel checksum workers (default: 8)
  --bwlimit SPEED         Bandwidth limit (e.g. 10M)
  --filter-from FILE      Load filter rules from file
  --log-file FILE         Write log to file
  --log-level LEVEL       DEBUG, INFO, NOTICE, ERROR
```

## Relationship to VintageVault Code

| rclone concept | VintageVault equivalent |
|---|---|
| `rclone config` remote | `GraphClientFactory.cs` credentials |
| `rclone sync --backup-dir` | Snapshot versioning in `BackupEngine.cs` |
| `--filter-from filters.txt` | Exclusion rules in `ExclusionManager.cs` |
| `rclone check` | Checksum validation in `SnapshotManager.cs` |
| `rclone crypt` | Planned encrypted storage tier |
