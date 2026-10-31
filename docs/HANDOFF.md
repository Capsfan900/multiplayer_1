# Handoff

Updated: 2026-10-31. **Photon-only Windows preview published; full multiplayer MVP remains open.**
Active worktree: `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`, branch `mvp-2`.
The original project at `C:\Users\tyler\Main Storage\multiplayer_1` remains untouched.

Latest public download: [Better Bean Battles preview 190bb5e](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261031-190bb5e)
(2026-10-31). It carries everything in the `b563bab3…` playable build below: health
packs, the D040 playtest fixes and the zone waypoint. Game sources were frozen from the
current files into `D:/WildstoneBuilds/release-20261031-100938/source` and built there;
this checkout stays uncommitted. Players should use this release, not the 2026-10-30
`07d8cc8` preview. [QA](QA.md#2026-10-31-current-state-public-preview-release) records
the checks and limits.

The user resumed all tasks and requested GPT-6.1 Sol delegates. We recovered the
existing authored checkpoint rather than reimplementing it: 194 files were copied
and SHA-256 checked before/copy/after. Evidence: `Artifacts/Resume/recovered-checkpoint.json`.
The original design/pause packet under `docs/handoffs/` remains historical and unchanged.

## Current evidence

- **Resume here (2026-10-31 11:45).** Uncommitted.
  - **Playable build `80f56a7c…`** (`Artifacts/Unity/Windows/WildstoneArena.exe`,
    not yet played). It adds the D041 feel pass to `b563bab3…`, which is archived.
    Both players must use it: the join token now carries the movement revision.
    Evidence: [QA](QA.md#2026-10-31-feel-pass-d041).
  - **Feel pass (D041), every value a proposal:** W+A air strafing (2.5 m/s wish,
    18 m/s² gain; carry decay unchanged, phase 2 deferred), Practice speed chip,
    predicted local hitscan shots online, eased recoil with lower viewmodel pitches
    and an additive SMG re-trigger, damage-scaled body hit marker, +4° speed lens,
    wind from 9 to 20 m/s, ADS bloom ring. Report:
    `Artifacts/Audit/2026-10-31-feel-pass-report.md`.
  - **Verified on the final source:** EditMode 526/528 and PlayMode 193/194,
    explicit skips only; five Photon smokes (gunplay baseline and adverse150,
    laser moving cover regional80, combat, late join) and the three capture modes
    pass with clean logs; 15 s headless start clean.
  - **Next:** the user's playtest of the feel pass, then the deferred air carry
    decay (0.8 → 0.5) if the strafe still feels short, and the two-hour gameplay
    soak below with nothing else on the PC.
- **Previous resume point (2026-10-31 02:20).** Uncommitted.
  - **Playable build `b563bab3…`** (`Artifacts/Unity/Windows/WildstoneArena.exe`,
    not yet played). It adds the HUD allocation fix, health packs (D039), the
    playtest fixes (D040) and the zone waypoint chip to `5ac42eed…`, which is
    archived. Both players must use it. The build hash method changed; see
    [QA](QA.md#2026-10-31-d040-playable-build).
  - **Verified on the final source:** EditMode 522/524 and PlayMode 184/185, with
    explicit skips only. All four capture modes pass, plus a frame sample (Ultra p99
    2.71 ms, about 560 B/frame). All 17 Photon regression scenarios pass
    ([QA](QA.md#2026-10-31-playtest-fixes-d040)). Two needed the smoke harness's
    one-magazine ammo pins fixed. Reconnect needed reruns after a transient Photon
    fault, then passed 10 of 10 cycles.
  - **Rewind cap 250 ms (D040 proposal):** regional80 shots are no longer clamped;
    adverse150 shots clamp at 250 ms against about 255–293 ms requested.
  - **Next: the two-hour gameplay soak, rerun with nothing else on the PC.** The
    first run failed at about 46 min, most likely from a machine-wide stall while
    Unity batch work overlapped it ([QA](QA.md#2026-10-31-gameplay-soak)). The
    runner now names a stall. Command:
    `Invoke-NetworkSmoke.ps1 -Scenario capacity -Gameplay -SoakSeconds 7200`.
    It occupies the PC for over two hours, so do not run it while the user is
    playing on that PC.
- **Playtest fixes (D040, 2026-10-31), implemented as proposals.** The eleven
  reports in [PLAYTEST-FIX-LATER](PLAYTEST-FIX-LATER.md) are implemented with the
  report's recommended values ([D040](DECISIONS.md)). Still the user's call: the
  250 ms rewind and attack window, the two-magazine ammo, the 0.2 s fire buffer,
  the eased recoil curve (unchanged), and which red mark the tester meant.
- **Quality pass D028–D031 (2026-10-30), uncommitted and not in any public build.**
  It covers rendering, armored soldiers with procedural animation, competitive
  gunplay revision 4, the menu and music, the derez, and D031's matte maps and weapon
  orientation. This continuation integrated two GPT-6.1 Sol Blender lanes:
  - six rebuilt special weapons;
  - luminance-separated team palette ▲ TEAL / ■ GOLD / ◆ CRIMSON / ● VIOLET, with
    ivory symbols on the chest, back and pauldrons.
  It also fixed:
  - **Viewmodel framing:** guns now draw at 0.6, lower-right, with readable sight relief.
  - **Slide:** the torso folds away while sliding, so it no longer fills the screen.
  - **Settings:** the default quality is now High (settings v3). Medium had silently
    selected the Low pipeline. VSync and FPS-cap writes had been commented out by
    mistake; they are restored.
  - **Grade:** written once again rather than on every build.
  - **Capture tools:** a probe NRE in forced render requests, and a sequence-gate hand
    lookup.
  Evidence:
  - final native EditMode 451/453 and PlayMode 148/149, with only explicit skips;
  - real Photon gunplay at baseline and adverse150, plus combat, reconnect and
    moving-laser regressions;
  - all four packaged visual modes with zero errors;
  - a 1080p frame-time sample: Ultra mean 1.60 ms / p99 2.04 ms on the RTX 4070 Ti.
  [QA](QA.md#2026-10-30-quality-pass-integration-d028d031) has paths and limits. The
  music was synthesized at the time because the FL Studio MCP was not connected
  (D030); D032 replaced it, see below. Human feel, balance and team recognition in
  motion are not accepted.

- **Presentation follow-up (2026-10-30), uncommitted.**
  - **Spawns** now face out of their arrival screen instead of into its inside corner.
  - **Hitscan beams** keep a readable on-screen width at range. Muzzle rings are
    gone; wall strikes spark, and first-person shots show a six-point star flare.
  - **HUD:** team score chips with fill bars, a central clock, a captor-tinted zone
    pill, a vitals panel, a compact weapon panel and ability chips.
  - **Laser kit:** a Sol art pass on the Line Driver, SMG, pistol and sniper.
  - **Framing:** the Pulse Lobber sits smaller.
  - **Slide body:** entry no longer puts pelvis armour across the view, and a
    sustained slide shows a shin and boot rather than an open belt bowl.
  - **Diagnostics:** the weapon-capture fixture waits for the equip raise. The gunplay
    smoke no longer reuses an action sequence or fires at the cooldown edge.
    Injected-latency smokes get a longer hang guard.
  Evidence:
  - EditMode 454/456 and PlayMode 150/151, with explicit skips only;
  - all four packaged visual modes;
  - eight real Photon runs on the final build, four of them at adverse150.
  [QA](QA.md#2026-10-30-presentation-follow-up-spawn-view-shot-reads-hud-slide-body)

- **Music (D032, 2026-10-30), uncommitted.** The menu and map loops are now
  instrumental chill-house takes from a local ACE-Step 1.5 run, approved by the user.
  `Tools/Music/loop_takes.py` cuts them into locked, byte-reproducible loops. Takes
  whose vocal stems carried vocal-like content were rejected.
  - New `MusicImportTests` check that Unity keeps each loop's exact length.
  - A new `Invoke-VisualCheck.ps1 -Music` mode checks that the packaged player
    streams the theme and all three map loops. Batch-mode players run without audio.
  - The synthesized set stays rebuildable with `build_music.py`.

  Evidence and limits are in [QA](QA.md#2026-10-30-music-ace-step-chill-house-loops-d032).
  No one has listened yet.

- **Recorded weapon and movement audio (D033, 2026-10-30), uncommitted.** The
  synthesized cues are replaced by 14 recorded CC0 banks copied from `vibegame1`
  with their GUIDs. Each weapon has its own bank and pitch. Other players now make
  positional footsteps, jumps, landings, dashes, slides and reloads; menu clicks are
  recorded too. `-Music` now also checks that a recorded shot reaches the mixer in
  the packaged player. Evidence and limits are in
  [QA](QA.md#2026-10-30-recorded-weapon-and-movement-audio). Not heard yet.
- **Laser beams (2026-10-30), uncommitted.** First-person beams now leave the drawn
  barrel instead of starting toward the screen centre, and a dark sheath keeps them
  readable against bright stone and sky.
  [QA](QA.md#2026-10-30-laser-beams-barrel-start-and-daylight-contrast) has the
  paths and the limits.
- **Maps, weapons and fire feedback (D034, 2026-10-30), uncommitted.** The maps now
  use the `visual-polish` look (bright ivory and cool-grey 4 m panels, neutral
  light, pale haze), rebaked. All eleven weapons got an energy art pass (coils,
  cells, emitter rings; a longer energy blade on the sword). Their `Signal*` parts
  now emit, which they never did before. They also flare and heat up when firing.
  Laser strikes burn cover, and local shots add a small cosmetic view punch.
  Remote soldiers flash white on a confirmed hit (brighter on a headshot), and
  glowing strips under each overhang show where its baked ceiling light comes from.
  Long laser beams keep their on-screen width out to 42 m, and the sniper rail is heavier.
  The soldier materials are now flagged realtime emissive. Without that flag, a
  `CaptureViewmodel` run stripped their emission and left the team glows dark.
  `CaptureViewmodel` also renders map overviews. Full suites, all five packaged
  modes, the frame-time sample and the playable build are in
  [QA](QA.md#2026-10-30-maps-weapons-and-fire-feedback). Not playtested yet.
- **Host removal reasons (2026-10-30), uncommitted.** For the external
  join/team-click disconnect, the host now logs
  `WILDSTONE HOST REMOVED PEER reason=<code> phase=<phase>` whenever it removes a
  peer. The line carries no identifiers. Five real Photon runs pass without
  reproducing the drop. Ask for the host's Player.log after the next failure. See
  [QA](QA.md#2026-10-30-host-removal-reasons).
- **Firefight sequences (2026-10-30), uncommitted.**
  `Invoke-VisualCheck.ps1 -Firefight` captures real Line Driver, SMG, shotgun and
  sniper headshots on a strafing soldier, an incoming sniper rail, and grenade and
  Pulse Lobber blasts, frame by frame. What they showed is now fixed:
  - the sniper rail was thinner than the Line Driver's;
  - the sniper's first-person flare was still violet;
  - the shotgun's barrels crowded the centre of view;
  - forced captures drew skinned arms one frame stale.

  See [QA](QA.md#2026-10-30-firefight-frame-sequences).
- **Map traversal (2026-10-30), uncommitted.** Every arena build now checks that
  each start reaches every pickup and zone point (and back) by walking and plain
  jumps. All three maps pass. The same check measures travel distance: Terrace
  Steps' northern teams spawn under the raised courts and walk 29-55% farther than
  team 0 to weapons and the zone. Fixing that changes spawn-area geometry, so it waits for the
  user.
- **Outer walls (2026-10-30), uncommitted.** A plain or running jump from the +4 m
  floors could land players on top of the 6 m outer walls on all three maps.
  Map revision 4 raises the walls to 8 m, rebakes the lighting, and the arena build
  now fails if any wall top is within jump reach. Built then as `889c10b8…`, since
  replaced ([QA](QA.md#2026-10-30-playable-build-refresh)).
  See [QA](QA.md#2026-10-30-ledge-jumps-and-outer-walls). See [the map spec](superpowers/specs/2026-10-25-wildstone-modern-map-design.md)
  and [QA](QA.md#2026-10-30-map-traversal-and-travel-distance).

- **Soldier outline (2026-10-30), uncommitted.** Ivory soldiers disappeared against
  ivory stone at 10-15 m, and the hit flash turned them into a white blob. Remote
  soldiers now get a thin dark outline on both quality tiers. Tests, all five
  packaged capture modes and the frame-time sample pass. See
  [QA](QA.md#2026-10-30-soldier-outline). Width and colour need a look in play.

- **Pickup pads (2026-10-30), uncommitted.** Weapon and item pickups now sit over
  a dark floor pad with a ring in the item's colour. After a pickup, the ring turns
  into a dim arc that refills as the item respawns. See
  [QA](QA.md#2026-10-30-pickup-pads).

- **Movement lens and dash streaks (2026-10-30), uncommitted.** Movement now has
  vibegame1's camera punctuation, scaled to this lens:
  - FOV kicks on dash, jump, wall jump and launch; a narrowing kick and a small
    downward nod on hard landings;
  - a punch on leaving a slide and a hold while wall-running;
  - an extra kick on a perfect wall jump, a bank into a strafe dash, and a nod as a
    slide commits;
  - speed streaks that stream past the lens on a dash.
  All of it is cosmetic. The eye stays authoritative, ADS fades it out and reduced
  motion removes it. See [QA](QA.md#2026-10-30-movement-lens-and-dash-streaks).

- **Shotgun hit sound fix (2026-10-30), uncommitted.** A shotgun blast used to
  play its hit tick and hit cue once per pellet that landed, up to eight stacked
  copies. It now plays them once per shot. See [QA](QA.md#2026-10-30-shotgun-hit-sounds).

- **Review fixes (2026-10-30), uncommitted.** An independent review of the
  afternoon's code found three real bugs, now fixed:
  - a kill could be credited to whoever landed a 0-damage hit later in the same
    tick, which also gave them the banner and streak;
  - shotgun pellets alternating between two targets re-played hit cues;
  - the knee tuck ran into the slide's release tail.
  See [QA](QA.md#2026-10-30-review-fixes-kill-credit-and-pellet-cues).

- **SMG held-fire check (2026-10-30), uncommitted.** The intermittent online SMG
  failure was the test harness, not the rules. A slow test client let bloom decay
  between shots, and the check expected it to keep growing. The check now allows
  that, and gunplay passes at baseline and adverse150. Held fire no longer waits a
  client frame per shot after the interval; see the
  [online held-fire cadence](QA.md#2026-10-30-online-held-fire-cadence-d036). See
  [QA](QA.md#2026-10-30-smg-held-fire-verdict).

- **Online held-fire cadence (2026-10-30, D036), uncommitted.** Held SMG and Line
  Driver fire online now keeps its interval. The host accepts an automatic shot
  slightly early and keeps the schedule, and the client repeats on its own schedule
  instead of waiting for the replicated cooldown. Two-process gunplay went from 4 SMG
  shots in a 0.6 s hold to 7 of 7 at baseline and adverse150. QA also records why
  strafing targets are under-compensated at 150 ms RTT (the accepted rewind cap). See
  [QA](QA.md#2026-10-30-online-held-fire-cadence-d036).

- **Kill banner (2026-10-30), uncommitted.** Your kill now names the victim under
  the crosshair, with `HEADSHOT` spelled out. Quick follow-ups within 4 s count up
  `DOUBLE`, `TRIPLE`, `×N`. See [QA](QA.md#2026-10-30-kill-banner).

- **Renamed Better Bean Battles (2026-10-30, D038), uncommitted.** This was the user's
  request. The window title, title-screen wordmark and loading heading changed.
  Code, folders, logs and the `WildstoneArena.exe` file keep the internal Wildstone
  name. Unity keys saved settings by product name, so they reset once, and the
  player log moves to `...\LocalLow\Wildstone\Better Bean Battles\`. See
  [DECISIONS](DECISIONS.md).

- **Online rematch (2026-10-30), uncommitted.** After a normally finished match,
  the host's Results offers Rematch. The whole room returns to its lobby with the
  same code, seats and teams, and ready cleared; clients follow. A two-round
  two-process check passes three times. See
  [QA](QA.md#2026-10-30-online-rematch-and-night-playable-build).

- **Late peer identity (2026-10-30), uncommitted.** 100 reconnect cycles exposed a
  race: the SDK sometimes reports a joining peer before its user ID, and the host
  removed that peer as `invalid-peer`. It failed 1 of 15 cycles before the fix. The
  host now retries that peer for up to 5 s, and 100 of 100 cycles passed after the fix.
  It may explain the earlier unexplained startup disconnect, but that is unconfirmed.
  See [QA](QA.md#2026-10-30-reconnect-cycles-and-late-peer-identity).

- **Push Lens deflection (2026-10-30, D037), uncommitted.** A Push Lens now sends
  the nearest enemy orb, disc, stun bolt or grenade in its cone back along the push,
  once. The pusher then owns it, so it can hit its original shooter. A real
  two-process check passes with the host pushing and with the client pushing, at
  baseline and adverse150. That check caught a pooled-projectile bug, where the next
  projectile could be born already deflected; it is fixed. See
  [QA](QA.md#2026-10-30-push-lens-deflection-d037).

- **Measured rewind age (2026-10-30), uncommitted.** The host now records the view
  age behind every hitscan query. At 80 ms RTT every query exceeded the accepted
  150 ms cap (mean 184 ms requested). Strafing targets are under-compensated at an
  ordinary ping. The cap is unchanged; whether to raise it is the user's call. See
  [QA](QA.md#2026-10-30-measured-rewind-age).

- **Weapon read fixes (2026-10-30), uncommitted, captured.** The grenade shows in the
  support hand while the throw key is held. Muzzle lamps are dimmer and the bottom HUD
  panels firmer. The sniper rail is heavier and the sword swing is a crescent. The
  two-line title no longer makes the 720p title menu scroll, and the kill banner
  clears the muzzle. Tests and captures pass;
  Ultra frame time is about 0.15 ms higher on average than the last sample, with p99
  in the same range. None of it has been played. See
  [QA](QA.md#2026-10-30-weapon-read-fixes-grenade-in-hand-sniper-rail-sword-slash-title-fit).

- **30-minute capacity soak (2026-10-30).** Eight processes held a real Photon room
  for 1800 s (480 s Running, then Results) and passed. Working set grew 4.5–8 MB per
  peer; only start and end values are kept. It has no gameplay actions, so the
  backlog's gameplay soak and the two-hour soak are still open. See
  [QA](QA.md#2026-10-30-30-minute-capacity-soak).

- **Stun end on a predicting client (2026-10-30), uncommitted, in the `5ac42eed…`
  build.** A stunned player on a client used to stay frozen about one round trip past
  the stun and then jump forward. The movement snapshot now carries the stun's end
  tick, so the client releases on the host's tick. Suites and seven real-Photon
  regressions pass. The new two-process `stun-client` smoke (2026-10-31) makes the
  client the target. It releases exactly on the end tick at about 10 ms and 163 ms
  RTT, with no catch-up jump. See
  [QA](QA.md#2026-10-30-stun-end-on-a-predicting-client) and
  [the proof](QA.md#2026-10-31-stunned-client-two-process-proof).

- **Gameplay soak and allocation figures (2026-10-30/31), uncommitted, smoke-only.**
  `Invoke-NetworkSmoke.ps1 -Scenario capacity -Gameplay` makes all eight peers move and
  fire and the host rematch every round; `-SoakSeconds` reaches 7200. A 100 s run
  passes:
  - 4 rounds; about 175 shots per peer;
  - 0.4 GC collections/s per peer, once the harness stopped allocating per frame;
  - heap about 20–24 MB;
  - working set +14–17 MB.

  The perf sample showed about 1.4 KB of managed allocation per frame and no
  frame-time regression (Ultra p99 2.49 ms against 2.78–2.99 ms before). The HUD
  now rebuilds a label only when its value changes, which cut that to 560 bytes
  and 3 collections per sample
  ([QA](QA.md#2026-10-31-hud-string-allocations)). The
  two-hour run failed at about 46 min on a coherence check, not yet diagnosed. See
  [QA](QA.md#2026-10-31-gameplay-soak).

- **Map name card and fixture FOV (2026-10-31), uncommitted.** Each round opens with
  the map's name for 3 s, and the hint line keeps it. The tester read the three
  maps as one, because they share a kit and spawn structure; making them look
  different is still a proposal. Capture fixtures now use the game's default view.
  Before, they used 90 degrees vertical and drew the world about 1.5x smaller. The
  `-Firefight` mode adds sword and 15/30/45 m range views. The Laser Sword slash is
  now a blade-mask smear 1 m out, its bright head leading the swing in front of the
  target; before, it was a thin streak hidden behind the target. See
  [QA](QA.md#2026-10-31-map-name-card) and
  [captures](QA.md#2026-10-31-firefight-sword-and-range-captures-fixture-fov).

- **Smoke harness (2026-10-30), uncommitted, smoke-only.** Peer status reads keep the
  last good copy, so a briefly locked file no longer restarts stability windows.
  Each run records a stage timeline. A host-side stun probe checks movement while
  stunned. Projectile and grenade fixtures re-send an attack the host never
  acknowledged. See
  [QA](QA.md#2026-10-30-smoke-harness-peer-reads-stun-probe-and-late-attacks).

- **No legs in view while sliding (2026-10-30), uncommitted.** The user reported the
  body getting into the view during a slide as a bug. Your visible legs now hide
  from the first slide frame until the slide has settled, at any look angle; your
  shadow stays whole, and the legs show again right after. This replaces the slide
  knee tuck and the steep slide leg hide, which still left loose shins and boots in
  some slide frames. See [QA](QA.md#2026-10-30-no-legs-in-view-while-sliding).

- **Remote strafe and backpedal cadence (2026-10-30), uncommitted.** Other players
  strafing or backpedalling at full speed cycled their legs about 6 steps a second,
  against 4.2 for running forward. The back and side runs now bound with a longer
  flight, so every direction runs at about 4.2. Wall runs now speed their cycle up
  instead of sliding. See
  [QA](QA.md#2026-10-30-remote-strafe-and-backpedal-cadence).

- **Remote wall-run lean (2026-10-30), uncommitted.** Other players' wall runs
  now lean 30° with their boots meeting the wall. Before, they floated 0.3 m off
  the wall with the head well outside their hit capsule. See
  [QA](QA.md#2026-10-30-remote-wall-run-lean).

- **Remote slide scrape (2026-10-30), uncommitted.** Other players' slides now
  scrape from where they slide, so you hear a slide coming online. See
  [QA](QA.md#2026-10-30-remote-slide-scrape).

- **Resent pickups no longer vanish (2026-10-30, D035), uncommitted.**
  - On a lossy connection, a resent pickup, reload or weapon swap could arrive just
    over the host's 150 ms limit and be silently ignored. You pressed F and nothing
    happened.
  - Those actions now get 500 ms. Shots, grenades, Blink and shield keep 150 ms.
  - Adverse-network gunplay went from 3 of 8 passing runs to 5 of 5. Every other
    Photon scenario still passes. See [QA](QA.md#2026-10-30-resent-inventory-actions).

- **Pistol beam (2026-10-30), uncommitted.** The pistol's shot is wider and lasts
  a little longer, so its heavy hits read on bright stone. See
  [QA](QA.md#2026-10-30-pistol-beam).

- **Low-ammo warning (2026-10-30), uncommitted.**
  - The ammo count turns red in the last quarter of a magazine, and each shot there
    clicks dry, so you know to reload before the gun runs empty.
  - The practice menu and HUD now name the strafing target instead of a stationary
    one.

  See [QA](QA.md#2026-10-30-low-ammo-warning).

- **Near-miss zip (2026-10-30), uncommitted.** Enemy beams that pass close to
  your head whoosh by from the side they passed, so being shot at is heard even
  when you are not hit. See [QA](QA.md#2026-10-30-near-miss-zip).

- **Hit tick and slide scrape (2026-10-30), uncommitted.**
  - Body and shield hits now tick quietly, so you hear that you connected at any
    range. The head ding and kill tone are unchanged.
  - Slides scrape along the floor, with pitch and level following the speed you
    still carry.

  See [QA](QA.md#2026-10-30-hit-tick-and-slide-scrape).

- **Speed wind (2026-10-30), uncommitted.** A wind loop rises as you move faster
  than a run, so dashes, slides, launches and hop chains are heard as speed. A
  plain run stays quiet. See [QA](QA.md#2026-10-30-speed-wind).

- **Strafing practice target (2026-10-30), uncommitted.** The Practice target no
  longer stands still. It faces you and strafes, pauses, jumps and dashes around its
  spawn, so tracking, spread and headshots can be tried against a moving body. It
  never shoots. See [QA](QA.md#2026-10-30-strafing-practice-target).

- **Weapon voices and empty-trigger reload (2026-10-30), uncommitted.**
  - The pistol and SMG now use a separate recorded laser "knock". The sniper and
    shotgun get a recorded boom tail. The guns no longer all sound like one pitched
    clip.
  - Pulling the trigger on an empty Line Driver reloads it. On a spent special it
    switches back to the Line Driver.

  See [QA](QA.md#2026-10-30-weapon-voices-and-empty-trigger-reload).

- **Crosshair rim (2026-10-30), uncommitted.** The ivory crosshair ticks nearly
  vanished on ivory stone. Each tick and the dot now has a 1 px dark rim, and the
  hitmarker has a dark outline. See
  [QA](QA.md#2026-10-30-crosshair-rim-and-movement-follow-ups).

- **Shield shell and stun halo (2026-10-30), uncommitted.** A shielded player now
  shows a clear energy shell fitted to the body that dims as the shield drains; the
  old ring was invisible in captures. A stunned player gets a green crackle around
  the head. A breaking shield's shell flares and swells for 0.22 s. See
  [QA](QA.md#2026-10-30-shield-shell-and-stun-halo).

- **One-frame T-pose fix (2026-10-30), uncommitted.** Remote soldiers drew their
  T-pose on the first frame after spawning and after every respawn, because animator
  culling skipped bodies not yet on screen. They now always animate. The old
  `weapon-remote-hit` and firefight frame-0 captures showed this. See
  [QA](QA.md#2026-10-30-remote-soldier-t-pose-frame).

- Menu revamp (2026-10-29, user request; authored, managed checks only): animated
  live-arena title backdrop (camera drift around a posed four-team squad), vibegame1
  title/column look, button motion, a dive-and-curtain arena entry and looping
  menu/map music with crossfade, pause duck and a conditional Music slider. The
  [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md#menu-backdrop-motion-and-music-2026-10-29-user-request-implemented-not-natively-run)
  owns behavior and proposed values; [SOURCE-REUSE](SOURCE-REUSE.md) records the
  ported files and the copied OFL Barlow font. Not yet run in Unity: USS/font import,
  composition per map, soldier poses, dive feel and audio need a native run and
  visual pass. Music clips come from the music lane.

- Grenade, Rebound Disc and Stun Gun now pass controlled real-Photon baseline and
  adverse150 acceptance. Checks cover gravity/bounce/fuse and blast cover; two disc
  ricochets without repeated damage; and stun expiry/immunity/renewed stun with
  matching peer timers. Duplicate special-fire RPCs spend once. Sol implementation
  and review plus native builds/runs are recorded in [QA](QA.md). These diagnostics
  leave normal gameplay/public builds unchanged. One pre-game requested disconnect
  remains unexplained; add safe admission-rejection reason logging before inferring
  a fix or linking it to the user's external team-selection report.

- Projectile/fault-profile continuation: Pulse Lobber flight, damage and cover
  pass baseline and added 150 ms RTT/jitter/2% loss; all three moving lasers pass
  added 80 ms/1% and 150 ms/2% profiles. Both peers record positive SDK RTT and
  matching authoritative verdicts. Nine normal session checks pass. This is
  controlled same-PC functional evidence; deflection remains unimplemented and
  full fairness/rewind/external/performance gates remain open. Diagnostic-only
  session config copies leave production/public players unchanged. [QA](QA.md)
  records exact builds, failed attempts and final runs.

- Latest diagnostic follow-up: real Photon moving-target/cover checks pass for
  pistol, shotgun and sniper, with matching damage/ammo/events across peers and
  zero damage through test cover. Continuous first-person starter-weapon samples
  pass slide/recoil/reload return gates; 32 frames inspected. Fixed offscreen
  capture readiness under OS-clamped windows; existing 13-pose and 16-screen modes
  pass again. Production/public players are unchanged. [QA](QA.md) owns paths and
  limits: projectiles, adverse latency, remote moving readability and human feel
  still need acceptance.

- 2026-10-29 continuation: native **49 EditMode/32 PlayMode** focused checks pass
  after fixing aborted online results, containing rejected recovery callbacks and
  adding remote firing/reload handling. Live controlled three-laser pickup/equip/
  damage/event verification, reconnect and abrupt-host-crash recovery pass locally.
  User reports good play overall with initial connection trouble; exact cause is
  still unconfirmed. [QA](QA.md) records red/green evidence and remaining limits.
  These fixes are local and are not in the public `e9e091f` preview.

- Earlier delivery: source snapshot `316a00f` was built and published as public
  [preview-20261028-316a00f](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261028-316a00f)
  at the user's request and explicit public-visibility confirmation. Build, headless
  startup and ZIP/digest checks pass; [QA](QA.md) records exact evidence. Concurrent
  edits made after capture remain outside that release. No new online/visual acceptance.

- D026 implemented: laser pistol/shotgun/sniper, held rapid starter fire, distinct
  models/handling/audio and bounded colored blast effects. Native155 EditMode/51
  PlayMode plus35 final focused regressions pass. Latest public snapshot `e9e091f`
  includes the completed continuation; the earlier `316a00f` preview predates final edits.

- Implemented continuation: visible first-person legs, articulated hands/arms and stronger
  slide presentation requested by the user (D025). Source audit confirms slide
  simulation tuning already matches `vibegame1`; changes are cosmetic and use
  Wildstone geometry. Focused native checks pass 35/35; thirteen packaged pose
  captures were inspected, and fresh Photon combat/reconnect regressions pass.
  Human animation/slide feel remains open. [QA](QA.md) records exact evidence.

- GitHub delivery is authorized: now-public repository `Capsfan900/multiplayer_1`
  and a manual Windows Practice prerelease workflow. [TOOLING](TOOLING.md#github-builds-and-private-releases)
  owns operation and access constraints; [QA](QA.md) records publication evidence.
  A fresh clean-source Windows build was packaged and published locally as private
  [preview-local-20261027-e3f5815-1](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-local-20261027-e3f5815-1).
  Automatic approval review blocked runner registration even after user authorization;
  manual setup and the first Actions build remain pending. No runtime/online acceptance
  was added by publication.

- Unity 6000.5.10f1 licensing, import, Compile and full content preparation now work.
- Fusion 2.1.3 Stable build 2390 is imported; the user-supplied App ID is configured
  in ignored local Photon settings and survives reload. A fresh native compile passes.
  An explicit native live smoke now creates a private Photon Host room, admits its
  guest, changes team/readies and closes cleanly. Separate Windows processes also
  pass real Photon gameplay, reconnect, movement/fire deduplication, controlled
  stationary hit replication, late admission, eight-player capacity and
  graceful/crash host migration. Real
  external-network and performance acceptance remain open. [QA](QA.md) owns evidence.
- Native EditMode, PlayMode, explicit preparation and fresh managed checks pass;
  [QA](QA.md) records their exact totals and run identities.
- All three maps build and bake in Unity. Axis/anchor/bounds/normals/material checks
  pass; human animation loop contracts and attachment checks pass.
- Four Python map regressions pass. Fourteen model/source hash pairs and all 73
  saved map meshes were verified without regenerating the approved source assets.
- Current playable build (2026-10-31 02:15): `Artifacts/Unity/Windows/WildstoneArena.exe`,
  hash `b563bab3…` (new hash method), not yet played; see Resume here above. It
  replaced the 00:32 build `5ac42eed…`, archived as `Windows-5ac42eed`, which was
  not played either. On top of the 23:33 build `db84d55e…`, `5ac42eed…` added the
  stunned-client release fix, the sword slash smear and the map name card; both
  players should use it ([QA](QA.md#2026-10-31-playable-build)). The 23:33 build,
  named Better Bean Battles (D038), had added since the 16:06 build:
  - D036 held-fire cadence;
  - no legs in view while sliding;
  - D037 Push Lens deflection;
  - online rematch and the late peer identity fix
    ([QA](QA.md#2026-10-30-online-rematch-and-night-playable-build));
  - the weapon read fixes: grenade in hand, dimmer muzzle lamps, firmer HUD panels,
    heavier sniper rail, crescent sword slash, title fit and kill banner
    ([QA](QA.md#2026-10-30-late-night-playable-build)).

  The unplayed 19:18 `ece027dc…` and 19:44 `625026d1…` builds were overwritten. The 23:33
  build `db84d55e…`, the 20:15 build `058fa3dd…`, the 16:06 build `d838ac03…`, which
  the user played, the 15:08 build `76f03229…`
  ([QA](QA.md#2026-10-30-second-playable-build-refresh)) and the 12:39 build
  `d6d14b5a…` are kept under `D:/WildstoneArchive/mvp-2/Artifacts/Unity/`.
- Older local Windows build: `D:/WildstoneBuilds/resume-20261029/Artifacts/Unity/Windows/WildstoneArena.exe`.
  It includes the 2026-10-29 fixes; see QA for verification.
  Historical laser preview: `Artifacts/Unity/Windows-20261028-203918-018/WildstoneArena.exe`.
  Photon-only Host/Join and offline Practice are packaged; no account login.
  Build succeeded and 15-second headless startup showed no logged exceptions.
  Source stayed unchanged during compilation (1,173 inputs; 200 output hashes).
  This historical local build preserved uncommitted inputs. The current source is
  now committed and rebuilt in the public preview above; full online MVP acceptance
  remains open. [QA](QA.md) records commands/manifests.
- Unattended polish fixes menu focus, HUD lifetime/layout, settings scrollbar contrast
  and visible recovery status. Runner-local discovery preserves late avatar changes
  without rebuilding unchanged presentation registries. Focused native checks pass
  (65 EditMode, 17 PlayMode); allocation measurement is unavailable after calibration.
- Sixteen packaged offscreen captures were inspected across menus and all three maps,
  including 720p, maximum UI scale, 1080p and ultrawide title. Settings contrast and
  static layouts pass. Desktop input remains untouched; physical input, moving
  readability and human feel remain open.
- Eight separate Photon processes held coherent gameplay for five minutes, with
  positive Windows memory observations and clean logs/exits. Fresh late-join,
  combat and abrupt-host-crash regressions pass. This is same-PC functional evidence,
  not external-network, eight-machine performance or endurance acceptance.

[QA](QA.md) owns exact run paths, failed attempts, coverage and remaining acceptance.
[TOOLING](TOOLING.md) owns reproducible commands; [MVP progress](MVP-PROGRESS.md)
preserves execution history. Do not carry historical licensing failures forward as
current blockers, or use passing local rules as evidence of internet behavior.

## Changes during this resume

Native execution exposed and fixed empty-startup scene creation, Unity missing-component
null handling, recovery of an interrupted animation controller, imported model axis
handedness, character capsule query registration at startup, negligible Blink landing
acceptance and lost short input taps across input updates. Native replay permits tiny
bounded PhysX vector differences while preserving exact timers and discrete state;
network acceptance targets are unchanged.

An independent Sol review found one test fixture that bypassed the shared author-scene
preservation guard. It now uses that guard and its native tests pass. The suspected
temporary-prefab dirty-scene problem did not reproduce in full Prepare; no speculative
isolation layer was added. CPU bake teardown now handles an initially empty batch scene set.

## Next actionable work

The user resumed the full backlog on 2026-10-29 and reports that playtesting worked
well overall, with an issue after joining and selecting a team. The other player
was in Texas on a different ISP, confirming user-reported external play. Exact
build and error wording are pending; the user identifies disconnect/menu return
after the team click. This is not a measured external-network pass. A new actual
team-switch diagnostic covers a gap in the older tests, which could keep players
on their automatically assigned teams without invoking the team-change RPC.
Current local work has added live three-laser RPC verification, fixed the identified
aborted-round results transition and recovery callback escape, and implemented
remote weapon handling. Moving/full-loop checks, connection symptom reproduction
and measured performance follow. [BACKLOG](BACKLOG.md) owns the prioritized queue;
the implementation plan has been reconciled against recorded evidence so completed
SDK, identity, content and menu work is not repeated.

0. User playtest of the D028–D031 build (the 16:06 `d838ac03…` playable; the 20:15
   `058fa3dd…` build adds the changes since). It began
   on 2026-10-30. First reaction: "so far that is really good and like those
   improvements, keep going". That is general approval, not per-item acceptance. Judge ADS,
   spread, headshots and the SMG; the viewmodel and slide; the new HUD; and team
   readability, maps and music.
   - Also judge the 2026-10-30 additions listed under Current evidence: weapon
     voices, empty-trigger reload, the strafing target, speed wind, hit tick, slide
     scrape, near-miss zip (online), low-ammo click and red count, pistol beam,
     remote slide scrape (online), single shotgun hit sounds, the kill banner, the
     remote wall-run lean, the remote strafe cadence, the online held-fire cadence,
     no legs in view while sliding, Push Lens deflection (D037), online rematch and
     the Better Bean Battles title screen (D038). The rename resets saved settings
     once.
   - Tune the lag-compensation cap. The 150 ms cap clamped every shot at an
     ordinary 80 ms ping ([QA](QA.md#2026-10-30-measured-rewind-age)) and dropped
     resent attacks under loss. D040 raised the cap and attack window to 250 ms as a
     proposal, which covers regional80 shots
     ([QA](QA.md#2026-10-31-playtest-fixes-d040)).

   Tune from that feedback. Spawns now face the exit of the arrival screen, but the first
   view is still a walled alley; opening it would mean a map geometry change. Listen
   to the D032 music in menus and matches against the D033 weapon and footstep
   sounds; their gains are proposals in `ArenaAudio`. Reject any track by
   name, and `loop_takes.py` can swap in another archived seed. An FL Studio rerender
   still needs the MCP connected.
1. Continue packaged physical-input and moving-readability checks in [QA](QA.md)
   when desktop control is available. Static menu/settings/all-map captures are
   inspected; rig animation, traversal, pickups, death/respawn, zone completion and
   human feel still need direct acceptance. Do not repeat completed static checks.
2. SDK provisioning is complete: Fusion 2.1.3 Stable build 2390 is imported and the
   user-supplied App ID is configured locally. Native compile and package/file-GUID
   checks pass; evidence is in `Artifacts/Resume/photon-setup.md` and [QA](QA.md).
   Live private-host service validation passed. Continue remote gameplay/recovery;
   do not request the SDK or App ID again.
3. The networking continuation now connects Photon guest host/join, peer-bound
   admission, lobby/team/ready/load, network prefabs, match simulation, combat state,
   hitscan history, shared-orb rollback and existing presentation. Late join,
   reconnect and complete host-recovery hooks are integrated. Final focused native runs passed 248 EditMode and 61 PlayMode checks, with no
   skips; later separate-process Photon tests pass gameplay, reconnect, movement/fire,
   eight-player capacity, graceful migration and abrupt-crash recovery. [QA](QA.md)
   owns the failed attempts, fixes and passing reruns; external acceptance remains open.
4. **No PlayFab (D023).** Its client and account UI have been removed. Photon-only
   private matches use a temporary guest ID and match credential in memory; no email,
   password, Title ID or profile backend is required. Held-seat recovery requires
   the same credential; app restart and cross-device account recovery are unsupported.
   Do not ask for PlayFab setup again. See [USER-SETUP](USER-SETUP.md).
5. Complete external-network, eight-machine,
   endurance and measured latency/performance gates from the foundation spec.
   Only after acceptance should a release-quality package be produced.

No paid services or new accounts were created. Public previews were published at
the user's request; publication does not complete multiplayer acceptance. Do not
re-audit the source projects or regenerate approved art merely because this is a new
worktree. Preserve existing asset GUIDs and editable Blender sources. The accepted
roster, Blink/shield-only consumables, Host Mode and art direction remain in their
[canonical owners](README.md) and [decision record](DECISIONS.md).
