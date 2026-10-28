# Unity tooling

## GitHub builds and private releases

The personal repository is `Capsfan900/multiplayer_1` (private). Run **Actions >
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
Private releases require GitHub repository read access; there is no anonymous link.
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
and migration composition remain unimplemented. The recoverable provider client uses
Unity's built-in `com.unity.modules.unitywebrequest` 1.0.0; no extra PlayFab SDK is
required for its four client endpoints. [QA](QA.md) records focused native integration
checks; compilation alone does not establish working networking.

The cached source Unity MCP package is Coplay `com.coplaydev.unity-mcp` 10.1.2,
source Git commit `4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50`. It is not
installed here: batch tooling and native editor builders currently supply the
authoring commands. The upstream commit's MIT license was verified; if an interactive
bridge is added, preserve that license and pin the package to the reviewed commit.
Select only this project's Editor instance.
