# Unity tooling

## GitHub builds and private releases

The personal repository is `Capsfan900/multiplayer_1` (now public, confirmed by the
user on 2026-10-28). Current-state public releases may be built locally from an
isolated committed source archive with the existing public Photon app settings
copied separately so Host/Join remains configured; never copy credential-bearing
service configuration. Record the source SHA, configuration inclusion and validation
limits in the release manifest/notes. See [QA](QA.md) for the current release.

The original workflow below remains unchanged and is not evidence of an Actions run.
Run **Actions >
Windows preview release > Run workflow** to build the selected committed branch.
The workflow uses a separate clean checkout, the existing `BuildWindows` wrapper
and saved scenes/lightmaps; it does not regenerate approved content. It checks the
player's required files, packages the player without Unity backup/debug folders,
and publishes a unique prerelease with the source SHA and SHA-256 checksum.
Failures stop publication; Unity logs are retained as Actions artifacts for seven days.

This uses a repository-scoped Windows x64 self-hosted runner labeled `wildstone-unity`
on the existing licensed Unity 6000.5.10f1 PC. Runner installation is outside the repo
at `C:\Users\tyler\orca\runners\wildstone`; its working checkout is separate from
the authoring worktree. It requires `pwsh`, `git`, `gh`, and that exact Unity editor
with Windows build support. No cloud Unity credentials or paid runner are configured.
Runner 2.338.2 was downloaded and its GitHub-published SHA-256 verified. Registration
and startup were blocked by automatic approval review, including after the user
explicitly authorized this PC. No runner is registered or running yet. Complete
registration yourself in PowerShell (the short-lived token is kept out of files):

```powershell
$runnerRoot = 'C:\Users\tyler\orca\runners\wildstone'
$registration = gh api --method POST repos/Capsfan900/multiplayer_1/actions/runners/registration-token | ConvertFrom-Json
& "$runnerRoot\config.cmd" --unattended --url https://github.com/Capsfan900/multiplayer_1 --token $registration.token --name wildstone-local-windows --labels wildstone-unity --work _work
```

Then launch it hidden in the current user session. It is not installed as a service
or startup task; repeat this launch after reboot:

```powershell
Start-Process -FilePath 'C:\Users\tyler\orca\runners\wildstone\run.cmd' -WorkingDirectory 'C:\Users\tyler\orca\runners\wildstone' -WindowStyle Hidden
```

The PC must be awake and the runner online. Only manual dispatch is enabled;
no pull request or push starts code on this PC. Only trusted repository writers
should be allowed to dispatch workflows on this runner. Photon account settings
and local service configuration stay ignored and are not copied into release builds.
These builds support offline Practice; internet acceptance remains open.

Share a specific prerelease URL from [Releases](https://github.com/Capsfan900/multiplayer_1/releases).
Releases of the now-public repository can be downloaded anonymously. Historical
private-release access notes describe the repository's earlier visibility.
The `/releases/latest` route excludes these prereleases. Release-run evidence lives
in [QA](QA.md); publication is distinct from runtime acceptance.

## Local tooling

This project targets Unity **6000.5.10f1** (`3bd4f66ad299`) on Windows. The
package manifest pins URP 17.5.0, Input System 1.20.0 and Test Framework 1.7.0,
and explicitly enables the required built-in Audio, Physics 2D and Unity Web Request modules 1.0.0.
Unity's generated `Packages/packages-lock.json` records resolved
transitive packages after import. No source-project package manifest is copied.

From this project root, run:

```powershell
& ./Tools/Unity/Invoke-Unity.ps1 -Action Compile
& ./Tools/Unity/Invoke-Unity.ps1 -Action Prepare
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform EditMode
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestFilter Wildstone.Tests.ContentPreparationTests
& ./Tools/Unity/Invoke-Unity.ps1 -Action Bake -TimeoutMinutes 60
& ./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows
```

The wrapper accepts only this repository's resolved root and an existing absolute
Unity executable path. It runs a hidden batch Editor, returns a nonzero PowerShell
status on Editor, compilation, stale/missing-build-output, or test failure, and writes timestamped
logs and test results under `Artifacts/Unity/`. The `Test` action requires a
completed, passing XML run with at least one test. Build output is
`Artifacts/Unity/Windows/WildstoneArena.exe`. `Compile` runs
`Wildstone.Editor.ProjectSetup.Configure`, which creates project-owned URP settings
with default post processing data and a bootstrap scene, sets linear color/text
serialization, Input System only, and a separate product identifier. Re-running
setup repairs missing renderer data and preserves authored build scenes and assets.
Only an empty, clean startup scene may be replaced by setup; nonempty untitled
author scenes must be saved first. Bake restores the saved scene setup, or a clean
empty scene when its batch process started without one.
`Prepare` validates the imported axis fixture, builds/validates the three saved
catalog arenas, creates presentation assets and assembles the playable Bootstrap.
`Bake` saves CPU lightmaps in prepared scenes; unlike the other batch actions it
keeps the graphics device enabled for Unity's bake pipeline. `BuildWindows` requires
all four prepared scenes, with Bootstrap first, and packages every enabled scene
without regenerating geometry or discarding baked lighting. The default timeout is
20 minutes; only the process started by that invocation is stopped on timeout.

Native licensing, import and Compile now run in the isolated `mvp-2` worktree.
The earlier permission/licensing block is historical. Read [QA](QA.md) for the
current results and remaining gates. `-TestFilter` selects one fixture/method or
semicolon-separated filters; use it to deliberately include the explicit content
preparation regression. Test execution still requires a nonempty passing XML run.

To preserve a running older executable, the post-integration build used Unity's
documented `-buildWindows64Player <absolute-new-path>` batch argument with the same
project and saved build scenes. The exact invocation and unique output directory
are recorded in `Artifacts/Resume/fusion-windows-build.json`. It did not rebuild
map geometry/lighting, replace the running build or launch desktop automation.
[Unity command-line reference](https://docs.unity.com/en-us/engine/6000.5/manual/unity-editor/command-line-arguments/editor).

Independent type/rule checks are available without an Editor launch:

```powershell
& ./Tools/QA/Compile-Managed.ps1 -Scope Tests -RunManagedTests
& ./Tools/QA/Compile-Managed.ps1 -Scope Runtime -ExportLayouts
```

They use the installed real Unity/compiler DLLs and this project's imported package
assemblies under `Library/ScriptAssemblies`; this local verification aid is not a portable Unity
build, import test or physics substitute. Installed engine DLLs do not prove that
Package Manager enables every required module. The helper asserts the explicit
Audio dependency discovered during review; a fresh project import is still required.
This helper scans the core Runtime/Editor/Tests trees. The separate Networking
assembly is verified with native Unity tests, including SDK weaving; managed core
totals must not be presented as networking validation.
`ExportLayouts` writes the actual default
map data for the Blender preview generator. An authored catalog changed in Unity
is authoritative for scene rebuilds; default-data previews must not be confused
with a later edited catalog.

Source layer names occupy indices 6-12 to ease selective source adaptation:
Player, Enemy, Interactable, Sky, Atmosphere, Viewmodel, Vista. These are names,
not copied collision rules. Set collision and culling rules deliberately as runtime
content arrives. Keep this project's Editor and logs separate from `vibegame1`.

Photon Fusion 2.1.3 Stable build 2390 is imported from
`Downloads/photon-fusion-2.1.3-stable-2390.unitypackage`. Its archive release history
and source/copy SHA-256 were verified in `Artifacts/Resume/photon-setup.md`.
Import used Unity's `-batchmode -nographics -quit -projectPath <this-checkout>
-importPackage <absolute-package-path> -logFile <log>` command. Fusion requires the
built-in `com.unity.modules.physics2d` dependency even for this 3D project. After
enabling it, a compile with `-accept-apiupdate` completed SDK initialization; the
normal Compile wrapper then passed without initialization exceptions. All imported
file payloads and file GUIDs still match the archive.
The App ID is configured in the ignored local
`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`; preserve its tracked meta
GUID and populate the App ID again when recreating a checkout. The source package
contains no account ID. SDK defaults are not acceptance of the project's network
rate/rewind budgets. The generated config now explicitly weaves `Wildstone.Fusion`
and selects eight players, 120 Hz host/client ticks and 60 Hz sends. Lag compensation
is enabled; migration publishes complete checkpoints manually through the SDK.
Local tests do not establish cloud recovery. D023 removes the PlayFab client and
account configuration. Photon-only guest sessions use the existing SDK and App ID. [QA](QA.md) records focused native integration
checks; compilation alone does not establish working networking.

The cached source Unity MCP package is Coplay `com.coplaydev.unity-mcp` 10.1.2,
source Git commit `4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50`. It is not
installed here: batch tooling and native editor builders currently supply the
authoring commands. The upstream commit's MIT license was verified; if an interactive
bridge is added, preserve that license and pin the package to the reviewed commit.
Select only this project's Editor instance.
## Network content preparation

After networking scripts compile, run `./Tools/Unity/Invoke-Unity.ps1 -Action PrepareNetworking`
in the active worktree. This calls `Wildstone.Networking.Editor.FusionContentBuild.Prepare`,
preserves saved author scenes, creates the three registered Fusion prefabs and
attaches online orchestration to Bootstrap. Do not run while another Unity process
owns this checkout or agents are changing its Assets. Network preparation is not a
provider connection test or internet-play acceptance.

Preparation and the build preprocessor generate ignored `Resources/WildstoneBuild.txt`
from a deterministic SHA-256 of runtime content/configuration. Host/join pins this
fingerprint instead of relying on the unchanged application version. Local provider
IDs/settings, the fingerprint itself, tests and Wildstone editor sources are excluded.
Missing or malformed build identity fails closed for online connections.


## Separate-process Photon checks

Build the isolated diagnostic player with
`./Tools/Unity/Invoke-Unity.ps1 -Action BuildSmoke -TimeoutMinutes 10`, then run
one of the following checks:

```powershell
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario basic
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario reconnect
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario combat
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario laser-combat
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario laser-moving-cover
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario projectile-combat
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario grenade-combat
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario special-projectiles
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario laser-moving-cover -NetworkProfile regional80
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario projectile-combat -NetworkProfile adverse150
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario team-switch
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario migration
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario migration-crash
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity -SoakSeconds 300
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario late-join
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario deflection -NetworkProfile adverse150
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario rematch
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity -SoakSeconds 1800 -TimeoutSeconds 2100
./Tools/QA/Invoke-ReconnectCycles.ps1 -Cycles 100
./Tools/QA/Check-NetworkEvidence.ps1
```

The builder enables
`WILDSTONE_SMOKE` only through that BuildPlayer invocation; normal players contain
no automatic menu driver. Runs launch owned, hidden Windows player processes:
two for `basic`, `reconnect`, `combat`, `laser-combat`, `laser-moving-cover`, `projectile-combat`, `grenade-combat`, `special-projectiles`, `deflection`, `stun-client`, `rematch` and `team-switch`; three for `migration`, `migration-crash` and `late-join`;
eight for `capacity`, admitted sequentially to avoid team-swap races in the test
driver. They use the existing Photon App ID and drive actual Host/Join, teams,
ready, loading and gameplay. The runner stops only its own processes on failure.
No new account, provider, deployment or paid subscription is required.

`Artifacts/Smoke/latest-build.json` identifies the diagnostic executable. Per-run
JSON and player logs are under `Artifacts/Smoke/<timestamp>-<scenario>/`. Each
peer's JSON carries `stageTimes`, the driver stages with the whole second each
began (`stage@seconds;`, latest ~3000 characters). On a `DeadlineExceeded` it shows
which step used the time.
A pass requires every required process report and clean exit codes. Room codes are temporary;
do not log guest identifiers or credentials. These checks exercise real cloud
connections from this PC and do not establish independent-network or hardware
performance acceptance. Runtime exceptions/assertions in any player log fail the
run, even if status counters pass. [QA](QA.md) records which scenarios actually pass.

`late-join` first requires ten seconds of two-player gameplay in both original
processes. Only then does the orchestrator launch the third client into the running
room. All three must observe coherent admission, existing seats and authority,
three connected avatars on three teams and ten more seconds of advancing gameplay.

`team-switch` changes the guest through all four teams, including the host's,
returning to its originally assigned team. Each change follows observed readiness
and must clear readiness on host and client before the next request. Runner,
PlayerRef and connection generation must remain stable; only a SHA256 guest
identity fingerprint is recorded, never the raw ID. The host waits for all four
acknowledged moves before ready/start and ten seconds of gameplay. This invokes
the production UI bridge/RPC, not physical pointer input or injected network delay.

`laser-combat` performs production pickup/equip/fire RPCs for pistol, shotgun and
sniper. Host-placed stationary fixtures and health resets are explicit; both peers
must agree on ammunition, damage and shot/pellet event identities. This is not
moving-target or adverse-network acceptance.

`laser-moving-cover` uses the same three production pickup/equip paths, then
moves the host target using normal network input while the client fires. Both
peers observe motion; each checks expected ammo, damage and event identities.
A second shot through an explicit temporary world collider must consume one round
and produce one matching shot event without target damage. Setup teleports, health
resets and cover placement are diagnostic fixtures. With baseline, no fault profile is injected,
and the test does not measure SDK rewind age or establish projectile acceptance.

`projectile-combat` uses a client pickup/equip/fire sequence for Pulse Lobber.
Each peer must sample at least two distinct projectile ticks and more than 0.2 m
of travel, then agree on shot/explosion/hit identities, ammo, falloff damage and
removal. The second shot encounters temporary world cover. The protected target
must remain strictly inside the blast radius and a ray from the actual detonation
center must hit that cover; zero damage outside the radius cannot pass. Teleports
and health resets occur only during explicit setup between shots.
A reliable RPC resent after injected loss can reach the host past the 150 ms
attack window, which drops it unseen (`WILDSTONE COMBAT RPC EXPIRED` in the host
log). If the host has not acknowledged a fire or throw after 1 s, the client
sends the same sequence again, as a player would press again; `resends` in each
shot record counts these. `grenade-combat` does the same.

`grenade-combat` uses two actual grenade throws following the production pickup
RPC. Both peers observe gravity, a backstop bounce, travel and removal, and require
exact fuse-expiry explosion ticks and single ammo spends. An elevated diagnostic
floor/backstop inside the map bounds isolates the trajectory; a lateral wall on
the second throw must protect a target strictly inside blast range. These colliders
and pre-throw avatar/health placement are explicit fixtures, not map-route proof.

`special-projectiles` uses real pickup/equip/fire for Rebound Disc and Stun Gun,
including duplicate identical fire requests that must spend once. The disc passes
the target, reflects off two diagnostic walls and passes back without a second hit;
sampled direction changes or reflected event positions prove its route. Removal
must precede lifetime/range expiry. Three stun shots check initial suppression and
expiry, damage without refreshing the active immunity interval, and stun after
immunity ends. Both peers compare events/ammo/timers; the host must observe active
suppression. On the third shot the host holds forward input on its stunned target
through expiry: the replicated position must move under 0.05 m in at least two
stunned host samples on both peers, then over 0.3 m by 0.4 s after expiry. This
probes the host's own avatar only, not a predicting client target or human feel.

`stun-client` (2026-10-31) makes the predicting client the stun target:
`./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario stun-client [-NetworkProfile adverse150]`.
- The host places the client's player on open ground and the client holds forward.
- After the client reports more than 1 m of travel, the host applies
  `CombatRules.Stun` to it, standing in for the bolt.
- The client gate: the last tick its predicted state was stunned must come before
  the stun's end tick, and the first free tick at or after it. This holds at any
  frame rate.
- Both peers: under 0.05 m of travel in the last 0.15 s before the end tick. Stun
  removes input, not momentum; earlier the player is still coasting.
- Both peers: over 0.3 m by 0.4 s after the end. The client's largest per-frame step
  after release must stay under 0.5 m.
- The wrapper re-checks these from both peers' `stunClient` records. Bolt flight and
  external networks are not covered.

`-NetworkProfile` defaults to `baseline` (SDK simulation disabled). `regional80`
configures added 80 ms RTT, +/-10 ms one-way jitter and 1% per-direction loss;
`adverse150` configures added 150 ms RTT, +/-20 ms jitter and 2% loss. Injected
profiles are restricted to `laser-moving-cover`, `projectile-combat`, `grenade-combat`,
`special-projectiles`, `deflection`, `stun-client` and `gunplay`, and require
Fusion Debug DLLs: Release DLLs disable the simulator. Injected-profile runs get a
180 s scenario deadline in the player and at least 210 s in the wrapper; baseline
keeps 120 s and 150 s. Both peers receive copied
session configs before startup; the shared config asset is never saved or edited.
The pinned SDK internally clones simulation settings on runner initialization;
the diagnostic preserves nonserialized prefab/execution-order data when creating
its config copy. No OS/firewall/network-adapter settings are changed.

The installed SDK applies half the configured RTT delay on receive at each peer.
Noise delay ranges are 0.060–0.100 seconds and 0.110–0.190 seconds, respectively;
additional jitter/loss fields stay zero. Existing transport latency is additional.
Reports include configured values/application count and positive finite SDK remote
RTT samples (count/min/max/mean), never a claim that observed RTT equals the profile
label. These short functional checks do not satisfy external-network, prediction
error, fairness, bandwidth, endurance or performance acceptance.
Clients query their own SDK PlayerRef for server RTT; hosts query remote players.
The SDK value includes peer processing time and is not raw ping. Output folders
include the profile name to separate concurrent runs of the same scenario.

`capacity -SoakSeconds` accepts 10–7200 seconds (default 10; `-TimeoutSeconds` up
to 2400; the timeout is never below `SoakSeconds`+150), extending only the bounded
diagnostic timeout. A match ends after 480 s
and then holds on Results, so a long hold is mostly connected Results time, and the
capacity peers send no gameplay actions: it is a connection and memory soak, not a
gameplay soak. All eight peers must retain coherent players, avatars,
seats and four teams of two throughout the hold, with advancing SDK ticks and no
recovery or authority change. The hold starts in Running; natural Finished time is
reported separately and never counted as running gameplay. The Windows runner
samples its owned processes and writes start/end working set, private bytes,
timestamps and observed hold duration to `result.capacityMemory`, plus a `series`
with one sample per minute of hold. Missing, zero
or incomplete memory evidence fails; Unity/Mono process counters are not used.
This is a short functional soak, not acceptance of frame budgets or endurance.
Each memory point also carries the peer's `managedHeapBytes` and `gcCollections`
(Gen0 count so far). Each status also records `maxFrameGapSeconds`, the largest
frame gap that process has seen. A coherence loss after a gap over 2 s fails as
"environment stalled … rerun on an idle machine" rather than as a game failure. The
host logs `WILDSTONE PEER LEFT participant=… generation=… tick=…` for every peer it
retires.

`capacity -Gameplay` turns the hold into a gameplay soak. From the first coherent
Running frame every peer feeds movement input (weaving forward while turning, with
jump, dash and slide presses), fires its starter at the nearest living enemy every
0.4 s and reloads when empty. The host rematches 3 s into each Results screen and the
ordinary lobby flow readies all eight seats again, so rounds chain in one room.
Between rounds only the eight retained seats (four teams of two), the same runner and
advancing SDK ticks must hold; each new round may carry a new authority generation.
`-RoundSeconds 20..480` makes the host smoke-finish each round early, for quick
functional runs; without it rounds last the natural 480 s. A gameplay result needs
every peer to have fired and to have started at least `1+floor(SoakSeconds/(round+120))`
rounds, and at least one death in total. `result.soak` lists rounds, shots, reloads,
deaths, running and between-round seconds per peer. Folders are named
`<stamp>-capacity-gameplay-<profile>`. Aim is the bot's straight line to the enemy,
so hits depend on cover; this exercises load and endurance, not balance or feel.
The result also carries `capacityMemory[].workingSetGrowthBytes`/`privateGrowthBytes`
(start to end only) and `capacityTraffic[]`: per-role in/out bytes per second for
running and finished time, from `NetworkRunner.TryGetFusionStatistics`. Those
statistics exist only with Fusion's Debug DLLs (the current import);
`capacityStatsAvailable` is false otherwise. Check that `statsForwardTicks` roughly
equals `sdkTicks` before trusting the rates; header and relay overhead are not
documented.

`laser-combat`, `laser-moving-cover` and `gunplay` results carry `rewind`, per weapon
and in total: hitscan queries, how many the 150 ms cap clamped, and the applied and
SDK-requested view ages (mean and max, ms) from `FusionCombatWorld`. These are
reported, not gated.

`deflection` (D037) runs two rounds in one session, host pushing then client
pushing. The shooter takes the Pulse Lobber and the pusher the Push Lens from their
production pickups, 12 m apart. The pusher fires when its predicted orb distance at
resolution is about 4.5 m. Both peers must see one `Deflected` event, the orb flipped
to the pusher (owner, team, flag, launch tick kept, velocity reversed), one blast
and one hit on the shooter credited to the push with the expected falloff damage,
no damage to the pusher and no projectile left.

`rematch` plays two rounds in one room. The 480 s clock is the only shortcut: the
host ends each round with smoke-only `FusionMatch.SmokeFinish`, which also wounds a
player and takes a pickup, so round 2 must visibly start fresh. Both peers must:
- show online Results with "Leave match": the host has "Rematch", the client
  "Waiting for the host…", and neither has "Play again";
- leave the session unchanged after a client rematch press;
- after the host's rematch, return to the lobby with ready cleared and the same
  runner, room, match ID, seat, team and connection generation;
- start round 2 with a new match object, zero score, no projectiles, full health,
  first lives with starter ammo, all pickups available and a new HUD.

The host must publish a round-2 recovery checkpoint before ending round 2. Both
peers must report the same winners. Baseline only.

`Invoke-ReconnectCycles.ps1 -Cycles N` runs `Invoke-NetworkSmoke.ps1 -Scenario
reconnect` N times on one build. Each cycle is a fresh host, client and room:
join, 10 s of play, host-side disconnect, reconnect into the held seat, 10 s more.
The summary `Artifacts/Smoke/<stamp>-reconnect-cycles.json` is rewritten after
every cycle. It does not repeat reconnects into one long-lived host. `-Executable`
pins a build; otherwise it pins the one `latest-build.json` names at the start.
`Check-NetworkEvidence.ps1` type-checks the Networking sources with and without
`WILDSTONE_SMOKE` (which `Compile-Managed.ps1` does not cover) and checks the rewind
clamp rule, rewind averaging and the soak bound.

`combat` first verifies actual SDK movement and duplicate-fire deduplication. It
then records a controlled host-authoritative teleport fixture, waits for both peers
to observe stationary targets and populate hitscan history, and sends one real
client fire command. Both peers must agree on the shot/hit identities, target life,
catalog damage, ammunition spent and unchanged unrelated health. Movement-distance
metrics stop before fixture placement. This checks stationary hit replication,
not natural traversal or moving-target behavior under adverse network profiles.

`migration` leaves the original host through the normal menu cleanup path. For
`migration-crash`, the original host first records `crashReady=true`, its pre-loss
match tick and a successful cloud snapshot publication tick. It keeps running and
does not call Leave or mark itself successful/finished. The orchestrator then kills
only that owned host process. Its expected kill exit code and missing final success
report are excluded from the survivor exit/status gates; `result.json` must record
`hostCrashInjected=true` and the host process as `expectedCrash=true`. Both survivors
must finish successfully and exit zero, with the ordinary authority, rollback,
single-successor and resumed-gameplay checks intact. All three player logs still
participate in the exception/assertion gate, including the terminated host's log.
An unexpected host exit before the kill handshake remains a failure.

An active remote client's transport `Timeout` can arrive before Photon supplies
its migration token. With a usable recovery baseline, keep the SDK cloud connection
and callbacks alive for election while freezing that disconnected runner's gameplay.
Do not preemptively shut it down. Only this timeout path waits: server kicks and
other disconnect reasons still release transport promptly and expose ordinary guest
reconnect. The first timeout starts the recovery deadline; token arrival, repeated
timeouts and replacement-runner startup reuse it without granting another window.
Explicit leave/cancel or expiry closes the waiting connection. The post-fix
`migration-crash` service check and reconnect/graceful regressions now pass;
[QA](QA.md) records their evidence. Other callback orderings, adverse network
profiles and independent-network acceptance remain open.

The elected host's synchronous `HostMigrationResume` callback validates and restores
the complete checkpoint. Its completion can occur after the `StartGame` task returns;
startup therefore awaits the callback's separate completion task within the recovery
deadline before accepting success. A callback exception, timeout or cancellation
aborts recovery. The [foundation contract](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#host-migration-contract)
owns the recovery limits.

## Offscreen packaged visual checks

After `BuildSmoke`, run `./Tools/QA/Invoke-VisualCheck.ps1`. It launches one owned
hidden D3D11 player and records sixteen PNGs plus JSON/logs under
`Artifacts/Visual/<timestamp>/`. The compile-time diagnostic driver uses the
existing screen/map flow, a RenderTexture and Unity's built-in ImageConversion
module. It submits actual camera render requests and composites the UI panel onto
the same target, including 720p, maximum UI scale, 1080p and an ultrawide title.
Normal builds exclude both diagnostic drivers. Settings are applied in memory
without saving; this does not send keyboard or mouse input to the desktop.

The script checks completion, capture files, process exit and runtime logs. Inspect
every image before accepting layout/contrast: file creation alone cannot detect
blank or accumulated frames. These static captures do not validate physical input,
moving readability, traversal or human game feel.

For first-person body/handling inspection, `./Tools/QA/Invoke-VisualCheck.ps1 -Motion`
records thirteen explicitly labeled visual pose fixtures: look-down, two gait phases,
air, slide entry/sustain/down/low-FOV/exit, fire, reload and sword. These sample the
actual presentation classes against a packaged map with deterministic fixture state;
they do not drive gameplay input or prove traversal/combat. Every image still needs
inspection; use native behavior tests and actual sessions for those separate gates.
The Unity wrapper accepts `-Graphics` for an explicit D3D11 test that needs real
rendering, such as `Wildstone.Tests.FirstPersonBodyTests.EachLegSegmentRendersWithBackfaceCulling`.
Ordinary focused tests retain the headless default.

Weapon expression captures: `./Tools/QA/Invoke-VisualCheck.ps1 -Weapons` records
35 packaged render fixtures: ten weapon shots against two map facings, plasma and
grenade blasts, disc ricochet, stun impact, Lens charge, the grenade held with the
throw key down and its arc shown (`weapon-grenade-aim`), overlapping laser beams,
a remote soldier 7 m ahead at the peak of its hit flash (`weapon-remote-hit`),
six pickup pads on the court, including a health pack, one of them mid-respawn (`weapon-pickups`), and a
remote soldier 6 m ahead with an active shield (`weapon-status-shield`), stunned
(`weapon-status-stun`), or on the frame its shield breaks (`weapon-status-shield-break`).
`weapon-remote-wallrun` shows two remote soldiers wall-running toward the lens
beside diagnostic walls, the left-wall run on the right of the frame.
`weapon-low-ammo` leaves five rounds in the starter so the HUD count shows the
low-ammo warning colour. The last frame, `weapon-kill-banner`, shows the HUD banner
after two quick kills, the second a headshot; it comes last because the banner
outlasts a step.
These use production presentation and explicitly empty-world projectile rules;
they are labeled fixtures and do not replace input/network/human-feel acceptance.
Continuous first-person samples: `./Tools/QA/Invoke-VisualCheck.ps1 -Sequence`
records 32 PNGs across run (with a dash at frame 11), slide/return, fire/recoil
settle and reload/return.
It retains presentation history while advancing fixed 1/60-second synthetic frames
with the matching combat tick rate. Each sample records state, camera/hands poses,
simulation time and separate wall time; wrapper gates require visible pose changes
and return to rest. It is not natural gameplay, remote readability or framerate proof.
The fixture uses the starter weapon; the other weapon modes remain distinct.

Firefight sequences (2026-10-30): `./Tools/QA/Invoke-VisualCheck.ps1 -Firefight`
records 67 PNGs: six sequences at frames 0, 1, 2, 4, 6, 9, 14 and 20 after each
shot, two at frames 0, 2, 4, 8, 14, 22, 34 and 50 after each blast, and three range
views.
- Four sequences fire a real Line Driver, Laser SMG, Laser Shotgun and Laser Sniper
  shot. Each lands a headshot on a remote soldier 12 m ahead, strafing at 5 m/s, so
  the frames show the beam, sparks, hit flash, view punch and viewmodel flare.
- A fifth swings the Laser Sword at the strafing soldier, placed at 60% of the
  sword's reach (`firefight-lasersword-NN`).
- The next is a remote sniper rail from the right flank that passes 0.7 m beside
  the lens.
- The last two detonate a grenade and a Pulse Lobber orb on the court 8 m ahead,
  2 m from the soldier. The fixture world has no floor, so the flight is skipped and
  the blast is placed where the host would report it.
- Particles age on the unscaled clock, so a shot fires only after a normal-length
  frame. Firing right after the previous sequence's PNG encoding aged a blast by
  most of a second before its first sample.
- Samples are copied on the GPU at render time and encoded after the sequence.
- `Time.captureDeltaTime` holds fast frames to 1/60 s. On a slow frame (the
  shot-setup frame) the unscaled effect clock still follows real time, so each
  capture records its true age. The wrapper requires ages within one frame of
  nominal and rising through each sequence.
- Three single views (`firefight-range-15m/30m/45m`, added 2026-10-31) show the
  strafing soldier straight ahead at each distance with the Line Driver held and
  nothing fired. They skip the age gates but keep the count and file checks.
- Fixture captures set `forceMatrixRecalculationPerRender` on skinned meshes,
  because the forced render runs before the frame's own skinning pass.
- Since 2026-10-31 every first-person fixture uses the game's default view, 100
  degrees horizontal at 16:9 (`FixtureFov`). The one exception is the explicit 60-degree slide pose.
  Earlier captures used 90 degrees vertical and drew the world about 1.5x smaller.

These are labeled fixtures, not gameplay, remote-network or feel proof.

Music streaming (2026-10-30): `./Tools/QA/Invoke-VisualCheck.ps1 -Music`. Batch-mode
players run without audio, so this mode drops `-batchmode` and launches a normal
hidden player with `AudioListener.volume` 0. That keeps real streaming and mixing
with silent speakers. It records five PNGs: the title, one per map and a recorded
weapon cue. Each music step waits until the active music voice plays the screen's
own track (`MusicMix.TrackFor`) for more than 0.5 s, with volume above 0 and
source-output RMS above 1e-4. The last step fires a Line Driver shot and a sniper
shot through `ArenaAudio` on Terrace Steps. It waits until a `LaserFire` clip and a
`LaserCellNova` tail both reach the mixer with output RMS above 1e-4. The step also
reports the speed-wind voice's clip, and the script fails unless all three are
present. Steps time out with
a diagnostic otherwise. The static mode records "not checked" for music. This proves
streaming and recorded-cue playback in the packaged player. It does not prove the
speaker level, the mix, or how anything sounds.

All modes validate the offscreen camera/UI target and PNG dimensions, so a clamped
hidden window does not stall capture or silently define the image's aspect ratio.
`width`/`height` describe the PNG; `screenWidth`/`screenHeight` describe the OS window.
`-Motion`, `-Weapons`, `-Sequence`, `-Firefight` and `-Music` are mutually exclusive. `-Action PreparePresentation`
imports/builds the model catalog without regenerating map geometry or baked scenes.
Forced render requests are skipped until every scene is loaded. A request during a
map load or unload can cull already-destroyed reflection probes, which raises a URP
`ReflectionProbeManager` NRE.

Viewmodel framing: `./Tools/Unity/Invoke-Unity.ps1 -Action CaptureViewmodel` (always
graphics). It renders the hip view and full ADS for ten held weapons over the baked
Atrium spawn into `Artifacts/Visual/Viewmodel-<stamp>/`. The log also gets one
`WILDSTONE VIEWMODEL MEASURE` line per weapon: lens-space grip, sight, muzzle,
support marker and weapon bounds. Bounds are a world AABB, so they are loose on the
diagonal spawn view. Use it to tune `WeaponView.HipPose` in minutes rather than
through a player build. Before the weapons, it renders two views of each baked map,
with no HUD or viewmodel: `map-<id>-overview` (a high corner looking at the centre)
and `map-<id>-ground` (eye height at the centre, looking toward the first spawn).
Use them to judge palette, lighting and grade changes after `Prepare` and `Bake`.

Frame-time sample: after `BuildSmoke`, run `./Tools/QA/Invoke-PerfSample.ps1`. It
opens a **visible** player window for about 40 s with audio muted. The player renders
Atrium through the normal render loop into a 1920x1080 target, so a small desktop
does not shrink it. The scene is the local fixture, seven strafing soldiers firing
beams every 0.1 s and a grenade blast every 1.2 s. It measures Ultra (level 5) and
then Low (level 1), each with a 3 s warm-up and 8 s of sampling, uncapped with VSync
off. It writes `perf.json` (mean/p50/p95/p99/max ms) plus one PNG per tier under
`Artifacts/Visual/Perf-<stamp>/`. Each tier also reports managed allocation per
measured frame (`gcAllocMeanBytes`, `gcAllocMaxBytes`, from the development player's
"GC Allocated In Frame" counter) and `gen0Collections` over those frames. This is
same-PC evidence, not a networked load or lower-end GPU measurement.
