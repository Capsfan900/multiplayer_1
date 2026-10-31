# Character and weapon design workflow

Date: 2026-10-25; updated 2026-10-29. Status: C3.1 map direction is accepted.
[D028](../../DECISIONS.md) replaced the simple human figures with armored sci-fi
soldiers (section 6), and D029 added sights. Section 6 is authored and
content-built in Unity; native tests and visual acceptance are recorded in
[QA](../../QA.md). All four weapon concepts and the expanded starting categories are accepted;
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

*Historical (pre-D028):* characters were simple **human silhouette people**, with recognizable head, torso, arms,
legs and facing. Section 6 now owns the soldier. This simplicity applies to characters, not weapons. Weapons may
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

### First-person body and handling (2026-10-28)

The user requires visible legs, properly animated arms/hands and a pronounced slide
like `vibegame1`. The local lower body stays level under camera pitch, follows local
yaw and turns its asymmetric boots-forward slide pose toward travel (since
2026-10-30 only the shadow shows it; the visible body hides while sliding, see the
Local body row in section 6). Walking uses
distance-related gait; air tuck, hand landing dip and slide recovery remain distinct.
Use Wildstone's existing original human and hand geometry. Source body/arm motion
is a reference; no source single-player managers, clocks or third-party assets are
imported. Exact pose amplitudes are builder tuning, validated against this camera.

Hands maintain their weapon grip/support relationship during motion. Existing
accepted combat events and authoritative action timers drive firing, reload and
throw/melee presentation; visuals cannot spend ammo, create hits or extend timers.
Locomotion and slide tuck articulate the hands and sleeves. Death, life changes,
teleports and teardown clear stale visual impulses; menus freeze cosmetic clocks.

Slide simulation already matches the audited source tuning. Strengthen body,
weapon and cosmetic camera feedback without changing speed, friction, collision,
stamina or network state. Camera eye position and center aim direction remain the
authoritative ones; cosmetic roll/FOV must not alter shot direction. Reduced motion
retains leg/hand state readability while suppressing decorative bob/roll/FOV/overshoot.

*Historical (pre-D028 bodies, superseded by section 6):* the initial implementation used six existing lower-body skins, with hips behind the
standing lens and under it during the slide. A 6 Hz damped transition raises the
legs; the view adds up to 6 degrees of FOV and 2.5 degrees of steering roll.
Wrist/finger pivots and length-only sleeve adjustment retain original geometry.
These are presentation defaults, not measured movement or performance targets.

Acceptance: native tests for grip/support contact, local body visibility and pose
reset, camera aim preservation and reduced motion; inspect packaged look-down,
walking, airborne, slide-entry/sustain/exit, fire/reload and weapon changes. Static
pose fixtures do not prove actual input, movement parity or human feel. Full moving
acceptance remains open until its evidence is recorded in [QA](../../QA.md).

### Remote weapon handling (2026-10-29)

Third-person wrist/held-weapon recoil consumes accepted Shot events once, through
the existing presentation event history. It runs after Animator and current player
state presentation, retains the authored grip, and uses distinct pistol/shotgun/
sniper impulse amplitudes. Starter reload pose follows authoritative `ReloadUntil`
and match tick rate, including late observation; visuals never alter those timers.
Death, weapon/life/connection changes, disconnect and movement discontinuities clear
old impulses. Cosmetic decay freezes with gameplay input suspended; reduced motion
retains a smaller cue. A valid committed same-life event may retain a historical
connection generation across recovery and must not be rejected solely for that.
The remote event queue is capped at 256 and clears on teardown. Focused native
pose/grip/reset/bridge-dedup regressions live in `RemoteWeaponHandlingTests`; moving
opponent-view and human readability acceptance remain separate.

Remote soldiers always animate (`AnimatorCullingMode.AlwaysAnimate`, set by
`PlayerVisual`; 2026-10-30). The prefab's `CullUpdateTransforms` skipped the bones of
a body no camera had drawn yet, so a soldier's first frame, and the first frame of
every respawn, rendered the T-pose. Off-screen shooters also kept a stale hand, and
so a stale muzzle for their beam. `DerezTests.FirstFrameAndRespawnAreAlreadyPosed`
covers both frames.

### Audio (2026-10-30)

Intent: weapons and movement sound recorded, not synthesized, and other players can
be heard moving. `ArenaAudio` owns all combat and movement cues; it is presentation
only and reads committed events and motor state.

- **Sources.** The recorded CC0 banks under `Resources/Audio/Sfx/`
  ([SOURCE-REUSE](../../SOURCE-REUSE.md#recorded-weapon-and-movement-audio-2026-10-30)).
  Each weapon has its own bank and pitch.
  - The Line Driver, shotgun, sniper and Pulse Lobber use the `LaserFire` zap, from
    1.0 down to 0.5 pitch.
  - The pistol (0.9) and Laser SMG (1.3) use the `TurretAnswer` knock, which is mostly
    mid-range rather than sub. This separates the three bullet guns by ear and stops
    the SMG's 12.5 shots/s piling up sub-bass (2026-10-30).
  - Guns that share a bank sit at least 20% apart in pitch.
  - The sniper and shotgun add a recorded `LaserCellNova` boom tail. It sits 6 dB
    (sniper) and 8 dB (shotgun, at 1.5 pitch) under the shot's K-weighted peak.
  - The shotgun, sniper, Pulse Lobber, Push Lens, blasts and derez add a quiet
    synthesized body layer under the recording.
  - A missing bank falls back to the old synthesized clip at its old level.
- **Events.** Shot, hit (sword hits use the sword bank), ricochet, explosion, stun,
  blink, shield up and break, pickup and death each map to one cue. Rejected events
  and zero effects volume play nothing. Combat cues vary pitch by ±3 %, movement
  cues by ±5 %.
- **Movement.** Per participant and alive only: one footstep per stride on the
  ground or a wall run above 1.5 m/s. The stride grows from 1.5 m to 2.6 m at
  11 m/s. A landing faster than 4 m/s down plays Land, scaled by fall speed. Leaving
  the ground faster than 2 m/s up plays Jump. Dash start, slide entry and reload
  start each play once. A position jump over 5 m (respawn, teleport) resets the
  stride silently.
- **Speed wind** (2026-10-30, vibegame1 `CameraBeats` law). One looping centred voice
  plays the `SpeedWind` recording.
  - It is written every frame and never starts or stops.
  - Volume is 0.30 × smoothstep(14, 24 m/s of horizontal speed) × effects volume.
    Pitch runs from 0.9 to 1.1 with the same factor.
  - An 11 m/s run and a straight fall stay silent. Dashes, slides, launches and hop
    chains bring it in.
  - It is silent in menus and while dead.
- **Near miss** (2026-10-30). An enemy's hitscan beam that passes within 1.6 m of
  the local camera plays a `Swing` whoosh at pitch 1.7 ±5%.
  - The sound comes from the beam's closest point, so it reads from the side it
    passed.
  - Gain is 0.5 × (1 − distance / 1.6) × effects volume.
  - The beam is the segment its effect draws (`ArenaEffects.ShotSegment`). A beam
    stopped by cover, one that starts behind the listener and one that hits the
    listener stay silent.
  - Own and teammates' shots never zip. The listener must be alive.
- **Slide scrape** (2026-10-30, vibegame1 `SlideFx`/`SlideImpulse` law). One looping
  centred voice plays a synthesized 1 s band-limited noise loop with a crossfaded seam.
  - Volume is 0.22 × (0.4 + 0.6f) × effects volume, and pitch runs from 0.78 to 1.32.
    f is the carried speed between the motor's 8 m/s slide end and 22 m/s maximum.
  - Attack is immediate and the release takes 0.12 s.
  - Menus and death cut it at once. The slide-entry whoosh is unchanged.
  - Other players' slides follow the same law from where they slide (2026-10-30).
    - Each gets one positional loop (spatial 0.9, 30 m linear rolloff), made on
      that player's first slide and reused after.
    - That adds at most one voice per remote participant.
    - Their death or leaving cuts it, and so do menus.
- **Mix.** 16 cue voices and 12 movement voices, fixed and reused round-robin, so
  steps never take a gunshot's voice. Other players are positional (spatial 0.9,
  linear roll-off to 30 m). The local player's own movement cues are centred at
  half level; reload stays at full level.
  A target's hit cue plays once per shot and target (2026-10-30), even when pellets
  alternate between two neighbours. A shotgun's per-pellet hits therefore neither
  stack nor take half the cue voices.
  Gains are proposals set from each bank's measured peak level; the menu clicks use
  the `Click` bank at 0.6 of effects volume.
- **Acceptance.** `ArenaWeaponAudioTests` covers recorded banks per weapon and
  event, distinct weapon sounds (shared banks 20% apart, at least three banks across
  the guns), body and recorded tail layers, fixed voice counts, rejected and muted
  events, footsteps by distance, single jump, land, slide and reload edges, silent
  teleports and centred local steps. Listening, mix balance and positional
  readability need a human; batch runs have no audio device.

### Weapon energy pass (2026-10-30)

Intent: the user asked for cooler-looking weapons with better effects. Each weapon
now shows its energy, and that energy reacts to firing. Presentation only.

- **Models** (`Tools/Assets/build_assets.py`, all eleven weapons). Each gun gets
  larger emissive features in its existing signal colour: barrel coils, capacitor
  cells, top strips, emitter rings and field segments. It also gets bolts, latches
  and layered plates.
  - The Laser Sword gets a longer, tapered faceted blade with an open glow envelope,
    a machined emitter collar and a ribbed grip. The envelope is 40 % longer than the
    old blade.
  - The Grenade gets an amber seam and a fuse halo.
  - Sockets, socket rotations, legacy part names and the export conventions are
    unchanged. Bounds stay within 5 %, except the sword's length (+25 % overall).
  - Every weapon is at most 9,000 triangles; the Grenade is 2,628. Provenance and
    measurements are in [SOURCE-REUSE](../../SOURCE-REUSE.md) and
    `ArtSource/weapon-energy-export-manifest.json`.
- **Emissive signal materials** (`PresentationBuilder`). `Signal*` materials now emit
  their colour × 1.2 over an albedo at 35 % of that colour. Before this they did not
  emit at all.
  - The dark albedo lets the emission set the hue. A lit full-colour albedo plus
    emission washed amber out to yellow.
  - Other palette materials have their emission cleared.
- **Fire response** (`WeaponView`). On each local shot the held weapon's signal parts
  flare.
  - Brightness is 1 + 1.2 × recoil + 0.8 × heat (+ 0.6 × Push Lens charge).
  - Each shot adds heat: 0.4 for the sniper, shotgun and Pulse Lobber, 0.2 for the
    pistol, 0.1 for the others. Heat is capped at 1 and cools at 0.5 per second.
  - Each part's brightest emission channel is capped at 1.6, so a flare never clips
    the hue.
  - Values are proposals pending a human look.
- **View punch** (`FirstPersonView.AddRecoil`). Each local shot tips the camera up,
  then the punch decays at e^(-18 t). Punch per shot: sniper 1.2°, shotgun 1°, Pulse
  Lobber 0.8°, Push Lens 0.7°, pistol 0.5°, disc 0.4°, Line Driver and Stun Gun 0.35°,
  SMG 0.25°. Sword and grenade have none. The total is capped at 3°.
  - It is cosmetic like the blast shake: shots still read `Yaw`/`Pitch` only (D025).
  - Reduced motion removes it.
- **Movement lens** (`FirstPersonView`, 2026-10-30). This adapts vibegame1's
  `PlayerFeedback`/`CameraFX` movement camera, with its kicks scaled from that game's
  95° vertical lens to this one's ~68°.
  - FOV impulses: dash +4°, jump +2°, wall jump +3°, launch +4°, and a hard landing
    -2.5°·k, where k is the fall speed over 22 m/s. Landings below k 0.08 do nothing.
    A perfect wall jump adds +2° more, the only read of its stamina refund.
  - Leaving a slide punches -2° as the slide's 6° hold releases. A wall run holds
    +3°. Impulses sum, are capped at ±10°, and decay at e^(-11 t).
  - A hard landing also nods the view down 2.5°·k, decaying at the same rate.
    vibegame1 drops the eye instead; that would move the camera off the authoritative
    shot origin (D025 above), so the eye never moves.
  - A slide entry nods the view down 1.2°. A strafe dash (more than 0.3 of its
    direction sideways) banks the view 1.4° into its travel through the wall-jump roll
    kick, fading over 0.28 s.
  - ADS fades the whole movement lens out (×(1 - ADS)), so a scoped shot is never
    warped. The viewmodel follows 35% of it, as before.
  - Cosmetic only: aim reads `Yaw`/`Pitch` and the authoritative eye. Reduced motion
    removes it. All values are proposals pending a playtest.
- **Dash streaks** (`WeaponView`, 2026-10-30, vibegame1 `DashFx` pattern). A dash
  fires ten lens-space lines on the viewmodel overlay for 0.22 s.
  - They flow against the travel: radially for a forward dash, sideways for a strafe.
  - They start at least 15% of the frame height off-axis, clear of the reticle, and
    fade as (1 - t)².
  - They are cold blue-white at peak 0.9 × 0.85 alpha, under the 1.05 bloom threshold.
  - They are camera furniture, so turning mid-dash smears nothing. Reduced motion
    shows none. Sizes are proposals.
- **Hit flash** (`PlayerVisual`). A host-confirmed `Hit` event lights the struck
  soldier's whole body white for 0.12 s: emission peak 1.4, or 2.2 on a headshot,
  fading linearly.
  - Reduced motion keeps a dimmer flash (× 0.35) so rapid fire never strobes.
  - The flash is a renderer-wide property block, cleared when it ends. A derezzing
    body ignores hits, and the derez block replaces the flash.
  - The soldier materials (`SoldierBuilder`) keep `_EMISSION` on, flagged realtime
    emissive, over a near-black base emission (.004). Editor material validation
    turned the keyword off on materials without that flag after a capture run. That
    also left the team glow materials dark.
- **Acceptance.**
  - `FirstPersonWeaponMotionTests.SignalPartsFlareOnShotsAndCoolBack`: authored
    emission, flare, cap, heat over sustained fire, cool-down.
  - `FirstPersonSlideViewTests.ShotPunchRecoversWithoutMovingAim`: punch, recovery,
    unchanged aim, reduced motion.
  - `DerezTests.ConfirmedHitFlashesTheBodyThenClears`: keyword present, flash,
    brighter headshot, decay, cleared override.
  - Packaged `-Weapons` captures (see [QA](../../QA.md#2026-10-30-maps-weapons-and-fire-feedback)).
  - How it feels in play needs a human.

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

Local diagnostic extension (2026-10-29): a continuous first-person starter-weapon
sequence retains body/view/hand state across run, slide, recoil and reload, with
sampled PNGs and transform/return checks. Fixed simulated time is separate from
wall time. This supplements posed captures; it does not accept natural traversal,
other weapon transitions, third-person readability, human feel or rendering cost.
[QA](../../QA.md) records the inspected sequence and remaining gates.

### Expanded laser arsenal (2026-10-28)

The user explicitly requested recognizable pistol, shotgun and sniper forms alongside
the sci-fi arena roster. Original `LaserPistol`, `LaserShotgun` and `LaserSniper`
Blender sources/FBX extend the original palette and socket contract. Compact slide,
wide paired barrels/fore-end and long barrel/scope distinguish their silhouettes.
`Tools/Assets/build_assets.py laser-kit` generates only these three models;
`ArtSource/laser-kit-export-manifest.json` records provenance and hashes. Gameplay
and initial tuning belong to the combat owner. Since D029 the sniper scope is a real zoom; the combat spec owns its values.
Native import/contact checks and actual packaged weapon images remain the acceptance
path. Earlier seven-weapon counts describe the historical starter roster.

2026-10-30 art pass: Line Driver, Laser SMG, Laser Pistol and Laser Sniper got a
stronger ivory and graphite two-tone, plus rail teeth, vents and accent strips and
cells. Cyan marks the AR, pistol and sniper (the sniper changed from violet); coral
marks the SMG. Sockets are unchanged and bounds stay within ±5%. The sniper's
first-person muzzle flare follows its cyan accents; until the firefight captures it
was still violet. The Laser Shotgun hip pose sits lower and further right
(.27, −.38, .50; was .24, −.34, .50), so its twin barrels clear the centre of view.
[SOURCE-REUSE](../../SOURCE-REUSE.md) records the triangle counts and hashes. The
viewmodel hip pose places the Pulse Lobber smaller and lower (scale .48), so the
launcher body no longer fills the lower-right of the view.

## 6. Armored soldier, arms, animation and sights (D028/D029, 2026-10-29)

**Intent.** The user asked for quality characters made with the forge tooling,
game-ready animation, and hands and fingers that actually hold the weapons. Their
choices: armored sci-fi soldiers, a hybrid forge concept plus Blender hard-surface
mesh, forge-procedural animation, and keeping the existing weapon sway, bob, tuck
and recoil.

**Ownership.** This section owns the soldier and arms contract. The rendering spec
owns materials, derez and effect cost. The combat spec owns ADS, scope and hit
volumes. [ASSET-PIPELINE](../../ASSET-PIPELINE.md) owns the build commands and
provenance. Presentation never feeds simulation.

| Part | Contract (implemented) |
| --- | --- |
| Body | `Soldier.fbx` from `Tools/Assets/build_soldier.py`. One Humanoid mesh, 52 bones (`soldier_skeleton.py`). LODs 21,084 / 8,888 / 3,464 tris. Same 1.787 m height and 1.8 m capsule hit envelope as before. Parts: contoured breastplate, faceted visor with brow and cheek guards, recessed comms pods, pauldrons, thigh/shin/calf plates. Armor plates are rigid to their bone; the suit is heat-weighted. |
| Team identity | Four material sets (accent, glow) plus one active `TeamMark1..4` mesh. That mesh carries the team symbol (▲ ■ ◆ ○) on the chest (fits 16.2 × 14.6 cm), back (15.4 × 17.4 cm), helmet pods and both pauldrons. Symbols are neutral ivory `SoldierMark` (sRGB .95/.95/.92, no emission) on near-black backings. They are 0.6–1.8 mm surface decals, weighted to `UpperChest`, `Head` and the upper arms, and do not change the silhouette. Team accents in `SoldierRig.TeamAccents` are luminance-separated: teal (.16,.73,.78), gold (.98,.92,.50), crimson (.40,.055,.10), violet (.43,.25,.60). The HUD names them ▲ TEAL, ■ GOLD, ◆ CRIMSON, ● VIOLET. |
| Sockets | `WeaponHand_R` (forward = wrist to knuckles, up = thumb/index side), `WeaponHand_L` mirrored, and head. Weapon `Grip`, `Muzzle`, `LeftHand` and `Sight` markers come from the weapon kit. The mirrored legacy weapon import leaves those markers facing the weapon's top, so `PresentationBuilder.ModelPrefab` gives every one the weapon frame (+Z barrel, +Y up). Before D031 the guns were held barrel-down and the fists wrapped the wrong axis. |
| Animation | `SoldierAnimations.fbx` from `Tools/Assets/soldier_clips.py`, 32 in-place clips at 30 fps. The clips: idle; walk and run in 4 directions; jump, fall and land; dash; slide enter and loop; wall-run left/right; wall jump; two deaths. Upper body: rifle/pistol/sword holds, rifle/pistol aims, reload, throw, sword swing and equip. Additive: recoil and front/back hit reacts. `SoldierAnimations.clips.json` is the clip sidecar: loop, speed, mask, additive and events. |
| Controller | `SoldierBuilder` generates `Soldier.controller`. Base layer: 2D freeform locomotion placed at each clip's measured speed, then air, slide, dash, wall-run and death. Upper-body masked layer by stance with an aim blend. Additive layer. Above the authored run envelope (forward 7.94, back 7.83, side 8.02 m/s) `SoldierRig.LocomotionScale` speeds the cycle so feet don't skate at 11 m/s. The wall-run state uses the same rate. Back and side runs bound, with shorter contact and longer flight, so every direction cycles at about 4.2 steps/s at 11 m/s ([QA](../../QA.md#2026-10-30-remote-strafe-and-backpedal-cadence)). Root motion is off and every root channel is baked into the pose. |
| Local body | `FirstPersonBody` spawns the team soldier with its Animator. The upper chest is collapsed, so nothing renders above 1.36 m over the feet at a 1.65 m eye; the hips sit 0.30 m behind the lens. While sliding, the visible body hides from the first slide frame until the slide spring has settled (about 0.1–0.2 s after the slide ends), at every look pitch, and a shadow-only copy keeps the full shadow throughout. The user reported on 2026-10-30 that the body getting into the view during a slide is a bug and asked that it not happen. Before that, the slide showed a fold of the third-person Slide pose (shins and boots thrown forward, a knee tuck on glances down, the legs hidden on steep glances), which still showed loose shins and boots in packaged slide frames. Death, life changes and teleports reset the slide spring, so the legs show at once. |
| Arms and hands | `SoldierArms.fbx` (37 bones, 10,904 tris) under the viewmodel lens. `WeaponView` aligns the weapon `Grip` to a hands pivot and solves two-bone IK per arm, sliding the shoulder when out of reach. Wrist rotation matches the grip markers. `FingerCurl` presets: gun, fist (sword/grenade), support cup, and relaxed during reload/throw release. The left hand rests when a weapon has no `LeftHand` marker. Arms are team-tinted and cast no shadow. Sway, bob, tuck and recoil are unchanged. Framing (2026-10-30, tuned with `-Action CaptureViewmodel`, proposed): kit models are about twice life size for third-person readability, so the viewmodel draws them at 0.6 (sword 0.7). The `WeaponView.HipPose` grip sits at about (0.22–0.25, −0.28 to −0.36, 0.42–0.56) in lens space, which places the sight about 37% below centre and right of centre. ADS sight relief is 0.15–0.16 m for holo and red dot, 0.24 m for iron sights and 0.10 m for the sniper before the scope overlay takes over. Grenade in hand (2026-10-30, proposed): while the throw key is held with a grenade (`WeaponView.GrenadeReady`, the same condition as the arc preview), the support hand leaves the gun for lens point (−0.115, −0.08, 0.35) holding the grenade model scaled to 0.08 m and centred on `Grip_L`. The throw still starts on the host's `Accepted`; after release the raised hand eases down over 0.4 s to bridge that round trip. The swing reaches 0.24 m forward, and the grenade leaves the hand at 35% of the 0.45 s swing. The first capture placed the hold at (−0.14, −0.13, 0.34), where the grenade sat half behind the HUD vitals panel; the test now keeps its centre between 20% and 60% of the view height ([QA](../../QA.md#2026-10-30-grenade-in-hand-muzzle-lamp-and-hud-backing-not-yet-captured)). |
| Remote hands | `PlayerVisual` rotates the right-hand bone so `WeaponHand_R` follows the aim. Remote recoil and reload from section 4 are kept. |
| Sights | Line Driver holo, Laser SMG red dot, pistol iron sight, sniper 4x scope. The ADS pose puts the `Sight` socket on the lens axis. Holo and red dot use the parallax-free `Reticle` shader. A full scope hides the viewmodel and shows the scope overlay. |
| Derez | On elimination the body freezes and dissolves into team-coloured voxels, then hides; the next life restores it. Specified in the [rendering spec](2026-10-25-unity-rendering-design.md) section 6. |

**Edge cases.**
- A missing or non-Humanoid soldier fails the presentation build.
- A missing clip or sidecar speed fails the controller build.
- A disconnected elimination hides immediately, with no derez.
- Thumbs keep their proximal spread and opposition axes. The current grip solver
  uses about 5° of opposition, because a deeper wrap collides with the grip.

**Acceptance.** Native tests cover:
- the Humanoid avatar and clip loop flags (`PresentationImportTests`)
- blend positions against the sidecar speeds
- the controller layers (`ContentPreparationTests`)
- the grip, support contact and ADS sight axis (`FirstPersonWeaponMotionTests`)
- weapon socket frames and hand sockets along the knuckles (`PresentationImportTests`)
- barrels downrange with the top up, first person (`HipGunsPointDownrangeTopUp`) and remote (`HeldGunBarrelFollowsAimTopUp`)
- the body ceiling, shadow copy, slide framing and slide lens clearance, settled (`FirstPersonBodyTests.SlideKneesStayClearOfTheLens`) and on every frame from a run into the slide (`SlideEntryKeepsTheBodyOutOfTheLens`)
- the derez (`DerezTests`)

Static tests do not accept feel. Packaged moving captures and user review remain the gate.

**Known gaps (measured 2026-10-29, not accepted):**
- **Team identification at 30 m is measured only on static renders.** The
  2026-10-30 palette and symbol pass lifted the minimum greyscale pair separation.
  The figures, out of 6,400 pixels front / side / back:

  | Measure | Before | After |
  | --- | --- | --- |
  | Palette plus symbols | 4 / 0 / 1 | 55 / 38 / 48 |
  | Symbols alone | — | 11 / 5 / 18 |

  [QA](../../QA.md) records the CVD simulations. Recognition in motion, under map
  lighting, is not accepted.
- **No visible legs while sliding.** The slide reads through the camera (FOV kick,
  roll, nod), the weapon and the shadow, not through the legs. A dedicated
  first-person slide pose that stays clear of the low lens could bring legs back, but
  only if the user asks for it.
- **Back and side runs** bound with a long flight phase to keep the forward run's
  cadence. They are more athletic than a real backpedal or side shuffle.
- **Wall runs** roll the run cycle 30° away from the wall and slide it 0.285 m
  toward the wall, so the boots meet the wall at the 0.4 m capsule radius.
  - The soles stay on the rolled plane rather than flat against the wall.
  - The drawn head sits about 0.46 m from the capsule axis. The head hitbox stays on
    the axis, so a shot at the drawn head of a wall-runner can miss.
  - Before 2026-10-30 the roll was 24° about the root, which put the head 0.60 m out
    and left the boots 0.31 m off the wall
    ([QA](../../QA.md#2026-10-30-remote-wall-run-lean)).
- **Forearm twist** reaches 126° in Throw; Humanoid twist limits may clamp it.
- **The LaserSword** sits blade-along-knuckles under the current socket convention.
