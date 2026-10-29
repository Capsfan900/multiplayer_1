# Handoff

Updated: 2026-10-29. **Photon-only Windows preview published; full multiplayer MVP remains open.**
Active worktree: `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`, branch `mvp-2`.
The original project at `C:\Users\tyler\Main Storage\multiplayer_1` remains untouched.

Latest public download: [laser arsenal and first-person polish preview e9e091f](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261028-e9e091f).
Built from committed snapshot `e9e091f` in `D:/WildstoneBuilds/wildstone-e9e091f`.
Fresh Windows build, 15-second headless startup, archive integrity and anonymous
download checks pass. [QA](QA.md) records the failed C: attempt and acceptance limits.

The user resumed all tasks and requested GPT-6.1 Sol delegates. We recovered the
existing authored checkpoint rather than reimplementing it: 194 files were copied
and SHA-256 checked before/copy/after. Evidence: `Artifacts/Resume/recovered-checkpoint.json`.
The original design/pause packet under `docs/handoffs/` remains historical and unchanged.

## Current evidence

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
- Latest local Windows build: `D:/WildstoneBuilds/resume-20261029/Artifacts/Unity/Windows/WildstoneArena.exe`.
  Successful current build includes the 2026-10-29 fixes; see QA for verification.
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
