# Wildstone MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the approved Unity FPS arena game with real Photon Fusion multiplayer, three distinct maps, complete starter content, polished minimalist flow and evidence-backed QA.

**Architecture:** Keep deterministic rules and explicit tick state separate from Unity presentation. Use Photon Fusion Host Mode directly for authoritative network execution, prediction/replay and recovery. Reuse audited source movement laws and geometry; never transplant campaign globals or fabricate a replacement networking proof.

**Tech Stack:** Unity 6000.5.10f1, URP 17.5.0, Input System 1.20.0, UI Toolkit, Fusion 2.1.3 (compatibility pending), Blender bpy 4.2 through the existing isolated Python environment. Windows standalone.

**Spec:** [Foundation](../specs/2026-10-25-multiplayer-foundation-design.md), [combat](../specs/2026-10-25-arena-combat-items-zone-design.md), [rendering](../specs/2026-10-25-unity-rendering-design.md), [maps](../specs/2026-10-25-wildstone-modern-map-design.md), [models](../specs/2026-10-25-characters-weapons-design-workflow.md), [asset pipeline](../../ASSET-PIPELINE.md).

**2026-10-27 resume:** existing authored work was recovered into `mvp-2` without
changing the original checkout. Native tests, preparation, three-map baking and an
offline Windows build now pass. [QA](../../QA.md) records evidence; unchecked mixed
offline/online steps remain open until their whole acceptance requirement is met.

## Global Constraints

### 2026-10-28 unattended polish continuation

The user authorized useful polish while away. Preserve the approved game, UI and
art contracts; proceed with local, reversible improvements and focused checks.

- [x] Fix concrete menu focus/HUD lifetime issues and expose honest recovery status
  in existing UI/controller files; add regressions and inspect packaged captures.
- [x] Replace coordinator scene-wide discovery allocations with existing Fusion
  runner-local list APIs; verify late spawn/despawn/render-root changes. Allocation
  calibration failed on this Unity runtime; measured savings remain unverified.
- [x] Extend the existing capacity diagnostic with a bounded five-minute soak and
  per-process memory observations; retain all admission, state and log gates.
- [x] Add compile-time-only automatic captures of actual menus and three maps using
  Unity RenderTexture/ImageConversion. Keep desktop input untouched and settings unsaved.
- [x] Run focused native/live checks, inspect captured images, build a fresh local
  Windows player and update the canonical evidence/handoff. External networks,
  eight-machine performance and human feel remain separate acceptance gates.

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

### 2026-10-28 first-person body and slide continuation

- [x] Adapt visible local legs in `FirstPersonBody.cs` using original human meshes;
  verify gait/air/slide, yaw-only body, death/life reset and framed boots.
- [x] Extend `WeaponView.cs` with articulated hands/sleeves, grip/support alignment,
  locomotion and confirmed combat/timer-driven animation; add native regressions.
- [x] Extend `FirstPersonView.cs` with bounded cosmetic slide feedback preserving
  authoritative eye/aim; verify reduced motion, settings and lifecycle resets.
- [x] Wire local motor/combat/presentation through `ArenaPresentation` and
  `PracticeSession`, once per render and with existing event deduplication.
- [x] Run focused native checks and diagnostic packaged pose captures, inspect
  visuals, build a fresh normal player and update QA/handoff with acceptance limits.

Root owns integration, captures, build and docs. GPT-6.1 Sol delegates own separate
body, hands and camera files/tests. Source `vibegame1` remains read-only; no gameplay
balance or source asset regeneration is planned.

- Malformed/stale/duplicate commands and changing connection/life generations must never double-spend, duplicate seats or control another avatar (tasks 2-4).
- Blink, melee and rewind across a teleport/death must not hit through the displacement or resurrect stale hit history (tasks 2 and 4).
- A reconnect or host change during pickups, effects, zone scoring or opponent absence must preserve one coherent checkpoint and original deadlines (tasks 3-4).
- Zone footprints on ramps and stacked floors, and unequal team sizes, must score only eligible occupants at a fixed team rate (tasks 4 and 6).
- Cancellation during connection/loading, focus loss and error/retry must leave usable input/UI and no orphan runner or leaking audio/VFX (tasks 3, 7 and 8).

## Builder rulings and external dependencies

The user resumed the already-discussed design and delegated implementation choices; no additional design approval loop is required. Three maps and menu polish are new explicit requirements. Proposed balance values become clearly labeled initial tuning defaults in the combat owner before implementation, not claimed past approvals.

SDK provisioning completed on 2026-10-27: the user supplied Fusion 2.1.3 Stable build 2390 and a Fusion App ID. The SDK is imported and compiles; the App ID persists in ignored local settings. Real runner/gameplay integration, Photon-only guest admission and internet validation remain open. Do not implement a fake Fusion shim. Any local exercise mode must visibly say offline practice/development and use the same game rules; it is not a multiplayer substitute.

An SDK access block is not a failed movement experiment. Independent rule tests, asset authoring and UI can proceed under D018 while integration is pending; network-dependent acceptance stays open. No runtime failure may be hidden by relaxing the spec.

## Task 1: Reproducible Unity workspace and verification tools

**Files:** `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `.gitignore`, `Assets/Wildstone/Editor/ProjectSetup.cs`, `Tools/Unity/Invoke-Unity.ps1`, `docs/TOOLING.md`, `docs/SOURCE-REUSE.md`.

**Interfaces:** `Wildstone.Editor.ProjectSetup.Configure()` generates this project's own render settings and test/bootstrap scene; `Tools/Unity/Invoke-Unity.ps1` supports compile, tests and Windows build against this absolute project root, with log/result paths under `Artifacts`.

- [x] Pin the installed Unity version and necessary packages only; establish local version control on an MVP branch if allowed by the environment.
- [x] Add build/test tooling with containment checks and failure propagation; exercise invalid action/path arguments before the valid run. Native execution now passes; focused `-TestFilter` runs are supported.
- [x] Import/install the real Fusion SDK; version, checksum, local App ID configuration and native compile are recorded in QA and `Artifacts/Resume/photon-setup.md`.
- [x] Configure URP, linear color, text serialization, separate product/save identity and source-based layer policy. Native tests and offline Windows build supersede the bootstrap-only smoke check; Fusion compatibility is separate.
- [ ] Record logs, licenses/source pins and compatibility results, then review the task.

## Task 2: Explicit movement state and local presentation

**Files:** `Assets/Wildstone/Runtime/Movement/ArenaMotor.cs`, `MotorState.cs`, `MotorInput.cs`, `MotorTuning.cs`, copied pure movement helpers under `Runtime/Movement/Source`, `Runtime/Presentation/FirstPersonView.cs`, tests under `Assets/Wildstone/Tests/EditMode` and `PlayMode`.

**Interfaces:** `ArenaMotor.Simulate(in MotorInput input, float deltaTime, int tick)` operates on restorable `MotorState`; `CaptureState()` / `RestoreState(in MotorState)` round-trip every simulation-affecting field. Inputs are finite, bounded intent; camera samples/rendering never supply authoritative position/delta. State has position/velocity/stance/grounding, traversal contacts and all stamina/jump/dash/slide/wall timers.

- [x] Preserve actual source prefab tuning and movement-law inventory, not AddComponent defaults. Build a compact source parity fixture.
- [ ] Write and observe failing state-restore/replay, input-edge, coyote/jump-buffer, wall-run, slide, water/launch and missing-input tests.
- [x] Adapt source laws and collision interaction; separate feedback from replay, keep next-frame camera response and tick-based movement. Human feel/source trajectory acceptance remains separate.
- [x] Run restored command sequences and real Unity collision tests on a small course. Record bounded native vector deviations without changing timers, discrete state or network budgets.
- [ ] Complete host-plus-two-client checks against the actual SDK. Input/state mapping, networked movement and a real Single-runner native fixture now pass; remote prediction/correction remains open.

## Task 3: Session lifecycle, identity, seats and recovery

**Files:** `Runtime/Networking/FusionSession.cs`, `FusionPlayer.cs`, `FusionMatch.cs`, `Runtime/Session/SeatTable.cs`, `SessionState.cs`, `Runtime/Profile/PlayerProfile.cs`, session/recovery tests.

**Interfaces:** `SeatTable.TryJoin(stableAccountId, preferredTeam, connectionGeneration)` / `Disconnect` / `TryReconnect` / `Expire` enforce 4x2 capacity. FusionSession owns one runner lifecycle and exposes state/error/cancel events for UI. D023 uses an SDK-bound temporary guest ID plus the original credential for held-seat recovery; no verified account is claimed. Network objects own motor/combat/match tick state.

- [ ] Write failing last-seat race, duplicate account, old generation, 30-second hold and expired-seat tests.
- [ ] Implement private room code/create/join, protocol/build/map/ruleset compatibility and ready/team selection, with timeouts and cancellation.
- [ ] Integrate a free eligible managed identity route only after its configuration is established; do not silently downgrade to trusted local identity. Local test identity is visibly development-only.
- [ ] Implement 5-second vulnerable disconnect avatar, 30-second seat hold and one non-extending 30-second opponent-absence deadline.
- [ ] Implement Fusion checkpoint migration: 2-second snapshots, at most 3 seconds lost simulation, at most 30 seconds recovery; abort otherwise. Rebuild state and lookup tables, clear old histories and rebind guest peers with original credentials.
- [ ] Test disconnect/cancel/rejoin and graceful/crash migration with actual SDK clients; record each field's restoration and fail paths.

## Task 4: Authoritative combat, items, pickups and travelling zone

**Files:** `Runtime/Combat/CombatState.cs`, `CombatRules.cs`, `WeaponCatalog.cs`, `ArenaProjectile.cs`, `Runtime/Match/ArenaMatch.cs`, `MovingZone.cs`, `PickupState.cs`, combat/match tests.

**Interfaces:** validated actions are tick/life/connection/action-sequence scoped. `CombatRules` returns committed state changes and events once; presentation consumes event IDs. `MovingZone` derives position from authored route and match tick; occupancy uses player feet and bounded floor height.

- [x] Record initial tuning and deterministic same-tick policy in the combat spec. Keep starter plus one replaceable special, grenade ammo and the two dedicated consumables.
- [ ] Write failing tests for repeated/stale actions, atomic pickup losers/full capacity, shield spillover/expiry, stun immunity, death cleanup, scoring ties/contests and incomplete teams.
- [ ] Implement the seven weapon behaviors, authoritative damage/impulses/cover and bounded projectile lifetimes/ricochets; hitscan uses actual Fusion lag-compensation when online.
- [ ] Implement wall-blocked short Blink with legal landing, no-charge rejection and discontinuity; shield does not stack/refresh and is not invulnerability.
- [ ] Enforce phase order: expire effects, validate/reserve actions, movement/projectiles/combat, deaths, occupancy/scoring. Accepted same-tick attacks may trade; dead-at-acceptance attacks fail.
- [ ] Verify sphere/capsule/cover interactions in Unity and snapshot restore for every timer/projectile/item; map-independent rules must pass offline as well as online.

## Task 5: Editable production models and effects

**Files:** `Tools/Assets/build_assets.py`, `ArtSource/*.blend`, `Assets/Wildstone/Art`, `Runtime/Presentation/PlayerVisual.cs`, `WeaponView.cs`, `ArenaEffects.cs`, `ArenaAudio.cs`.

**Interfaces:** stable named root, grip, muzzle and bone/socket transforms; explicit Unity meter/axis convention. Generator requires a contained output root and retains `.meta` GUIDs. Effects consume authoritative/predicted event IDs, never cause gameplay damage.

- [x] Verify bpy import in an isolated process without writing into sibling environments; export/import an asymmetric 1 m fixture with grip/muzzle. Blender and native Unity fixture checks pass.
- [x] Produce a shaped low-poly human with explicit rig/weights, first-person hands and four team symbols; validate bind pose and animation scale in Blender. Native deformation remains open.
- [x] Produce all seven equipment models plus Blink/shield pickups, retaining the four approved v2 silhouettes and shared matte material treatment. Native framing/readability remains open.
- [ ] Add local readable firing/projectile/ricochet/blast/impulse/stun/melee/Blink/shield/death/zone VFX and restrained generated or audited sound cues. Bound pools and particle counts; no global time scale/hitstop.
- [ ] Capture moving first-/third-person evidence, inspect dark/light backgrounds and low quality, then fix framing and readability issues.

## Task 6: Three distinct authored arenas

**Files:** `Runtime/Maps/ArenaDefinition.cs`, `Editor/ArenaBuilder.cs`, copied `Runtime/Maps/Source/LevelMesh.cs`, map definitions/scenes under `Assets/Wildstone/Maps`, editor map validators/tests.

**Interfaces:** stable map IDs `atrium`, `split-gallery`, `terrace-steps`; definitions contain geometry, four team spawn groups, pickups, zone route and playable bounds. Builder creates render meshes and simple colliders separately, saves assets, and generates UV2 before any bake.

- [x] Extend the owning map spec with the three layouts and initial dimensions. Use one modular C3.1 kit, distinct route topology and silhouette landmarks.
- [ ] Write failing validators for missing team starts, invalid pickup IDs, disconnected/obstructed zone routes and floor-support gaps.
- [x] Adapt the pure source chamfered mesh builder and minimal platform/ramp patterns; do not import the campaign level builder wholesale.
- [x] Build Atrium's court/gallery, Split Gallery's parallel corridors/crossovers and Terrace Steps' staggered elevations/broad ramps. All three saved Unity scenes and CPU lighting bakes pass; fairness/visual acceptance remains separate.
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

**Files:** `Tools/QA`, `Artifacts/QA`, `docs/QA.md`, `docs/HANDOFF.md`, `docs/USER-SETUP.md`, Windows build under `Artifacts/Unity/Windows`.

- [x] Run relevant EditMode/PlayMode tests and Windows compilation; review important resume findings and retest fixes. See QA for native evidence and limits.
- [ ] Exercise all maps/weapons/items, full UI flow, death/respawn and zone completion in the standalone build; save moving evidence and exact build identity.
- [ ] Run actual Fusion multi-client/network impairment tests when SDK/App ID are configured. Local processes are functional evidence only.
- [ ] Perform scoped performance captures without screenshot polling. Final full-network acceptance remains the canonical separate-machine 8-player/direct-relay matrix, 30-minute profiles, two-hour soak and 100 reconnect cycles.
- [x] Finish independent authoring/review and provide exact remaining setup/test steps in HANDOFF, USER-SETUP and QA. Licensing and SDK/App ID provisioning are resolved; live Photon admission and external machines remain dependencies. Full visual, internet and performance acceptance remains open.
- [ ] Package a versioned Windows build with hashes, editable sources, source/license provenance and a concise current handoff. No public release or paid action is implied.

## Execution record

Live task status, evidence and rulings: [MVP progress](../../MVP-PROGRESS.md). The historical pause archive is immutable.

### 2026-10-28 weapon expression continuation

- [ ] Gameplay delegate: update WeaponCatalog/CombatState/CombatRules/ArenaMatch starter
  tuning and revision; shared PracticeInput held cadence consumed by PracticeSession
  and FusionGameCoordinator. Update focused input/combat fixtures and diagnostic
  ammo expectations. Preserve tap edges, state generations and host validation.
- [ ] World VFX delegate: extend ArenaEffects with fixed pooled laser cores/halos,
  animated impact/burst/shockwave shapes and distinct projectile trails/cores;
  infer missing event weapon context safely or request narrow payload integration.
  Add native lifecycle/geometry/budget/state-isolation checks.
- [ ] Handling delegate: extend WeaponView with authored-muzzle pooled flashes,
  distinct recoil/action/part motion and resets; ArenaAudio with distinct original
  weapon cues in existing voice pool. Add native handling/audio regressions.
- [ ] Root: integrate optional local muzzle endpoint with authoritative eye trace,
  add diagnostic packaged weapon sequences using production presentation paths,
  update HUD/control text only where needed. Run focused native, live Photon,
  inspect actual captures and build normal player; update owning specs/evidence.

Boundaries: root owns docs/ArenaPresentation/FusionVisualCheckDriver/QA tooling;
workers own separate files named above. Root alone runs Unity after workers freeze.
No source asset copy, new package, PlayFab, public release or GitHub visibility change.
