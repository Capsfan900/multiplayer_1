# multiplayer_1

**Active MVP development, resumed by the user on 2026-10-25.** Current evidence and
account/setup dependencies live in [docs/HANDOFF.md](docs/HANDOFF.md). The
[complete pause handoff](docs/handoffs/2026-10-25-paused-project.md) is historical.

A Unity first-person arena game for internet friend matches: four teams of up to
two, uneven teams allowed, weapon races and a moving capture zone.

The full MVP includes three distinct maps, the approved weapon roster, human models,
readable VFX and minimalist finished menus. Implementation begins with the foundation, reusing
the separate `C:\Users\tyler\Main Storage\vibegame1` project after a source audit.

Selected direction: Photon Fusion Host Mode, with one participating player hosting;
C3.1 / Wildstone Modern, soft-lit mostly monochrome architecture and simple human
characters. Managed service setup and detailed technical proposals remain pending.

Production assets will use Blender and Unity tooling, reusing suitable `vibegame1`
work and selected `ai_skelly_tool` helpers under the [asset pipeline](docs/ASSET-PIPELINE.md).
The multiplayer foundation is new integration rather than copied single-player logic.

Start at the [documentation index](docs/README.md), then read the
[handoff](docs/HANDOFF.md) and [backlog](docs/BACKLOG.md). Contributors should also
read [AGENTS.md](AGENTS.md). The index identifies the owner of each design contract;
concept art does not establish gameplay statistics or runtime performance.
