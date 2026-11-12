# Backup Coverage Gaps

_Created: 2026-11-11 | Source: Rick's first real OneDrive → external SSD backup with rclone (tutorial 03)_

Data that a plain `rclone copy onedrive: <drive>` does **not** capture. Each item needs a decision before VintageVault can tell a family "everything is backed up".

## 1. OneNote notebooks (must fix)

**Why it matters:** Rick's family keeps a lot of irreplaceable material in OneNote. This is likely true of many target users, because OneNote ships with Microsoft 365 Family.

**What happens today:**
- **rclone:** hides OneNote notebooks from listings by default, so they're skipped with no warning. `--onedrive-expose-onenote-files` makes them visible, but notebooks are special "package" items in OneDrive, and downloading them as plain files may fail or give unusable output. _Unverified: test on a real notebook._
- **VV's same-account POC:** unknown how `driveItem: copy` handles notebooks ([poc-spec open question 5](../poc-spec.md#open-questions-resolve-during-poc)).

**Options to investigate:**
1. Export from the OneNote desktop app (File → Export → Notebook → `.onepkg`). Gives a restorable package, but it's manual per notebook, and only the desktop OneNote app does it.
2. The Graph OneNote API (`Notes.Read`): pages as HTML plus resources. Automatable, but it's a separate scope, and the result doesn't round-trip back into OneNote.
3. rclone with `--onedrive-expose-onenote-files`. Check what actually lands on disk, and whether OneNote can reopen it.
4. Back up the local OneNote cache. Fragile and undocumented, and probably a last resort.

**Done when:** a test notebook can be backed up and then restored, readable in OneNote, by a non-technical user. A restore drill ([adoption-and-trust.md](adoption-and-trust.md)) covers it.

**Until then:** export each family notebook by hand to `.onepkg` alongside the drive backup.

## 2. Personal Vault

Graph doesn't let apps read Personal Vault, so `rclone` gets an error there and VV can't see it. Locally it only appears while unlocked (it relocks after about 20 minutes idle).

**Workaround today:** unlock it, copy its contents to the (BitLocker-protected) drive by hand, then lock it again. VV should at least **tell the user** that the vault isn't covered, rather than skip it silently.

## 3. Lower priority

| Not captured | Note |
|---|---|
| Version history | Only the current version of each file is copied. VV's snapshots (or `--backup-dir`) give history from the first run onward. |
| "Shared with me" items | They live in other people's drives. Back them up from the owner's account, or copy them in on purpose. |
| Sharing links, permissions, photo albums | Metadata, not file content. Lost on restore. |
| Files skipped by filters | Filters such as `--max-size` and `--exclude` skip files without any message. VV must report what it skipped. |
