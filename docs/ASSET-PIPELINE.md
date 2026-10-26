# Blender and Unity asset pipeline

Date: 2026-10-25. Status: proposed production procedure, grounded in read-only
source inspection. No models were generated, imported or tested here.
Return to the [documentation index](README.md). The
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md) owns graphics
strategy; [combat](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md)
owns gameplay; the [asset workflow](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
owns visual review.

Use Blender and Unity to build editable maps, humans and weapons. Audit
`C:\Users\tyler\Main Storage\vibegame1` first and selectively inspect sibling
`ai_skelly_tool`; leave both projects untouched. Multiplayer authority, prediction, reconciliation and replicated state
are new Fusion integration, not inherited single-player behavior. Foundation proof
precedes arena production.

## Tool and project preflight

Unity MCP tools are available in this session, but no `multiplayer_1` Editor instance
was verified. Before editing, enumerate instances, verify the project path, and pin
this project's own instance. Never reuse the source project's instance or another
checkout's identifier. This workspace currently has no imported Unity project.

`Get-Command blender` found no executable on PATH; no Blender MCP tool was observed.
The source scripts use `bpy` through sibling `ai_skelly_tool`; its
`.venv/Scripts/python.exe` and `stages/rig.py` exist, but execution and installed
versions were not verified. Check the actual executable/environment before use.
An available Blender CLI or compatible existing `bpy` environment is sufficient;
do not require a new plugin. Audit scripts' hard-coded source/output paths and
dependencies before adapting them into this workspace. Do not run them against
the original checkout.

## Verified reuse leads

Paths below are relative to the read-only `vibegame1` root. Existence and the named
code paths were inspected; compatibility, licensing and visual fit still need review.

| Existing source | Decision and reason |
| --- | --- |
| [Assets/Editor/LevelStudio/](../../vibegame1/Assets/Editor/LevelStudio/) and [LevelDefinitionBuilder.cs](../../vibegame1/Assets/Editor/LevelDefinitionBuilder.cs) | Adapt native authoring. `BuildWorldContent` uses an explicit root without opening/saving scenes or creating campaign managers; audit optional runtime behaviors before reuse. |
| [LevelPieceFactory.cs](../../vibegame1/Assets/Scripts/Level/LevelPieceFactory.cs), [LevelMesh.cs](../../vibegame1/Assets/Scripts/Level/LevelMesh.cs) | Reuse procedural structural geometry where faster. Structural boxes retain BoxColliders while rendering shared chamfered meshes. |
| [Tools/env_models/build_architecture.py](../../vibegame1/Tools/env_models/build_architecture.py), [LevelArchitectureBuilder.cs](../../vibegame1/Assets/Editor/LevelArchitectureBuilder.cs), [ArchitectureKitPlan.cs](../../vibegame1/Assets/Editor/ArchitectureKitPlan.cs) | Adapt modular sizing/export conventions; simplify the old detailed masonry to the accepted modernist forms. |
| [Tools/weapon_models/build_weapons.py](../../vibegame1/Tools/weapon_models/build_weapons.py), [build_laser_kit.py](../../vibegame1/Tools/weapon_models/build_laser_kit.py) | Adapt geometry, grip/emitter and export-roundtrip checks; replace ornate surface treatment. Audit sibling helper dependencies. |
| [output/laser-kit/LaserPistol.blend](../../vibegame1/output/laser-kit/LaserPistol.blend), [Assets/Art/Weapons/Modeled/](../../vibegame1/Assets/Art/Weapons/Modeled/), [Assets/Art/Viewmodels/](../../vibegame1/Assets/Art/Viewmodels/) | Editable source/export and hand-fit leads. Preserve useful geometry and anchors; they do not prove a suitable full human rig. |
| [WeaponViewmodel.cs](../../vibegame1/Assets/Scripts/Player/WeaponViewmodel.cs), [PrefabFactory.cs](../../vibegame1/Assets/Editor/PrefabFactory.cs) | Adapt local presentation/attachment setup after dependency tracing. Rewrite network ownership and combat integration: `LaserPistol` uses global `GameEvents`; `LaserShot` damages source enemies. |

Do not carry source global pause, hitstop/time-scale mutation or campaign `GameEvents`
into shared simulation. Preserve movement laws through the foundation's explicit
fixed-tick adaptation rather than treating asset reuse as multiplayer conversion.

### Selective ai_skelly_tool reuse

Read-only inspection found useful code, not executed proof:

- [stages/rig.py](../../ai_skelly_tool/stages/rig.py): humanoid armature/binding helpers
  and bind-frame-aware FBX export. Existing `vibegame1` weapon builders already
  import its helpers. The heuristic body fitter still needs comparison against
  the deliberately simple shared human; do not adopt it unquestioningly.
- [stages/weapon.py](../../ai_skelly_tool/stages/weapon.py): hand sockets, blade
  clearance and two-hand grip helpers. Reuse attachment/measurement ideas, not
  boss hit events as multiplayer damage authority.
- [tests/test_skin_stretch.py](../../ai_skelly_tool/tests/test_skin_stretch.py),
  [test_mesh_artifacts.py](../../ai_skelly_tool/tests/test_mesh_artifacts.py) and
  [test_anim_quality.py](../../ai_skelly_tool/tests/test_anim_quality.py): deformation
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

Exit with editable sources, reproducible exports, dependency/provenance records,
Unity import/rig/collision checks and moving readability evidence. Profile the
finished slice and busy eight-player build against the rendering/foundation gates.
A 2D concept, source asset or attractive Blender render proves neither mesh quality
in gameplay nor FPS, network reliability or collision correctness.
