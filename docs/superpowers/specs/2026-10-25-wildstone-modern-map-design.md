# Wildstone Modern — map direction

Date: 2026-10-25. Status: the user accepted **C3.1 / Wildstone Modern** as the map
visual direction and its simple human character direction. On resuming, the user
requested three complete maps and delegated implementation. Layouts below are builder
starting designs, subject to traversal/readability/route validation; no runtime or
performance result is inferred from this document.

## Selected direction

Keep C3 / Wildstone Ruins' layered terraces, bridges, interrupted monumental walls
and central courtyard. The user wants a sleeker, mostly monochrome modernist design.
Use C / Ink & Concrete's graphic treatment with simpler surfaces and restrained
contours, softened toward VHOLUME's lighting reference. Prefer broad gentle light
transitions and matte forms to harsh comic bands or glossy detail. The environment
should look intentional and finished at gameplay scale. The
[Unity rendering spec](2026-10-25-unity-rendering-design.md) owns the technical strategy.

![C3.1 — Wildstone Modern](../../art-direction/C3-1-wildstone-modern.png)

This built-in ImageGen concept establishes the accepted atmosphere, architectural
language and simple human direction, not collision geometry or final character
assets. The mechanical weapon remains provisional context. Production validation
and weapon exploration follow the separate
[character and weapon workflow](2026-10-25-characters-weapons-design-workflow.md).
All four [standalone weapon concepts](../../art-direction/weapon-concepts.md) are
accepted and have simpler visual refinements. The expanded roster, Blink, shield
and moving-zone scoring are owned by the
[combat spec](2026-10-25-arena-combat-items-zone-design.md).
The picture's matching triangle markers need distinct shapes in production, its
weapon screen coverage needs testing, and its surface texture should be simplified
further if it creates noise at combat distance.

## Architecture and rendering language

- **Forms:** broad planar walls, precise joints, cantilevered slabs, rectangular
  openings and occasional abstract arch remnants. Use gaps and interrupted volumes
  to retain the ruin identity. Avoid rubble piles and ornate temple ornament.
- **Values:** warm ivory, pale stone, ash gray and charcoal. Read routes through
  silhouette, light and shadow rather than large painted color blocks. Functional
  team, pickup and objective accents may use small amounts of color.
- **Materials:** matte stone/concrete with broad variation; sparse seams and wear.
  Keep most surfaces opaque. Fine cracks, dense vines and grassy carpets from C3
  are removed. A distant sparse tree is optional dressing, not essential identity.
- **Depth:** broad soft-edged shadows, gentle ambient fill, clear foreground/midground
  separation and a quiet skyline. Avoid white-on-white washout and dark figures
  vanishing in shade. Softness comes from lighting and forms, not motion blur.
- **Economy:** propose a small reusable kit of wall, floor, bridge, ramp, stair,
  cover and opening pieces. Bake static world lighting where compatible with the
  selected stylized shader. Restrict outlines to places where tests show a benefit.
  Neither the image nor this material strategy demonstrates a frame-rate saving.

Preserving the lush temple appearance would conflict with the requested refinement;
replacing everything with a flat abstract box arena would discard the selected C3
spatial identity. The chosen direction keeps that identity through clean modernist
forms. Exact shader, palette values and module dimensions remain builder proposals.

## Initial layout proposal

Design for four teams of up to two, including partially filled teams. Start with
two main combat elevations and a connected circulation loop. Skyline structures
can suggest more height without adding inaccessible combat positions.

| Area | Purpose | Required layout property |
| --- | --- | --- |
| Four outer arrival courts | Separate initial team approaches | Room for two; no direct opening spawn sightline; comparable travel effort to contested rewards |
| Central court | Strong early weapon race and an objective passage | Several entrances, edge cover, short exits; avoid an exposed bowl watched from every balcony |
| Side galleries and terraces | Alternate weapon races and flanks | Connected outer loop; each valuable pickup has more than one approach |
| Elevated bridge routes | Vertical repositioning and crossfire | Limited sightlines and multiple ways up/down; no single perch controls every objective segment |
| Broad connecting ramps | Moving-zone travel between spaces | Continuous walkable route, enough width for the zone and contesting players; no travel across a void |

Proposed objective circuit: central court → east terrace → rear gallery → west
terrace → central court, using broad ramps between levels. This is a spatial
proposal, not approved movement speed, scoring, timing or pickup balance. The
central ring in the concept shows only one point along the path. Place additional
weapon opportunities around the loop so the center is valuable without being the
only viable opening. Put pickup plinths outside travel pinch points.

Use architectural landmarks—an interrupted arch, stepped tower, long gallery and
open court—to aid orientation within a monochrome palette. Balance paths by measured
travel time, exposure and escape options rather than assuming visual symmetry is
fair. Do not add smaller-team buffs or special spawn rules through this art brief.

Spawn facing (2026-10-30; implemented and tested). Each arrival court has an L-shaped
side and front screen, which blocks a direct opening sightline. Each corner spawn
faces out through the gap between the front screen's outer end and the boundary
wall (`ArenaLayouts.Kit.CornerStarts`), not into the inside corner of the screen. A
player's first view is the exit route.
`ArenaWorldTests.SpawnsFaceTheirShelterOpening` checks every map: the first thing
the eye ray hits must not be an arrival piece, and the view must stay clear past
4.5 m. Practice respawns restore the same yaw. The view still looks down a walled
alley; opening it further would mean changing the screen geometry and rechecking
the sightline rule.

Plain traversal and travel distance (2026-10-30; the check is implemented, the
distances are measurements). `ArenaValidation.ValidateTraversal` builds a 0.5 m
grid of standable floors from the real colliders. It links cells by walking (rise
up to 0.45 m), by a jump onto a ledge up to 2.2 m (under the motor's 2.4 m apex) or
by a drop. Jumps and drops may land up to 1.15 m away, because the 0.38 m capsule
keeps the cells right beside a ledge face unstandable. Every start must reach every
pickup and zone point and get back. The check is part of `ValidateWorld`, so an
unreachable pickup now fails the arena build. Wall-runs, dashes and launches only
add routes, so a failure means a place needs advanced movement.

Outer walls (implemented; it enforces the clear death boundaries required below,
and its 8 m reach and 2.8 m clearance are proposed values from motor arithmetic).
The same check fails the build when a reachable floor within 8 m of an outer wall
sits less than 2.8 m below its top, because a running jump could land a player on
the perimeter. The 6 m walls of map revision 3 failed it on all three maps:
- Atrium's gallery and Terrace Steps' upper court run flush into the side walls,
  2 m below the top;
- Split Gallery's crossover and Terrace Steps' north edge stop 3 m short of a wall.

Map revision 4 raises every outer wall to 8 m. Advanced movement (wall-run chains,
launches) is not checked.

The check also reports each team's walking distance from its nearer start. The
"comparable travel effort" requirement above is not yet met on Terrace Steps. Mean
distances in metres (teams 0 / 1 / 2 / 3), map revision 4:

| Map | To weapon pickups | To zone points |
| --- | --- | --- |
| Atrium | 45 / 47 / 46 / 48 | 48 / 50 / 48 / 50 |
| Split Gallery | 48 / 49 / 49 / 49 | 49 / 50 / 49 / 50 |
| Terrace Steps | 48 / 62 / 51 / 68 | 44 / 63 / 51 / 68 |

The northern starts (teams 1 and 3) spawn under raised slabs: Atrium's north
gallery, and Terrace Steps' upper court and west terrace. They walk out south and
jump onto a ramp's low side. On Atrium that costs at most 3 m. On Terrace Steps,
team 3 walks about 86 m to the sniper against 65 m for team 0, and the northern
teams walk 29-55% farther than team 0 on average. One measured option, not adopted:
lifting the northern starts and their screens onto the +4 m courts. That gives
weapon means of 48 / 47 / 51 / 49 m and zone means of 44 / 48 / 51 / 56 m. But team 1
would reach the sniper in 27 m (team 0: 65 m), and its screens would block the zone
route on the west terrace. A layout change for this is open and needs the
user's direction. Walking distance is not travel time; exposure and escape options
are not measured.

## Authoring and validation sequence

### Three-map MVP scope

All maps share the accepted modular palette and silhouettes. They must have distinct
route topology, not merely rearranged decoration. Stable map IDs are used in session
compatibility checks. Host selects a map in the lobby; rematch returns to that lobby
and preserves the selection, allowing an explicit map change before ready/start.
No automatic mid-match map transfer is required.

| ID / name | Builder starting layout | Zone circuit and landmark |
| --- | --- | --- |
| `atrium` / Atrium | About 64 x 64 m; central open court, +4 m upper gallery, interrupted bridges, four screened corner arrivals and multiple broad ramps | Continuous court-edge/gallery loop on supported surfaces; broken rectangular arch marks center |
| `split-gallery` / Split Gallery | About 84 x 48 m; two parallel halls divided by a monumental spine, staggered cross-passages and a middle elevated crossover | Long figure-eight through both halls and crossover approaches; paired skyline fins identify each half |
| `terrace-steps` / Terrace Steps | About 66 x 72 m; offset courts at 0/+4 m, three broad ramps, side return lanes and an interrupted central wall | Perimeter ascent and a different descent ramp; cantilevered terrace tower marks high ground |

The zone's entire footprint must fit traversable routes: widen ramps and turns rather
than shrink the scoring volume inconsistently. Each map needs all four two-player
start groups, center and side special-weapon races, at least two approaches to valuable
pickups, deliberate cover and clear death boundaries. Test zone support on ramps and
stacked floors and measure each opening route with the actual adapted motor.

The shared kit is slab, wall, pier, low cover, parapet, ramp, bridge, rectangular
opening and abstract broken arch. Use source chamfered mesh construction with separate
simple colliders. Persist meshes and generate UV2 before baking. The first material
slice uses stock URP Lit, soft key light and restrained ambient/probe fill.

1. After the foundation's movement and minimal combat gates, reuse the source's
   level data and authoring tools in the separate project. Audit the available kit;
   greybox the loop, four arrival courts and two elevations before decorative work.
2. Measure each team's opening routes to the center and side pickups using the same
   movement inputs. Record exposure and exits; test uneven teams as well as 2v2v2v2.
   Revise routes that consistently grant one start a dominant uncontested reward.
3. Sweep the full moving-zone footprint along its route and contest it in motion.
   Check obstructions, stairs, corner transitions, escape routes, spawn visibility
   and low/high-angle lines of fire. Continuous travel must not hide sudden unsafe
   drops or force everyone through one campable doorway.
4. Apply one finished material/lighting slice. Test human silhouettes, team symbols,
   weapons and effects against both light and dark surfaces in moving captures,
   with low graphics settings and color-accessibility previews.
5. Expand art only after route tests, readability review and eight-player host/client
   profiling meet the [foundation gates](2026-10-25-multiplayer-foundation-design.md).
   Record CPU, GPU and memory evidence on the agreed hardware. A pretty empty map
   is insufficient evidence for a busy match.

Networking remains first in implementation order. Concept work can proceed alongside
foundation design. Build reusable kit pieces in Blender where appropriate and assemble
routes with Unity's adapted level tools under the [asset pipeline](../../ASSET-PIPELINE.md).
This map brief does not fix numeric weapon balance or establish competitive fairness.

Map revision 2 adds one laser pistol, shotgun and sniper pickup per arena. Placement
uses existing supported floors and preserves baked scene geometry/lightmaps. The
combat owner defines ammo/behavior; default layouts, saved catalog and scene metadata
must agree. Native support/clearance checks cover the new pickup locations.
Map revision 4 raises the outer walls from 6 m to 8 m (see the outer-wall rule
above) and needs a fresh bake.
