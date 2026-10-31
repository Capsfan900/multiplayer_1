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

### Original laser kit, 2026-10-28 (historical first pass)

The surgical `laser-kit` mode in `Tools/Assets/build_assets.py` creates original
LaserPistol/LaserShotgun/LaserSniper .blend sources, FBX and reference renders in this
worktree only. It uses existing Blender 4.2 through the audited Python environment,
not source meshes/textures or GPU generation. New triangles: 984 / 1212 / 1300.
`ArtSource/laser-kit-export-manifest.json` records exact source/export hashes;
existing fourteen production source/export pairs remain unchanged. The Unity import
reflection whitelist now includes these three named exports. Regenerating maps or
approved earlier models is not needed for this weapon pass.

### Weapon kit and special-weapon art, 2026-10-29 (historical checkpoint)

The five-weapon `weapon-kit` manifest records the current finished kit; its source
and FBX files were read, not rebuilt during this specials pass. The surgical
`weapon-specials` mode replaces only LaserShotgun, PulseLobber, ReboundDisc,
PushLens, StunGun and LaserSword with original procedural geometry using the same
lofted-section, chamfered side-slab, lathe and bevel techniques. The read-only
technique sources are `vibegame1/Tools/weapon_models/build_laser_kit.py`
(SHA-256 `a39c590e41eb79334dcb3445a8c1536d5e29adb897148e901047aa2c39576648`)
and `build_weapons.py`
(`c7aad1c4daeb1f84d77b1b0ff42821688c74382d56bb1ece7a4d6ed8c6831c80`),
already audited for the kit at `2cb6e5760bccc0733f34b367be2f18c90b802766`.
No source meshes, textures, scanned materials, third-party assets or generated 3D
outputs were copied, and neither source checkout was modified. Only its existing
Python/bpy environment is used; no dependencies were added. Existing FBX files are
overwritten in place and their `.meta` files remain byte-identical.

`ArtSource/weapon-specials-export-manifest.json` records Blender 4.2.0, source
hashes, FBX hashes, reimported bounds, triangle counts, palette and socket checks.
Matching entries in the original export and laser-kit manifests are refreshed;
other entries are preserved. These are authored/Blender-verified assets, pending
Unity held-view, moving-part and distance-readability acceptance.

| Weapon | Reimported triangles | FBX SHA-256 |
| --- | ---: | --- |
| LaserShotgun | 5132 | `114b1093045d60c5ea895bcfe6f465cd7869fe5b55d2c4facc291d272dd6d36c` |
| PulseLobber | 4652 | `af23f00d670c393896ca50664c074edd93206914fbc649e30197464227c500bc` |
| ReboundDisc | 3656 | `98b4d1288d2fdb728668c22652381bbd3d30c4e04b9d20d5b0ee1dc233ef802e` |
| PushLens | 3936 | `0f322d1faf73a578fca075a3657bdf0c5db1d6f58896215c3c433cc95cf80dc6` |
| StunGun | 2888 | `d73083b550fc45470063fd35c800931d808445075a854d4d725e67ea3edb3d4d` |
| LaserSword | 3156 | `8fca830bd35fe5493af90a394a521d4d980af67b10b2dbbc66831512c37001fc` |

### Starter laser-kit visible-face art pass, 2026-10-30 (historical first pass)

LineDriver, LaserSMG, LaserPistol and LaserSniper now use stronger graphite/ivory
layering, broad top/right receiver plates, rail notches and exposed signal cells
or windows. Cyan identifies AR/pistol/sniper; coral identifies SMG. Geometry is
original procedural work in their existing `build_assets.py` branches, using the
already-audited kit helpers above; no source tools were run and no dependencies,
meshes or textures were imported. Grip, Muzzle, LeftHand and Sight match the
pre-pass FBXs exactly, with identity rotations; existing object names are retained.
Reimported triangles are 6342 / 5482 / 4416 / 7000 (AR/SMG/pistol/sniper).
`ArtSource/laser-kit-export-manifest.json` contains current source/FBX hashes and
measurements for all four; matching `export-manifest.json` entries are refreshed.
The older `weapon-kit-export-manifest.json` is historical for these four guns and
was outside this lane's write scope. Grenade, specials and all `.meta` files were
preserved. Rear-right-above Blender previews were inspected; packaged first-person
readability, ADS/hand IK and 30 m recognition still need Unity acceptance.

### Eleven-weapon energy art pass 2, 2026-10-30

All eleven weapon sources/FBXs now include larger exposed signal cells, emitter
windings, field/rim segments or live seams, plus restrained machined details.
This is original procedural geometry using the existing audited helpers; no
source-project script, generated/downloaded mesh, texture or new dependency was
used. The sword has a longer faceted core and a separate open four-edge glow
envelope on the same SignalCyan material, with a machined collar and grip ribs.
Grenade tessellation was reduced to keep its new amber seam/fuse halo under 3,000
triangles; other weapons stay under 9,000. All existing sockets and object names
survive. `ArtSource/weapon-energy-export-manifest.json` is the current source/FBX
hash and measurement record; matching entries in the older manifests are also
refreshed. Earlier counts/hash tables above are historical pass checkpoints.
Lit Eevee previews use local Signal node emission and compositor glare; Unity
continues to remap the existing material prefixes. Blender checks and renders do
not establish native Unity emission/bloom, animation, held-view or distance acceptance.

### Menu revamp, 2026-10-29

User request: make the menu like `vibegame1` with its animated main screen. Source
files were read at the pinned commit with clean status; SHA-256 below.

| Source (`vibegame1`, `2cb6e576`) | SHA-256 | Use in Wildstone |
| --- | --- | --- |
| `Assets/Scripts/UI/MenuCameraDrift.cs` | `82c72b843205fd5dc219e0ed27fefb6d33912c6ad1e37825eb81f63d7074f287` | Lissajous ratios 1/1.37/0.71 and phases ported to `MenuMotion.Drift`; amplitudes rescaled for a 8.5 m squad shot |
| `Assets/Scripts/UI/CloudDive.cs` | `770743f46d7769b2c7910681c9de7caccd92d77461e18a34b3af55a8facf5fe4` | Ease `1.3k²−0.3k³`, tan-space `Widen`, 0.15 s/-80 px column exit, still-mode fade; push-in replaces the cloud drop, ivory curtain replaces the white |
| `Assets/Editor/UiSprites.cs` (`RampH`) | `35b729a8128dd9f1d7fd96e967fc38915cded19038e55a2f4d257a66822429c8` | Eased alpha ramp, generated at runtime with an opaque column band (`MenuMotion.RampAlpha`) |
| `Assets/Editor/MainMenuBuilder.cs` | `a09ae8e93f5aaa81724059c4d0d1a11b0e670672ba89ee50dcfab3f3853cd9b4` | Layout language only: left-hung column on a ramp scrim, margin rule, offset title copy, slab rule with fading tail, inverting buttons. No prefab, scene, palette or neon treatment copied |
| `Assets/Fonts/BarlowCondensed-Black.ttf` (+ `.meta`) | `5943ef8b1b2f7ebb3515df6307d556d536a15cfa4f36783e9d1c2f1bf4058e35` | Copied byte-identical to `Assets/Wildstone/UI/Fonts/`; meta GUID `b67e91cbb606dfb4abc5b8bba2510543` (meta `b8ee433ce294a4ec077780632f13dcd72adf2340ca985b9f1f829d4690924916`) is unique here |
| `Assets/Fonts/OFL-Barlow.txt` (+ `.meta`) | `186d750eb496a4c17a76385f82be6aea2ac1cf2de074a811d63786cf374ea73f` | Shipped beside the font. Barlow, Copyright 2017 The Barlow Project Authors, SIL Open Font License 1.1: redistribution and embedding permitted with the license; the font is not sold alone |

The other three Barlow cuts, TMP font assets, sprites, prefabs, the source
`AudioManager` click sample and `MainMenuLookTests` were not copied; the menu click was
an original synthesized tick. The 2026-10-30 audio pass below replaced it with the
source `Click` bank and kept the tick as the fallback. The
[UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md) owns the resulting behavior.

### Quality pass (D028), 2026-10-29

Source files were read at the pinned `vibegame1` commit. Hashes are SHA-256 of the
source file; copied files are byte-identical in Wildstone.

| Source (`vibegame1`, `2cb6e576`) | SHA-256 | Use in Wildstone |
| --- | --- | --- |
| `Assets/Shaders/ArchitecturalStone.shader` | `35a690bf7498bc13300f86cf33c955aaccffe98154c0acfbcea6705115526b25` | Adapted as `Art/Shaders/ArchitecturalStone.shader`. Adds an emissive inlay and a Meta pass for baked bounce. Wildstone uses its procedural mode only (`_PhotoBlend` 0); the photo-scan sets were tried and dropped because they read as old brick against C3.1. |
| `Assets/Shaders/VfxAdditive.shader` | `c004e90c6d82d7b67ed804087ae2c3ee8cb94ff0861c71a0946dfae1433798c3` | Adapted as `Art/Shaders/Vfx.shader`. Adds premultiplied blending for occluding smoke and a mask-channel option. |
| `Tools/vfx/make_vfx_textures.py` | see `Art/VFX/CREDITS.md` | Ported to `Tools/VFX/make_vfx_textures.py`; outputs match the source PNGs pixel-for-pixel. |
| `Player/ViewmodelArm.cs`, `Player/HandPose.cs`, `Fx/SparkBurstFx`, `ImpactBlast`, `ShockwaveFx`, `SlideFx`, `PrefabFactory` overlay camera, `PC_Renderer` SSAO and the PC grade profile | not copied | Concepts and tuning values only. Two-bone arm IK with shoulder slide, per-digit grip curl presets, pooled emit-only bursts, the overlay camera arrangement, SSAO values and the grade's starting values were rewritten in `WeaponView`, `ArenaVfx`, `RenderingSetup` and `ArenaBuilder`. |
| `Feel/PlayerFeedback.cs`, `Feel/GameFeelSettings.cs`, `Feel/CameraFX.cs`, `Feel/DashFx.cs`, `Feel/DashImpulse.cs` (read 2026-10-30) | not copied | Concepts and tuning values only. The movement FOV kicks, wall-run hold, landing response and dash speed streaks were rewritten in `FirstPersonView` and `WeaponView`, with the kicks scaled to Wildstone's narrower lens. The landing eye dip became a view nod ([QA](QA.md#2026-10-30-movement-lens-and-dash-streaks)). |
| `Feel/SlideFx.cs`, `Feel/SlideImpulse.cs`, `Data/GameFeelSettings.cs` (scrape, read 2026-10-30) | not copied | The slide-scrape law and its noise recipe only: one-pole low pass (0.34) less a slow high pass (0.015), a 2048-sample seam crossfade, seed 1207, gain 0.22 × (0.4 + 0.6f), pitch 0.78–1.32 and a 0.12 s release. It was rewritten in `ArenaAudio` ([QA](QA.md#2026-10-30-hit-tick-and-slide-scrape)). |
| `Feel/CameraBeats.cs` (wind section, read 2026-10-30) | not copied | The speed-wind law only: one loop written every frame, volume 0.30 × smoothstep(14, 24 m/s), pitch 0.9–1.1. It was rewritten as `ArenaAudio.PresentWind` ([QA](QA.md#2026-10-30-speed-wind)). |
| `Feel/SlashFx.cs` (read 2026-10-31) | not copied | The layering idea only: a near-white hot core along the leading edge over a wider tinted fringe drawn with a crescent blade mask. Wildstone's Laser Sword smear extends the existing `ArenaEffects` burst pool and uses the in-repo `T_VfxBlade.png` (provenance in `Art/VFX/CREDITS.md`); no code, pooling or single-player state was copied. |

Not ported: the full-screen `Atmosphere` pass, eclipse shafts, cloud sea, lens dirt,
chromatic aberration, moss/grime presets and the photo surface sets. The EnemyForgeLab_vfx
flipbooks and their hashes are recorded in `Assets/Wildstone/Art/VFX/CREDITS.md`. The
soldier model, animation and music lanes record their own tool provenance in
[ASSET-PIPELINE](ASSET-PIPELINE.md).

### Recorded weapon and movement audio, 2026-10-30

The Wildstone cues were synthesized sine sweeps, and the user had already rejected
synthesized weapon audio in `vibegame1`. Fourteen recorded banks (70 clips, 1.5 MB)
were copied byte-identical from `vibegame1` `Assets/Resources/Audio/Sfx/` at `2cb6e576`
(clean; the folder last changed in `ddfaddb`, 2026-10-11) to
`Assets/Wildstone/Resources/Audio/Sfx/`. Folder and clip `.meta` GUIDs are preserved
and unique here; the only meta edit is `preloadAudioData: 1`. Every source is CC0 as
recorded in `vibegame1` `CREDITS.md` (SHA-256 `5089efd31fa30f91c0b05c1a87b005b16fc0def275bfaf08cb28baf9a510c0a4`),
which records each page's licence check; it is not re-verified here.

The bank hash is SHA-256 over the sorted `sha256sum` lines of the bank's clips, run
from the source `Sfx` folder.

| Bank (clips) | Bank hash | CC0 sources, per `vibegame1` `CREDITS.md` | Wildstone use |
| --- | --- | --- | --- |
| LaserFire (4) | `98844e62b4f5bd09` | mickeymephistopheles "Slinky Pew"; bmaczero Electricity SFX; rubberduck 80 CC0 RPG SFX and 100 CC0 SFX #2 | Every laser shot, pitched per weapon |
| LaserReload (1) | `448d25a69f918a0f` | satanicupsman flash charging; jackthemurray Glock slide; bmaczero; rubberduck; Vehicle Fantasy Accessory SFX | Reload start |
| Footstep (10), Land (5), Block (5) | `240a431c9d6e0b5c`, `c84574526a2726be`, `f1bc41df4100540a` | Kenney Impact Sounds | Steps, landings, body hits and ricochets |
| Jump (4) | `12e30b8337210979` | Kenney RPG Audio | Jump take-off |
| Dash (3) | `14a30cc73b9f770c` | artisticdude RPG Sound Pack | Dash, slide entry, blink, Push Lens |
| Swing (13) | `4502c51073358866` | qubodup Swishes Sound Pack | Sword, disc and grenade throw |
| Hit (10) | `34049e5cf8fa7ab8` | StarNinjas 20 Sword Sound Effects | Sword hits |
| Stun (3) | `5ece0e64e309da2a` | bmaczero Electricity SFX | Stun Gun shot and stun |
| SpellFireImpact (3) | `1c8078c3dc76614c` | LEGIT Audio Fire Staff SFX; rubberduck 100 CC0 SFX #2 | Grenade and Pulse Lobber blasts |
| DeflectorOpen (2), DeflectorParry (2) | `db2d1db172dd5570`, `2e8458def7f0d752` | mickeymephistopheles; bmaczero; rubberduck | Shield up/break, pickup, derez |
| Click (5) | `d99c7809684e6833` | Kenney UI Audio | Menu clicks |
| TurretAnswer (3) | `48f09b23fa4bd4f2` | mickeymephistopheles "Slinky Pew"; rubberduck 80 CC0 RPG SFX and 100 CC0 SFX #2 | Pistol and Laser SMG shots (added later on 2026-10-30) |
| SpeedWind (1) | `c54d36b082ed76f5` | Vital_Sounds "high wind 1" (Freesound 559095, CC0) | Local speed wind (added later on 2026-10-30) |
| LaserCellNova (2) | `2f2b7ed8d7ea1c62` | rubberduck 80 CC0 RPG SFX and 100 CC0 SFX #2; bmaczero; Stephan "Gong or bell vibrant" (PDSounds, CC0 1.0) | Sniper and shotgun boom tail (added later on 2026-10-30) |

The last three banks were copied the same way from the same clean `2cb6e57`. Their
six clip GUIDs and three folder GUIDs were unused here.

Not copied: the source `AudioManager`, `ProceduralSfx`, its cue routing and every
other bank. `ArenaAudio` maps combat events and movement edges to these banks itself.
The heavy weapons and blasts keep a quiet synthesized body layer under the recording,
and every cue falls back to its synthesized clip if a bank is missing. The
[character and weapon spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
owns the behavior.
