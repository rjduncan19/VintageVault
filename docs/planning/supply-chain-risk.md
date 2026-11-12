# Supply-Chain Risk: The Backup Tool as the Attack Path

_Created: 2026-11-11 | Status: **open concern; proposed practices below are not yet decided** | Raised by a family member during Rick's first real backup, then developed in a follow-up chat_

## The concern

VintageVault's engine is rclone ([draft ADR](../../rclone-exploration/findings/recommendation.md)), and rclone holds the keys to people's cloud accounts. If rclone, any Go module it's built from, or VintageVault's own release chain were compromised, the attacker would get exactly the access users trusted us with. Our whole pitch is trust, so dependencies are part of our attack surface, not someone else's problem.

This may be **existential** for VintageVault. The product promises to be the last line of defense, and the backup tool sits exactly where an attacker would most want to be.

A hands-on version for the learning series is [tutorial 11](../rclone-exploration/learning/11-supply-chain-security.md).

## Why our users make us a target

Installing a backup tool **tells an attacker that this person has data they value** and would pay to get back. That makes a compromise of rclone or VV more targeted and more profitable than a compromise of a generic library.

- Ransomware groups already go after backups first, because destroying them removes the victim's way out.
- VV's users are mostly non-technical, so they're less likely to notice odd behavior or a suspicious update.
- A compromised backup tool offers three things:
  - **Credentials to everything:** a backdoored release could quietly exfiltrate OAuth refresh tokens.
  - **The backups themselves:** it can corrupt, encrypt or skip files while reporting success.
  - **Scale:** it reaches every user at the next upgrade.
- The risk is not only rclone. VV's own update channel, installer, NuGet dependencies and any service we run are just as attractive.

That gives VV a specific responsibility: **hold as little as possible** (no stored credentials, consistent with [ADR-005](../architecture/005-credential-gating-and-isolation.md) D2, and no file data on our servers), sign everything we ship, and treat our own release process with the same suspicion we apply to our dependencies.

## Case study: the XZ Utils backdoor (2024)

XZ Utils is a small compression library in nearly every Linux distribution, maintained for years mostly by one volunteer.

- **From late 2021:** a contributor using the name "Jia Tan" sent genuinely helpful patches. Several apparently separate accounts, probably sock puppets, pressured the tired maintainer to hand over more responsibility.
- **About two years in:** Jia Tan was a co-maintainer with release rights.
- **Early 2024:** a backdoor shipped in releases 5.6.0 and 5.6.1. The payload was disguised as binary test files in the repository. The build-script change that activated it existed **only in the release tarballs**, not in the git source. On distributions whose SSH server loads liblzma indirectly through systemd, it hooked SSH so that the holder of a specific key could get remote access.
- **Discovery:** Andres Freund noticed SSH logins were about half a second slower than expected and dug in. He caught it before it reached most stable releases.

**Lessons:**
1. The attack was social first and technical second. It targeted a trusted, under-supported maintainer over years.
2. The published artifacts didn't match the readable source, and the hostile payload looked like inert test data.
3. It was caught by luck and curiosity, not by any formal process.

### Was rclone affected?

No. The backdoor only activated inside the SSH server on certain Linux systems. rclone is statically compiled Go, it doesn't load the system liblzma, and its SFTP support uses Go's own SSH implementation. On Windows the whole chain doesn't exist.

That's the wrong place to stop, though.

## The real exposure: transitive dependencies

rclone depends on dozens of Go modules: cloud provider SDKs, crypto, compression, networking and more. Some are maintained by one or two people, which is exactly the XZ profile.

**Pinning an exact rclone version freezes that whole tree as it was at build time** (`go.sum`). That protects us from a malicious update that arrives later. It does **not** protect us if something was already compromised when that release was built. Pinning limits exposure; it doesn't remove it.

## Attack paths

| Path | Example |
|---|---|
| Social-engineered or exhausted maintainer | XZ Utils, above |
| Maintainer account or signing-key compromise | A malicious release published from the real repo |
| Malicious or compromised dependency | A Go module or NuGet package changed upstream |
| Build or release pipeline | Artifacts that don't match the source (the XZ gap) |
| Distribution channel | A tampered winget, Chocolatey or Scoop manifest, or a mirror |
| Look-alike downloads | SEO or ad results pointing to a fake "rclone" site |
| VV's own release process | The same list, applied to VintageVault |

## Proposed practices (not yet decided)

**For the engine:**
- **Pin an exact rclone version.** Never pull "latest" automatically. Upgrades are deliberate and reviewed. (This is already a mitigation in the draft rclone ADR.)
- **Verify what we ship.** Check the release's checksum and signature. Better: build rclone ourselves from the tagged source, with Go's checksum database (`GOSUMDB`) enforced. _Confirm what rclone signs today, and how._
- **Let releases age.** Wait a few weeks after a new rclone release before adopting it. Most compromises are found quickly by someone else.
- **Scan the tree.** Run `govulncheck` against the pinned version and watch advisories for every module in it.
- **Keep an SBOM.** When the next XZ happens, we should be able to answer "are we affected?" in minutes.

**Assume the engine might be hostile:**
- **Least privilege:** use read-only OneDrive scopes (`Files.Read`) for backup, so a compromised tool can read but not destroy the source. This was learned on the first real run.
- **Credential gating:** revoke or lock the grant between runs ([ADR-005](../architecture/005-credential-gating-and-isolation.md) D2).
- **Egress limits:** a firewall rule that lets the engine reach only Microsoft's sign-in and Graph endpoints.
- **Independent verifier:** check backups with code that doesn't share a supply chain with the writer, so one compromise can't both corrupt a backup and approve it. This extends the verifier idea in [credential-gating.md](credential-gating.md).
- **Retention the writer can't override:** keep several generations on a drive that's unplugged between runs, so one poisoned run doesn't overwrite the only good copy.

**For VintageVault itself:**
- Sign every release and installer. Adopt Sigstore-style build provenance, and reproducible builds where we can.
- Publish the pinned rclone version, checksums, SBOM and upgrade notes, per [MISSION.md](../../MISSION.md)'s "Open by Default" and the [radical transparency](radical-transparency.md) plan.
- Track our own OpenSSF Scorecard.

## What the open-source community has done since XZ

- **Signed builds (Sigstore):** verify that a release really came from the project's own build pipeline.
- **Reproducible builds:** anyone can rebuild from source and confirm the result matches the published release. This closes the exact gap XZ used.
- **OpenSSF Scorecard:** rates projects on practices like branch protection, code review and number of active maintainers.
- **Maintainer funding:** efforts such as Germany's Sovereign Tech Fund now pay for critical but under-resourced projects.

The root cause, critical code resting on a single exhausted volunteer, is still not solved. That's why VV can't rely on the community alone.

## Open questions

1. Should the proposed practices become an ADR (a supply-chain policy for VV and its bundled engine), and when?
2. Is depending on one large third-party engine acceptable, or does the independent verifier need to be VV's own code?
3. How is a compromise detected and disclosed to users after the fact?
4. Should VV contribute back (funding, reviews, reproducible-build work) to rclone and its smallest critical dependencies?

## Further reading

- Andres Freund's original disclosure to the oss-security mailing list (March 2024)
- The OpenSSF Scorecard and Sigstore project sites
- Go's `govulncheck` documentation
