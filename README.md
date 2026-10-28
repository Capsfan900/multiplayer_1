# multiplayer_1

Private GitHub repository: [Capsfan900/multiplayer_1](https://github.com/Capsfan900/multiplayer_1).
Download offline Windows Practice previews from [Releases](https://github.com/Capsfan900/multiplayer_1/releases).
Extract the entire player ZIP and run `WildstoneArena.exe`. GitHub sign-in and repository
access are required; the link does not grant access. These are development previews,
not accepted online multiplayer releases. See [build workflow](docs/TOOLING.md#github-builds-and-private-releases).

**Active MVP development, resumed on 2026-10-27 in the isolated `mvp-2` worktree.** Current evidence and
account/setup dependencies live in [docs/HANDOFF.md](docs/HANDOFF.md). The
[complete pause handoff](docs/handoffs/2026-10-25-paused-project.md) is historical.

A Unity first-person arena game for internet friend matches: four teams of up to
two, uneven teams allowed, weapon races and a moving capture zone.

The full MVP includes three distinct maps, the approved weapon roster, human models,
readable VFX and minimalist finished menus. The workspace now contains the adapted
movement, host-only rules, offline Practice/UI integration and editable models/maps.
Native tests, three-map lighting and an offline Windows build now pass. Real Fusion
integration and internet acceptance remain open. Run
`Artifacts/Unity/Windows-20261027-204502-089/WildstoneArena.exe` for local offline Practice. The separate
source projects and original authoring checkout remain unchanged.

Selected direction: Photon Fusion Host Mode, with one participating player hosting;
C3.1 / Wildstone Modern, soft-lit mostly monochrome architecture and simple human
characters. Managed service setup and detailed technical proposals remain pending.

Production assets use Blender sources and Unity authoring scripts, reusing suitable
`vibegame1` work and selected `ai_skelly_tool` leads under the [asset pipeline](docs/ASSET-PIPELINE.md).
The multiplayer foundation is new integration rather than copied single-player logic.

Start at the [documentation index](docs/README.md), then read the
[handoff](docs/HANDOFF.md) and [backlog](docs/BACKLOG.md). Contributors should also
read [AGENTS.md](AGENTS.md). The index identifies the owner of each design contract;
concept art does not establish gameplay statistics or runtime performance.
