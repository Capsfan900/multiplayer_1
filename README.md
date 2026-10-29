# multiplayer_1

Public GitHub repository: [Capsfan900/multiplayer_1](https://github.com/Capsfan900/multiplayer_1).
Download Windows development previews from [Releases](https://github.com/Capsfan900/multiplayer_1/releases).
Current snapshot: [Laser arsenal and first-person polish / e9e091f](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261028-e9e091f).
Extract the entire player ZIP and run `WildstoneArena.exe`. Downloads are public.
These are development previews,
not accepted online multiplayer releases. See [build workflow](docs/TOOLING.md#github-builds-and-private-releases).

**Active MVP development, resumed on 2026-10-27 in the isolated `mvp-2` worktree.** Current evidence and
setup and acceptance dependencies live in [docs/HANDOFF.md](docs/HANDOFF.md). The
[complete pause handoff](docs/handoffs/2026-10-25-paused-project.md) is historical.

A Unity first-person arena game for internet friend matches: four teams of up to
two, uneven teams allowed, weapon races and a moving capture zone.

The full MVP includes three distinct maps, the approved weapon roster, human models,
readable VFX and minimalist finished menus. The workspace now contains the adapted
movement, host-owned simulation, Photon-only private host/join, lobby/recovery,
offline Practice/UI and editable models/maps. PlayFab is removed (D023).
Focused native checks and separate-process Photon gameplay, reconnect, late joining,
controlled hits, eight-player capacity and graceful/crash host migration pass. Full
external-network, visual and performance acceptance remain open. Download the current
Windows preview above for the recorded, published source snapshot. The separate
source projects and original authoring checkout remain unchanged.

Selected direction: Photon Fusion Host Mode, with one participating player hosting;
C3.1 / Wildstone Modern, soft-lit mostly monochrome architecture and simple human
characters. Fusion and the local App ID are configured; no PlayFab setup is needed.

Production assets use Blender sources and Unity authoring scripts, reusing suitable
`vibegame1` work and selected `ai_skelly_tool` leads under the [asset pipeline](docs/ASSET-PIPELINE.md).
The multiplayer foundation is new integration rather than copied single-player logic.

Start at the [documentation index](docs/README.md), then read the
[handoff](docs/HANDOFF.md) and [backlog](docs/BACKLOG.md). Contributors should also
read [AGENTS.md](AGENTS.md). The index identifies the owner of each design contract;
concept art does not establish gameplay statistics or runtime performance.
