# Playtest fixes to make later (2026-10-31)

This file holds root causes and recommended fixes for the external-playtest reports,
plus one later report about the hot zone. It started as analysis only. The fixes were
implemented later on 2026-10-31 ([D040](DECISIONS.md)); see
[Implementation status](#implementation-status-2026-10-31). Each item gives:

- **Confidence.** *Confirmed in code* means the code path explains the symptom. *Measured*
  means existing QA numbers back it. *Likely* means it is plausible, but needs a tester's
  `Player.log` to prove.
- **Fix.** The smallest change at the shared cause.
- **Check.** The test that would catch a regression.

Some values below are proposals for the owner to accept. They change balance or
fairness, and the canonical specs own them:

- the rewind cap and attack window:
  [foundation spec §6](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#6-combat-lag-compensation-and-fairness);
- ammo, spread and Blink: the [combat spec](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md);
- the HUD: the [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md);
- the zone look: the [rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md).

The health-pack work that is in progress edits these files:

- `ArenaLayouts.cs`;
- `WeaponCatalog.cs`, `ArenaMatch.cs` and `CombatRules.cs`;
- `ArenaHud.cs`;
- the pickup visuals in `ArenaEffects.cs`.

Fixes 3, 7, 9, 10 and 12 touch those files, so rebase them on that work. Line numbers
below are from the uncommitted tree on 2026-10-31.

| # | Report | Cause, in one line | Confidence |
| --- | --- | --- | --- |
| 1 | Changing settings gets stuck | Apply replaces the input object. That resets the online movement sequence, so the host drops every input until you die. | Confirmed in code |
| 2, 4, 11 | Hitbox in the wrong place, moving hitbox, random no-reg | The 150 ms rewind cap is clamped at ordinary ping. Late attack RPCs are dropped. The box doesn't match the model, or Practice. | Measured, and confirmed in code |
| 3 | Can't pick up while running | Pickup choice and prompt use a lagged copy of your own position. The reach is 2.5 m and an early press is lost. | Confirmed in code |
| 5 | Main menu or lobby closes the match at random | Rejoining a room you left gets you kicked for a credential mismatch. Any client exception, including one bad 20 Hz snapshot, ends the match. | Confirmed (host log), and likely |
| 6 | Recoil too bouncy | Weapon sway reads the camera's recoil punch as mouse movement and bounces on an underdamped spring. Recoil snaps instantly. | Confirmed in code |
| 7 | Red hit marker | Two red marks sit near the crosshair: the kill ✕, and the damage-direction bar 120 px above center. | Confirmed in code; needs the tester's meaning |
| 8 | Sniper doesn't fire properly on the press | Online fire isn't predicted (round-trip delay). A press during the cooldown is dropped. The scope appears at 90 % zoom, but full accuracy needs 100 %. | Confirmed in code |
| 9 | What is Blink? | Nothing in game explains it. It fails silently unless you aim almost level at reachable floor. | Confirmed in code |
| 10 | More ammo from gun pickups | A special weapon gives exactly one magazine, with no reserve and no reload. | Confirmed in code |
| 12 | Hot zone blends into the map | The idle colour is palette grey. The floor decal is at most about 23 % opaque. The curtain and beacon are additive, so they vanish over ivory stone and the bright sky. There is no on-screen waypoint. | Confirmed in code |

## Implementation status (2026-10-31)

Implemented under [D040](DECISIONS.md). Every value is a proposal for you to tune.
Evidence is in [QA](QA.md#2026-10-31-playtest-fixes-d040). The specs own the
contracts. Managed and native tests and the Photon regression set pass; all fixes are
in the `b563bab3…` playable build. A human playtest is still needed before any item
counts as fixed for players.

| # | Status | What changed | Still open |
| --- | --- | --- | --- |
| 1 | Implemented | The new input object continues the movement sequence. | Online rebind smoke |
| 2, 4, 11 | Implemented | Rewind 250 ms, attack window 30 ticks, hitbox buffer 300 ms. One torso box plus head sphere in both worlds. | Proxy collider on the interpolated pose (fix 4); regional80 no longer clamps, adverse150 still clamps at 250 ms |
| 3 | Implemented | Prompt and choice use predicted feet; the host accepts 0.5 m extra. | Retrying a press made just before reach |
| 5 | Implemented | Per-run credential per room; 2 s of refused checkpoints tolerated; "Host left the match". | `FusionMenuController` connection-lost text; fast rejoin before the host processes the leave |
| 6 | Implemented | Sway follows aim on a critically damped spring. | The eased recoil curve needs your OK |
| 7 | Implemented (bar) | The damage mark is a 35%-height arc. | Which red mark you meant; the kill ✕ is unchanged |
| 8 | Implemented | Exact from the scope threshold; an early press fires at ready. | Predicted local shot cosmetics |
| 9 | Implemented | Horizontal aim; prompt names the key and 6 m; refusal cue; Practice line. | None |
| 10 | Implemented | Two magazines per pickup (catalog revision 5). | The count |
| 12 | Implemented | Magenta free zone over a dark band, painted curtain and beacon, HUD waypoint. | Luminance captures of each map |

## 1. Changing settings gets stuck

**Symptom.** After pressing Apply in Settings, the player can look around but can't move,
jump or slide. It clears after a death.

**Root cause (online only).** Practice is unaffected, because
`PracticeSession.SimulateMovement` stamps its own sequence.

- `PracticeSession.ApplySettings` disposes the input object and builds a new one
  (`Assets/Wildstone/Runtime/Game/PracticeSession.cs:91`). That creates a new
  `MotorInputBuffer`, whose sequence restarts at 1
  (`Runtime/Movement/MotorInputBuffer.cs:21`).
- Online samples carry that sequence (`FusionGameCoordinator.CollectInput`, `:307`).
  Host and client both drop any sample whose sequence is not above `LastSampleSequence`
  (`Networking/Runtime/FusionMotorInput.cs:12`).
- After minutes of play `LastSampleSequence` is in the tens of thousands, so every new
  sample is ignored. Only a new life resets it to −1 (`FusionPlayer.cs:153`).
- `PracticeInput.Clear` already says "Keep the monotonic movement sequence". The Apply
  path breaks that rule.

**Secondary (if "stuck" meant "didn't save").** Apply and Cancel sit below all 16
binding rows in the scroll view (`Runtime/UI/ArenaMenu.cs:340`). Escape throws away the
draft by design (heading at `:314`). Players who press Escape lose their changes.

**Fix.**
- Carry the movement buffer across replacement. Give `PracticeInput` an optional
  previous instance and reuse its `MotorInputBuffer` (or copy its sequence). Pass the
  old input from `ApplySettings` before disposing it.
- Optional: keep Apply/Cancel visible as a fixed footer, or put them first.

**Check.** An EditMode test: consume N samples, replace the input from the previous
one, and assert the next sequence is N+1. Add a two-process smoke step: apply settings
mid-match, then assert the client still moves.

## 2, 4, 11. Hitbox position, hitbox shifting while moving, random no-reg

**Root causes.** All are host-authoritative.

1. **The rewind cap clamps at normal ping (measured).** `FusionCombatWorld.MaximumRewindSeconds = .150f`
   (`Networking/Runtime/FusionCombatWorld.cs:26`).
   - The SDK's requested view age is about 80 ms with no added latency. It is
     184 ms mean (239 max) at regional80 and 283 ms at adverse150
     ([QA](QA.md#2026-10-30-measured-rewind-age)).
   - Every regional80 shot was clamped, 34 ms short on average. At sprint speed
     (about 11 m/s) that is about 0.37 m, more than the 0.35 m box half-width.
   - So a moving target's hitbox trails where you saw it. Stationary targets are fine,
     which matches "when moving hitbox changes".
2. **Late attack RPCs are dropped silently.**
   - `FusionPlayer.AttackWindowTicks = 18`, which is 150 ms (`FusionPlayer.cs:51`).
   - A reliable RPC resent after packet loss arrives late and is discarded before
     validation ([QA](QA.md#2026-10-30-smoke-harness-peer-reads-stun-probe-and-late-attacks): `age=25`).
   - The client never draws its own shot until the host's Shot event returns (item 8),
     so a dropped shot looks like no fire at all.
3. **The hit shape doesn't match the model, or Practice.**
   - Online: a 0.70 × 0.70 × 1.8 m box plus a 0.17 m head sphere
     (`Networking/Editor/FusionContentBuild.cs:62`). The box's top corners fill the
     space beside the head, so a near-miss past the ear counts as a body hit.
   - Offline Practice: the 0.4 m-radius CharacterController capsule
     (`Runtime/Movement/MotorTuning.cs:11`, `Runtime/Combat/UnityCombatWorld.cs:40`).
     That target is wider and rounded, so Practice trains a different hitbox.
4. **The proxy collider leads the drawn body.** On a client, a remote player's
   CharacterController sits at the latest state, while the body is drawn interpolated
   behind it (`FusionPlayer.Render`, `:85`). Bumping into players happens ahead of
   where they appear. This affects body-blocking, not shot verdicts.

**Fixes** (owner decides 1 and 2 together; they are one fairness setting):
1. Raise `MaximumRewindSeconds` to about 0.25 s. That covers the regional80 maximum.
2. Raise `AttackWindowTicks` to match (about 30 ticks).
3. Use one hit shape in both worlds, sized to the soldier mesh:
   - a capsule hitbox if the installed Fusion version has one;
   - otherwise a narrower body box (about 0.27 half-width) that stops below the head
     sphere.

   Give Practice a dedicated hit collider of that shape instead of the motor capsule.
4. Optional: put a proxy's collider on its interpolated pose, or exclude
   proxy-versus-local collisions.

**Check.**
- Rerun laser moving cover and gunplay at regional80 and adverse150. Expect 0 clamped
  queries at regional80, and 8/8 shotgun pellets.
- Add a test that checks the online and Practice hit shapes against the same ray
  fixture, including a near-ear miss.

## 3. Can't pick up something while running

**Root cause.**
- Online, the client picks the pickup ID from the 20 Hz client model
  (`FusionGameCoordinator.SendCombat`, `:320–324`). The HUD prompt also uses
  `session.LocalPlayer.Feet` (`ArenaHud`, pickup prompt, about `:316–321`).
- That is the host's copy of your position, one round trip plus up to 50 ms behind
  your predicted body. At 11 m/s, 100–150 ms is 1.1–1.6 m.
- The host checks reach against your current host position, within 2.5 m and with
  line of sight (`Runtime/Match/ArenaMatch.cs:225`). So:
  - the prompt appears late;
  - a press can name no pickup (−1, rejected "Unknown pickup");
  - or a press can name one you have already passed.
- Even offline, a 2.5 m radius at sprint speed is a window under half a second.
  A press just before it is consumed and lost; there is no buffering
  (`PracticeInput.RecordPress`).

**Fix.**
- Choose the pickup and show the prompt from the local predicted position
  (`session.Motor.CaptureState().Position`) in Practice and online.
- Keep a Pickup request alive for about 0.3 s, retrying until it is in reach. The host
  already rejects duplicates by sequence and generation.
- On the host, allow a small latency margin: reach 2.5 m on the client and about 3 m
  on the host. Or accept reach from any tick inside the rewind window.
- Optional design change: auto-collect a weapon when you have no special weapon.

**Check.** A Practice test that sprints past a pad, presses 0.2 s early and expects a
claim. A smoke step that claims a pickup at full sprint at adverse150.

## 5. Main menu or lobby closes the match at random

**Root causes.**
1. **Rejoining a room you left gets you kicked (confirmed from the host's own log).**
   - The user's preview `Player.log` (`%USERPROFILE%/AppData/LocalLow/Wildstone/Better Bean Battles/Player.log`)
     contains `WILDSTONE HOST REMOVED PEER reason=seat-credential-mismatch phase=Lobby`.
   - The guest ID lives for the whole app run. The reconnect credential is regenerated
     for every new `FusionSession` (`Networking/Runtime/FusionSession.cs:72`).
   - A player who leaves a lobby or match and joins the same room again still has a
     held seat under the same guest ID. Their new credential doesn't match
     (`FusionLobbySeat.cs:53`), so the host removes them. To that player the room just
     closed.
2. **Any client exception ends the match (likely; needs a client log).**
   - The whole client loop in `FusionGameCoordinator.Update` (`:87–157`) is one
     try/catch, and the catch calls `CloseOnline`. That calls `LeaveWithFailure`, which
     shows "The online match could not continue" back on the menu.
   - That includes the strict 20 Hz `clientModel.TryRestore(...)` validation (`:134`).
     One snapshot it refuses ends the match.
   - Only the exception type is logged, not the reason, so testers' logs can't show
     which rule failed.
   - The host's log shows no "Online match stopped" line, so if this happened it was
     on a client.
3. **The host leaving ends the match for everyone.** That's expected, but nothing
   tells players. `Player-prev.log` shows this machine resumed as host after a host
   migration (`RECOVERY START mode=Host ok=True isResume=True`). The earlier host had
   left mid-match.

**Fix.**
1. Keep one reconnect credential for the app run, stored statically like the guest ID,
   so a peer can always reclaim its own seat. Alternatively, release a seat at once on
   a graceful leave during Lobby.
2. On a refused snapshot, keep the last good model and count failures. Close only after
   about 2 s of consecutive failures. Make `TryRestore` report which rule failed, and
   log `exception.Message` plus that reason.
3. Show "Host left the match" rather than the generic failure text.

**Check.**
- An admission test: leave, then rejoin the same room within the hold. Expect the same
  seat back.
- A coordinator test: one corrupt snapshot does not close the match, and sustained
  ones do.
- Ask testers to send `Player.log` straight after a drop. It is overwritten on the
  next launch.

## 6. Recoil too bouncy

**Root cause.**
- Weapon sway is computed from the world camera's rotation
  (`WeaponView.UpdateSway`, `Runtime/Presentation/WeaponView.cs:491–505`). The
  viewmodel camera is a child of it (`:87`).
- Each shot tips that camera up 0.25–1.2° in one frame
  (`FirstPersonView.AddRecoil`, `:119`, applied at `:179`). Sway reads that as a fast
  mouse flick (1.2° in 7 ms is about 170°/s) and drives a spring:
  - stiffness 90, damping 14, so a damping ratio of about 0.74 (underdamped);
  - as the punch decays at exp(−18t), the camera "turns" back and the spring swings
    the other way;
  - the effect scales with frame rate.
- On top of that, viewmodel recoil jumps to full instantly and returns linearly:
  - up to 8° of pitch (sniper 17°, shotgun 16°) and 5.5–13 cm back (`:347–351`, `:416–420`);
  - the Laser SMG (0.08 s interval) re-triggers before the 0.125 s return finishes,
    a jackhammer sawtooth.

**Fix.**
1. A bug fix, not a look change: drive sway from the aim (`View.Yaw/Pitch`) rather than
   the camera transform. Punch, shake, landing nod and roll then stop feeding the
   spring.
2. Critically damp the spring: damping 2·√90 ≈ 19.
3. Needs the user's OK, because the weapon motion is approved: a short eased attack
   (about 30 ms) and an eased return. Lower the viewmodel pitch to about 3° (sniper
   about 6°).

**Check.** A PlayMode test: one shot with no mouse input leaves `sway` at zero, and
the weapon settles without overshoot. Compare `-Motion` and `-Weapons` captures before
and after.

## 7. Red hit marker

Two red elements sit near the crosshair:

- the kill hitmarker ✕, colour (0.92, 0.33, 0.25) (`ArenaHud.Mark`, about `:214`).
  Body hits are ivory ×, heads gold and shield breaks blue;
- the damage-direction bar, 56 × 7 px, red (0.86, 0.30, 0.22), 120 px from center and
  rotated toward the attacker (`ArenaHud` constructor, about `:136–137`). Close to the
  reticle, it can read as a red hit marker when you are the one being hit.

**Fix (needs the tester's meaning).**
- If the bar caused the confusion, make it a thin arc at about 35 % of screen height,
  or at the screen edge, so it can't be mistaken for a reticle mark.
- If they dislike the red kill ✕, recolour it in one line. Red kill markers are a
  genre convention, so ask first.

## 8. Sniper doesn't fire properly when you press it

**Root causes.**
1. **No local fire prediction.** The local muzzle, beam, sound and recoil play only
   when the host's Shot event returns (`ArenaPresentation.OnStep`/`LateUpdate`,
   `Runtime/Presentation/ArenaPresentation.cs:101–107`, `:136–139`). Online, every
   trigger pull lags by about RTT plus the snapshot interval. It is most obvious on a
   single-shot weapon.
2. **A press during the 1.2 s cooldown is lost.** Semi-automatic presses are sent once.
   The host rejects one that is early ("Attack cooldown",
   `Runtime/Match/ArenaMatch.cs:198`), and nothing is buffered client-side.
3. **Item 2's 150 ms attack window** can drop the press outright.
4. **Scope versus accuracy.**
   - The scope overlay appears at 90 % zoom (`WeaponCatalog.ScopeThreshold = .9f`,
     `Runtime/Combat/WeaponCatalog.cs:52`).
   - Sniper spread is 6° × (1 − ADS) (`:104`), so a shot fired as the scope appears
     still has about 0.6° of spread, about 0.4 m at 40 m.
   - Online, the host's ADS advances from the replicated Aim bit, so it trails the
     local zoom by the input delay. That makes the spread bigger still.
   - The beam then visibly misses the reticle.

**Fix.**
1. Predict the local shot cosmetics (muzzle, beam toward the crosshair, sound, kick)
   on press when the client expects the weapon to be ready. Then skip the duplicate
   when the host's Shot arrives, and correct the beam to the host's ray.
2. Buffer a semi-automatic press for about 150–200 ms before the expected ready tick.
3. Make full accuracy match the scope: spread uses `saturate(ads / ScopeThreshold)`.
   Or show the scope at 100 %.
4. See item 2 for the window.

**Check.**
- Unit tests: spread is 0 at the scope threshold, and a press 0.1 s before ready
  fires once at ready.
- Smoke: a scoped sniper shot at adverse150 hits a stationary target 10/10.

## 9. What is Blink?

**Root cause.**
- Blink is a pickup ability: one charge, Q, a teleport up to 6 m along your aim. Nothing
  in game says so.
  - The pickup prompt says only "PICK UP BLINK".
  - The HUD always shows "Q BLINK 0", greyed (`ArenaHud`, about `:300`).
  - Settings lists only "Blink".
- It also fails silently in common cases:
  - The direction includes pitch (`ArenaMatch.cs:187`).
  - The landing must be supported floor (`UnityCombatWorld.TryBlink`, `:83–98`).
  - Aiming up, at a gap, or at the floor nearby is rejected with no sound or HUD cue.
  - The charge is kept, which is correct, but players learn nothing.

**Fix.**
- Use the horizontal (yaw-only) aim for Blink.
- Name what it does in the prompt and on first pickup: "BLINK · Q TO TELEPORT 6 m".
- Hide or dim the empty Blink and Shield slots.
- Play a short deny cue on a rejected Blink. Add one line to the Practice description.

**Check.** A rules test: Blink while looking 45° up still lands 6 m ahead on flat
ground. A HUD test for the empty-slot state.

## 10. More ammo from gun pickups

**Root cause.**
- Claiming a special weapon sets `SpecialAmmo = WeaponCatalog.Get(weapon).Ammo`,
  exactly one magazine (`ArenaMatch.cs:231`). Reload is refused for specials (`:180`),
  and there is no reserve.
- Magazines (`WeaponCatalog.cs:30–39`): sniper 6, Push Lens 6, shotgun and Stun Gun 8,
  Pulse Lobber and Rebound Disc 10, pistol 18, Laser SMG 32.
- The weapon falls back to the starter when empty.

**Fix (the owner sets the numbers).**
- The smallest change: give two or three magazines on pickup (`SpecialAmmo = Ammo × N`),
  shown as one count.
- Alternatively, add a reserve and allow reloading specials.

Either way, also update:

- the `CombatRules.ValidPlayer` bound `SpecialAmmo <= Ammo` (`CombatRules.cs:66–67`),
  or every snapshot with extra ammo is refused (see item 5);
- the replicated field range in `FusionCombatState`;
- `WeaponCatalog.Revision`;
- the `ArenaHud.LowAmmo` threshold.

**Check.**
- A rules test: picking up a weapon gives N × magazine, and a snapshot with that ammo
  restores.
- The two-process gunplay smoke at baseline and adverse150.

## 12. Hot zone blends into the map

**Root causes** (`Runtime/Presentation/ArenaEffects.cs:755–794`,
`Editor/PresentationBuilder.cs:71–85`, `Art/Shaders/ProjectedDecal.shader:104–106`):

1. **The idle colour is palette grey:** `ZoneIdle` = (0.65, 0.66, 0.67). Every zone part
   (decal, curtain, beacon and motes) is tinted from it, so the idle zone is grey on
   ivory and ash stone.
2. **The floor decal is nearly transparent by construction.**
   - Interior alpha is (0.16 + 0.12·hex) × 0.35 ≈ 6–10 %. The rim peaks near 23 %.
   - It uses premultiplied blending, so on ivory it lightly dims, then adds back about
     the same grey.
3. **The curtain and beacon are additive** (`Vfx(..., occluding: false)` → `DstBlend One`).
   - Additive light can't show over surfaces that are already bright.
   - The curtain is 40 % grey, under 1.8 m tall and fades to 0.
   - The beacon is 0.44 m wide at 50 % grey, against a bright sky.
   - The [pickup pads](QA.md#2026-10-30-pickup-pads) hit the same wall: additive rings
     were "nearly invisible" until they became a hue painted over a dark track.
4. **The owned state is one olive for every team.** The HUD arrow uses the captor's
   team tint, so the world and the HUD disagree.
5. **There is no on-screen waypoint.** The HUD offers a small ▲ compass arrow and
   "MOVING ZONE 23 m" under the score strip (`ArenaMenu.uss:59–61`). The zone moves at
   2 m/s, so once the 8 m disc is out of sight, nothing in view marks it.

**Fix.**
1. Give the idle zone a saturated accent that avoids the four team tints (cyan, yellow,
   red, violet) and contested orange; a white-hot core over a dark track would work.
   Use the captor's team tint for the owned state.
2. Draw the decal like the pickup pads:
   - a dark track band just outside the rim;
   - a hue ring at about 80 % alpha;
   - interior alpha about 20–30 %.
3. Draw the curtain and beacon alpha-blended, not additive. Widen the beacon to about
   1–1.5 m with a darker edge, so it reads against the sky.
4. Add a HUD waypoint: the projected zone center, state-coloured, with its distance,
   clamped to the screen edge when off-screen. Keep the compass arrow.

**Check.**
- Add zone captures to `-Firefight` or default: idle, owned and contested, near and
  about 40 m away, over sunlit and shaded floor.
- Measure the zone-to-floor luminance contrast in those captures before and after.
- Add a HUD test that the waypoint clamps to the edge when the zone is behind the
  player.
- Perf sample: one more HUD element and the same three zone draws.
