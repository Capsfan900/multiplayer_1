# Verification and remaining acceptance

Updated 2026-10-27. Active project: `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`.
Authored code, native Unity behavior, a Windows build and real internet play remain
separate evidence levels. The full MVP is not complete.

## Current observed evidence

### GitHub delivery

Personal private repository `Capsfan900/multiplayer_1` was created on 2026-10-27.
Source commit `e3f5815c1e863a32a23ed7c482e2efc48a324557` is uploaded to `main`.
The manual Windows preview workflow is active on GitHub. YAML/manual-trigger assertions,
PowerShell parsing of every run step and actionlint 1.7.12 pass (custom runner label
explicitly allowed). Account settings, local service config, Library and Artifacts
are absent from the tracked snapshot; a filename-only credential-pattern scan found
no candidate secrets in the published source trees.

A clean `git archive` of that commit was expanded to
`C:\Users\tyler\orca\builds\wildstone-e3f5815`, with no copied Library or local config.
Running its `Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`
passed with exit zero and `WILDSTONE BUILD OK`. Evidence:
`Artifacts/Unity/BuildWindows-20261027-205939-312.log` under that clean build root.
First import emitted SDK API-updater assembly-resolution and config-initialization
exceptions before completing import and a successful build; this was not a clean-log
runtime test. The generated Photon account settings contain no Fusion App ID.

The exact workflow packaging and publication PowerShell steps were then executed
locally. The ZIP's 200 entries include the executable, UnityPlayer DLL and player
data; Unity backup/debug folders are absent. Published private prerelease:
[preview-local-20261027-e3f5815-1](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-local-20261027-e3f5815-1).
ZIP SHA-256: `caee19d7ed6e91b0ac5ea79c383884fb96eb6378b1cfc24bb56798e0e1f39f0f`.
The release has a player ZIP and a checksum file. This new player was not launched
or visually inspected; historical visual evidence describes the earlier local build.

**Automation remains unverified:** automatic approval review blocked runner registration
and startup twice, including after explicit user authorization; the reported reason
was only `blocked by policy`. GitHub lists zero registered runners. Runner 2.338.2
is downloaded with its published SHA-256 checked, but must be registered/launched
manually using [TOOLING](TOOLING.md#github-builds-and-private-releases). No Actions run
has built this release. The first automated run remains pending. Source publication
and packaging are not online gameplay acceptance.

SDK provisioning checkpoint (2026-10-27): Fusion 2.1.3 Stable build 2390 imported
under `Assets/Photon`, with the supplied App ID in ignored local Photon settings.
`Compile-20261027-202021-250.log` exits zero with `WILDSTONE CONFIGURED`, no compiler
errors or exceptions, and the App ID persists after reload. The earlier native
test suites and Windows executable below predate SDK import and were not rerun or
rebuilt for this provisioning step. No Photon connection was attempted.

The first import failed on the missing Physics 2D module. Adding
`com.unity.modules.physics2d` 1.0.0 resolved compilation. The next initialization
run created Fusion's config assets and logged a transient config-load exception;
the final clean wrapper run above contains no exception. Exact commands, package
hash and imported-asset checks are in `Artifacts/Resume/photon-setup.md` and
`Artifacts/Resume/fusion-sdk-import.json`. All 241 file payloads/file GUIDs match
the archive; Unity omitted eight empty folders and generated the Resources folder
GUID. This is SDK provisioning/compile evidence, not runtime/network acceptance.

The native commands below use `Tools/Unity/Invoke-Unity.ps1` in this worktree.
Run files are under `Artifacts/Unity/`. Historical 2026-10-26 paths in the execution
ledger refer to the original authoring checkout and were not copied as fresh evidence.

| Check | Result | Evidence |
| --- | --- | --- |
| Checkpoint recovery | 194 source/copy hashes verified; original read-only | `Artifacts/Resume/recovered-checkpoint.json` |
| Managed compile/rules | 172 passed, 0 failed/skipped; 43 runtime, 8 editor, 24 test source files typechecked | `Artifacts/ManagedCompile/20261027-200257-396` |
| Native Compile | Passed | `Compile-20261027-194530-787.log` |
| Native EditMode suite | 194 total: 192 passed, 0 failed, 2 explicit skipped | `EditMode-20261027-195009-614.xml` |
| Explicit full Prepare + interrupted controller repair | 2 passed, including open-scene/root/dirty-state preservation | `EditMode-20261027-194924-596.xml` |
| Reviewed setup-fixture safety follow-up | 2 passed after shared scene-guard reuse | `EditMode-20261027-200122-833.xml` |
| Native PlayMode suite | 62 passed, 0 failed/skipped | `PlayMode-20261027-195621-403.xml` |
| Input causal check | Recovered original code fails only the new cross-update tap test; restored fix passes all 3 input cases | `PlayMode-20261027-195720-081.xml`, `PlayMode-20261027-195816-338.xml` |
| CPU lightmaps/probes | All 3 maps baked; command exits successfully | `Bake-20261027-200209-874.log` |
| Windows standalone build | Passed; fresh executable and required scenes produced | `BuildWindows-20261027-200340-100.log` |
| Existing Python/Blender/map checks | 4 regressions pass; 14 model/source pairs match; 73 saved map meshes and 9 previews verified | `Artifacts/Resume/asset-verification.md` |
| Independent resume-delta review | One fixture scene-safety finding fixed; no other material runtime finding | `Artifacts/Resume/resume-review.md`, `native-readiness.md` |
| Native Fusion/provider EditMode checks after SDK import | 42 passed, 0 failed/skipped: exact mappings, actual SDK weaving, input validation, transport boundaries and offline identity responses | `EditMode-20261027-204049-361.xml` |
| Native Fusion PlayMode checks | 9 passed, 0 failed/skipped: real Single runner spawn/ticks/teleport/cleanup, proxy collider/visual boundaries, missing-input timeout and credential-free lifecycle | `PlayMode-20261027-204334-222.xml` |
| Post-integration core managed check | 172 passed, 0 failed/skipped, using this project's own package assemblies | `Artifacts/ManagedCompile/20261027-204725-610` |
| Post-integration Windows build | Success; includes SDK and settings contrast fix; not launched or visually rechecked | `FusionWindows-20261027-204502-089.log` |

Latest executable: `Artifacts/Unity/Windows-20261027-204502-089/WildstoneArena.exe`.
This is an **offline, local development build**; the online gameplay path remains
unwired and cannot host/join a match. The old `Windows/WildstoneArena.exe` was left
running and unchanged. No release archive or public upload was made. Exact build
arguments are in `Artifacts/Resume/fusion-windows-build.json`; post-build source
and output hashes (1,044 inputs, 200 output files) are in `fusion-build-manifest.json`.
Dirty source inputs mean this is not a release/reproducibility claim.
A packaged Practice frame from the earlier executable was observed rendering arena geometry, hands, weapon and
HUD; `Artifacts/Resume/standalone.log` had no reported exception at that checkpoint.
Title/online-unavailable/invalid-code feedback, settings resolution/apply, Atrium
and Split Gallery loading, restart and leave were exercised. Settings input/dropdown
contrast was fixed in source; rebuilt appearance is not yet verified. Computer Use
was stopped with physical Escape, and desktop automation remains paused. Full visual,
all-map interaction, human feel and performance acceptance remain open.

The new `Wildstone.Fusion` assembly is woven by the actual SDK. Imported project
configuration now explicitly selects eight players, 120 Hz host/client simulation
and 60 Hz sends. These settings and woven layout checks are not latency/bandwidth
measurements or evidence of remote prediction. The separate networking tests do
not belong to the historical managed-only compiler's runtime/test source scan.

The real Single-runner fixture bakes/registers a real network prefab, starts Fusion
without contacting Photon Cloud, initializes its authoritative spawn/life/tick
origin and observes motor advancement/gravity, teleport state and shutdown. The
initial fixture incorrectly copied ArenaMotor's pre-placement Awake position from
the prefab; the corrected spawn explicitly supplies the host's intended pose and
asserts both state and collider position. Proxy interpolation is tested through
the real pose application boundary, not through a remote snapshot connection.
Host/client reconciliation, authenticated connection, two remote peers, hitbox rewind,
full-match replication and migration are still untested/uncomposed.

Initial integration failures caught missing test assembly/plugin references, the
SDK/NUnit Assert name collision, a motor fixture incorrectly run in EditMode where
Awake does not initialize it, Unity JSON nested-object defaults, and cancellation
exception subtype assertions. The final EditMode run above covers the corrected
EditMode set. Identity tests make no HTTP requests and do not establish provider
configuration, credential validity or successful login.

Native PlayMode covers actual CharacterController/physics queries, local motor replay,
Blink/cover/support, input edges, presentation lifecycle, probe refresh, scene flow
and Practice coordination. This does not validate Fusion prediction, hitbox rewind,
identity, host migration, external-network behavior, cross-machine determinism or FPS.
The warmed coordinator allocation assertion passes without presentation subscribers;
this is not a whole-frame allocation result.

PhysX replay vector limits and measured maxima are in the foundation owner and
`Artifacts/Resume/motor-replay-verification.md`; timers/discrete fields remain exact.
These tightly bounded same-process checks do not change the network budgets.
The input negative control restored the fixed file byte-for-byte, then reran its tests.

## Failures found and resolved

Initial native Compile failed because additive scene creation cannot start from an
untitled default scene. Setup now replaces only disposable empty startup state and
preserves/refuses unsaved author work. Native fixtures use the same preservation guard.
A reviewer found and closed one fixture that had bypassed that guard.

The first EditMode run exposed imported-axis mismatch plus scene-fixture/preparation
dependencies. The authored FBX whitelist retains Unity's root axis conversion and
applies the required X reflection; exact native bounds, anchors, normals/materials,
hand attachment and prepared animation loops pass. No Blender meshes were regenerated.

Prepare exposed Unity fake-null component fallbacks and a zero-layer animation
controller left by an interrupted run. Explicit native component checks and repair
of the generated controller fix both. The suspected temporary-prefab dirty-scene
problem did not reproduce; no speculative isolation code was added.

PlayMode exposed nonqueryable startup capsules, negligible floor-adjusted Blink
movement, batch input-focus routing and input edges lost across update steps. Capsule
configuration now rebuilds its native shape while preserving disabled state. Blink
checks final displacement. Test input routing is restored after each fixture;
production press callbacks retain taps until consumption.

The initial bake saved all maps but failed restoring an empty batch scene setup.
Its successful rerun covers the teardown fix; nonempty untitled/dirty author scenes
are rejected before bake scene replacement.

## Reproduce native checks

Close only this worktree's interactive Editor before batch commands. Do not operate
another project's Editor. The historical licensing/approval block is resolved.

```powershell
& ./Tools/Unity/Invoke-Unity.ps1 -Action Compile
& ./Tools/Unity/Invoke-Unity.ps1 -Action Prepare
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform EditMode
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestFilter Wildstone.Tests.ContentPreparationTests
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode
& ./Tools/Unity/Invoke-Unity.ps1 -Action Bake -TimeoutMinutes 60
& ./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows
```

Prepare and its explicit regression regenerate content and invalidate old lighting;
run them before Bake. Build packages saved scenes without rebuilding geometry.
Only the wrapper's own launched process is terminated on timeout. Preserve logs and
NUnit XML; neither a marker in a failed log nor a compile alone establishes a pass.

## Standalone Practice pass

After a successful build, choose **Practice**, then an arena. Default controls:
WASD move, mouse look, Space jump, Left Shift dash, Left Ctrl slide, left mouse fire,
R reload, 1 starter, 2 special, G grenade, Q Blink, E shield, F pickup and Escape menu.
Bindings can be changed in Settings. This mode has one stationary target fixture;
it does not supply opponents or demonstrate multiplayer.

Exercise title, settings/apply/cancel/defaults, keyboard focus/rebinding, Practice
selection on all three maps, loading cancel/error/retry, pause, focus loss, death,
respawn, results/rematch and leave. Pause must leave simulation running and release
input cleanly. Confirm 720p/1080p/1440p/ultrawide layouts and reduced-motion behavior.

Traverse every ramp, wall seam, gallery and arrival screen with the actual motor.
Measure all four starts' routes to central and side pickups. Follow the entire
zone footprint across ramps and stacked floors; verify floor-qualified occupancy,
contesting and fixed team scoring. Record native failures and opening advantages.

Use the explicitly labeled stationary target to exercise each weapon, pickup
replacement/ammunition, reload, grenade fuse/self-damage/cover, disc ricochets,
Push Lens displacement, stun/immunity, sword windup/coverage, blocked/clear Blink,
shield spillover/expiry, death cleanup and respawn. Check model/weapon framing,
animation and effects in moving first-/third-person captures against lit/shaded
surfaces. An unattended still or scripted target is not human feel approval.

## Real network and performance acceptance

The legitimate Fusion SDK and app are configured. Finish verified identity setup
as described in [USER-SETUP](USER-SETUP.md) and compose the initial real SDK adapters
into the actual lobby/player/match flow. Gate admission on provider identity and
content compatibility, compose one whole-game checkpoint, and use actual Fusion
prediction and lag compensation. The present physics adapter is offline only.

Run host plus clients first, then independent-home networks and direct/relay
paths. Exercise stale/malformed/duplicate actions, ownership/life changes, losses
during attacks/pickups/scoring, cancellation, disconnect holds, recovery flicker,
graceful departure and host crash. Verify the whole transaction rather than only
session or combat metadata. Local multi-process tests cannot replace this evidence.

The [foundation](superpowers/specs/2026-10-25-multiplayer-foundation-design.md)
owns the exact latency/loss, correction, CPU/GPU, bandwidth, migration, eight-machine,
30-minute profile, two-hour soak and reconnect matrix. Establish service eligibility
before high-volume backend tests. All 120 Hz / 60 Hz snapshot / 144 FPS values are
targets, not measured results. Do not describe player-hosted networking as lag-free
or esports proven without the specified measurements and host-trust limits.

The HUD's per-frame formatted display text remains a recorded optimization candidate.
Measure it in the complete native frame profile; the core zero-byte checks exclude it.

Only after these gates pass: copy the actual Windows output to a versioned build
folder, verify copied file hashes, record source commit and dirty inputs, include
the exact QA report, and ZIP that verified copy. Preserve previous builds. Public
release is outside the current authorization.
