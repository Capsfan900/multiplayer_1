# Unity rendering — soft Wildstone Modern

Updated: 2026-10-27. C3.1's soft modern appearance is accepted. Native model import,
three-map CPU lighting and an offline Windows build pass; full moving visual and
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
| Laser sword | Solid/emissive blade core, bounded swing trail and local contact spark. The visual trail never defines damage. |
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

Start without the source's eclipse shafts, volumetric cloud sea, lens dirt and
chromatic aberration. Test gentle distance fog and modest bloom only where helpful;
soft lighting does not require blur. Optional SSAO must not be the sole contact
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
