# Supply-Chain Risk: The Backup Tool as the Attack Path

_Created: 2026-11-11 | Status: **open concern, no solution yet** | Raised by a family member during Rick's first real backup_

## The concern

A sophisticated attacker may want to corrupt rclone itself, or VintageVault, or any backup tool.

**People who install backup tools single themselves out as having valuable data.** Compromising one widely used backup tool gives an attacker:
- **Credentials to everything:** the tool holds OAuth refresh tokens for the user's cloud accounts. A backdoored release could exfiltrate them silently.
- **The backups themselves:** a backdoored writer can corrupt or encrypt backups, or quietly skip files, while reporting success. When the user finally needs the backup, it's gone.
- **Scale:** a single compromise reaches every user at the next upgrade.

This may be **existential** for VintageVault: the product's whole promise is the last line of defense, and the backup tool sits exactly where an attacker would most want to be. Choosing rclone ([draft ADR](../../rclone-exploration/findings/recommendation.md)) inherits rclone's supply chain. VV's own code, its NuGet dependencies and its release process are the same kind of target.

## Attack paths

| Path | Example |
|---|---|
| Maintainer account or signing-key compromise | A malicious release published from the real repo |
| Social-engineered maintainer | The 2024 xz-utils backdoor: years of trust-building, then a hidden payload |
| Malicious or compromised dependency | A Go module or NuGet package changed upstream |
| Build or CI pipeline | The binary doesn't match the source |
| Distribution channel | A tampered winget, Chocolatey or Scoop manifest, or a mirror |
| Look-alike downloads | SEO or ad results pointing to a fake "rclone" site |
| VV's own release process | The same list, applied to VintageVault |

## Mitigations to evaluate (none decided)

- **Pin and verify:** pin an exact rclone version. Check the release's published checksums and signature before use (confirm what rclone actually signs, and with which key).
- **Upgrade cooldown:** don't adopt a new release until it has been public for a while (weeks), so others find problems first.
- **Build from source:** reproducible builds, vendored dependencies, `govulncheck`, and an SBOM for each release.
- **Least privilege** (already learned on the first run): read-only OneDrive scopes (`Files.Read`) for backup, so a compromised tool can read but not destroy the source. Revoke the grant between runs ([ADR-005](../architecture/005-credential-gating-and-isolation.md) D2).
- **Egress limits:** a firewall rule that lets the tool reach only Microsoft's sign-in and Graph endpoints.
- **Independent verifier:** check backups with code that doesn't share a supply chain with the writer, so one compromise can't both corrupt the backup and approve it. This extends the "verifier separate from the writer" idea in [credential-gating.md](credential-gating.md).
- **The air gap still holds:** a drive that was unplugged during a compromise keeps its earlier snapshots. Keep several generations, so that one poisoned run doesn't overwrite the only good copy.
- **Transparency:** publish VV's own build provenance (signing, OpenSSF Scorecard, and similar), in line with [MISSION.md](../../MISSION.md) "Open by Default".

## Open questions

1. Is this worth an ADR (supply-chain policy for VV and its bundled engine), and when?
2. Is depending on one large third-party engine acceptable, or does the independent verifier need to be VV's own code?
3. How is a compromise detected and disclosed to users after the fact?
