# Blender and Unity asset pipeline

Updated: 2026-10-27. The isolated Blender environment and native Unity asymmetric
axis fixture pass. Production model moving visual acceptance remains open. See
[MVP progress](MVP-PROGRESS.md) for current evidence.
Return to the [documentation index](README.md). The
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md) owns graphics
strategy; [combat](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md)
owns gameplay; the [asset workflow](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
owns visual review.

Use Blender and Unity to build editable maps, humans and weapons. Audit
`C:\Users\tyler\Main Storage\vibegame1` first and selectively inspect sibling
`ai_skelly_tool`; leave both projects untouched. Multiplayer authority, prediction, reconciliation and replicated state
are new Fusion integration, not inherited single-player behavior. The user's D018
instruction permits independent asset work while Photon provisioning is pending;
it does not waive foundation or imported-asset acceptance.

## Tool and project preflight

Native batch Unity now runs against the isolated `mvp-2` project through
[TOOLING](TOOLING.md). No Unity MCP bridge is configured in this worktree.
Before any future bridge-based editing, enumerate instances and verify the exact
project path. Never reuse another checkout's instance identifier.

`Get-Command blender` found no executable on PATH; no Blender MCP tool was observed.
The source scripts use `bpy` through sibling `ai_skelly_tool`. Its
`.venv/Scripts/python.exe -B` was verified to run Python 3.11.16 and bpy/Blender 4.2.0
in background mode. The isolated process writes assets only into this destination.
An available Blender CLI or compatible existing `bpy` environment is sufficient;
do not require a new plugin. Audit scripts' hard-coded source/output paths and
dependencies before adapting them into this workspace. Do not run them against
the original checkout.

## Verified reuse leads

Source-qualified paths below use the read-only roots in [SOURCE-REUSE](SOURCE-REUSE.md).
Existence and the named
code paths were inspected; compatibility, licensing and visual fit still need review.

| Existing source | Decision and reason |
| --- | --- |
| `vibegame1/Assets/Editor/LevelStudio/` and `vibegame1/Assets/Editor/LevelDefinitionBuilder.cs` | Adapt native authoring. `BuildWorldContent` uses an explicit root without opening/saving scenes or creating campaign managers; audit optional runtime behaviors before reuse. |
| `vibegame1/Assets/Scripts/Level/LevelPieceFactory.cs`, `vibegame1/Assets/Scripts/Level/LevelMesh.cs` | Reuse procedural structural geometry where faster. Structural boxes retain BoxColliders while rendering shared chamfered meshes. |
| `vibegame1/Tools/env_models/build_architecture.py`, `vibegame1/Assets/Editor/LevelArchitectureBuilder.cs`, `vibegame1/Assets/Editor/ArchitectureKitPlan.cs` | Adapt modular sizing/export conventions; simplify the old detailed masonry to the accepted modernist forms. |
| `vibegame1/Tools/weapon_models/build_weapons.py`, `vibegame1/Tools/weapon_models/build_laser_kit.py` | Adapt geometry, grip/emitter and export-roundtrip checks; replace ornate surface treatment. Audit sibling helper dependencies. |
| `vibegame1/output/laser-kit/LaserPistol.blend`, `vibegame1/Assets/Art/Weapons/Modeled/`, `vibegame1/Assets/Art/Viewmodels/` | Editable source/export and hand-fit leads. Preserve useful geometry and anchors; they do not prove a suitable full human rig. |
| `vibegame1/Assets/Scripts/Player/WeaponViewmodel.cs`, `vibegame1/Assets/Editor/PrefabFactory.cs` | Adapt local presentation/attachment setup after dependency tracing. Rewrite network ownership and combat integration: `LaserPistol` uses global `GameEvents`; `LaserShot` damages source enemies. |

Do not carry source global pause, hitstop/time-scale mutation or campaign `GameEvents`
into shared simulation. Preserve movement laws through the foundation's explicit
fixed-tick adaptation rather than treating asset reuse as multiplayer conversion.

### Selective ai_skelly_tool reuse

Read-only inspection found useful code, not executed proof:

- `ai_skelly_tool/stages/rig.py`: humanoid armature/binding helpers
  and bind-frame-aware FBX export. Existing `vibegame1` weapon builders already
  import its helpers. The heuristic body fitter still needs comparison against
  the deliberately simple shared human; do not adopt it unquestioningly.
- `ai_skelly_tool/stages/weapon.py`: hand sockets, blade
  clearance and two-hand grip helpers. Reuse attachment/measurement ideas, not
  boss hit events as multiplayer damage authority.
- `ai_skelly_tool/tests/test_skin_stretch.py`,
  `ai_skelly_tool/tests/test_mesh_artifacts.py` and
  `ai_skelly_tool/tests/test_anim_quality.py`: deformation
  and motion validation candidates. Audit sidecar/helper dependencies and calibrate
  thresholds on our mesh rather than inheriting enemy acceptance results.

Keep `bpy` isolated through the existing `stages/rig_cli.py` subprocess boundary;
its documented Windows DLL conflicts preclude merging the GPU and Blender imports.
`tools/make_weapon.py` invokes GPU generation and defaults to source
`output/_weapons`; it is not needed for this asset path. Redirect all adapted outputs
here. The source README flags `rembg` model-license caveats and `bpy` redistribution
terms; verify actual selected code/model/asset licenses before shipping. Do not
inherit its whole generation stack or treat its README as a complete license audit.

## Editable source and reproducible export

Keep versioned `.blend` sources, generator scripts, exported FBX, material/texture
dependencies and import settings. Record source revision/hash, tool versions,
export invocation/settings, asset identity and license/provenance. For reused Unity
assets, preserve `.meta` GUIDs and their dependency closure; retain GUIDs on re-export.
New independent assets receive their own identities. Do not copy a whole output
directory without establishing which inputs are required and distributable.

Observed weapon export uses `FBX_SCALE_ALL`, forward `-Z`, up `Y`, no leaf bones,
no animation bake and no embedded textures; it checks reimported bounds, triangle
counts, materials and anchor origins. Architecture also converts authored Unity
coordinates and reverses winding before export. These are existing conventions,
not a universal preset: avoid double axis conversion and do not apply the static
mesh settings blindly to a skinned rig.

Establish one unit/axis roundtrip fixture in Blender and Unity. Verify known-size
geometry, pivot, handedness, normals, grip/muzzle orientation, material mapping and
nonuniform scale. For a rig, also verify bind pose, bone orientation, skin weights,
animation scale and foot contact. Save the successful settings and checks before
batch production; a Blender-only reimport does not prove Unity correctness.

The authored models use Blender `(X,-Z,Y)` and FBX `-Z` forward / `Y` up. Native
import exposed an X reflection: the fixture Grip imported at `(-.12,.10,-.05)`
instead of the authored `(.12,.10,-.05)`. `WildstoneModelImportPostprocessor`
corrects handedness on the imported root with an X scale reflection, restricted to
the fifteen named procedural models in `Assets/Wildstone/Art/`; other models are
untouched. Editable sources and FBX exports remain unchanged.
Keep the imported root's axis rotation and reflection when instantiating models.
Fixture checks use asset coordinates including that root, matching the presentation
builder. This import correction requires native fixture/rig/presentation acceptance;
source/export hash checks alone do not validate it.

## Production branches and acceptance

- **Map:** use a small modular kit and Unity-native Level Studio placement. Keep
  procedural walls, ramps and simple primitives when they already solve the shape;
  Blender is for useful authored geometry, not a mandatory detour for every box.
  Keep collision separate from decorative bevels and details. Generate validated
  UV2 for lightmapped geometry: inspected `LevelMesh` writes UV0 but no UV2.
  Version geometry with matching lightmaps/probes; edits invalidate the bake.
- **Humans:** build one simple shared human base/rig for the initial eight players,
  with four team markings and a common fair hit envelope. Verify head/limbs/facing,
  movement poses and hitbox alignment. Cosmetic variation cannot shrink the target.
- **Weapons:** preserve each accepted mechanism's identity in first-person and
  world/pickup models. Give them separate detail/rendering budgets; retain consistent
  grips, emission direction and gameplay attachments. Visual trails and meshes do
  not define authoritative damage.

Weapon specials (2026-10-29): run `Tools/Assets/build_assets.py weapon-specials
--output-root .` with the audited Python interpreter above to rebuild only
LaserShotgun, PulseLobber, ReboundDisc, PushLens, StunGun and LaserSword in place.
The `weapon-specials-previews` mode renders their Workbench previews and the eleven-weapon
`ArtSource/Previews/WeaponSpecials-ContactSheet.png`. Lofted shells, layered panels,
lathed emitters and bevels follow the existing kit; the shotgun pump is one joined
moving assembly. Grip remains `(0,-.12,-.18)` with the handle descending along -Y;
LeftHand is placed on each fore-end (lower grip on the sword). Axis/export settings,
palette identities and required moving-part names are preserved. All six reimported
FBXs pass bounds, 1.5k–6k triangle, palette, identity-socket and moving-part checks;
exact hashes/measurements are in `ArtSource/weapon-specials-export-manifest.json`.
Existing matching manifest entries are refreshed without rebuilding other models.
Blender renders were inspected; Unity import, held first-/third-person animation,
30 m recognition and gameplay readability remain with the Unity lane.

Starter-gun visible-face pass (2026-10-30): the four existing `weapon(root, name)`
branches for LineDriver/LaserSMG/LaserPistol/LaserSniper add graphite lower
receivers, layered plates, top rails/windows and rear/right signal accents.
Their sixteen socket positions remain exactly equal to the pre-pass FBX readings;
all legacy names and optic targets survive. Blender reimport checks pass at
4,416–7,000 triangles and under 4% extent change on every axis (limit ±5%).
Current hashes are in `ArtSource/laser-kit-export-manifest.json`; the grenade and
specials were not rebuilt. The four named preview PNGs now show rear-right-above
surfaces, with the eleven-weapon contact sheet refreshed. No Unity was run;
0.6-scale packaged framing, emission, ADS, hand IK and distance readability need
native review. Reproduce these four only by calling `weapon(root, name)` through
the existing bpy interpreter; the older `laser-kit` CLI also rebuilds LaserShotgun
and is not the surgical invocation for this pass.

Energy art pass 2 (2026-10-30) supersedes the preceding weapon-pass measurements.
Run `build_assets.py weapon-energy --output-root .` with the audited interpreter
to rebuild only the eleven weapons, compare pre-/post-export sockets, names and
bounds, and refresh matching export-manifest entries. The current canonical
record is `ArtSource/weapon-energy-export-manifest.json`. Budgets are 9,000
triangles per weapon and 3,000 for Grenade. The sword alone permits blade-length
growth up to 40%; all socket positions remain fixed, including its existing Muzzle.
`weapon-energy-previews` renders lit Eevee full views, close rear-right-above views
under `ArtSource/Previews/FirstPerson/`, and a contact sheet in each directory.
Local Signal emission plus glare at threshold 1.05 visualizes the energy geometry;
it is an approximation, not the Unity shader/bloom pipeline. The sword's separate
open glow envelope shares SignalCyan and leaves the core visible without requiring
an unsupported transparent material. Native import, ADS/IK, mechanism motion,
0.6-scale packaged appearance and 30 m readability still need Unity review.

Health pack (2026-10-31, D039): run `build_assets.py health-pickup --output-root .`
with the audited interpreter to build only `ArtSource/HealthPickup.blend` and
`Assets/Wildstone/Art/HealthPickup.fbx`. It uses the existing box helper: an ivory
canister, graphite end caps and a `SignalMint` plus on the top and both faces. The
Blender re-import check passes at 972 triangles and 0.29 × 0.18 × 0.22 m; hashes are in
`ArtSource/health-pickup-export-manifest.json`. No other model is rebuilt. Unity maps
`SignalMint` through the presentation palette like the other `Signal*` accents.

Exit with editable sources, reproducible exports, dependency/provenance records,
Unity import/rig/collision checks and moving readability evidence. Profile the
finished slice and busy eight-player build against the rendering/foundation gates.
A 2D concept, source asset or attractive Blender render proves neither mesh quality
in gameplay nor FPS, network reliability or collision correctness.

## Armored soldier and animation (D028)

Updated: 2026-10-29. The character contract is in the
[character workflow](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
section 6. Run every command below from the repo root with the audited interpreter
`"C:\Users\tyler\Main Storage\ai_skelly_tool\.venv\Scripts\python.exe" -B`. That
`ai_skelly_tool` checkout stays read-only: modules are imported by path, and outputs
land only in this repo.

| Step | Command | Outputs |
| --- | --- | --- |
| Concept (reference only) | forge `stages.concept.build` (SDXL + OpenPose ControlNet + IP-Adapter), then a TripoSR (MIT) proportion mesh | `ArtSource/Characters/Forge/soldier_concept/`. These are not shipped or meshed directly. `concept.json` records prompts and seeds. |
| Skeleton | `Tools/Assets/soldier_skeleton.py` | 52-bone Humanoid naming and the socket table; prints `52 bones OK` |
| Body and arms | `Tools/Assets/build_soldier.py all --output-root .` | `ArtSource/Characters/Soldier.blend`, `SoldierArms.blend`, `Assets/Wildstone/Art/Soldier.fbx`, `SoldierArms.fbx`, `ArtSource/Previews/Soldier*.png` and `soldier-export-manifest.json` |
| Animation | `Tools/Assets/soldier_clips.py {build,verify,previews} --output-root .` | `ArtSource/Characters/SoldierAnimations.blend`, `Assets/Wildstone/Art/SoldierAnimations.fbx`, `SoldierAnimations.clips.json`, `ArtSource/Previews/Anim/*.png` and `soldier-anim-manifest.json` |
| Unity import | `./Tools/Unity/Invoke-Unity.ps1 -Action PreparePresentation` | `SoldierBuilder` builds the Humanoid avatar, the copied-avatar clips, `Soldier.controller`, `SoldierTeam1..4.prefab` and `SoldierArms.prefab` |

**Provenance:**
- The manifests record the forge commit `450fce5c` (branch `stitched-motion`, 188
  uncommitted entries at the time) and the hashes of each reused forge file
  (`stages/anim.py`, `tests/test_anim_quality.py`, `tests/asset_checks.py`).
- The gauntlet and hand construction is ported from `vibegame1`
  `Tools/weapon_models/build_art.py` at `2cb6e576`, listed per function in the
  manifest.
- Animation is original procedural keyframing, not mocap or generated motion.
- No Hunyuan3D or MoMask output is used.

**Gates:**
- **Body build:** bone heads and parents, sockets, weights (no cross-body weights),
  materials, triangle budgets and height, all on the reimported FBX.
- **Clip build:** loop seam under 0.5°; nothing below the floor by more than 1 cm;
  foot slip under 2 cm; leg-IK miss of 1 cm or less.
- **verify:** FBX against the solved pose within 0.71 mm, plus the forge's own
  animation quality checks.

**Determinism:** `SoldierArms.fbx` changed hash without a mesh change, so FBX export
is not byte-reproducible. Compare the manifest measurements rather than the hashes.

## Music

Updated: 2026-10-30 (D032). Four looping chill-house instrumentals from a local
ACE-Step 1.5 run ship in `Assets/Wildstone/Resources/Audio/Music/`. They replace
the code-synthesized set (D030 fallback), which stays rebuildable. Playback is
owned by the menu/UI lane, which loads `Resources` path `Audio/Music/<name>`.

- **Generation (not part of the build):** `Tools/Music/ai_takes.py` records the
  captions, keys, tempos, seeds and negative prompt. It ran ACE-Step 1.5 turbo
  (`acestep-v15-turbo` with the 1.7B LM), instrumental, 180 s per take, on the
  user's RTX 4070 Ti, through the `ai.py` runner from the user's
  code-music-production skill. The user approved the GPU run. No reference audio,
  lyrics, artist or song names were used. 15 takes were made, about 40 s each.
- **Choosing takes:** `loop_takes.py --probe` checks each seed's kick grid and
  groove window. Stem separation then measured the vocal stem (RoFormer, the skill's
  `stems.py`). Every chosen take's vocal stem is 55–65 dB below the mix, which is
  separation bleed. `terrace_steps_s11` had ten 2 s windows of vocal-like content
  (up to −27 dB), so it was replaced by seed 66. Chosen takes and their sha256:
  - `menu_s11.wav`: `641c82e5d968a5677a5f7d7de403e600e013a0cba478b047a285147e581cd613`
  - `atrium_s11.wav`: `8b18b97217079657d432073d3bb24f1dab0baca3e5f21006ba1bfe0508723ad4`
  - `split_gallery_s33.wav`: `fd74548502050086e94d659c50192668036cb3d295bf4fe2ad01a535550c2bbf`
  - `terrace_steps_s66.wav`: `89fbcada298995daa9c9d104472ac7c2619ea7f1dadc88dc66c86bffa9f216a4`

  All takes are archived outside the repo in `D:\WildstoneArchive\music\takes`
  (48 kHz float WAV, about 69 MB each). Regenerating on other hardware or drivers
  may not be bit-identical.
- **Loop cut (the build step):** `uv run Tools/Music/loop_takes.py <takes dir>`.
  - Resample to 44.1 kHz, then apply a −2 dB 3 kHz dip that keeps the bed out of
    the weapon-SFX presence band.
  - Find the constant-tempo kick grid. The downbeat is the beat phase where the
    harmony changes most.
  - Cut the longest run of whole 8-bar phrases whose first and last bars carry the
    groove.
  - Crossfade the beat that followed the loop end in the take over the loop start,
    so the wrap plays what came next.
  - Master to −15 LUFS and −1 dBTP with `build_music.py`'s circular limiter and
    ffmpeg libvorbis q6, using bitexact muxing.

  Each track's bar count and sample count are locked in `loop_takes.py`, so a
  changed take or grid fails the build. Two consecutive builds produced
  byte-identical OGGs. Verify the shipped files with `--check`, which does not need
  the takes.
- **License and provenance:**
  - ACE-Step 1.5 code and weights are MIT. Its model card states that generated
    music may be used commercially and that it was trained on licensed,
    royalty-free/public-domain and synthetic data.
  - The prompts are original, and no copyrighted reference conditioned any take.
  - The audio ships in this project's free public build (D027).
  - The tools are not shipped: ACE-Step and its venv, the RoFormer separation models
    used only for the vocal check, uv, numpy/scipy/soundfile, and ffmpeg/libvorbis.

Measured 2026-10-30 by `loop_takes.py --check` on the encoded OGGs (ffmpeg 9.0.1
`ebur128`):

| File | Take | Grid BPM / key asked | Bars / length | Integrated | LRA | True peak | Seam step (file p99.9) | Seam curvature (file p99.9) | 2-5 kHz energy |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `MenuTheme.ogg` | menu s11 | 116.00 / D major | 80 / 2:45.522 | -15.0 LUFS | 1.6 LU | -3.4 dBTP | 0.026 (0.317) | 0.025 (0.454) | -21.9 dB |
| `Level_atrium.ogg` | atrium s11 | 122.00 / A minor | 72 / 2:21.635 | -15.0 LUFS | 2.1 LU | -1.2 dBTP | 0.054 (0.272) | 0.123 (0.370) | -22.9 dB |
| `Level_split-gallery.ogg` | split gallery s33 | 124.99 / C minor | 72 / 2:18.248 | -15.0 LUFS | 2.7 LU | -3.4 dBTP | 0.008 (0.281) | 0.006 (0.383) | -26.0 dB |
| `Level_terrace-steps.ogg` | terrace steps s66 | 121.00 / E major | 48 / 1:35.207 | -15.0 LUFS | 3.2 LU | -2.1 dBTP | 0.034 (0.285) | 0.017 (0.400) | -21.3 dB |

The model played split gallery at 125 and terrace steps at 121 BPM rather than the
asked tempos. The loops use the measured grids.

Unity import and playback:
- `MusicImportTests` (EditMode, Native) asserts that each imported clip is a 44.1 kHz
  stereo Streaming clip whose sample count equals the OGG's final granule position.
  Unity re-encodes on import, so this is the loop length the game actually plays.
- `Invoke-VisualCheck.ps1 -Music` runs a windowed, listener-muted packaged player.
  It asserts that the title streams `MenuTheme` and each map streams its own loop,
  with non-silent source output.

Limits:
- No one has listened to these takes. Measurements and stems stand in for ears.
- AI music can contain small artefacts that a vocal stem and band checks will not
  catch.
- The terrace steps loop is shorter (48 bars) because only seed 66 combined a clean
  vocal stem with a long groove window.

### Synthesized set (2026-10-29, D030 fallback; superseded by D032)

`Tools/Music/build_music.py` with `engine.py` still renders the original code-only
loops. These are original compositions synthesized from oscillators and noise, with
no samples, presets, recordings, AI models or references. They used the same format
and targets as the current set: OGG q6, -15 LUFS, -1 dBTP, exact bar-length loops. Running it overwrites
the shipped OGGs with that set. Its `--check` mode measures against the synthesized
lengths, so it fails on the current files.

Measured 2026-10-29 by `build_music.py --check` on the decoded OGGs (historical) (ffmpeg 9.0.1 `ebur128`):

| File | BPM / key | Bars / length | Integrated | LRA | True peak | Seam step (file p99.9) | Seam curvature (file p99.9) | 2-5 kHz energy |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `MenuTheme.ogg` | 118 / D major | 56 / 1:53.898 | -14.8 LUFS | 2.7 LU | -3.4 dBTP | 0.0105 (0.166) | 0.0065 (0.229) | -23.2 dB |
| `Level_atrium.ogg` | 122 / A dorian | 72 / 2:21.639 | -14.9 LUFS | 2.3 LU | -3.9 dBTP | 0.0057 (0.203) | 0.0085 (0.285) | -22.7 dB |
| `Level_split-gallery.ogg` | 124 / C minor | 80 / 2:34.839 | -14.9 LUFS | 2.0 LU | -3.5 dBTP | 0.0088 (0.197) | 0.0049 (0.277) | -24.9 dB |
| `Level_terrace-steps.ogg` | 120 / E major | 72 / 2:24.000 | -14.9 LUFS | 2.5 LU | -4.0 dBTP | 0.0188 (0.178) | 0.0086 (0.246) | -23.7 dB |

How to read the measurements:

- Lengths are exact on the bar grid: 5,022,915, 6,246,295, 6,828,387 and 6,350,400
  samples, with zero offset.
- Seam step is the largest last-to-first sample jump. The second number in brackets
  is the same file's 99.9th-percentile sample-to-sample jump.
- Seam curvature is the second difference across the wrap.
- A click would exceed both percentiles. Every seam is 2-11% of them.
- Two consecutive builds produced byte-identical OGGs.

Limits:

- No one has listened to the tracks yet.
- The FM Rhodes, mallet and pad are code approximations, not sampled or DAW
  instruments.
- The three level tracks share one arrangement template (intro, A, B, short
  kick-less break, C, outro). They differ in key, tempo, instruments and grooves.
- Unity re-encodes on import, so the seam must be re-checked in Unity.
  `PresentationBuilder.ConfigureMusic` sets these clips to Streaming, Vorbis quality
  0.7, no preload and load in background.
