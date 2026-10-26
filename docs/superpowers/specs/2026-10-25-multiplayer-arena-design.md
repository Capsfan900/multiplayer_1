# Multiplayer arena — proposed first playable

Status: historical gameplay proposal; implementation has not started.

**Superseded for execution by [the foundation design](2026-10-25-multiplayer-foundation-design.md).**
The user subsequently required networking, sessions and player data first, then
requested peer-to-peer play without operating servers. The active proposal is Photon
Fusion Host Mode with managed NAT/relay, prediction, reconciliation and bounded lag
compensation. It replaces the intermediate dedicated-server proposal. The owner-driven
movement, Unity Netcode/Relay and latency shortcuts below remain retired; player
hosting now follows the foundation's explicit host-trust and migration contracts.
Gameplay numbers and weapon concepts below are unapproved examples to revisit after
the foundation passes its acceptance gates. C / Ink & Concrete is the selected base
art direction, with C3 / Wildstone Ruins selected for a sleek, mostly monochrome
modernist map refinement. See the [current map brief](2026-10-25-wildstone-modern-map-design.md)
and [separate character/weapon workflow](2026-10-25-characters-weapons-design-workflow.md).
Characters are simple human silhouettes; the original animal/equipment theme is retired.

## Intent and confirmed requirements

Create a separate Windows game in `C:\Users\tyler\Main Storage\multiplayer_1`, using
`C:\Users\tyler\Main Storage\vibegame1` as source material. Preserve its developed
systems and level-making tools, then build a multiplayer arena battler around them.
Four teams can each contain up to two players; teams do not have to be full.
Players race into the center and other valuable areas for unusual, powerful weapons,
fight one another, and capture a moving zone. WARDOGS is a reference for the objective,
not a request to copy its whole game. The user confirmed **internet play with friends**.

The rules below are proposed defaults, not additional user requirements. The first
playable should deliver a complete repeatable match with real remote players.

## Approach

The requested reuse points to a **separate source fork with a new arena mode**.
Keep the source's Unity version, assets, GUIDs, movement, presentation, and authoring
tools. Give the new game its own identity, save location, scenes, match flow, and
networked combat. Leave the original project untouched.

Other approaches considered:

- Extract only a few scripts into a clean project: fewer initial files, but much
  more dependency repair and lost authoring capability. This conflicts with keeping
  the developed systems available.
- Add multiplayer inside the original game: easiest access to assets, but couples
  this experiment to the original campaign and ongoing work. The requested separate
  game and empty destination make the fork the appropriate boundary.

This is a new game project, not a small multiplayer toggle. Reusing the existing
content saves substantial work; multiplayer ownership and match rules are new work.

## Verified source baseline

Inspected on 2026-10-25, at commit
`09e10da3fb1fd64003aa14ec6597acb936470740`. The working tree has untracked personal
files; no tracked changes appeared in the inspection. Handoff gameplay evidence
refers to `28483c9`; the newer HEAD records that handoff and packaging evidence.
The four main source trees contain 4,633 tracked files totaling approximately
0.33 GiB. `multiplayer_1` was empty before this design was written.

| Existing system | Evidence read | Reuse and required adaptation |
| --- | --- | --- |
| Unity 6000.5.10f1, URP, New Input System | ProjectVersion, package manifest, AGENTS | Keep the toolchain and asset references. The Multiplayer Center package is present, but no gameplay network implementation was found. |
| Dash, slide, wall run/jump, momentum and traversal | `FirstPersonMotor.cs`, `MOVEMENT-PRINCIPLES.md` | Retain the motor and shipped tuning. Only the owning client's input drives its player; remote actors must not run local input/camera code. |
| Firearms and first-person weapon presentation | `FirearmData.cs`, `LaserPistol.cs`, `LaserShot.cs` | Five profiles exist: pistol, sniper, assault rifle, SMG, akimbo. Reuse assets and presentation. The current trace resolves enemy health, so it is not a PvP damage implementation. |
| Combat feel and effects | `PlayerCombat.cs`, `TimeScaleController.cs`, effect file inventory | Retain suitable cues, impacts and recoil. Global hitstop, pause and slow motion cannot control a shared match. |
| Level data and generation | `LevelDefinition.cs`, `LevelDefinitionBuilder.cs`, `LevelPieceFactory.cs` | Reuse geometry, ramps, materials and traversal factories. `BuildWorldContent` already separates world construction from campaign player/manager/HUD instantiation. |
| Level Studio and runtime editing | `LEVEL-EDITOR.md`, Level Studio file inventory | Keep the tools. Arena geometry remains editable, while team spawns, weapon pads and zone route are authored as arena data. Live matches cannot enter the campaign editor. |
| Local state and UI | `GameManager.cs`, `InputReader.cs`, `GameEvents.cs` | Singletons and player-wide events assume one local player. Retain them only as local presentation/input services where appropriate; match state is separate and networked. |

Copy committed `Assets`, `Packages`, `ProjectSettings`, and relevant `Tools`, with
credits and source provenance. Keep the source documentation as reference without
installing its campaign-specific instructions as this project's active contract.
Preserve `.meta` files. Do not copy caches, builds, credentials, local tool settings,
or untracked personal radio files. Imported campaign systems stay available as
source material; they are not all activated in an arena match.

## Proposed match rules

- First-person movement and mouse/keyboard/gamepad support from the source.
- Four named/color-coded teams, maximum two players each, maximum eight total.
  A host can start with two players on different teams. Empty teams are allowed;
  a one-player team can win normally. A host alone can explore in warmup.
- Host creates a room, shares a join code, players choose available team slots,
  and the host starts a 10-second countdown. Team capacity is enforced by the host.
- A match lasts up to 10 minutes. First to 180 capture points wins. Kills clear
  opponents and earn scoreboard credit, but do not add capture points.
- One continuously moving capture zone follows a visible, authored loop through
  the center and side areas. Initial tuning: 8 m radius, 2 m/s. Show its boundary,
  current destination and travel direction. All clients use the host's match clock.
- One team alone inside scores one point per second, regardless of whether it has
  one or two occupants. Multiple teams inside means contested and no scoring.
  Empty zones retain no owner and award no points. No two-player capture multiplier.
- Capture requires a living, connected, unprotected player's grounded position in
  the zone's authored floor volume; being above it on an unrelated floor does not
  count. The route stays on connected, reachable surfaces, with vertical bounds.
- At the time limit, highest score wins. A tied lead enters overtime, with ordinary
  scoring continuing until the first scoring tick produces one leader. Results show
  all team scores and individual eliminations, followed by return to the room.
- Death respawns after five seconds with the starter weapon. Spawn selection uses
  several authored points in the team's area and prefers ones away from enemies.
  Protection lasts 1.5 seconds, ends on firing or leaving the spawn area, and prevents
  capture and contesting while active. Falling out of bounds uses the same death flow.
- No friendly damage or friendly knockback. Death clears temporary pickups; no
  persistent power or shop upgrades carry between matches.
- Players may join an available team slot during a match with the starter kit.
  Teams cannot be switched mid-match. Disconnect removes presence immediately.
  If fewer than two teams remain occupied, end with an abandoned-match result,
  not free capture farming. Host departure ends the session with a clear message.

## Arena and weapon loop

Build one compact arena from the existing architecture, materials, ramps, wall-run
faces, bounce orbs and water traversal. Four equally distant starts feed inward
through cover and flank routes. The central arena holds the strongest contested
pickup, side platforms hold different power weapons, and outer routes let a team
rotate without crossing every opponent's sight line.

Start the blockout around a 120 m footprint, then tune it by measured travel time:
roughly 8–12 seconds from each start to a central pickup using ordinary movement.
Routes must provide a normal traversable way to every objective; advanced parkour
provides faster or less exposed approaches. No spawn has direct sight of another
spawn. No single elevated perch sees the whole zone route. Match routes stay fixed
for this first arena so movement mastery remains useful.

Everyone has a reliable starter pistol with renewable magazines and one power-weapon
slot. Interact explicitly to take or replace a pickup. A power weapon has a finite
ammo pool, cannot be refilled by a normal reload, and falls back to the pistol when
empty. Picking up another replaces the slot; death deletes it. Weapon pads have
clear silhouettes and respawn timers. All pad claims are resolved once by the host.

Proposed initial power weapons:

| Weapon | Fight-changing behavior | Counterplay |
| --- | --- | --- |
| Blast cannon | Slow explosive projectile with radial damage and knockback; center pickup | Visible projectile, cover blocks blast, direct projectile can be deflected |
| Ricochet launcher | Bounded wall bounces let shots pressure enemies behind a corner | Visible trail, finite lifetime and bounce count, deflectable |
| Gravity orb | A short-lived field pulls opponents off a route or out of the zone | Visible arming cue, limited radius/duration, breaks against cover; no damage over time |

Use existing gun models, spell materials and impact effects where they fit. New
weapon effects need their own server-side behavior; enemy-targeting code cannot
be treated as already multiplayer-ready. Weapon damage, ammo, cooldowns, projectile
speed, radii and forces are data values, tuned after a complete match is playable.
Additional source firearms remain available for later balancing and content work.

Retain a dedicated deflector action as a proposed skill-based answer to visible
projectiles. Use the source's cue language and gauntlet presentation, available to
every player without purchasing it. Only the host accepts a valid active window and
front-facing collision; a reflected projectile transfers attacker/team credit and
can hit its original shooter. Hitscan and area fields are not reflectable. Do not
carry over cinematic deathblows, PvE posture locks, or world-wide slow motion.

## Multiplayer architecture and internet access

Use Unity Netcode for GameObjects and Unity Transport for the existing GameObject
codebase. Use Unity Multiplayer Services sessions with Relay for friend join codes.
Prefer the integrated session lifecycle rather than starting the transport twice
through both session and manual Relay APIs. Pin compatible package versions after
checking them in Unity 6000.5.10f1. Do not implement a custom network protocol.

The host is the authority for team allocation, health, damage, ammo, cooldowns,
projectiles, pickup claims, deaths, respawns, zone state, scoring and results.
Clients submit intent; they cannot submit trusted damage, score, health, weapon
ownership or arbitrary spawn locations. Requests are bounded and checked against
sender identity, current state, rate, weapon, origin proximity and aim validity.

To preserve the existing responsive motor, the first friends-only build uses
owner-driven movement with remote interpolation and host checks for impossible
displacement, world bounds and unauthorized teleports. Account for legal dash,
launch and weapon impulses. Host-originated respawns/impulses have explicit network
events. This is not ranked competitive movement validation or full prediction and
reconciliation. The host resolves hits against its current accepted snapshots;
no rewind lag compensation is promised for the first playable.

Use separate network player state and local presentation. Only the owner has active
input, first-person camera, audio listener and viewmodel; remote players have visible
third-person bodies, held-weapon indicators and team markings. Only the local player
feeds legacy HUD events. Remote death must not freeze the host or reset another
player's weapon. Use local recoil, sound and brief visual response for impact feel,
without changing shared time. Escape opens a local menu while the match continues.

Late join synchronizes team roster, match clock, scores, zone position, existing
players, health and pickup availability. Invalid/full/expired rooms and connection
failures return an actionable message. No automatic host migration in this version.
Use separate anonymous-authentication profiles for multiple local test clients.

The inspected source has an empty `cloudProjectId`. **Internet join-code testing
requires a Unity cloud project with Authentication and Relay configured.** The
implementation can include direct localhost/LAN connection for development, but
that is not acceptance evidence for the requested internet play. Account access or
service enablement may require the user; do not invent a project ID or enable paid
services on their behalf. Report any external configuration blocker specifically.

Official references checked on 2026-10-25:

- [Unity sessions with Netcode for GameObjects](https://docs.unity.com/en-us/mps-sdk/tutorials/build-your-first-session)
- [Relay integration and project configuration](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo)
- [Session lifecycle](https://docs.unity.com/en-us/mps-sdk/sessions)

## Authoring and change boundaries

Keep `VibeGame1` namespace and existing GUIDs in copied systems to avoid a cosmetic
rename migration. Arena-specific code can live in `VibeGame1.Arena` under new Arena
folders. Give the build a distinct product/save identity so it cannot overwrite
campaign preferences or progress.

Use the existing `LevelDefinition`/Level Studio pipeline for geometry and traversal.
An arena definition references that geometry and stores team spawn groups, weapon
pads, zone waypoints/volumes, and match tuning. Build an arena scene without campaign
managers, boss portals, run progression or shop flow. A dedicated editor command
regenerates the arena and preserves a manual-content root using existing conventions.
Author arena-only markers in the Inspector for the first playable; do not invent a
second full level editor or a collaborative network editor.

Expected change surface: package/settings files; arena data, scenes, network player
and weapon prefabs; lobby and HUD; arena build/validation commands; narrow local-
ownership hooks in copied motor/input/presentation code; scoped tests; build entry
point and player instructions. Before edits, trace all callers of each copied core
system being changed. Keep existing motor math and its tests. Do not silently run
source-project generators or point a copied Unity bridge at the source editor.

## What proves the first playable works

1. The new project imports and compiles in the pinned Unity version. Its arena
   regenerates from data, and its Windows build starts in the new host/join menu.
2. Focused rule tests cover team caps, uneven rosters, uncontested/contested scoring,
   vertical zone exclusion, overtime, pickup races, death/respawn, reflection credit,
   disconnect cleanup and rejecting unauthorized combat requests.
3. Retained movement tests pass after ownership changes. Editor validation checks
   all team starts, reachable zone paths, pickup definitions and missing references.
4. Separate processes verify owner-only input/camera, visible remote players,
   replicated damage/projectiles, pickup contention, late join and host departure.
   Eight clients validate roster capacity and replication; this can be automated
   locally and is explicitly distinct from eight human internet players.
5. A host and a client on independent external networks join by code and finish a
   full match. Test uneven teams. If a second external endpoint is unavailable,
   report that internet acceptance is still pending rather than calling LAN proof
   internet verification.
6. A moving capture demonstrates traversal, pickup competition, combat, a zone
   contest, death/respawn and results in a Windows build. Stills and compile checks
   do not prove match feel. Simulated latency/loss tests expose jitter and duplicate
   actions, with results and limitations recorded.

The builder may choose exact weapon tuning, names, asset assignments, match colors,
file decomposition and test harness details within these rules. Team readability
uses labels/shapes as well as color; the source settings and controller support
remain available. A human playtest decides fairness and feel.

## Deliberate limits

The first delivery is a private-friends playtest with one arena and a complete
match loop. It does not include ranked matchmaking, dedicated hosting, host
migration, progression, a cosmetic store, bots, split-screen, workshop downloads,
destructible environments or a public release. Existing campaign content stays
available in the source fork without requiring it in the arena runtime.

Main risks are preserving fast movement over latency, separating single-player
events from remote actors, and access to the cloud configuration needed for real
internet testing. Nothing in the inspection establishes that these are implemented.
