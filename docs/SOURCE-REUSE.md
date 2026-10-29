# Source reuse boundary

Source project: `C:\Users\tyler\Main Storage\vibegame1`, read-only at commit
`2cb6e5760bccc0733f34b367be2f18c90b802766` during the 2026-10-25 audit.
The secondary tooling checkout is `C:\Users\tyler\Main Storage\ai_skelly_tool`.
Source-qualified paths in the asset/rendering audits resolve against these roots,
not relative to a Git worktree. Both source checkouts remain read-only.
Unity version and relevant package versions were checked against its project files;
its manifest was not imported. This project owns its Unity settings and asset GUIDs.
Source Library files are read only as local compiler references, never copied into
the destination or used as a distributable dependency.

## Actual reuse in the authored MVP

- `Assets/Scripts/Player/FirstPersonMotor.cs` and related traversal/stamina laws
  became explicit fixed-tick modules under `Runtime/Movement`. `MotorTuning` uses
  the shipped `Assets/Prefabs/Player.prefab` values at the pinned commit, covered
  by the source-parity fixture. Pure air, forgiveness, perfect-window, traversal
  and wall-run helpers retain their source logic; scene/global input/campaign
  managers were not transplanted. Native source-vs-adaptation trajectories remain
  unverified, so matching constants is not a movement-parity claim.
- `Assets/Scripts/Level/LevelMesh.cs` is reused under `Runtime/Maps/Source` with
  namespace and provenance changes. The destination builder persists meshes and
  generates UV2 before a separate lighting bake; source UV0-only geometry did not
  establish baked-lighting correctness. Simple box colliders remain independent
  of the 44-triangle chamfered render meshes.
- Weapon/rig/export source tools were inspected as leads. The production geometry
  in `Tools/Assets/build_assets.py` is original, driven by the approved local art
  references. No third-party model/texture/audio or generated source output was
  imported. The existing `ai_skelly_tool` Python/bpy environment is invoked in an
  isolated subprocess with outputs contained in this destination; its GPU stack
  and mutable output folders are unused.
- First-person body/slide continuation (2026-10-28): inspected source `PlayerBody`,
  `ViewmodelArm`, `ViewmodelMotion`, `MovementPose`, `PlayerFeedback`/`SlideFx` and
  shipped prefab tuning. Adapted yaw-only local legs, asymmetric slide posing,
  closed-form body spring and hand/sleeve attachment concepts to existing original
  Wildstone human/hand meshes. Source clocks, global managers and assets were not
  copied. Source slide movement constants already match `MotorTuning`; cosmetic
  feedback preserves authoritative eye and aim. The character contract owns the
  requested behavior; native/visual checks are recorded separately in QA.
- Photon authority, sessions, combat rules and prediction integration are separate
  work. Host-only managed rules now exist; no source single-player manager is used
  as a multiplayer foundation, and no real Fusion adapter has been completed.

| Source candidate | Decision before reuse |
| --- | --- |
| `Assets/Scripts/Player/FirstPersonMotor.cs`, stamina and traversal state | Preserve movement laws/tuning only after dependency audit; convert all future simulation inputs and timers to restorable fixed-tick state. Do not copy frame/global input execution. |
| `Assets/Editor/LevelStudio/`, `LevelDefinitionBuilder.cs`, `LevelPieceFactory.cs`, `LevelMesh.cs` | Selectively adapt level authoring and simple collision geometry after checking prefab/material/scene dependencies and GUID closure. Source UV2/lightmap correctness remains unproven. |
| Weapon/viewmodel builders and source `.blend`/FBX | Geometry and socket leads only; verify each asset's provenance/license and Blender-to-Unity axis/scale roundtrip before import. Do not copy campaign damage events. |
| Source settings, packages and player prefabs | Do not copy wholesale. This project owns its product/save identity, URP assets, scenes, layer policy and networking authority. |

The source repository has no top-level license file. This is the user's source
checkout, but redistribution rights for any third-party model, texture, shader,
sample or plugin still need per-asset review before a public build. Copy selected
Unity assets with their `.meta` files and required dependency closure only after
that review. The independent `ai_skelly_tool` rig/export helpers remain audit leads,
not runtime or generation dependencies.

The candidate table preserves the audit decisions; the actual-reuse list above
records what has been adapted. [QA](QA.md) distinguishes managed compilation and
Blender checks from pending Unity import/native behavior and online compatibility.
The [asset pipeline](ASSET-PIPELINE.md) owns production import and validation steps.
