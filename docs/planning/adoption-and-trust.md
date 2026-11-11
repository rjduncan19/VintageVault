# Adoption and Trust: Reaching People Who Have Never Backed Up

_Created: 2026-11-11 | Source: [design chat handoff](2026-11-design-chat-handoff.md), section 3.5_

Complements [product-strategy.md](product-strategy.md) ("The Messaging Problem"). That doc covers what to *say*; this one covers what the product should *do* to earn trust.

## The population

- About **20% of computer owners have never backed up** (Backblaze survey, flat for years). Other surveys put "rarely or never" at about 40%.
- About **97% of recent ransomware attacks also target backup storage** (secondary-source stat).

> ⚠️ **Verify both figures against primary sources before publishing them** anywhere public (site, pitch, README).

## Principles

### 1. Make value visible before disaster
The first run should end with a concrete, personal result, not "Backup complete":
> "4,200 photos protected, going back to 2009."

People don't value insurance they can't see. Show them what's now safe.

### 2. Be honest about gaps
Many people believe OneDrive sync *is* a backup. Show what is **synced but not protected**:
- Files outside the folders VV covers.
- Anything newer than the last gated run.
- Whether an isolated copy (the external drive, [ADR-005 D3](../architecture/005-credential-gating-and-isolation.md#d3-v1-isolation-comes-from-an-external-drive-air-gap-hard-decision)) exists and how old it is.

Honesty here is the trust strategy ([MISSION.md](../../MISSION.md) principle 5).

### 3. Spontaneous restore drills
Now and then, VV restores a random file to a temporary location and shows the user it worked. This:
- proves the backup is real (a backup that's never been restored is a hope, not a backup)
- teaches the restore flow before it's needed under stress
- is the natural moment for **deferred enrollment**: right after a successful drill, ask the user to set up the external drive ([credential-gating.md](credential-gating.md#deferred-enrollment-decided-in-the-chat))

### 4. Zero-question setup
Sensible defaults, no choices on first run. Connect OneDrive, approve with Windows Hello, done. Filters, drives and schedules come later, and only if the user wants them.

## Interaction with gating

Because every run needs a gesture ([ADR-005 D2](../architecture/005-credential-gating-and-isolation.md#d2-every-backup-run-needs-a-user-gesture-hard-decision)), adoption depends on the reminder flow. Each prompt should carry a small piece of visible value ("12 new photos since last week") so confirming feels worth the second it takes.
