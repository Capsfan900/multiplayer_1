# Character and weapon design workflow

Date: 2026-10-25. Status: C3.1 map and simple human character visual directions are
accepted. All four weapon concepts and the expanded starting categories are accepted;
exact production assets and numeric balance remain unapproved. Networking
and implementation gates remain in the
[foundation design](2026-10-25-multiplayer-foundation-design.md).

## 1. Confirmed boundaries

The accepted map visual direction is **C3.1 / Wildstone Modern**, a sleek, mostly
monochrome refinement of C3 / Wildstone Ruins. The user also accepted its simple
human character direction; weapon design keeps its own identities with simpler
surfaces and softer shading that fit the game. The earlier fox/hare
characters and their matching themed equipment do not carry forward. See the
[map brief](2026-10-25-wildstone-modern-map-design.md).

Characters are simple **human silhouette people**: recognizable head, torso, arms,
legs and facing. This simplicity applies to characters, not weapons. Weapons may
be expressive, strange and varied; they need not resemble ordinary firearms or
inherit animal, fantasy or ruin motifs. First-person combat must remain legible.

Four teams need reliable identification. The accepted starting scope is Line Driver,
Pulse Lobber, Rebound Disc, Push Lens, grenades, stun guns and laser swords, plus
exactly two special consumables: short Blink teleport and temporary shield. The
[combat spec](2026-10-25-arena-combat-items-zone-design.md) owns their behavior.
Character classes, body-size advantages, numeric balance, loadout rules and progression
are not approved here. Exact assets, collision and performance still need validation.
The [four simplified weapon designs](../../art-direction/weapon-concepts.md), saved
as `*-simple-v2.png`, are approved production references. Their visual direction and
the accepted character style do not need another selection round; validate the
resulting models, animation and gameplay framing during production.

## 2. Character track: accepted direction to production checks

Use the accepted C3.1 simple human figures as the visual reference. The former
H1–H3 comparison is superseded; do not reopen that choice. Develop a production
sheet with consistent human proportions and a common hit envelope, without
implying different combat roles.

Show front, side, rear and three-quarter views, plus a small aiming/running pose.
Avoid tiny heads, oversized armor, narrow waists that imply smaller targets,
animal features and ornamental silhouettes. Surface style must not change the
visible relationship between body and hitboxes. First-person hands should match the
chosen body language; first-person weapons receive their own presentation review.

Propose four restrained team accents paired with four distinct symbols and short
team labels. Repeat identification on readable front/back locations and permitted
HUD markers. Preserve clear head/torso value separation. Color alone cannot carry
team identity; grayscale and common color-vision-deficiency previews are required.
Markers obey the game's visibility rules and must not reveal opponents through walls.

**Sheet exit:** readable facing and body parts at matched combat scale; team cues
survive grayscale and the body matches the common hit envelope. Resolve production
issues in the accepted direction before detailed modeling; approval of the visual
theme does not approve an exact mesh, rig or collision representation.

## 3. Weapon track: accepted identities, simplified production treatment

Keep W01 Line Driver (precision split-rail blaster),
W02 Pulse Lobber (rounded orb launcher), W03 Rebound Disc (broad disc launcher) and
W04 Push Lens (open ring displacement emitter). All four are selected. Preserve
their distinctive large forms and emission direction while removing tiny seams,
grip texture, decorative mechanisms and shiny photoreal surfaces. Use broad matte
ivory/graphite shapes, soft bevel highlights and restrained accents. The
[weapon concept gallery](../../art-direction/weapon-concepts.md) is the current
review output. This replaces the earlier W1–W3 language-board exercise.

Weapons retain more expressive shapes than characters while sharing the game's
simple material language. Avoid filling every weapon with the same glow. Team identity
and threat state must use distinguishable cues; a bright effect cannot mean both
“friendly” and “about to explode.”

**Production-review exit:** record the simplified contour/material treatment for
each accepted weapon, then produce first-person, opponent-held and pickup views.
Reviewers must identify role and emission direction from the opponent view without
a caption. Extend the same process to grenades, stun guns, laser swords and the two
consumables; no new comparison is needed to re-approve their inclusion.

## 4. Behavior and rendering contracts

Use the [combat/items/zone spec](2026-10-25-arena-combat-items-zone-design.md) as the
single owner of weapon roles, counterplay, authority and status interactions. Use
the [rendering spec](2026-10-25-unity-rendering-design.md) for materials, lighting,
effect readability and cost. This workflow does not duplicate their rules or budgets.
Prototype one roster item at a time after minimal foundation combat works. Each
addition must survive replay, late join, migration and measured busy-match cost.

## 5. Review sequence and required outputs

1. **Audit reuse.** Inspect existing `vibegame1` models, materials, rigs, animations,
   weapon presentation and relevant licenses/dependencies. Record reuse, adapt or
   replace with reasons. Do not assume an asset's appearance or multiplayer readiness
   from its name. Source stays untouched; approved reuse follows the separate-project
   foundation import boundary. Exit: a small inventory with explicit missing pieces.
2. **Refine accepted visuals.** Use the simpler W01–W04 concept pass and accepted
   C3.1 map/humans. Extend that material language to the added weapon/item categories.
   Exit: recorded contours, material limits and unresolved production details.
3. **Resolve production sheets.** Supply orthographic body/weapon views, team markings,
   material/value swatches, first-person framing and ready/attack/recovery cues.
   Overlay the common body hit envelope. Exit: approved contours and cues, with no
   ambiguous facing, muzzle direction or team/threat color collision.
4. **Rig and greybox.** After foundation movement and the minimal combat fixtures
   needed by the selected concept pass, test one human rig and selected weapon proxies
   in the host-mode build. Check aim, run, slide, jump, death and weapon handling from
   both first- and third-person views. Exit: no hitbox mismatch, severe clipping,
   obscured aim point, duplicated effect or missing replicated state.
5. **Readability and cost pass.** Record matched moving captures against light/dark
   C3.1 backgrounds at representative near/mid/far combat distances. Test four teams,
   overlapping effects, low graphics settings and color-accessibility views. Ask
   reviewers to identify team, facing, weapon role and imminent threat; record misses
   and revise. Profile eight-player host/client CPU, GPU, effects and traffic against
   the foundation budgets. Exit: documented readable cases and measured budget
   compliance before expanding production assets.

No FPS, latency, migration reliability or network quality is proved by concept sheets.
An attractive design that fails recognition, hit fairness or measured cost returns
to the relevant stage. Weapon inclusion is settled; exact models, animation,
framing and numeric statistics remain production work. Use the documented
[Blender-to-Unity asset pipeline](../../ASSET-PIPELINE.md), auditing reusable source
assets before replacing them. Concept illustrations are references, not final meshes.
