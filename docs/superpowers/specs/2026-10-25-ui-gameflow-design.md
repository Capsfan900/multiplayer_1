# Wildstone menus and game flow

User requirement: minimalist, good-looking menus matching the soft modern FPS.
This is the UI owner; simulation/security/performance remain in the foundation,
and weapons/match rules remain in the combat spec. Menus, local settings and
offline Practice are authored. Photon-only host/join,
team/ready/load, pause/results and retained-session reconnect are composed with
the networking adapter. No account sign-in is required (D023). Missing Photon app configuration keeps online
actions unavailable; guest reconnect lasts only while the game remains open.
Packaged keyboard interaction, moving visual/feel and external-network acceptance remain open;
[QA](../../QA.md) owns current local service evidence.

## Presentation

Use warm off-white text/panels, graphite accents and restrained team/objective color.
Large deliberate headings, compact secondary labels, generous spacing and a small
number of strong actions should make the interface feel finished. Render a quiet
arena view behind the title and lobby; do not use concept art as fake gameplay.
Subtle short fades/hover transitions are permitted; no mandatory animated intro,
motion blur or busy glass/neon treatment. Use Unity UI Toolkit with a small shared
stylesheet and state controller, not a new UI framework.

Scale from 1280x720 through 1080p/1440p/ultrawide. Keep text readable at low graphics
settings. Keyboard focus is visible, Escape consistently backs out/cancels, and input
never stays trapped after leaving a session. Team symbols accompany team colors:
▲ Teal, ■ Gold, ◆ Crimson and ● Violet. The names match the soldier accents (`SoldierRig.TeamAccents`),
which the [character workflow](2026-10-25-characters-weapons-design-workflow.md) owns. Before D031 the HUD said
Ivory/Ash/Sage/Clay while soldiers wore other colours.
Editable slider values and dropdown selections use ivory text on graphite fields,
with visible hover and keyboard-focus borders. `ArenaMenu.uss` owns these native
UI Toolkit input styles. The menu scrollbar uses a restrained graphite track,
ivory thumb and visible focus border. Static packaged captures now verify Settings
contrast and layout; [QA](../../QA.md) records the images and interaction limits.
Transitions must be interruptible; reduce-motion preference disables decorative motion.
Same-screen status and roster refreshes preserve the focused logical control,
including focus inside a text field. A new Settings screen focuses sensitivity
before the controls list. Scheduled focus belongs to the card that created it;
an old detached card must not focus a replacement screen.

### Menu backdrop, motion and music (2026-10-29, user request; implemented, not natively run)

The user asked for a revamped menu "like vibegame with the animation in the main
screen" and chill house music for menus and levels. Accepted: an animated live-arena
title backdrop, vibegame-style menu look/motion and looping menu/map music. Every
number below is a **proposed** builder value pending a visual/feel pass; constants
live in `MenuMotion` and `MusicMix` (managed regressions: `MenuMusicBackdropTests`).
The port of vibegame1 `MenuCameraDrift`, `CloudDive`, `UiSprites.RampH` and the
`MainMenuBuilder` title language is recorded in [SOURCE-REUSE](../../SOURCE-REUSE.md).

- Ownership: `MenuBackdrop` and `ArenaMusic` (Presentation) are added at runtime by
  `ArenaMenu`; the saved Bootstrap scene needs no rebuild. Both are presentation only:
  no simulation, input, network or authority state.
- Backdrop: active while no match is attached and the screen is not Playing (title,
  online/lobby, practice, settings, failure). It uses the existing main camera and its
  post stack at a 50° vertical lens. Four soldiers, one per team
  (`PresentationCatalog.Humans[team]` through `PlayerVisual`, idle, low-ready, two
  periodically raising aim), stand at the map's first zone point snapped to the floor.
  The camera sits 8.5 m out and 4.2 m up on the arena's outer side looking inward,
  swings ±30° over 96 s and breathes with the source's 70 s Lissajous drift
  (±0.6/0.25/0.6 m, ±1.5° yaw, ±0.8° pitch); the squad composes into the right third
  (16° turn). A sphere-cast boom pulls the camera in front of blocking architecture.
  Missing or incomplete team prefabs/weapons skip that soldier; a missing catalog
  leaves the drifting arena only. Soldiers hide during loading and matches.
- Reduced motion freezes the camera at one composed still; soldiers keep their idle
  but do not raise aim, and the dive below keeps only a short fade.
- Layout: the column hangs left on a graphite ramp scrim (opaque to 58% of a 1000 px
  band, then the source's eased fall) with a thin sand margin rule; the world is not
  flat-dimmed except under Pause/Settings/Results over a match. The title uses
  Barlow Condensed Black (OFL) italic "BETTER BEAN / BATTLES" (the game's name, D038;
  two 64 px lines, formerly one 92 px "WILDSTONE") over a sand offset copy, a slab rule
  with fading tail and a letter-spaced subtitle. Two 80 px lines made the 720p title
  column scroll and pushed Quit below the fold on ultrawide, so the face is 64 px and the
  column's top and bottom padding is 3% of the width (was 4%). Screen titles and eyebrows use the
  same face unless the text carries a team symbol.
- Motion: a new screen's column fades in from -24 px over 0.22 s; same-screen refreshes
  do not animate. Buttons carry a sand accent bar, invert to ivory (primary to sand)
  on hover with a 6 px nudge, and press to 98.5%. Activation plays a soft synthesized
  tick at 0.35x effects volume.
- Dive: choosing a Practice arena whips the column out (-80 px, 0.15 s), pushes the
  camera halfway to the squad with the source ease `1.3k²−0.3k³` and a tan-space lens
  widen, and closes an ivory curtain before loading starts 0.7 s later (0.25 s under
  reduced motion). Escape or any re-render cancels it. The curtain stays closed while
  any arena loads and opens over 0.6 s into the next screen.
- Music: `Resources/Audio/Music/` loops `MenuTheme` on every screen without an attached
  match and `Level_<mapId>` (`Level_atrium`, `Level_split-gallery`,
  `Level_terrace-steps`) while a match is attached, returning to the theme on Results.
  Track changes crossfade over 1.5 s on an equal-power curve; reversing mid-fade
  resumes the fading voice. Pause/Settings over a match duck to 0.35 over 0.4 s.
  A missing clip makes that track silent and never blocks. Volume is Music × duck,
  under the master listener volume. Audio files belong to the music lane
  ([ASSET-PIPELINE](../../ASSET-PIPELINE.md#music); D032 ACE-Step loops since
  2026-10-30). Packaged streaming of the theme and all three map loops is verified by
  `Invoke-VisualCheck.ps1 -Music`. No one has judged the listening balance against
  SFX.

Keep the HUD tree for the same actual match through pause/settings/resume and
replace it when the match changes. Hit feedback starts hidden until a hit event.
The crosshair is centered in the viewport; HUD text wraps within its available
width instead of overflowing at larger UI scales. Packaged off-screen captures
cover 720p, maximum UI scale, 1080p and an ultrawide title; this does not replace
moving readability, keyboard/mouse interaction or human feel acceptance.

Combat feedback (2026-10-29, gunplay revision 4; implemented, not yet natively
captured). Effects are presentation only, deduplicated through
`PresentationEventHistory`, and never change simulation. The combat spec owns spread,
ADS and head rules; values here are proposed.

- Dynamic crosshair: four ticks whose gap is the authoritative cone (current ADS and
  bloom) projected at the camera's current vertical FOV, minimum 3 px. Sighted guns
  hide it past half ADS; the sniper scope replaces it.
  - Each tick and the centre dot is a 2 px ivory core inside a 1 px near-black rim at
    0.8 alpha, so the reticle reads over ivory stone as well as dark walls. The cone gap
    is measured to the core. (2026-10-30, implemented and captured.)
- Sniper scope: a dark ring leaves a circular aperture of 90% of the short screen side,
  with a thin reticle. It is shown from 0.9 local ADS.
- Hitmarkers reuse the centred hit cue: ivory body, blue shield break, larger amber
  head hit with a confirm tone, red kill mark with a kill tone. During its display a
  stronger marker is not replaced by a weaker one. Tones use 0.5x effects volume.
  The cue carries a 1.5 px dark text outline for the same contrast reason.
  Body and shield-break markers play a 50 ms tick at 0.45 of the confirm level
  (2026-10-30). Connecting is heard at any range, and the head ding still stands
  out. A stronger marker on display still suppresses a weaker one and its tick.
  A shotgun reports one hit per pellet. Since 2026-10-30, a frame sounds once, so
  its pellets cannot stack into a clipped burst. A stronger marker in the same frame,
  such as the kill, still sounds.
- Kill feed: the last four kills for 5 s, using team symbol and participant number.
  A kill is attributed only to the last same-tick hit that dealt damage, and notes
  whether that hit was a headshot. The rules finish a target only at the end of the
  tick, so later pellets and other shooters land 0-damage hits that tick; those
  never take the credit (2026-10-30).
- Kill banner (2026-10-30; implemented and captured, values proposed):
  - Your own kill shows `ELIMINATED` plus the victim's team symbol and number in 17 px
    text on a graphite pill (0.8 opacity) 59% down the screen. A headshot adds
    `HEADSHOT` in words, because a `◎` read as the violet team's `●`. At 63%, 15 px and
    0.62 the pill ran into the Line Driver's muzzle and the red streak word was faint
    on the grey it made over ivory stone.
  - Kills within 4 s of your previous kill count up `DOUBLE`, `TRIPLE`, then `×N`.
    The streak word is in the kill-mark red. A longer pause starts over.
  - The banner lasts 1.8 s and fades over its last third of a second (no fade with
    reduced motion). It hides while you are dead.
  - It uses the same attribution as the feed and is presentation only.
- Map name card (2026-10-31; implemented and captured, values proposed, not
  human-accepted). The three maps share a kit and spawn structure, and the tester
  read them as one. The first 3 s of each round show the map's display name in
  capitals on a graphite card under the zone pill (34 px, wide letter spacing).
  It fades over its last half second, or cuts with reduced motion. It names the
  map; it does not stand in for making the maps look different.
- Damage direction: a red arc rotates toward the attacker's eye for 1 s and fades
  (it stays static when reduced motion is on).
  - Since 2026-10-31 it is a thin 44° arc at 35% of screen height from the centre
    (at least 60 px; `ArenaHud.DamageArcRadius`). A 56 px bar 120 px from the
    reticle read as a red hitmarker
    ([playtest item 7](../../PLAYTEST-FIX-LATER.md#7-red-hit-marker)).
  - The red kill ✕ is unchanged. Which red mark the tester meant is still open.
- Low health below 35: an edge vignette. It pulses gently unless reduced motion is on.

Playing HUD layout (2026-10-30; implemented and captured, not human-accepted). `ArenaHud`
builds it and `ArenaMenu.uss` styles it. Panels are graphite at about 55% opacity, so
the map shows through them.

- Top centre: four team chips and a match clock. The clock sits between the second and
  third chip and shows `PRACTICE` or `PRIVATE MATCH` under the time. Each chip shows
  its team symbol and score, plus a thin bar in the team tint (`ArenaHud.TeamTints`)
  that fills toward `ArenaHud.WinSeconds`. The bar is visible even at zero, so the
  colour always shows. The player's own team chip has an ivory border.
- Under the chips: a zone pill. Its ▲ arrow points toward the zone. The arrow is
  the idle zone magenta (`ArenaHud.ZoneIdle`, matching the world zone) while the
  zone is free, the captor's tint while it is held, and warning red with
  "ZONE CONTESTED" while it is contested. It was ivory before 2026-10-31.
- Zone waypoint (2026-10-31, playtest item 12; implemented and captured, not
  human-accepted): a ◆ with the distance in metres. It sits over the zone centre,
  2 m up, in the arrow's colour on an 80 px graphite chip (0.72, like the bottom
  panels). The first version drew the text with a 1.5 px dark outline, which ate the
  14 px glyphs and left them hollow.
  - Off screen in front, it slides to the nearest screen edge along its direction
    from the centre (`ArenaHud.ZoneMarkerPosition`), 44 px in from the edge so the
    whole chip stays on screen.
  - Behind the camera, the projection is mirrored and its height means nothing, so
    it only shows which way to turn: the left or right edge at mid-height, or
    bottom centre when the zone is almost directly behind (within 5% of the
    screen width of centre).
  - It hides inside the zone radius and while dead.
- Bottom left, stacked upward from the vitals panel: ability chips (Blink, Shield,
  Grenade, each with its key; dimmed at zero), then a faint hint led by the map name
  (`ATRIUM · ESC MENU`). At the
  largest UI scale, a top-left hint ran into the score chips. The vitals panel has
  a left border in the team colour, a shield value and bar that appear only while
  shielded, a large health number that turns red below 35, a health bar and a
  stamina bar.
- Bottom right: the weapon panel, kept compact so it clears the viewmodel. It shows
  the weapon name, a large ammo count (∞ for the sword), an `AUTO` or `RELOADING`
  tag and a reload bar.
  - Both bottom panels use the score chips' 0.72 backing (2026-10-30; it was 0.55).
    Several viewmodels still reach under the weapon panel, and through the thinner
    backing the sniper's scope lens and the guns' cyan stripes muddied the count.
  - Low ammo (2026-10-30): in the last quarter of the held ammo (rounds × 4 ≤
    capacity), the count turns the warning red. Since catalog revision 5 a
    special's capacity is its two-magazine pickup count (`WeaponCatalog.PickupAmmo`).
  - Each shot in that range also plays a 45 ms dry click on the HUD voice at 0.35
    of the confirm level. It is pitched well under the hit tick so the two stay
    distinct.
  - Switching weapons, picking up ammo and death never click.
- While dead, the panels hide and the centre shows `RESPAWN` with the seconds left.
- Pickup prompt: `E   PICK UP <ITEM>` while a pickup is within reach of the predicted
  feet. Since 2026-10-31 a Blink pickup reads `E   PICK UP BLINK · Q TO TELEPORT 6 m`
  (`ArenaHud.PickupPrompt`, with the player's bindings), and the Practice screen says
  what Blink does ([playtest item 9](../../PLAYTEST-FIX-LATER.md#9-what-is-blink)).
- Blink refusal (2026-10-31): a Blink press that still holds its charge 0.6 s later
  (`ArenaHud.BlinkDenySeconds`, which covers the round trip online) was refused. It
  plays the dry click and shows `NO FLOOR TO BLINK TO · AIM AT OPEN GROUND` in the
  prompt slot for 1.5 s. The host's refusal reason is not replicated; this
  inference is presentation only.

The tints lift crimson and violet so they read on graphite. Team symbols always go
with the tints.

## Screens and transitions

| Screen | Primary content and behavior |
| --- | --- |
| Title | Game identity, Play, Practice, Settings, Quit; current connection state quietly visible |
| Online play | Host and Join actions; join code with validation and region where relevant; no account sign-in or profile setup |
| Host setup | Map preview/name, private room creation, concise retry on service error |
| Lobby | Room code/copy, four two-slot team cards with symbols, ready state, host map selection and start; explain unmet two-team minimum |
| Loading/connecting | Bounded progress/state and cancel; all paths release old runners and ignore stale callbacks |
| Playing | Small dynamic crosshair, health/shield, ammo/slot, grenade count, Blink/shield charges, four-team scores, match timer and zone status/direction |
| Pause | Resume, settings, controls, leave; local menu never pauses network simulation |
| Recovery | Honest reconnect/migration state and countdown; input/scoring behavior belongs to foundation; failed recovery offers retry/back |
| Results | Win/draw/abort distinction, team totals, rematch/back (online: host-only Rematch, below); no invented persistent ranking or trusted progression |

An authoritative aborted round is a terminal result, not a generic connection
failure. Capture its validated independent summary before retiring network objects;
close the session, clear motors/view/input/event references and display Results.
Keep the summary marked online so it cannot tick locally or offer offline replay.
Back discards it. A connection failure without a terminal match still uses the
existing online retry/back path. Native lifecycle regression: `AbortedOnlineResultsTests`.

Online rematch (implemented 2026-10-30):
- After a normally finished online match, the host's Results offers Rematch and Leave
  match. Clients see "Waiting for the host to start a rematch." and Leave match.
- Rematch takes every peer to the same room's lobby. Teams stay; readiness clears.
  The host starts the next match through the ordinary lobby.
- Aborted results never offer Rematch. Online Results never offers the offline Play
  again.
- Session rules belong to the [foundation](2026-10-25-multiplayer-foundation-design.md#7-sessions-seats-and-recovery).
  Regression: `MenuPresentationTests.OnlineResultsOfferRematchOnlyToTheHostOfALiveFinishedMatch`.

Loading Cancel, Escape and failure Back must leave an active network session even
before gameplay attaches. Clear the exposed session immediately on leave so a late
load cannot acknowledge or spawn. An online loading failure must not offer an offline
retry while that session is active.

Automatic recovery takes the foreground instead of leaving a frozen gameplay HUD.
Show the ceiling of the remaining seconds from the session's original recovery
deadline; presentation never starts or extends that deadline. Hide stale lobby
team/ready/start actions and suppress a second reconnect while recovery is pending.
Leave, Back and Practice still cancel the active session. Once recovery completes,
resume only an attached current match with an active session; cancellation must
never reopen the retired gameplay presentation. Focused regressions are authored
in `MenuPresentationTests` and `OnlineMenuTests`; native results belong in QA.

Practice is explicitly offline, useful for validating movement, maps and weapons.
It may have target fixtures but no claimed online players or gameplay bots. An absent
SDK/service configuration must not make online buttons silently launch practice.
The target fixture runs a strafing drill so weapons can be judged against movement
(2026-10-30; implemented, values are proposals pending playtest).
- It faces the player and strafes at 0.7 input in uneven legs of 0.5–1.3 s.
- It pauses every fourth leg, jumps into every third and dashes into every fifth.
- Once it strays 3 m from its spawn it is pulled back.
- It never attacks, so it stays a fixture rather than a bot. It is frozen while
  gameplay input is inactive and while it is dead or stunned.
Developer provisioning instructions live outside ordinary player menus; a missing
configuration gives a concise unavailable state and a useful local log.

## Local settings

Mouse sensitivity, horizontal FOV, ADS sensitivity, hold/toggle aim, master/effects volume, resolution/fullscreen, VSync/FPS
cap, quality preset, UI scale, reduced motion and input bindings. Clamp finite ranges,
store in this project's namespace, apply/cancel predictably and restore defaults.
Never persist authority, health/ammo, provider passwords or login tokens as settings.
Show the current binding when prompts are used; releasing focus clears held movement.
Practice input latches action press callbacks until render sampling, preserving a
complete tap across input updates for one simulation tick. Clearing input discards
pending presses and combat actions; repeated render samples do not repeat an edge.
Native regressions live in `PracticeInputTests`: all three pass in the 2026-10-27
native PlayMode run `PlayMode-20261027-195621-403.xml` (62/62 overall). The fixture
explicitly routes synthetic keyboard input to the unfocused batch player and
restores the previous input settings; this is local input evidence, not online proof.
Revision-4 settings (proposed values):

- FOV is horizontal degrees, 80 to 120, default 100. Each aspect converts it to
  vertical with `Camera.HorizontalToVerticalFieldOfView` math, so ultrawide gains
  width without changing the vertical view.
- Practice passes only that hip FOV to presentation. Presentation applies ADS zoom.
- Look while aiming scales by tan(ADS FOV/2)/tan(hip FOV/2), keeping angular feel
  constant, then by the ADS sensitivity multiplier (0.2 to 2, default 1).
- Aim defaults to hold on the right mouse button, which is now a valid binding.
  Toggle aim is opt-in, and reload, grenade, weapon switch or a new life clear it.
- The settings payload carries `Version` 3. A version-1 payload stored vertical FOV,
  so it resets FOV to 100 and appends the Aim binding, keeping custom bindings.
- Quality defaults to level 3, "High" (implemented 2026-10-30). D028 maps levels 0–2
  to the Low pipeline and 3–5 to High (rendering spec section 6). The old default,
  Medium, therefore gave every new player the Low tier. A payload older than version 3
  that still holds Medium moves to High. Any other saved tier is kept.
  `MenuSettingsTests.LegacySettingsMigrateToHorizontalFovAndGainAimBinding` covers it.

Native evidence still needed: a Unity parity test against
`Camera.HorizontalToVerticalFieldOfView` and a legacy-payload load test, both in
`LocalSettingsPersistenceTests`. Only expose a music volume control when the game has music:
Settings shows "Music volume" (0 to 1, default 0.4) only when at least one music
clip loads. `MusicVolume` has been stored since version 1, so it needs no schema
version change; versions 1 and 2 keep their stored value.

## QA

Exercise every screen in a packaged Windows build: keyboard/mouse focus, resizing,
invalid/empty code, failed provider request, cancellation, loading failure, reconnect,
leave/rematch and reset settings. Check screenshots for overflow/contrast and moving
captures for disruptive transitions. UI polish is not proved by compilation.
