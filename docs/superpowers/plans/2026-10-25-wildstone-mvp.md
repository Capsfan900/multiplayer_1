# Wildstone MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the approved Unity FPS arena game with real Photon Fusion multiplayer, three distinct maps, complete starter content, polished minimalist flow and evidence-backed QA.

**Architecture:** Keep deterministic rules and explicit tick state separate from Unity presentation. Use Photon Fusion Host Mode directly for authoritative network execution, prediction/replay and recovery. Reuse audited source movement laws and geometry; never transplant campaign globals or fabricate a replacement networking proof.

**Tech Stack:** Unity 6000.5.10f1, URP 17.5.0, Input System 1.20.0, UI Toolkit, Fusion 2.1.3 (compatibility pending), Blender bpy 4.2 through the existing isolated Python environment. Windows standalone.

**Spec:** [Foundation](../specs/2026-10-25-multiplayer-foundation-design.md), [combat](../specs/2026-10-25-arena-combat-items-zone-design.md), [rendering](../specs/2026-10-25-unity-rendering-design.md), [maps](../specs/2026-10-25-wildstone-modern-map-design.md), [models](../specs/2026-10-25-characters-weapons-design-workflow.md), [asset pipeline](../../ASSET-PIPELINE.md).

## Global Constraints

- Four teams of up to two, eight participants maximum; incomplete teams allowed; start needs at least two occupied teams.
- Unity FPS, Fusion Host Mode, internet friend matches. Neither a local preview nor multiple processes on one machine demonstrates internet or eight-machine performance acceptance.
- C3.1 Wildstone Modern, soft lighting, mostly monochrome matte architecture, simple human silhouettes and approved simplified v2 weapons.
- Line Driver, Pulse Lobber, Rebound Disc, Push Lens, grenade, stun gun, laser sword; exactly Blink and temporary shield as special consumables.
- Keep `vibegame1` and `ai_skelly_tool` unchanged. Copy only selected committed inputs; record provenance and licenses. No GPU generation stack.
- No paid plans, purchases or deployments. SDK/account access requiring the user stays an explicit dependency. Do all independent work while the user is away.
- Foundation performance targets remain 120 Hz simulation / 60 Hz snapshots; 144 FPS reference rendering. Targets are not measurements and may only be revised openly with evidence.
- Preserve historical pause packet/archive. Update active evidence, accepted decisions and canonical owners, not the historical snapshot.
- Minimalist finished menus and a complete play loop are deliverables. Avoid shipping a developer-only IMGUI menu as the final UI.

## Review Focus

- Malformed/stale/duplicate commands and changing connection/life generations must never double-spend, duplicate seats or control another avatar (tasks 2-4).
- Blink, melee and rewind across a teleport/death must not hit through the displacement or resurrect stale hit history (tasks 2 and 4).
- A reconnect or host change during pickups, effects, zone scoring or opponent absence must preserve one coherent checkpoint and original deadlines (tasks 3-4).
- Zone footprints on ramps and stacked floors, and unequal team sizes, must score only eligible occupants at a fixed team rate (tasks 4 and 6).
- Cancellation during connection/loading, focus loss and error/retry must leave usable input/UI and no orphan runner or leaking audio/VFX (tasks 3, 7 and 8).

## Builder rulings and external dependencies

The user resumed the already-discussed design and delegated implementation choices; no additional design approval loop is required. Three maps and menu polish are new explicit requirements. Proposed balance values become clearly labeled initial tuning defaults in the combat owner before implementation, not claimed past approvals.

Photon's official SDK link currently requires a signed-in Photon account. No SDK copy was found in Downloads or the source workspace. A Fusion 2 App ID and verified identity-provider configuration are also absent. Work on code/assets independent of those inputs is authorized while the user sleeps; authentic SDK integration and internet validation remain unproved until access is available. Do not implement a fake Fusion shim. Any local exercise mode must visibly say offline practice/development and use the same game rules; it is not a multiplayer substitute.

An SDK access block is not a failed movement experiment. Independent rule tests, asset authoring and UI can proceed under D018 while integration is pending; network-dependent acceptance stays open. No runtime failure may be hidden by relaxing the spec.

## Task 1: Reproducible Unity workspace and verification tools

**Files:** `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `.gitignore`, `Assets/Wildstone/Editor/ProjectSetup.cs`, `Tools/Unity/Invoke-Unity.ps1`, `docs/TOOLING.md`, `docs/SOURCE-REUSE.md`.

**Interfaces:** `Wildstone.Editor.ProjectSetup.Configure()` generates this project's own render settings and test/bootstrap scene; `Tools/Unity/Invoke-Unity.ps1` supports compile, tests and Windows build against this absolute project root, with log/result paths under `Artifacts`.

- [ ] Pin the installed Unity version and necessary packages only; establish local version control on an MVP branch if allowed by the environment.
- [ ] Add build/test tooling with containment checks and failure propagation; exercise invalid action/path arguments before the valid run.
- [ ] Import/install the real Fusion SDK when legitimately available; record version and package checksum. Otherwise record the specific blocked SDK gate without placeholder APIs.
- [ ] Configure URP, linear color, text serialization, separate product/save identity and source-based layer policy. Compile a Windows bootstrap and run an empty-suite/tooling smoke check without claiming gameplay coverage.
- [ ] Record logs, licenses/source pins and compatibility results, then review the task.

## Task 2: Explicit movement state and local presentation

**Files:** `Assets/Wildstone/Runtime/Movement/ArenaMotor.cs`, `MotorState.cs`, `MotorInput.cs`, `MotorTuning.cs`, copied pure movement helpers under `Runtime/Movement/Source`, `Runtime/Presentation/FirstPersonView.cs`, tests under `Assets/Wildstone/Tests/EditMode` and `PlayMode`.

**Interfaces:** `ArenaMotor.Simulate(in MotorInput input, float deltaTime, int tick)` operates on restorable `MotorState`; `CaptureState()` / `RestoreState(in MotorState)` round-trip every simulation-affecting field. Inputs are finite, bounded intent; camera samples/rendering never supply authoritative position/delta. State has position/velocity/stance/grounding, traversal contacts and all stamina/jump/dash/slide/wall timers.

- [ ] Preserve actual source prefab tuning and movement-law inventory, not AddComponent defaults. Build a compact source parity fixture.
- [ ] Write and observe failing state-restore/replay, input-edge, coyote/jump-buffer, wall-run, slide, water/launch and missing-input tests.
- [ ] Adapt source laws and collision interaction; separate feedback from replay, keep next-frame camera response and tick-based movement.
- [ ] Run deterministic command sequences from restored snapshots, then real Unity collision tests on a small course. Capture deviations and correct causes.
- [ ] Add Fusion input/state mapping and host-plus-two-client checks only against the actual SDK. Keep the gate open while SDK/account access is absent.

## Task 3: Session lifecycle, identity, seats and recovery

**Files:** `Runtime/Networking/FusionSession.cs`, `FusionPlayer.cs`, `FusionMatch.cs`, `Runtime/Session/SeatTable.cs`, `SessionState.cs`, `Runtime/Profile/PlayerProfile.cs`, session/recovery tests.

**Interfaces:** `SeatTable.TryJoin(stableAccountId, preferredTeam, connectionGeneration)` / `Disconnect` / `TryReconnect` / `Expire` enforce 4x2 capacity. FusionSession owns one runner lifecycle and exposes state/error/cancel events for UI. Real managed identity is a provider-verified subject, never an RPC-supplied string. Network objects own motor/combat/match tick state.

- [ ] Write failing last-seat race, duplicate account, old generation, 30-second hold and expired-seat tests.
- [ ] Implement private room code/create/join, protocol/build/map/ruleset compatibility and ready/team selection, with timeouts and cancellation.
- [ ] Integrate a free eligible managed identity route only after its configuration is established; do not silently downgrade to trusted local identity. Local test identity is visibly development-only.
- [ ] Implement 5-second vulnerable disconnect avatar, 30-second seat hold and one non-extending 30-second opponent-absence deadline.
- [ ] Implement Fusion checkpoint migration: 2-second snapshots, at most 3 seconds lost simulation, at most 30 seconds recovery; abort otherwise. Rebuild state and lookup tables, clear old histories and rebind verified identities.
- [ ] Test disconnect/cancel/rejoin and graceful/crash migration with actual SDK clients; record each field's restoration and fail paths.

## Task 4: Authoritative combat, items, pickups and travelling zone

**Files:** `Runtime/Combat/CombatState.cs`, `CombatRules.cs`, `WeaponCatalog.cs`, `ArenaProjectile.cs`, `Runtime/Match/ArenaMatch.cs`, `MovingZone.cs`, `PickupState.cs`, combat/match tests.

**Interfaces:** validated actions are tick/life/connection/action-sequence scoped. `CombatRules` returns committed state changes and events once; presentation consumes event IDs. `MovingZone` derives position from authored route and match tick; occupancy uses player feet and bounded floor height.

- [ ] Record initial tuning and deterministic same-tick policy in the combat spec. Keep starter plus one replaceable special, grenade ammo and the two dedicated consumables.
- [ ] Write failing tests for repeated/stale actions, atomic pickup losers/full capacity, shield spillover/expiry, stun immunity, death cleanup, scoring ties/contests and incomplete teams.
- [ ] Implement the seven weapon behaviors, authoritative damage/impulses/cover and bounded projectile lifetimes/ricochets; hitscan uses actual Fusion lag-compensation when online.
- [ ] Implement wall-blocked short Blink with legal landing, no-charge rejection and discontinuity; shield does not stack/refresh and is not invulnerability.
- [ ] Enforce phase order: expire effects, validate/reserve actions, movement/projectiles/combat, deaths, occupancy/scoring. Accepted same-tick attacks may trade; dead-at-acceptance attacks fail.
- [ ] Verify sphere/capsule/cover interactions in Unity and snapshot restore for every timer/projectile/item; map-independent rules must pass offline as well as online.

## Task 5: Editable production models and effects

**Files:** `Tools/Assets/build_assets.py`, `ArtSource/*.blend`, `Assets/Wildstone/Art`, `Runtime/Presentation/PlayerVisual.cs`, `WeaponView.cs`, `ArenaEffects.cs`, `ArenaAudio.cs`.

**Interfaces:** stable named root, grip, muzzle and bone/socket transforms; explicit Unity meter/axis convention. Generator requires a contained output root and retains `.meta` GUIDs. Effects consume authoritative/predicted event IDs, never cause gameplay damage.

- [ ] Verify bpy import in an isolated process without writing into sibling environments; export/import an asymmetric 1 m fixture with grip/muzzle.
- [ ] Produce a shaped low-poly human with explicit rig/weights, first-person hands and four team symbols; validate bind pose and animation scale.
- [ ] Produce all seven equipment models plus Blink/shield pickups, retaining the four approved v2 silhouettes and shared matte material treatment.
- [ ] Add local readable firing/projectile/ricochet/blast/impulse/stun/melee/Blink/shield/death/zone VFX and restrained generated or audited sound cues. Bound pools and particle counts; no global time scale/hitstop.
- [ ] Capture moving first-/third-person evidence, inspect dark/light backgrounds and low quality, then fix framing and readability issues.

## Task 6: Three distinct authored arenas

**Files:** `Runtime/Maps/ArenaDefinition.cs`, `Editor/ArenaBuilder.cs`, copied `Runtime/Maps/Source/LevelMesh.cs`, map definitions/scenes under `Assets/Wildstone/Maps`, editor map validators/tests.

**Interfaces:** stable map IDs `atrium`, `split-gallery`, `terrace-steps`; definitions contain geometry, four team spawn groups, pickups, zone route and playable bounds. Builder creates render meshes and simple colliders separately, saves assets, and generates UV2 before any bake.

- [ ] Extend the owning map spec with the three layouts and initial dimensions. Use one modular C3.1 kit, distinct route topology and silhouette landmarks.
- [ ] Write failing validators for missing team starts, invalid pickup IDs, disconnected/obstructed zone routes and floor-support gaps.
- [ ] Adapt the pure source chamfered mesh builder and minimal platform/ramp patterns; do not import the campaign level builder wholesale.
- [ ] Build Atrium's court/gallery, Split Gallery's parallel corridors/crossovers and Terrace Steps' staggered elevations/broad ramps. Keep four screened approaches and center/side reward routes.
- [ ] Verify traversal, zone footprint support, spawn sightlines and pickup reachability; measure opening routes and document fairness limits rather than assume symmetry proves balance.

## Task 7: Minimalist finished menus and full play loop

**Files:** `Runtime/UI/ArenaMenu.cs`, `ArenaHud.cs`, `LocalSettings.cs`, `Assets/Wildstone/UI/*.uss`, bootstrap scene and UI/settings tests.

**Interfaces:** UI binds to session/match events; settings affect local presentation only. Screen states: title, play/create/join, lobby, loading, playing/pause, results, failure/recovery. Offline practice must be explicitly labeled until online service setup is complete.

- [ ] Implement clean soft-modern typography, spacing, warm ivory/charcoal palette, subtle accents and short transitions with a real rendered arena backdrop.
- [ ] Provide readable keyboard/mouse focus, scalable layout, sensitivity/FOV/audio/quality controls, team symbols and connection/error feedback. Do not expose service credentials or put routine implementation detail in player flows.
- [ ] Add compact HUD: health/shield, ammo/weapon, two consumables, four-team score, timer, moving-zone status and restrained damage/hit cues.
- [ ] Test empty/invalid join code, cancellation, lost focus, screen resize, settings clamping/persistence, results/rematch and leave/retry transitions without stranded input.
- [ ] Validate actual packaged-window interaction and screenshots; fix clipping, visual hierarchy and error recovery.

## Task 8: Build, adversarial QA, performance and user completion report

**Files:** `Tools/QA`, `Artifacts/QA`, `docs/QA.md`, `docs/HANDOFF.md`, `docs/USER-SETUP.md`, Windows build under `Builds/Windows`.

- [ ] Run relevant EditMode/PlayMode tests and Windows compilation; review all important findings and retest fixes.
- [ ] Exercise all maps/weapons/items, full UI flow, death/respawn and zone completion in the standalone build; save moving evidence and exact build identity.
- [ ] Run actual Fusion multi-client/network impairment tests when SDK/App ID are configured. Local processes are functional evidence only.
- [ ] Perform scoped performance captures without screenshot polling. Final full-network acceptance remains the canonical separate-machine 8-player/direct-relay matrix, 30-minute profiles, two-hour soak and 100 reconnect cycles.
- [ ] If the user is still needed for SDK/account access or external machines, finish all independent work, retain clearly marked open gates and provide exact free setup steps, expected outcomes and tests to complete. Never label unrun internet or secure identity tests passed.
- [ ] Package a versioned Windows build with hashes, editable sources, source/license provenance and a concise current handoff. No public release or paid action is implied.

## Execution record

Live task status, evidence and rulings: [MVP progress](../../MVP-PROGRESS.md). The historical pause archive is immutable.
