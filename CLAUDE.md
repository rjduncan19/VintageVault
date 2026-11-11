# CLAUDE.md

VintageVault is open-source (Apache 2.0) **cloud-to-cloud** backup that protects families, freelancers, small businesses and nonprofits from ransomware and data loss. Why it exists: [MISSION.md](MISSION.md).

## Status

- **Pre-MVP**, planning and architecture phase.
- **Engine:** rclone is the proposed transfer engine ([draft ADR](rclone-exploration/findings/recommendation.md), status *Proposed*). The custom Graph copy path is slated to be retired.
- **POC:** a C# CLI for same-account OneDrive snapshots (`src/VintageVault.Cli`). 86 unit tests pass. It has not yet run against a real test OneDrive; see [NEXT-STEPS.md](NEXT-STEPS.md).
- **Security model:** every backup run is gated by a user gesture, and v1's isolated copy is an external drive ([ADR-005](docs/architecture/005-credential-gating-and-isolation.md), Proposed). The current CLI's DPAPI token cache doesn't meet this yet.

## Where decisions live

- `docs/architecture/`: numbered ADRs (001 and 002 are superseded or partly superseded; read their banners).
- `docs/planning/`: strategy, segments, design explorations. [REVIEWER-GUIDE.md](REVIEWER-GUIDE.md) gives a reading order.
- `docs/prompts/README.md`: session log of prompts and decisions.

## Build and test

Needs the .NET 8 SDK.

```bash
dotnet build VintageVault.sln
```

```bash
dotnet test VintageVault.sln
```

The backup engine is tested without OneDrive: `BackupOrchestrator` depends on `IDriveOperations`, tests use the in-memory `tests/VintageVault.Tests/Fakes/FakeDrive.cs`, and `DriveOperations` is tested against a stubbed HTTP handler. Keep new engine logic testable the same way.

## Working with Rick

- About 30 years in software, 25 of them on Windows. Expert in C++; comfortable with COM and IPC. Skip beginner explanations of Windows internals.
- **ADR changes need Rick's sign-off.** Draft them as *Proposed*, and ask before changing an accepted ADR or a decision marked as a hard decision.
