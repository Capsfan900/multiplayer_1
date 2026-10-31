# Unity rendering — soft Wildstone Modern

Updated: 2026-10-29. C3.1's soft modern appearance is accepted. [D028](../../DECISIONS.md)
raises it to `vibegame1` fidelity; section 6 records that stack, which is authored
but not yet rebuilt, baked, captured or profiled. Native model import,
three-map CPU lighting and an offline Windows build passed for the pre-D028 slice; full moving visual and
performance acceptance remain open. This strategy is grounded
in the read-only source audit and still needs a finished playable slice, moving
visual review and profiling. See [current evidence](../../MVP-PROGRESS.md).

## 1. Ownership and intended result

This is the **canonical owner of rendering strategy**: materials, lighting,
graphics tiers, effects presentation and graphics validation. The
[foundation](2026-10-25-multiplayer-foundation-design.md) owns implementation order,
authority and performance budgets; the [map brief](2026-10-25-wildstone-modern-map-design.md)
owns architecture and routes; [combat](2026-10-25-arena-combat-items-zone-design.md)
owns attack, status and zone behavior. The
[character/weapon workflow](2026-10-25-characters-weapons-design-workflow.md) owns
production sheets and asset review. Change those contracts in their owner.

Keep Wildstone's terraces, interrupted walls and bridges, expressed as broad,
mostly monochrome matte forms. Seek soft light, gentle value transitions and clear
contact between surfaces. [VHOLUME](https://store.steampowered.com/app/4131730/VHOLUME/)
is a visual reference for atmosphere and architectural restraint; its appearance
does not establish how its renderer works. Improving on `vibegame1` means a more
coherent, readable result judged in motion, not automatically more rendering features.

## 2. Verified reuse and material baseline

The source uses `vibegame1/ProjectSettings/ProjectVersion.txt`
and `vibegame1/Packages/manifest.json#L13`. Retain them subject
to foundation compatibility checks. Source-qualified paths below use the original
read-only roots in [SOURCE-REUSE](../../SOURCE-REUSE.md); imports and adaptations belong here.

| Existing implementation | Reuse decision and limit |
| --- | --- |
| `vibegame1/Assets/Scripts/Level/LevelMesh.cs#L36` builds small geometric chamfers; `:45` defines vertex AO and `:81` caches shared meshes. | Reuse the geometry/cache approach. Soft edge highlights and controlled face values are useful inexpensive cues. Vertex AO is authored shading, not scene-wide baked occlusion. |
| `vibegame1/Assets/Shaders/ArchitecturalStone.shader#L617` samples lightmaps/SH and shadowmask, then adds a material side-light term at `:622`. | Baked-light reception exists. Avoid adding the old fake ambient over already sufficient baked bounce. No Meta pass is present; bake contribution requires verification. |
| `vibegame1/Assets/Scripts/Level/LevelMesh.cs#L166` writes UV0, normals, tangents and colors, without a lightmap UV channel. | Generate and validate UV2 for lightmapped kit meshes. Static flags alone cannot supply a sound bake. |
| `vibegame1/Assets/Materials/M_ArchitecturalStone.mat#L75` retains grime, moss and photo surfaces; `vibegame1/Assets/Shaders/ArchitecturalStone.shader#L319` samples repeated albedo/normal/mask sets. | Do not inherit the detailed stone preset. Disable photo detail, moss, grime, tiny joints and strong relief for the modernist comparison. |
| `vibegame1/Assets/Shaders/CharacterBlock.shader#L115` samples per-vertex SH; `:138` uses a shadowed main light and tunable tone transition. | Try a softer tone transition, restrained rim and quiet matte human surfaces. It has no additional-light loop; weapon lights will not behave like URP/Lit. |

Compare two material paths on identical geometry and lighting: **stock URP Lit
with simple matte materials**, and a stripped Architectural Stone variant retaining
only useful face values, vertex shading and bevel treatment. Start with the stock
baseline; retain custom code only for a visible benefit or measured cost advantage.
URP Lit does not automatically consume the kit's vertex AO. If it wins, baked
contact shading may make that extra shader feature unnecessary.

Use a small shared palette of warm ivory, pale gray and charcoal. Prefer broad
roughness/value differences to noise textures. Avoid shiny photoreal surfaces,
dense paneling and decorative outline passes. Mesh bevels, sparse recessed seams,
simple background silhouettes and deliberately placed light/shadow can supply
depth without expensive surface detail.

## 3. Lighting and authoring contract

Propose **baked static bounce plus one practical realtime shadowed directional
key**, with classic diffuse light probes for moving humans and weapons. Choose the
matching Mixed-lighting configuration during the slice; confirm that static direct
light is not counted twice and moving objects receive the intended shadows.
Unity's [Mixed-lighting guidance](https://docs.unity.com/en-us/engine/6000.5/manual/lighting-overview/lighting-light-sources/components/configuring/lighting-mode/lighting-mode)
and [light-probe guidance](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/direct-and-indirect-lighting/light-probes/light-probes)
describe these separate responsibilities.

Validate UV2 overlap/padding, lightmap seams, material albedo/emission contribution,
probe interpolation and shadow behavior in bright courts, covered galleries and
ramps. Supply a correct Meta path if the custom shader requires it. Reduce its
side ambient, foot darkening and vertex AO where they double-darken baked contacts.
Keep shadowed humans readable without flattening the architecture.

The source's `vibegame1/Assets/Editor/AtmosphereBuilder.cs#L257`
creates realtime reflection probes refreshed OnAwake with all faces together.
These are not diffuse light probes. Prefer precomputed reflection captures for
the static arena; validate startup cost and moving-weapon appearance. The builder
also rewrites global lighting/fog at `:170`, so import its useful structure without
silently restoring the source's dark blue scene preset.

Preserve level-making through two explicit states:

- **Editable preview:** existing level data/tools rebuild geometry quickly using
  a provisional ambient/key setup. Geometry edits invalidate associated baked
  lighting. Preview remains useful and clearly labelled; stale lightmaps are not
  presented as final illumination.
- **Distributable authored map:** freeze a versioned geometry snapshot, generate
  UV2, author probes, bake lighting/reflections offline, validate, then package
  matching geometry and lighting dependencies. Every peer loads that map version
  under the foundation's content checks. A geometry revision returns to preview
  and rebake. No live baking during a match.

This retains authoring capability without implying that arbitrary runtime geometry
automatically acquires finished baked lighting. Avoid paid assets or new lighting
frameworks before the existing Unity path is tested.

## 4. Weapons, effects and presentation state

All four accepted concepts retain their recognizable mechanisms: **Line Driver's
split rails, Pulse Lobber's orb chamber, Rebound Disc's broad disc and Push Lens's
open ring**. Simplify their surfaces to match the game: broad matte housings, soft
bevels, fewer panels/tiny seams, shared materials, restrained emission and minimal
transparency. Their distinct construction and silhouettes can remain expressive
without detailed shiny machinery. Review first-person, opponent-held and pickup
views; keep the aim point and nearby opponents visible.

Effects consume approved gameplay state and action IDs. They do not own damage,
timers, hit volumes or zone occupancy. Predicted effects reconcile without repeats;
late join/migration reconstructs current persistent cues without replaying old
bursts. Proposed visual treatment:

**Current implementation (2026-10-27; full visual/performance acceptance open):**
`ArenaPresentation` binds to the offline Practice session, reads committed
`ArenaMatch` events and state, suppresses repeated event IDs, and renders a bounded
viewmodel overlay, team human/weapon models, projectile cores/trails, pickup models,
shield bands, a moving depth-tested zone boundary and short event cues. A builder
references the editable FBX exports through prefabs and maps their matte materials.
The effects and generated audio have fixed pools and no colliders or gameplay writes.
Old bursts are not replayed after a presentation restart; persistent zone, shield,
pickup and projectile cues are reconstructed from current match state. The managed
typecheck and pure dedup test pass. The reviewed implementation aligns authored
grips to hand sockets, follows current aim/death and persistent fuse/stun/immunity
state, uses numeric identities with conservative eviction, and synchronizes
overlay projection on settings changes. Native import, rig clip/attachment,
camera-stack lifecycle and effects tests now pass; [QA](../../QA.md) records the
commands and limits. Grayscale recognition, complete first-person framing,
accessibility and graphics cost still require visual/profile acceptance. This is an offline rendering slice, not
acceptance of the proposed visual or performance targets below.

| Event | Small readable treatment |
| --- | --- |
| Shots, orbs and discs | Opaque projectile/core where applicable, a few depth-tested trail or flipbook meshes, brief local impact cue; recognizable direction and ownership. |
| Grenade | Visible solid body, readable fuse/threat cue, short localized blast and aftermath; no opaque full-screen wash. |
| Stun | Distinct bolt/impact and restrained body/HUD status cue; keep facing, team and surroundings readable. |
| Laser sword | Solid/emissive blade core, bounded swing trail and local contact spark. The visual trail never defines damage. Implemented (2026-10-30, proposed widths): the swing draws a 130° arc at 2.1 m (glow, .3 m; .12 m with reduced motion) and 1.7 m (bright core, .06 m), both crescents that are full width mid-swing and pointed at the tips. The earlier constant .14/.035 m arcs read as two thin wires across the view in `weapon-lasersword-0/1`. True-age firefight frames (2026-10-31) still read as a thin streak, because the beam material's mask is a narrow core. Revised 2026-10-31 (implemented and captured; proposed values): a .38 m fringe in the beam tint drawn with the `T_VfxBlade` mask (`VfxBlade`), plus a .04 m near-white core along its forward edge. Both sit 1 m from the eye across 130°, with heights scaled from the 2.1 m design so the angles match. The fringe is drawn front to back, so the mask's bright head leads the swing and its tail trails. At 2.1 m, and still at 1.25 m, a struck target inside the 2.3 m reach hid the widest part of the slash. Both reveal left to right over 55 ms, hold to 70 ms and fade out by 200 ms. They come from the existing burst pool, with no per-swing allocation and no light. Reduced motion draws one static .06 m ribbon across 80°. A catalog without `VfxBlade` keeps the 2026-10-30 arcs. |
| Short blink | Compact departure/arrival cues following the accepted wall-blocked teleport; no prolonged screen distortion. |
| Temporary overshield | Sparse surface bands or silhouette accents plus HUD state; distinct break/expiry, no giant transparent bubble. |
| Moving scoring zone | Thin depth-tested boundary, clear accessible footprint and capture/contest cues; no filled translucent cylinder hiding the fight. |

Do not give every projectile a point light. Add a brief local light only when it
improves recognition and survives the load test. Team identity and imminent danger
need different cues; color alone is insufficient. Visibility rules still apply to
markers. Quality changes may simplify particles, shadows and secondary trails,
but cannot remove team, fuse, stun, shield, attack or zone cues or alter hit volumes.

Reuse the source's `vibegame1/Assets/Editor/PrefabFactory.cs#L1247`:
one local overlay camera, restricted layer and post-processing once. Remote players
use world models. Verify renderer features do not run unwanted full-screen effects
on the overlay; the source already has a
`vibegame1/Assets/Shaders/Atmosphere.shader#L222`.

## 5. Cost controls and acceptance

*Historical (pre-D028):* start without `vibegame1` features. D028 replaced that
rule; section 6 lists what is now adopted. Eclipse shafts, the volumetric cloud sea,
lens dirt and chromatic aberration stay out. Soft lighting does not require blur. Optional SSAO must not be the sole contact
cue. Preserve contrast on Low. Keep opaque surfaces dominant, effect lifetimes
bounded and shared materials stable; inspect actual batching rather than assuming
shader sharing guarantees it. The source
`vibegame1/Assets/Settings/PC_RPAsset.asset#L72` enables SRP
Batcher, but runtime property overrides still require inspection. Follow Unity's
[URP performance guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/configure-for-better-performance.html)
to disable unused work, then measure the selected configuration.

Collect the new game's actually drawn shader variants with the adapted
`vibegame1/Assets/Editor/ShaderWarmupBuilder.cs#L12`
and `vibegame1/Assets/Scripts/Core/ShaderPrewarm.cs#L33`.
Do not copy the source collection as coverage proof. Include all supported effects
and graphics tiers; measure cold startup and first-use behavior separately.

Acceptance proceeds after the applicable foundation gates:

1. **Finished slice:** one representative courtyard/gallery/ramp with human and
   weapon assets. Deliver matched static snapshots and moving first-person clips,
   including light/shade transitions and the material comparison. User review
   establishes whether it improves on the source while retaining C3.1 softness.
2. **Rendering correctness:** prove bake/UV/probe consistency, overlay isolation,
   effect reconciliation and unchanged gameplay at each quality tier. Test four
   teams, grayscale/color-accessibility views, overlapping threats and near/far
   recognition; record misses and revise.
3. **Busy-match cost:** meet the foundation's existing CPU/GPU/network budgets on
   agreed hardware with eight players total: one rendering host and seven remote
   clients. Profile the full supported roster, consumables, moving zone and
   simultaneous effects. The foundation's 32-projectile fixture is only a baseline,
   not coverage of the expanded game. Define and record the supported worst load.
4. **Build evidence:** retain build/content identity, settings, raw timings, memory,
   first-use stalls and direct/relay conditions. Repeat problematic moving scenes
   in the packaged Windows build before expanding art production.

The existing `vibegame1/Assets/Editor/RenderCostProbe.cs#L97`
forces GPU completion with `ReadPixels`; use it only to isolate costs, not as a
normal-frame acceptance benchmark. Its feature restoration also needs correction
before Low-tier use. No source measurement, attractive concept or empty-map frame
rate proves this multiplayer design's performance.

## 6. D028 quality stack

**Status (2026-10-30):** rebuilt, baked, captured in packaged players and
frame-time sampled (latest: [QA](../../QA.md#2026-10-30-maps-weapons-and-fire-feedback)).
Every value below is still a proposal for the user to judge in play, not accepted
appearance. The [character/weapon workflow](2026-10-25-characters-weapons-design-workflow.md)
owns soldier and weapon art; [combat](2026-10-25-arena-combat-items-zone-design.md) owns sight
and zone behavior.

| Area | Implementation (owner file) |
| --- | --- |
| Tiers | `Editor/RenderingSetup.cs`. Quality levels 0–2 use `WildstonePipelineLow`, 3+ use `WildstonePipeline`. The local-settings default is level 3, High (UI spec, settings version 3). High: 4 soft cascades (.05/.15/.35), 2048 shadow map, 60 m distance, Medium soft shadows, SSAO at `vibegame1` PC values (.45 intensity, .35 radius, half resolution, blue noise). Low: 2 cascades, 1024 map, 40 m, render scale .77 with FSR, no SSAO. Both use Forward+, depth texture, HDR grading, box-projected blended reflection probes, no MSAA. Renderer index 1 serves the viewmodel overlay. |
| Anti-aliasing | SMAA High on the main camera (`BootstrapBuilder`). TAA is excluded because it ghosts fast beams. Far clip is 1500 m for the skyline. |
| Grade | `ArenaBuilder.AddLighting` creates a profile per map once; authored edits survive rebuilds (delete `<map>-Grade.asset` to regenerate). D034 values: Neutral tonemapping, post exposure .6, contrast 8, saturation 0, neutral gamma with gain +.05, slightly cool shadows, neutral highlights and white balance, bloom threshold 1.05 and intensity .6, vignette .15, thin film grain .04. |
| Materials | `Art/Shaders/ArchitecturalStone.shader`, an audited `vibegame1` port, replaces URP Lit on the three stone tones. D031: procedural mode only (`_PhotoBlend` 0). The CC0 photo scans tried first read as old brick against C3.1 and were removed, as section 2 already advised. D034 retunes it to the `visual-polish` look: 4 × 4 m deck slabs and wall panels, 10 mm joints, real bevels with a light sheen, near-zero grain (.02) and wear, and the `visual-polish` ivory (.82, .80, .75) and ash (.63, .65, .64) values. Charcoal walls are lifted to (.29, .31, .32). Only charcoal keeps an inlay, a faint neutral white at .45 replacing the amber one. |
| Daylight (D034) | The `visual-polish` daylight on the existing stack. Key: (1, .97, .92) at 1.35. Trilight ambient: sky (.62, .68, .74), equator (.74, .76, .75), ground (.49, .49, .46). Linear fog (.79, .81, .80) from 65 to 230 m. Sky: zenith (.40, .53, .66), ground (.49, .49, .46), cloud cover .3. The `visual-polish` Quiet Sky and URP Lit tile textures were not ported: the existing Day Sky and stone shader reach the same look and keep bevels and baked AO. Each baked ceiling bounce light gets a visible source: unlit HDR strips (1.75–1.85, URP Unlit `CeilingStrip`), 0.22 m wide across 70 % of the overhang's short side. They are spaced about every 3.5 m along 85 % of its long side, with at least two. They have no collider, shadows or GI, and are static-batched. |
| Sky, backdrop and vista | `DaySky.shader` (gradient, sun disc and halo, horizon matched to fog) replaces the default skybox. `Editor/ArenaBackdrop.cs` (from the `visual-polish` branch) adds near, unreachable architecture outside each arena: a colonnade gallery, stepped masses, a pavilion and, on Atrium, an interrupted arch. It uses the stone materials, casts no shadows, has no colliders and bakes at .15 lightmap scale. Beyond it, a seeded ring of unlit `Vista` towers adds far silhouettes without shadows, colliders or bake cost. The full-screen `Atmosphere` pass was **not** ported: fog plus the vista carry depth more cheaply. |
| Bake | `ArenaLighting`: Progressive GPU (CPU fallback), 16 texels/unit, 2048 maximum atlas, baked AO (1.2 m), four blended box-projected baked reflection probes per map. Flat overhangs at least 6 m wide and 2.4 m up get a baked rectangle bounce light (.85) so players under cover stay readable. Geometry changes still invalidate the bake (section 3). |
| Effects | `ArenaVfx` pools emit-only particle systems (sparks, glow, smoke, fire, dust, debris, motes, derez pixels) using `Vfx.shader`, a port of `vibegame1` `VfxAdditive`. It also has 4 shadowless point lights (range ≤ 8 m, intensity ≤ 6, ≤ .25 s; the muzzle lamp uses 1.8 and 2.5 m for sniper, shotgun and Pulse Lobber and 1.1 and 2 m otherwise, lowered on 2026-10-30 from 3 and 4 m and 1.8 and 3 m because it left pale tinted blobs on the ivory floor, not yet re-captured), 12 shockwaves and 12 blast scorch marks lasting 14 s. A separate ring of 24 laser burns (6 s) marks cover where a beam stopped, projected along the surface normal, with a .4 s cooling ember. Burns never evict blast scorches, and nothing is drawn on players. Textures are regenerated in-repo by `Tools/VFX/make_vfx_textures.py`. |
| Shot reads (2026-10-30) | Hitscan beams taper their line width with distance from the main camera (scale = distance / 7, clamped .35–6, so the width holds out to 42 m; it was capped at 2.5, or 17.5 m, before D034), so a beam keeps a roughly constant on-screen width: slim by the lens and still readable far away. D034 also gives the sniper the heaviest rail: glow .26 m for .3 s and core .075 m lingering .4 s (proposed). The earlier values were .065/.24 and .018/.28, then .09/.03, which was still thinner than the Line Driver's .11 m glow in the [firefight captures](../../QA.md#2026-10-30-firefight-frame-sequences), then .15/.045. At .15 the shooter's own rail still read as a hairline: the beam texture (`T_VfxStrip`) is above half brightness across only the middle 22% of its width, so past 2.45 m the visible core was about 2.5 px at 720p and the sheath darkened only the faint tails. The pistol, the heaviest single shot after the sniper, has a .075 m glow and a .022 m core for .13 s, still under the Line Driver's glow. Its earlier .045 m glow, .014 m core and .11 s nearly vanished on ivory stone ([QA](../../QA.md#2026-10-30-pistol-beam)). Transient effects (beams, rings, particles, derez) read the per-frame unscaled clock (`Time.unscaledTime`), so every effect in one frame sees the same time. Muzzle rings are removed, because their hollow circles cluttered the view. Only the sniper keeps an end ring. A beam that stops on cover before its maximum range adds a flash and a spark burst at that point (every second shotgun pellet). In first person, `WeaponView` draws a core tongue plus a six-point star flare that blooms and shrinks during the shot, rotates between shots and is sized per weapon. The Push Lens charge keeps its ring and arcs. All of this is presentation only and stays inside the existing burst and particle pools. |
| Beam start and daylight contrast (2026-10-30) | World effects that start at the first-person muzzle use the point the viewmodel camera shows: `WeaponView.TryGetMuzzle` keeps the socket's screen position and depth under the wider world projection. Before this, a beam started short of the barrel, toward the screen centre (about 17 % of the barrel's offset at the default FOV). Every laser beam and shotgun pellet also draws a dark premultiplied sheath (colour 0, .05, .12 at alpha .5, 1.8× the glow width; .09 m per pellet), queued just before the additive glow. Additive cyan alone washed out to white on ivory stone and sky. The sheath is a proposed value pending a human look in motion. |
| Decals | URP's Decal renderer feature is **not** used. `ProjectedDecal.shader` projects the zone floor, scorches and burns from the depth texture, so they conform to steps on both tiers. It paints only surfaces facing the box's own up axis, so a box turned onto a wall marks the wall. |
| Hot zone | A projected hex floor with a soft rim and travel chevrons, a boundary curtain fading with height, and a far-visible beacon. Contested pulses are suppressed by reduced motion. Gameplay occupancy is unchanged. Since 2026-10-31 ([playtest item 12](../../PLAYTEST-FIX-LATER.md#12-hot-zone-blends-into-the-map); implemented and captured, not human-accepted): the colour follows the state (`ArenaEffects.ZoneColor`). A free zone is saturated magenta (.95, .14, .80), away from the team hues, danger orange, health mint and stone grey. A held zone takes the captor's team tint; a contested one is warm orange. Hue is painted over a dark band, not added: the decal has a 0.5 m dark track outside a 0.85-alpha rim with a 20–30% fill. The curtain and a 1 m beacon are premultiplied over darker, wider copies. Proposed values: curtain alpha 0.55 when not contested (`ArenaEffects.ZoneCurtainAlpha`), flashing 0.25–0.65 while contested (held at 0.45 under reduced motion), over a 0.35-alpha shade. The more opaque curtain tinted a target standing behind it, so the resting alpha stays at or below 0.6. The curtain fades out within 2 m of the camera, so standing in it never tints the screen. In captures the free zone reads as a magenta floor and curtain against ivory stone; a pickup seen through the curtain takes its tint ([QA](../../QA.md#2026-10-31-playtest-fixes-d040)). The HUD waypoint is in the UI spec. |
| Pickup pads (2026-10-30) | Each pickup gets a floor pad from `ArenaEffects` under its floating item: a dark sheath track (.3 m wide, 0.6 m radius) and a ring painted in the item's hue. The ring is premultiplied, not additive, because additive glow washed out to white on ivory. While the item is ready, the ring is a full loop that breathes gently (steady with reduced motion). After a pickup, it becomes a dim arc that refills clockwise as the [combat-owned](2026-10-25-arena-combat-items-zone-design.md) respawn runs down. This is presentation only, read from `PickupState.AvailableTick`. A soft light shaft and flat floor-aligned rings were tried and dropped: the shaft read as a hazy glass box, and flat rings faded at grazing angles ([QA](../../QA.md#2026-10-30-pickup-pads)). Health packs (D039, 2026-10-31) draw their model at 1.8× (`ArenaEffects.HealthPickupScale`) on a .2 m ring (`HealthRingWidth`; others .11 m): at 10 m the 0.29 m canister was a few pixels wide and its ring a thin line. Widths and scale are proposed values. |
| Status reads (2026-10-30) | A shielded player gets an energy shell from `ArenaEffects`: the built-in sphere stretched to 1.15 m across and the authoritative capsule height plus .25 m, so it fits the body and crouches with a slide. `Wildstone/ShieldShell` is additive and shows only a fresnel rim (power 2.6) with faint bands climbing on the C#-owned `_Clock`, so the body inside stays clear. That keeps it a silhouette accent, not the giant bubble the overshield row rules out. Its strength runs from .45 to 1 with the remaining shield. Back faces are culled, so a shielded player's own camera sees nothing. A stunned player gets a jagged 16-point halo .36 m around the head, painted solid green (premultiplied, .055 m wide) and re-struck at 20 Hz (still with reduced motion). Before this, the shield was a thin ring that captures could not find, and the stun halo was a thin "W" ([QA](../../QA.md#2026-10-30-shield-shell-and-stun-halo)). When the shield breaks, the shell flares to strength 2 and swells 35% as it fades over 0.22 s, alongside the existing break ring, sparks and flash; reduced motion skips the swell. The 12-point shield ring remains only as a fallback without the shell material. Sizes, strengths, the pop and the strike rate are proposed values. |
| Soldier outline (2026-10-30) | Remote soldiers' renderers sit on the `Enemy` layer (`PlayerVisual.OutlineLayer`, 7). On both tiers a URP Render Objects pass ("Soldier outline", after opaques) redraws that layer with `SoldierOutline.mat`. That material is an inverted hull in near-black (.05, .055, .065), grown 2 px at 1080p in clip space and pushed 6 cm back from the camera, so overlapping armour plates do not outline each other. It takes scene fog. The ivory armour otherwise vanished against ivory stone at combat range, and the hit flash turned the body into a white blob with no edge ([QA](../../QA.md#2026-10-30-soldier-outline)). The dissolving body leaves the layer, and respawn restores it. The menu squad (also `PlayerVisual`) is outlined too. The local first-person body and all weapons are not. This is a readability outline that captures showed a benefit for, as the [map spec](2026-10-25-wildstone-modern-map-design.md) allows, not a decorative pass. Width and colour are proposed values. |
| Derez | `Derez.shader` dissolves 4.5 cm world-space voxels top-down, with a team-colour pixel edge (D030). `PlayerVisual` runs it for .7 s after a .18 s delay and emits ≤ 280 voxel particles (90 with reduced motion). The body is hidden at the end and restored on the next life. A disconnected elimination hides immediately. Authoritative death, respawn and hit volumes are untouched. |
| Viewmodel | Overlay camera with its own field of view (combat/UI specs own the settings), no shadow casting from viewmodel renderers, and post-processing applied once across the stack. Holo and red-dot reticles use `Reticle.shader`. |

**Remaining acceptance for section 6:**

1. Rebuild all generated content, then rebake.
2. Frame Debugger check that SSAO and the grade are not applied twice to the overlay.
3. Matched before/after captures against `Artifacts/Visual/20261028-*`.
4. Grayscale team and threat review, including the derez and the zone.
5. High and Low frame time with eight soldiers and saturated effects against the foundation budget.
6. Shader-variant warm-up for the new shaders.
