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
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario team-switch
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario migration
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario migration-crash
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity -SoakSeconds 300
./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario late-join
```

The builder enables
`WILDSTONE_SMOKE` only through that BuildPlayer invocation; normal players contain
no automatic menu driver. Runs launch owned, hidden Windows player processes:
two for `basic`, `reconnect`, `combat`, `laser-combat`, `laser-moving-cover` and `team-switch`; three for `migration`, `migration-crash` and `late-join`;
eight for `capacity`, admitted sequentially to avoid team-swap races in the test
driver. They use the existing Photon App ID and drive actual Host/Join, teams,
ready, loading and gameplay. The runner stops only its own processes on failure.
No new account, provider, deployment or paid subscription is required.

`Artifacts/Smoke/latest-build.json` identifies the diagnostic executable. Per-run
JSON and player logs are under `Artifacts/Smoke/<timestamp>-<scenario>/`.
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
resets and cover placement are diagnostic fixtures. No fault profile is injected,
and the test does not measure SDK rewind age or establish projectile acceptance.

`capacity -SoakSeconds` accepts 10–600 seconds (default 10), extending only the
bounded diagnostic timeout. All eight peers must retain coherent players, avatars,
seats and four teams of two throughout the hold, with advancing SDK ticks and no
recovery or authority change. The hold starts in Running; natural Finished time is
reported separately and never counted as running gameplay. The Windows runner
samples its owned processes and writes start/end working set, private bytes,
timestamps and observed hold duration to `result.capacityMemory`. Missing, zero
or incomplete memory evidence fails; Unity/Mono process counters are not used.
This is a short functional soak, not acceptance of frame budgets or endurance.

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
26 packaged render fixtures: ten weapon shots against two map facings, plasma and
grenade blasts, disc ricochet, stun impact, Lens charge and overlapping laser beams.
These use production presentation and explicitly empty-world projectile rules;
they are labeled fixtures and do not replace input/network/human-feel acceptance.
Continuous first-person samples: `./Tools/QA/Invoke-VisualCheck.ps1 -Sequence`
records 32 PNGs across run, slide/return, fire/recoil settle and reload/return.
It retains presentation history while advancing fixed 1/60-second synthetic frames
with the matching combat tick rate. Each sample records state, camera/hands poses,
simulation time and separate wall time; wrapper gates require visible pose changes
and return to rest. It is not natural gameplay, remote readability or framerate proof.
The fixture uses the starter weapon; the other weapon modes remain distinct.

All modes validate the offscreen camera/UI target and PNG dimensions, so a clamped
hidden window does not stall capture or silently define the image's aspect ratio.
`width`/`height` describe the PNG; `screenWidth`/`screenHeight` describe the OS window.
`-Motion`, `-Weapons` and `-Sequence` are mutually exclusive. `-Action PreparePresentation`
imports/builds the model catalog without regenerating map geometry or baked scenes.
