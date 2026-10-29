# Verification and remaining acceptance

Updated 2026-10-27. Active project: `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`.
Authored code, native Unity behavior, a Windows build and real internet play remain
separate evidence levels. The full MVP is not complete.

## Current observed evidence

### 2026-10-28 current-state public preview release

The user requested a fresh build/release of the current state and explicitly
confirmed keeping the now-public repository public. Snapshot source commit:
`316a00f36524a823bd92e77c5741d43d87ad7c19`. It captures the worktree at build start;
concurrent edits made afterward are untouched and excluded from this release.
No gameplay changes or content regeneration were performed for delivery.

- Built from `git archive` in `C:\Users\tyler\orca\builds\wildstone-316a00f`,
  using its `Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`.
  Unity 6000.5.10f1 returned zero and `WILDSTONE BUILD OK`; log:
  `Artifacts/Unity/BuildWindows-20261028-202235-794.log` under that build root.
- The existing public Photon app settings were copied separately into the isolated
  build so Host/Join remains configured. No credential-bearing local service config
  was copied or committed. This configuration is not part of the source archive.
- The normal player stayed alive for a 15-second hidden headless startup, with no
  exception/assertion match in `Artifacts/Unity/release-startup.log`, then only the
  owned process was stopped. This does not verify rendered visuals or network play.
- Packaged ZIP: 200 entries, required EXE/DLL/player data verified, Unity backup/debug
  folders excluded. SHA-256:
  `dcd1c21839e73446c15467370758eb92ec7b3d03cab70dd69cce011efe1a2ae7`.
  GitHub's asset digest matches. ZIP, checksum and `release-manifest.json` uploaded.
- Public prerelease: [preview-20261028-316a00f](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261028-316a00f).
  Normal gameplay code is the captured work in progress, including D026 changes
  present at capture. Full tests, visual/input checks and external-network acceptance
  were not rerun for this release. Historical tests below are not new release tests.

Local delivery record: `Artifacts/Release/current-release.json`. This was a local
build/upload, not a GitHub Actions run. No runner setup was attempted.

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

# Networking continuation, 2026-10-27

- `PlayMode-20261027-212938-098.xml`: broader focused integration passes 22/22.
- `EditMode-20261027-213932-695.xml`: 217/218 pass after recovery integration. The existing recovery test needed loaded acknowledgements for the new mandatory recovery-ready gate; corrected with an explicit assertion that warmup cannot end before those acknowledgements.
- `PlayMode-20261027-214027-588.xml`: 56/57 pass, including motor regressions. The local recovery fixture used object-wide state copying between different SDK IDs, which Fusion correctly rejected.
- `PlayMode-20261027-214259-821.xml`: corrected local recovery hook fixture passes. It uses SDK behaviour-state copying under Single mode, validates complete gameplay/motor restoration, clears authority and tests warmup. Object-ID preservation with an actual migration token remains a live gate. Production follows the SDK resume-object flow, documented in [Photon's recovery sample](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect).

Native integration is ongoing; these runs do not establish live provider or internet acceptance.

- `Compile-20261027-211110-663.log`: integrated menu/lobby/match code compiles.
- `NetworkPrepare-20261027-211132-647.log`: registered network lobby/match/player prefabs and Bootstrap composition generated successfully.
- `EditMode-20261027-211946-281.xml`: 154/154 focused networking, identity, menu and combat rules checks pass, no skips.
- `PlayMode-20261027-212008-854.xml`: 19/22 pass. Three new multi-prefab fixtures used the same asset path; unique fixture paths corrected registration.
- `PlayMode-20261027-212139-442.xml`: three match spawns exposed actual SDK per-object limit (41,164 bytes versus 32,768). The presentation-only event ring was reduced to 128 entries, with a woven-state size regression.
- `PlayMode-20261027-212401-201.xml`: all three reruns pass: authoritative combat/movement plus canonical checkpoint restore, event delivery/eviction, and actual SDK hitscan damage during disconnect grace.

Initial `EditMode-20261027-211244-497.xml` had 79/82 passing: invalid fixture zone route, NUnit attribute encoding replacing an unpaired surrogate, and an outdated expected player wire size were corrected. The surrogate is now constructed at runtime so the rejection case remains meaningful.

No PlayFab HTTP request, authenticated Photon room, external-client session, packaged visual pass or performance acceptance was performed by these checks. Desktop automation remains paused.


- Pre-identity-change final focused runs: `EditMode-20261027-220250-290.xml` **242/242**, `PlayMode-20261027-220339-162.xml` **58/58**, no failures/skips. Networking preparation passed in `PrepareNetworking-20261027-220543-063.log`.
- Core managed checks at `Artifacts/ManagedCompile/20261027-214840-881`: **174/174**. These precede the final guest/migration validation changes and do not prove service behavior.
- D023 subsequently removed PlayFab; earlier identity tests and configuration requirements above are historical. Current private matches use temporary Photon guests and held-seat credentials. Focused guest binding check **11/11** at `Artifacts/Resume/photon-guest-check/managed-check/results.xml`; native guest-flow checks follow separately.


## Photon-only verification, 2026-10-27

- `EditMode-20261027-221901-948.xml`: **231/231** focused native checks, no failures/skips. Removed PlayFab fixtures; covers guest startup validation, copied-ID/wrong-credential rejection, menu actions, session/recovery and the committed-attack rebind regression.
- `PrepareNetworking-20261027-221954-358.log`: regenerated registered prefabs and online build fingerprint successfully.
- Explicit live service test `PlayMode-20261027-222118-458.xml`: **1/1**, no skips. Used the configured Fusion App ID in region `us`, created a transient private Host room, admitted its actual SDK guest peer, changed team, readied and shut down cleanly. No PlayFab or account request. This proves one host's cloud connection and lobby commands, not remote-client, direct/relay, match or migration acceptance.
- First live run `PlayMode-20261027-222009-059.xml` reached the ready-state assertion but failed because NUnit compared boxed Fusion `NetworkBool` to a CLR bool. Casting the assertion to `bool` corrected the fixture; production behavior was unchanged for the passing rerun.
- Review found a pending committed attack retained its original connection generation across migration while snapshot validation required the replacement generation. Validation now permits positive historical generations for an already committed same-life attack; new commands still require the current generation. Focused baseline regression failed before the fix; **51/51** managed combat checks passed after it at `Artifacts/Resume/pending-attack-check/managed-check/results.xml`.

- Map retention and presentation: `PlayMode-20261027-222538-355.xml` **10/10**. The new fixture keeps the same scene and floor collider through 180 sequential suppressed motor ticks; different/stale/pending maps reject reuse.
- Online cancellation/failure UI: `EditMode-20261027-222706-395.xml` **13/13**. Tests exercise actual menu handlers and rendered Retry gating while network loading has no attached external simulation. Physical Escape now delegates to the same menu cancellation path.

- Final graceful-departure freeze integration: `EditMode-20261027-223104-070.xml` **238/238**, no failures/skips. Includes validated frozen-tick/clock-anchor behavior and all current guest/menu/session/combat checks. The preceding compile attempt `Test-20261027-223037-314.log` exposed missing test-assembly internal access; the existing assembly friend list now includes the PlayMode test assembly, without broadening production APIs.

- Final focused native PlayMode `PlayMode-20261027-223211-340.xml`: **59/59**, no failures/skips. Includes actual SDK freeze primitive with coherent final motor state while transport ticks continue, map retention, movement, match/events/orbs/hitscan and recovery hooks. Explicit live service test is excluded from this run.
- Final content preparation `PrepareNetworking-20261027-223313-704.log` passed. Independent Sol source review of the graceful-freeze delta found no confirmed important defect; this is not cloud migration evidence.


## Earlier local Windows playtest checkpoint

`Artifacts/Unity/Windows-20261027-223327-783/WildstoneArena.exe` built successfully
with Unity 6000.5.10f1; log `GuestWindows-20261027-223327-783.log`. Reproducible command
and exact arguments are saved in `Artifacts/Resume/Build-GuestPreview.ps1` and
`guest-build-20261027-223327-783.json`. `guest-manifest-20261027-223327-783.json`
records HEAD `8d88f1569f39a53208c4c184530457bd325509a2`, all 1,131 source hashes
before/after (zero changes), the generated content identity and 200 output hashes.
This preserves uncommitted inputs, is **local-playtest-only**, and was not published
or substituted for the earlier private offline preview.

A separate owned player process ran `-batchmode -nographics` for 15 seconds, remained
alive, and logged no exceptions/errors before the tool stopped it. Evidence:
`Artifacts/Resume/guest-startup-20261027-223327-783.json` and
`Artifacts/Unity/GuestStartup-20261027-223327-783.log`. This verifies headless standalone
startup only, not rendering, input, gameplay, remote transport or cloud migration.
The earlier running build was not touched. Desktop automation remains paused.

Documentation link check: 89 local links, zero missing targets at this checkpoint.
`git diff --check` reports only two Unity-generated empty `m_Name` field trailing
spaces in Bootstrap; no gameplay source whitespace defects. Remaining full gates:
remote clients, real cloud migration/reconnect, external direct/relay networks,
eight-player load/endurance/performance, and the packaged visual/human-feel pass.


## Separate Windows process continuation

Diagnostic build `Artifacts/Unity/Smoke-20261028-025035-388/WildstoneArena.exe`
(first exact path is owned by `Artifacts/Smoke/latest-build.json`) was built through
`BuildSmoke-20261027-225025-891.log`. The first actual host/client run at
`Artifacts/Smoke/20261027-225108-139-basic` reached two-player gameplay in both
processes with replicated avatars and advancing match ticks for ten continuous
seconds. However, the client log contains two sequential movement-tick exceptions.
The initial state-only pass was invalidated in `result.json`; this run is **failed**.
The runner now additionally rejects runtime exceptions/assertions in either player
log. Root cause and a clean rerun are required before treating this as accepted.

- Diagnostic rerun `Artifacts/Smoke/20261027-225528-626-basic` correctly failed the new log gate: first client forward prediction received motor tick 9 while the current callback expected tick 15 (`runner=335`, `origin=319`). One-time confirmed-snapshot catch-up now replays the missing steps with the existing missing-input policy, preserving tick origin and strict subsequent checks.
- Focused native bootstrap/orb/match tests: `PlayMode-20261027-230343-217.xml` **4/4**; boundary cases `EditMode-20261027-230432-831.xml` **8/8**, no failures. A clean separate-process rerun remains required; these tests alone do not close the integration failure.


### Confirmed separate-process results

- `Artifacts/Smoke/20261027-230638-668-basic`: **passed** actual two-process Host/Join/team/ready/load and ten seconds of running gameplay, with no runtime exceptions/assertions in either player log. Closes the initial prediction-arrival regression for this setup.
- `Artifacts/Smoke/20261027-231445-459-reconnect`: **passed** host-triggered client disconnect and retained-session retry. Both peers observed connection generation 2 -> 3 in about 1.41 seconds, preserving seat, life, health 100 and starter ammo 12, followed by ten seconds of stable play and clean cleanup.
- Earlier reconnect runs `20261027-230714-754-reconnect` and `20261027-231018-570-reconnect` remain failed: gameplay recovery succeeded but atomic status-file reporting hit Windows sharing errors. Shared-delete readers and bounded diagnostic-file replacement retries fixed the harness; the passing rerun above also had no runtime log exceptions. A focused Windows file-sharing reproduction confirmed restrictive readers block replacement.
- `Artifacts/Smoke/20261027-232436-756-combat`: **passed** real SDK input and duplicate fire RPCs. Both processes observed local and remote horizontal displacement of about 2.809 m, starter clips 12 -> 11, and exactly one replicated shot per action with matching event IDs. This does not prove hit damage against a controlled moving target.
- `Artifacts/Smoke/20261027-232806-735-capacity`: **passed** eight separate Windows processes through real Photon admission. All eight independently confirmed eight connected players, eight avatars and four teams with sizes `[2,2,2,2]`, ten seconds of advancing gameplay, no logged runtime exceptions/assertions and clean exits. This is same-PC functional capacity evidence, not the eight-machine performance/endurance or external-network matrix.
- Migration runs `20261027-231543-174-migration`, `20261027-232236-475-migration` and `20261027-232614-831-migration` **failed**. The original host left; survivors changed runners, but the elected host returned `StartGame.Ok=true` without completing the restore callback. Strict state/content checks were not waived. Current diagnostics distinguish missing uploaded snapshots, callback timing and actual restore failures; migration acceptance remains open.
- `Artifacts/Smoke/20261027-233354-813-migration`: **passed** graceful host departure with two surviving Windows processes. The SDK can return a successful `StartGame` before invoking its resume callback; startup now awaits that callback within the existing recovery deadline and retains its cancellation source until restoration finishes. Both survivors replaced their runner, kept the same match/content/seat, advanced authority 1 -> 2 and observed exactly one new host, zero lost match ticks and ten further seconds of running play. All three exited cleanly with no runtime exception/assertion logs. The recorded migration duration includes the ten-second stability observation; it is not a pure recovery latency measurement.
- `Artifacts/Smoke/20261027-234225-897-migration-crash`: **failed** after the orchestrator forcibly terminated its owned original-host process following a confirmed periodic snapshot. Both survivors entered session failure around the transport's ten-second disconnect timeout, before runner replacement. Graceful migration does not cover this failure. The original host's unfinished/unsuccessful status and expected killed exit are preserved rather than relabeled as a pass.
- The timeout path previously called `StopAsync` immediately, closing the cloud connection before the migration token could arrive. Only an active client transport timeout with a valid recovery baseline now preserves that connection, freezes old-runner simulation and awaits the token within one non-extending recovery deadline. Explicit kicks still fail immediately and expose retained-session retry. `EditMode-20261027-234813-738.xml`: **248/248**, no failures/skips, including the disconnect-policy boundary checks and safe diagnostic formatting.
- `Artifacts/Smoke/20261027-234918-922-migration-crash`: **passed** actual owned-host process termination after periodic snapshot publication. Both survivors restored the same match/seat/content with authority 1 -> 2, exactly one new host, 17 match ticks of observed rollback and ten further seconds of stable running gameplay. Both survivors exited zero; the killed host remains unfinished and is explicitly marked as the expected crash. All three logs pass the runtime exception/assertion gate. This run proves the scenario, not every transport callback ordering.
- Post-fix reconnect `Artifacts/Smoke/20261027-235017-897-reconnect`: **passed**, connection generation 2 -> 3 in about 1.11 seconds and ten seconds of further play. Post-fix graceful migration `Artifacts/Smoke/20261027-235112-556-migration`: **passed**. Both preserve clean runtime logs and normal owned-process cleanup.
- Sol review found the log gate omitted bare `Exception:` records. Its pattern now covers those, with a focused PowerShell self-check. Rechecking all five earlier accepted live runs with the corrected pattern found zero runtime exceptions/assertions. Routine periodic snapshot success logs are diagnostic-build-only.

## Focused checks and pre-fixture Windows build

- `EditMode-20261027-234813-738.xml`: **248/248** focused checks, zero failures/skips.
- `PlayMode-20261027-235152-137.xml`: **61/61** focused checks, zero failures/skips. Includes SDK movement/match/orb/recovery fixtures, initial prediction bootstrap, map retention and presentation composition; excludes the explicit service test already recorded separately.
- `PrepareNetworking-20261027-235249-558.log`: network content preparation passed.
- Fresh normal Windows player: `Artifacts/Unity/Windows-20261027-235301-648/WildstoneArena.exe`; build log `GuestWindows-20261027-235301-648.log`. `Artifacts/Resume/guest-build-20261027-235301-648.json` records exact arguments and HEAD; `guest-manifest-20261027-235301-648.json` records 1,143 source hashes before/after with zero changes and 200 output hashes. Built using `Artifacts/Resume/Build-GuestPreview.ps1`.
- `Artifacts/Resume/guest-startup-20261027-235301-648.json`: fifteen-second owned headless startup remained alive with zero runtime exception/assertion records. The shipped `Wildstone.Fusion.dll` contains no `FusionSmokeDriver` type name; the normal build excludes the diagnostic define. Startup log: `GuestStartup-20261027-235301-648.log`.

The new normal build contains the production prediction, migration-callback and
transport-timeout fixes exercised by the preceding diagnostic players. It preserves
uncommitted inputs and is **local-playtest-only**, not published or a clean release.
The earlier private offline preview and earlier player directories are preserved.
Same-PC cloud tests do not establish external direct/relay paths, eight-machine
performance, adverse latency/loss profiles or endurance. Controlled moving-target
hit verdicts and the paused full packaged visual/human-feel pass remain open.

## 2026-10-28 additional local acceptance and current build

- `Artifacts/Smoke/20261028-000141-951-late-join`: **passed**. Two original clients independently verified ten seconds of play before the runner launched a third process. All three then confirmed three connected players/avatars/teams, coherent lobby bindings and existing seats, unchanged authority, and ten more seconds of gameplay. All exited zero with clean runtime logs.
- `Artifacts/Smoke/20261028-000141-946-combat`: **passed** actual movement/deduplication followed by controlled stationary hit replication. Host-authoritative test placement is explicitly recorded; the client then sent one real fire RPC. Both peers observed health 100 -> 74, shooter ammo 11 -> 10, target ammo unchanged at 11, one shot and one hit with matching identities and target life, and unchanged shooter health. This is not moving-target/adverse-latency acceptance. Review found the final distance metrics included the later fixture teleport; the movement threshold itself had been verified beforehand. Do not quote this run's final distances as input-only movement.
- Current normal Windows build: `Artifacts/Unity/Windows-20261028-000246-678/WildstoneArena.exe`, log `GuestWindows-20261028-000246-678.log`. Exact command/HEAD: `Artifacts/Resume/guest-build-20261028-000246-678.json`; source/output manifest: `guest-manifest-20261028-000246-678.json`. All 1,145 source inputs stayed unchanged during build; 200 output files were hashed.
- `Artifacts/Resume/guest-startup-20261028-000246-678.json`: fifteen-second owned headless startup stayed alive, no runtime exceptions/assertions, no smoke driver in the shipped assembly. Startup log: `GuestStartup-20261028-000246-678.log`. The subsequent movement-metric correction changes only code excluded from normal builds.

This remains an unpublished local playtest with uncommitted inputs. The full MVP
still needs independent external networks, eight-machine latency/performance,
adverse network profiles/endurance, moving-target combat acceptance and the paused
packaged visual/input/human-feel pass. No PlayFab setup or account dependency exists.
- Corrected-distance combat rerun `Artifacts/Smoke/20261028-000411-316-combat`: **passed** all movement, deduplication and controlled-hit gates with clean logs/exits. Input-only horizontal distances were about 2.28-2.62 m at each process's completion observation, retained before fixture teleport; both peers again observed exactly 26 damage and one hit/shot.
- Final documentation check: 83 local links resolve. PowerShell smoke-runner syntax passes. `git diff --check` reports only the same two Unity-generated empty `m_Name` trailing spaces in Bootstrap; no new source whitespace issue.

## 2026-10-28 unattended polish and current build

This batch preserves the approved game/art and Photon-only flow. Menu focus survives
same-screen updates; settings starts on sensitivity rather than a binding action;
the same match retains its HUD through pause/settings. The crosshair is centered,
hit cue starts hidden, zone distance is horizontal, and enlarged HUD labels wrap.
Recovery occupies the foreground with the original deadline and suppresses stale
lobby/reconnect actions. Settings has a readable scrollbar and keyboard focus.
Coordinator discovery uses reusable runner-local SDK lists at the existing cadence
and rebuilds presentation only after participant/avatar/network-ID/render-root change.

### Native and live evidence

- `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform EditMode -TestFilter 'Wildstone.Tests.Menu;Wildstone.Tests.LocalSettings;Wildstone.Tests.OnlineMenu;Wildstone.Tests.FusionSessionBoundary' -TimeoutMinutes 5`: **65/65**, no skips, `Artifacts/Unity/EditMode-20261028-081207-323.xml`.
- `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode -TestFilter 'Wildstone.Tests.FusionPlayerRunnerTests;Wildstone.Tests.NetworkPresentationBridgeTests;Wildstone.Tests.PracticeSessionTests;Wildstone.Tests.FusionSessionLifecycleTests' -TimeoutMinutes 5`: **17/17**, no skips, `Artifacts/Unity/PlayMode-20261028-102420-846.xml`. Includes real SDK spawn/despawn and changed visual-root propagation through actual presentation.
- Earlier `PlayMode-20261028-081315-197.xml` failed one allocation-comparison assertion (16/17). The BCL counter reported zero even for known allocations. The corrected fixture retains a 64 KiB calibration allocation and legacy query arrays; calibration still reports zero, so measurement is explicitly unavailable. Behavioral assertions remain active. Do not claim zero allocations or quantified savings; 20 Hz render snapshots still allocate.
- Final diagnostic build: `Artifacts/Unity/Smoke-20261028-143650-489/WildstoneArena.exe`, built with `./Tools/Unity/Invoke-Unity.ps1 -Action BuildSmoke -TimeoutMinutes 10`; log `BuildSmoke-20261028-103642-789.log`. All final live/capture runs below used it.
- `./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity -SoakSeconds 300`: **passed**, `Artifacts/Smoke/20261028-103753-715-capacity/result.json`. All eight processes held coherent eight-player/eight-avatar/four-team `[2,2,2,2]` gameplay for at least 300 seconds, entirely Running (zero Finished time), with advancing SDK ticks, clean runtime logs and zero exit codes.
- Windows-owned process memory samples: 550–556 observations per role, spanning 299.26–299.99 seconds of the hold. Per-process endpoint private-byte changes were +76 to +2,252 KiB; working-set changes +5.30 to +7.70 MiB. These are short-run observations, not leak absence, frame budgets or long endurance acceptance. Exact timestamps/bytes are in `result.capacityMemory`.
- Earlier `Artifacts/Smoke/20261028-102443-743-capacity` remains **failed**: gameplay held for five minutes, but Unity/Mono process counters returned zero. The runner now measures its actual Windows processes; zero/missing/incomplete evidence still fails. Focused runner duration/bounds/memory gate self-checks passed under `Artifacts/Resume/capacity-soak-check/`.
- `./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario late-join -TimeoutSeconds 210`: **passed**, `Artifacts/Smoke/20261028-104654-655-late-join`. Existing two peers continued through third-player admission with coherent seats, avatars and authority.
- Same command with `-Scenario combat`: **passed**, `Artifacts/Smoke/20261028-104654-659-combat`, including actual movement, duplicate-fire rejection and controlled stationary-hit replication.
- Same command with `-Scenario migration-crash`: **passed**, `Artifacts/Smoke/20261028-104654-656-migration-crash`. The owned original host was forcibly terminated; both survivors restored authority 2 with exactly one new host, 18 ticks of rollback and ten seconds of continued play. Reported migration duration includes that stability observation. Expected host kill is distinguished from survivor zero exits; all log gates passed.

### Packaged visual evidence

`./Tools/QA/Invoke-VisualCheck.ps1` generated sixteen D3D11 camera/UI captures in
`Artifacts/Visual/20261028-103753-693/`; all sixteen were inspected. Title, settings
top/controls, online, practice selection, pause and all three map spawn views show
readable text, scrollbar contrast, centered crosshair and no observed HUD overflow.
Map views cover 720p, 720p at UI scale 1.4 and 1080p; title additionally covers
1920x810. This is real offscreen rendering, not concept images. The driver uses
existing screen/map transitions, without desktop input or saved preference changes.
Normal builds exclude it. Static images do not establish keyboard/mouse operation,
full-map traversal, animation/moving readability or human game feel.

Earlier capture runs are preserved: `20261028-081714-524` failed with black frames
and timeout; `20261028-102622-032` was rejected after image inspection because the
batch camera was not rendering, accumulating UI over old frames. Explicit render
requests fixed that. `20261028-102830-235` rendered correctly but exposed target
release warnings on resize; final cleanup clears camera references before release.
The final log has no warning/exception/assertion lines. It does include Unity
shutdown memory telemetry (about 0.95 MB, no allocation stack), which is not enough
to attribute a leak to this batch and does not establish performance acceptance.
Independent GPT-6.1 Sol read-only review found no actionable defect in menu/recovery,
HUD, discovery or the compile-time visual driver.

### Current normal Windows player

`Artifacts/Unity/Windows-20261028-103823-536/WildstoneArena.exe` built through
`./Artifacts/Resume/Build-GuestPreview.ps1`; log `GuestWindows-20261028-103823-536.log`.
`Artifacts/Resume/guest-build-20261028-103823-536.json` records exact arguments and
HEAD `8d88f1569f39a53208c4c184530457bd325509a2`;
`guest-manifest-20261028-103823-536.json` records 1,149 source inputs unchanged during
compilation and 200 output hashes. `guest-startup-20261028-103823-536.json` records
fifteen seconds alive, zero runtime exceptions/assertions and neither diagnostic
driver in the shipped assembly. This is **unpublished, local-playtest-only** with
uncommitted inputs; prior builds and the separate private offline preview remain intact.

Full acceptance still needs independent external networks/direct-relay paths,
eight machines, latency/loss profiles, moving-target combat, long endurance/frame
budgets and physical-input/human-feel checks. No PlayFab setup is needed.

Final documentation check: 85 local links across ten current documents resolve.
`git diff --check` reports only the two pre-existing Unity-generated empty `m_Name`
trailing spaces in Bootstrap (lines 367 and 379); no new source whitespace issue.

## 2026-10-28 first-person body, hands and slide (D025)

The requested local body/handling continuation is implemented with original
Wildstone geometry. Source `vibegame1` was inspected read-only: its slide simulation
values already match this project. No source assets/dependencies or gameplay tuning
were copied. Camera eye and aim remain authoritative; leg animation, wrist/finger
articulation, sleeves, confirmed action feedback and slide FOV/roll are cosmetic.

- Final command: `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode -TestFilter 'Wildstone.Tests.FirstPersonBodyTests;Wildstone.Tests.FirstPersonWeaponMotionTests;Wildstone.Tests.FirstPersonSlideViewTests;Wildstone.Tests.PresentationRuntimeTests;Wildstone.Tests.NetworkPresentationBridgeTests;Wildstone.Tests.PracticeSessionTests' -Graphics -TimeoutMinutes 5`.
- `Artifacts/Unity/PlayMode-20261028-200805-947.xml`: **35 passed, zero failed/skipped**. Coverage includes original six skin renderers and real D3D11 backface-culling visibility, look-down/slide framing, imported cuff mesh contact, grip/support alignment, confirmed-event recoil, real action duration on slow frames, lifecycle/short-Blink reset, reduced motion, exact camera eye/aim and frame partition behavior.
- Earlier 193606 run failed one new camera test because it assumed 90 FOV while its camera fixture defaults to 60. It now asserts relative to the captured baseline. Earlier 200355 run failed its new cuff test because the imported mesh is not CPU-readable; editor read-only mesh data fixed the test without changing import settings. Final run above supersedes these failures.
- `./Tools/Unity/Invoke-Unity.ps1 -Action BuildSmoke -TimeoutMinutes 10` passed; log `Artifacts/Unity/BuildSmoke-20261028-200829-311.log`, executable `Artifacts/Unity/Smoke-20261029-000836-240/WildstoneArena.exe`.
- `./Tools/QA/Invoke-VisualCheck.ps1 -Motion` passed in `Artifacts/Visual/20261028-200856-832/`. All thirteen real D3D11 pose fixtures were inspected: forward/down, two gait phases, airborne, slide entry/sustain/down/base-FOV60/exit, fire, reload and sword. Connected legs/boots and distinct slide framing are visible; hand/weapon action poses differ. Low-poly original geometry remains. These are explicitly sampled presentation states, not actual input or human animation-feel proof.
- Rejected captures remain preserved: `193457-428` aligned leg segments along the viewing ray; `195511-468` tried a waist cap that occluded the legs. Final code removes that geometry and places hips behind/under the lens. Intermediate `194808-055` and `200309-732` captures/builds are superseded. Their successful screenshot production is not visual acceptance.
- Fresh `./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario combat -TimeoutSeconds 210` and `-Scenario reconnect` both pass on the final diagnostic build: `Artifacts/Smoke/20261028-200856-824-combat/` and `20261028-200856-824-reconnect/`. Actual separate Photon processes verify movement/fire deduplication/controlled stationary hits and retained-seat reconnect; runtime log and exit gates pass.
- Independent GPT-6.1 Sol final read-only body/integration review found no actionable defect. Hand review exposed a reflected-import-axis assumption; the production basis now derives from authored grip points and the rendered cuff regression passes.

Fresh normal player: `Artifacts/Unity/Windows-20261028-200856-804/WildstoneArena.exe`,
built by `./Artifacts/Resume/Build-GuestPreview.ps1`. Manifest
`Artifacts/Resume/guest-manifest-20261028-200856-804.json` records HEAD
`8d88f1569f39a53208c4c184530457bd325509a2`, **1,157 unchanged inputs / 200 output hashes**.
Startup record `guest-startup-20261028-200856-804.json` reports fifteen seconds alive,
zero runtime exceptions/assertions and both diagnostic drivers excluded. This is
**unpublished, local-playtest-only**, preserving uncommitted inputs. Human animation
feel, physical input, adverse/external networks and performance acceptance remain open.
