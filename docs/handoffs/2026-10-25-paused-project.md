# Complete project pause handoff — multiplayer_1

Saved: 2026-10-25, approximately 20:00 America/New_York (2026-10-26 00:00 UTC).
Status: **paused by the user; do not start more work until they resume**.

The final instruction was: “stop all tasks and create handoffs for everything so I
can resume back to this at a later time and not lose progress or context.” This
document captures the current project state; the linked canonical specs hold the
detailed contracts. This is a handoff, not implementation or a new design decision.

## 1. Location and actual state

- Workspace: `C:\Users\tyler\Main Storage\multiplayer_1`.
- Main reuse source: `C:\Users\tyler\Main Storage\vibegame1`.
- Secondary helper source: `C:\Users\tyler\Main Storage\ai_skelly_tool`.
- Shell: Windows PowerShell. Project files are local; there is no Git repository
  in this workspace and no remote backup or commit was created.
- This workspace contains documentation and raster concept art only. It has no
  imported Unity project, C# gameplay implementation, playable build, cloud setup,
  production `.blend` assets or runtime test results.
- Neither source project was edited or executed during this design work. Existing
  source assets were inspected read-only; unrelated user work remains outside scope.
- At pause, all project subagents were complete. No image generation, shell job or
  project goal was still running. No Unity/Blender instance or game server was
  launched by this work. Unrelated applications were not stopped.

## 2. Confirmed user intent — retain these decisions

Build a separate **Unity FPS multiplayer arena battler** for friends playing from
different homes. **Four teams of up to two**; incomplete teams are allowed. Players
race to the center and other valuable map locations for unusual weapons and fight
over a **moving capturable scoring zone**, inspired by WARDOGS. The zone remains the
main objective; it has not been replaced by a shrinking battle-royale circle.

**Foundation first:** networking, sessions, player data, prediction/reconciliation,
failure recovery and measured responsiveness before arena production. The user wants
competitive feel and good performance. Do not promise zero internet lag, perfect
fairness or “bulletproof” behavior without evidence.

**Hosting:** the user initially selected Photon Fusion plus dedicated servers, then
explicitly changed to player hosting to avoid operating servers. The active direction
is **Photon Fusion Host Mode**: one participating player is authoritative, with
managed direct/NAT/relay connectivity. Dedicated servers and the earlier owner-driven
Unity Netcode/Relay proposal are retired. Host trust, host latency advantage, migration
rollback and service dependency/possible charges were explained and remain real limits.
No custom backend, paid subscription or cloud deployment has been performed.

**Look:** C / Ink & Concrete developed into C3 / Wildstone Ruins, then **C3.1 /
Wildstone Modern**. C3.1 is accepted: sleek, mostly monochrome modernist terraces,
bridges and interrupted monumental walls; soft modern lighting with VHOLUME as a
visual reference. Simple human silhouette people are accepted. No fox/hare characters
or matching animal/ruin-themed equipment. Weapon silhouettes can be expressive, but
their materials and detail must be simplified to match the game's soft modern style.

**Starting content:** keep Line Driver, Pulse Lobber, Rebound Disc and Push Lens.
Also include grenades, stun guns and laser swords. Exactly **two initial special
consumable types**: short **Blink teleport** and temporary shield. The user answered
the mobility question explicitly: short blink teleport, not a visible dash. The
stated Blink contract blocks walls and invalid destinations. Grenades are an ammo-based
tactical weapon category in the proposal, not a third special consumable type.

**Production:** use Blender and Unity tooling. Reuse useful `vibegame1` assets,
movement, level-making and presentation systems first; build multiplayer authority
and synchronization separately. Also inspect `ai_skelly_tool` selectively where it
saves effort. Maintain supporting docs/specs, decisions, handoff and evidence throughout.

The last approval before pausing was “yeah that is fine use those.” D014 records
the **four simplified v2 weapon images as approved production shape/material
references**. Do not ask the user to choose those four designs again.

## 3. Canonical documents and workstream handoffs

Start with [AGENTS](../../AGENTS.md), the [documentation index](../README.md),
[current handoff](../HANDOFF.md), [decisions D001–D015](../DECISIONS.md), and
[staged backlog](../BACKLOG.md). Preserve the division of ownership below.

| Workstream | Saved result / canonical owner | Actual state at pause |
| --- | --- | --- |
| Network, sessions and data | [Foundation spec](../superpowers/specs/2026-10-25-multiplayer-foundation-design.md) | Host authority, identity binding, teams, replay/rewind, reconnect, migration, failure states and performance gates specified; no SDK integration |
| Weapons, items and objective | [Combat/items/zone spec](../superpowers/specs/2026-10-25-arena-combat-items-zone-design.md) | Confirmed content scope; proposed item/status/scoring rules, atomic requests, tick ordering and recovery cases documented; no gameplay code |
| Unity graphics | [Rendering spec](../superpowers/specs/2026-10-25-unity-rendering-design.md) | Source-backed reuse audit, soft-light strategy, simplified effects, bake/preview contract and busy-match acceptance specified; no rendering slice |
| Map | [Wildstone map spec](../superpowers/specs/2026-10-25-wildstone-modern-map-design.md) | Visual direction accepted; four arrivals, central court, connected side routes and moving-zone circuit are spatial proposals, not proven collision/balance |
| Character and weapon production | [Asset review workflow](../superpowers/specs/2026-10-25-characters-weapons-design-workflow.md) | Character style and v2 weapon references accepted; rigging, production sheets, first-person/world models and readability checks pending |
| Blender/Unity/source reuse | [Asset pipeline](../ASSET-PIPELINE.md) | Exact reuse leads, dependencies, export/roundtrip checks and selective `ai_skelly_tool` helpers recorded; tools not executed |
| Visual evidence | [Art gallery](../art-direction/README.md), [weapon gallery](../art-direction/weapon-concepts.md) | Original comparisons and selected refinements saved with prompts; images only |
| Historical context | [Original arena proposal](../superpowers/specs/2026-10-25-multiplayer-arena-design.md) | Explicitly superseded. Its topology, statistics and old weapon examples are not the active contract |

The source has movement and combat code, but this project does not yet. Do not confuse
source availability with completed multiplayer conversion. The current specs also
do not approve all proposed tuning merely because visual concepts were approved.

## 4. Exact approved visual files

- Map/character reference: [C3-1-wildstone-modern.png](../art-direction/C3-1-wildstone-modern.png).
- Line Driver: [W01-line-driver-simple-v2.png](../art-direction/W01-line-driver-simple-v2.png).
- Pulse Lobber: [W02-pulse-lobber-simple-v2.png](../art-direction/W02-pulse-lobber-simple-v2.png).
- Rebound Disc: [W03-rebound-disc-simple-v2.png](../art-direction/W03-rebound-disc-simple-v2.png).
- Push Lens: [W04-push-lens-simple-v2.png](../art-direction/W04-push-lens-simple-v2.png).

Retain their open split rails, opaque orb chamber, broad disc platter and open ring
identities. Use broad matte forms, soft bevel highlights, restrained accents and fewer
tiny seams/internal parts. These PNGs are visual references, not meshes or shader proof.

Earlier A/B/C, C1/C2/C3 and original four weapon images remain in `docs/art-direction/`
as history. The downloaded VHOLUME screenshot in `references/` is credited inspiration,
not a distributable game asset. All generated deliverables were copied into this
workspace; resuming does not depend on the image tool's external generated-image folder.

Exact prompts: [original styles](../art-direction/prompts.md),
[C variants](../art-direction/C-variations-prompts.md),
[C3.1](../art-direction/C3-modern-prompts.md),
[original weapons](../art-direction/weapon-concepts-prompts.md),
[simplified weapons](../art-direction/weapon-simplification-prompts.md).
Built-in ImageGen produced the art. Three simplification edits initially hit a tool
moderation error; a clearly scoped surface-style retry succeeded. No image task is
pending and no API/CLI generation fallback is needed.

## 5. Source findings already established — do not repeat without changed inputs

Detailed paths and limits are in the rendering spec and asset pipeline.

- `vibegame1` source declares Unity **6000.5.10f1**, URP **17.5.0** and Input System
  **1.20.0**. These are source versions; Fusion compatibility remains to be proved.
- Reuse leads include Level Studio, `LevelDefinition`, `BuildWorldContent`,
  `LevelPieceFactory`, cached chamfered `LevelMesh`, weapon viewmodel attachment,
  editable weapon `.blend` files and export validators.
- Source `FirstPersonMotor` and stamina use frame/global state. Preserve movement
  laws while making tick/restorable state explicit. Global pause/hitstop, campaign
  managers, `GameEvents` and enemy-only damage routes are not network authority.
- Architectural Stone samples lightmaps/SH, but generated `LevelMesh` has UV0 and
  no UV2, and the shader has no observed Meta pass. UV2 and bake contribution must
  be validated. Existing fake ambient/AO can double-shade baked lighting.
- Try a small material/light slice before custom expansion: stock URP Lit versus
  a stripped source shader, quiet matte values, soft key/fill, diffuse probes,
  restrained effects. Source atmosphere/reflection/post effects should not be
  copied wholesale. Use the source's local viewmodel separation once per local player.
- Existing shader warmup must collect this game's actual variants. `RenderCostProbe`
  uses synchronous `ReadPixels` and is not a normal-frame acceptance benchmark.
- `ai_skelly_tool/stages/rig.py` has binding/FBX helpers; `stages/weapon.py` has
  socket/clearance/grip helpers. Its deformation and animation tests are adaptation
  leads; thresholds need calibration. `rig_cli.py` isolates Blender subprocesses.
- Existing `vibegame1` weapon scripts depend on sibling helpers and contain output
  paths that need auditing. Do not run them against either original source tree.
  The GPU generation path is unnecessary for this approved simplified asset direction.
- Unity MCP tools are available, but no target Editor instance is verified. Pin
  this project's actual instance when one exists, never the source instance.
  No Blender executable was on PATH and no Blender MCP tool was observed. A sibling
  `bpy` Python environment exists; its execution/version compatibility is unverified.

These are code/file inspection results. No source shaders, rigs, export scripts,
Unity scenes or Blender models were executed or modified during the audit.

## 6. Proposed details and evidence limits

Preserve these as proposals until validated or deliberately resolved:

- Exact Fusion SDK pin, managed PlayFab identity choice, account/region/plan setup,
  authentication-to-peer binding, and real host-migration feasibility.
- Reference host/client hardware and uplink; tick/snapshot/FPS/bandwidth/correction
  and recovery budgets in the foundation. Do not replace targets with measured claims.
- Detailed ammo, range, damage, slots, charges, stun timing/immunity, shield capacity,
  match duration/score target and simultaneous-attack policy.
- Proposed shield is finite personal absorption, not invulnerability; stun is bounded
  with a recovery/immunity rule. Blink marks a position discontinuity while legitimate
  pre-blink hits still obey bounded rewind. The combat spec owns these details.
- Proposed zone scoring rewards exactly one occupying team at a fixed team rate;
  multiple teams contest it, with floor-aware occupancy. These are this game's proposed
  rules, not a claim about WARDOGS' exact implementation. Uneven teams remain supported.
- Actual map collision, rigs/hit envelopes, quality settings, bake pipeline and
  first-person weapon framing remain to be built and tested.

Independent document reviews found no consequential contradiction in the revised
Host Mode foundation or the combat/rendering contracts. Reviews establish consistency
only. Image copies were hash-checked; local Markdown links were checked. There are
no Unity compile/test results, network sessions, gameplay feel tests, performance
measurements, production assets or package releases for this project.

## 7. Resume order

On an explicit user resume, read the linked documents and verify current files/tooling.
Do not reopen the approved art, weapon, Blink or hosting choices. Keep the source
boundaries and maintain documentation alongside implementation.

1. Produce the staged implementation plan. Finish the remaining motor/dependency
   inventory and establish an isolated target Unity project and compatible Fusion
   path. Reuse completed rendering/asset audits rather than starting them over.
2. Prove movement parity, fixed-tick restoration and client prediction/reconciliation.
3. Establish identity, private sessions, team seating, reconnect and host migration
   with observable failure handling.
4. Add minimal authoritative combat fixtures, then the moving-zone greybox and
   pickup races. Extend the accepted roster and two consumable types incrementally.
5. Use Blender/Unity to produce one finished visual slice and the accepted asset
   treatments, retaining editable sources and validated exports.
6. Validate on separate internet-connected machines under direct/relay and adverse
   network conditions, then profile the busy eight-player game on agreed hardware.

The [backlog](../BACKLOG.md) carries the implementation stages; all remain not started.
Service identities and spending decisions are still open. Do not buy plans, publish,
create credentials or deploy services on the strength of this handoff.

### Copyable resume prompt

> Resume the multiplayer_1 project at C:\Users\tyler\Main Storage\multiplayer_1.
> Read AGENTS.md, docs/HANDOFF.md and docs/handoffs/2026-10-25-paused-project.md,
> then the canonical specs in docs/README.md. Preserve all accepted decisions,
> especially Fusion Host Mode, foundation-first development, C3.1 Wildstone Modern,
> simple humans, the four approved simplified v2 weapons, the added weapon categories,
> short Blink and temporary shield, and the moving capture zone. Continue from the
> next concrete foundation task without redoing completed art/source audits. Keep
> vibegame1 and ai_skelly_tool untouched and keep docs current.

## 8. Local snapshot and restoration

The project-root `handoffs/` directory contains a dated ZIP and JSON SHA-256 manifest
for this pause. The archive includes root README/AGENTS, every file under `docs/`,
and an embedded `snapshot-manifest.json`. File paths, byte counts and SHA-256 values
allow the archive to be verified. The archive is checked entry-by-entry after creation.

This is a local snapshot on the same machine, not an off-device backup or Git history.
It excludes the original `vibegame1` and `ai_skelly_tool` trees and their tool environments.
Their relative source links require those sibling projects, or path adjustment on
another machine. It also excludes chat/tool transcripts; the actionable decisions,
context and completed results are captured here and in the bundled canonical files.

To restore, extract into a new folder, read this handoff, and compare the manifest
before replacing any newer work. Keep the approved images and editable future assets
distinct. The working files are already saved in place, so extraction is unnecessary
to resume in the current workspace.
