# 11 — Supply Chain Security: Trusting rclone and Everything Under It

> **Goal:** Understand how open-source supply chain attacks work, why VintageVault is exposed through rclone and its dependencies, and what we do about it.

For the full risk analysis and open questions, see [supply-chain-risk.md](../../planning/supply-chain-risk.md). The practices below are **proposed** there and not yet decided.

## Why this matters for VintageVault

VintageVault's engine is rclone, and rclone holds the keys to people's cloud accounts. If rclone, or any library rclone is built from, were compromised, the attacker would inherit exactly the access our users trusted us with. Our whole pitch is trust, so we have to treat our dependencies as part of our attack surface, not as someone else's problem.

## Why our users make us a target

Installing a backup tool tells an attacker that this person has data they value and would pay to get back. That makes a compromise of rclone, or of VintageVault itself, more targeted and more profitable than a compromise of a generic library.

- Ransomware groups already go after backups first, because destroying them removes the victim's way out.
- Our users are mostly non-technical, so they are less likely to notice odd behavior or a suspicious update.
- The risk is not only rclone. VintageVault's own update channel, installer and any service we run are just as attractive to an attacker.

That gives us a specific responsibility: hold as little as possible (no stored credentials, no file data on our servers), sign everything we ship, and treat our own release process with the same suspicion we apply to our dependencies.

## Case study: the XZ Utils backdoor (2024)

XZ Utils is a small compression library included in nearly every Linux distribution. For years it was maintained mostly by one volunteer.

- **Late 2021 onward:** a contributor using the name "Jia Tan" started sending genuinely helpful patches.
- **Pressure to hand over:** several apparently separate accounts (likely sock puppets) publicly pressured the tired maintainer to hand over more responsibility.
- **About two years in:** Jia Tan became a co-maintainer with release rights.
- **Early 2024:** a backdoor shipped in releases 5.6.0 and 5.6.1.
  - The payload was disguised as binary test files committed to the repository.
  - The build-script change that switched it on existed **only in the release tarballs**, not in the git source.
  - On some Linux distributions the SSH server loads liblzma indirectly through systemd. The backdoor hooked SSH there, so whoever held a specific key could get in remotely.
- **Discovery:** Andres Freund noticed SSH logins were about half a second slower than expected, dug in, and found it before it reached most stable releases.

**Lessons:**

1. The attack was social first and technical second. It targeted a trusted, under-supported maintainer over years.
2. What was published didn't match the readable source, and the hostile payload looked like inert test data. Reviewing the git history alone wouldn't have caught it.
3. It was caught by luck and curiosity, not by any formal process.

## Was rclone affected?

No. The backdoor only activated inside the SSH server on certain Linux systems. rclone is written in Go and statically compiled, so it doesn't load the system liblzma, and its SFTP support uses Go's own SSH implementation. On Windows the whole chain doesn't exist.

That's the wrong question to stop at, though.

## The real exposure: transitive dependencies

rclone depends on dozens of Go modules: cloud provider SDKs, crypto, compression, networking, and more. Some of those are maintained by one or two people, which is exactly the profile XZ had.

**Pinning a stable rclone version freezes that whole dependency tree as it was at build time.** That protects us from a malicious update arriving later. It does *not* protect us if something was already compromised when that release was built. Pinning limits exposure; it doesn't remove it.

## Hands-on: check the rclone you're running

**1. What version is it, and where did it come from?**

```powershell
rclone version
(Get-Command rclone).Source
```

A path under `WinGet\Packages` or `chocolatey` tells you which package manager installed it. Each of those is another link in the chain.

**2. Does the download match the official release?** Every release on rclone's GitHub releases page includes a `SHA256SUMS` file. It covers the release **zip files**, not the extracted `rclone.exe`, so hash the zip:

```powershell
Get-FileHash .\rclone-v1.xx.x-windows-amd64.zip -Algorithm SHA256
```

Compare the result with that zip's line in `SHA256SUMS`. Then compare the zip's `rclone.exe` with the one you're running (`Get-FileHash` on both). rclone's install docs describe how to verify the signature on `SHA256SUMS` itself. A checksum only proves the file wasn't altered after release; the signature proves who made the release. _Confirm the current signing method there; it isn't recorded here yet._

**3. What's built into it?** Go binaries record every module they were compiled from. If you have Go installed:

```powershell
go version -m (Get-Command rclone).Source
```

This prints each dependency and its version: a rough bill of materials for the exact binary on your disk.

## What VintageVault does about it (proposed)

- **Pin an exact rclone version.** Never pull "latest" automatically. Upgrades are deliberate and reviewed.
- **Verify what we ship.** Check the release signature and checksum, or better, build rclone ourselves from the tagged source with Go's checksum database (`GOSUMDB`) enforced.
- **Let releases age.** Wait a few weeks after a new rclone release before adopting it. Most compromises are found quickly by someone else.
- **Scan the tree.** Run `govulncheck` against the pinned rclone version and watch advisories for every module in it.
- **Keep a software bill of materials (SBOM).** When the next XZ happens, we should be able to answer "are we affected?" in minutes.
- **Publish all of it.** Our pinned version, checksums, SBOM and upgrade notes are public, consistent with our radical-transparency principle.
- **Assume the engine might be hostile.** VintageVault's design already assumes the writer could be compromised:
  - **Credential gating:** read-only scopes, gated per run.
  - **A separate verifier**, so the writer can't approve its own work.
  - **Retention the writer can't override.**

  Together these limit what a poisoned rclone could actually do.

## What the open-source community has done since XZ

- **Signed builds (Sigstore):** lets you verify a release really came from the project's own build pipeline.
- **Reproducible builds:** anyone can rebuild from source and confirm the result matches the published release, closing the exact gap XZ used.
- **OpenSSF Scorecard:** rates projects on practices like branch protection, code review, and number of active maintainers.
- **Maintainer funding:** efforts such as Germany's Sovereign Tech Fund now pay for critical but under-resourced projects.

The root cause, critical code resting on a single exhausted volunteer, is still not solved. That's why we don't rely on the community alone.

## Further reading

- Andres Freund's original disclosure to the oss-security mailing list (March 2024)
- OpenSSF Scorecard and Sigstore project sites
- Go's `govulncheck` documentation

Return to → [rclone Exploration README](../README.md)
