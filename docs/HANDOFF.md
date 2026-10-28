# Handoff

Updated: 2026-10-27. **Native offline build available; full multiplayer MVP remains open.**
Active worktree: `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`, branch `mvp-2`.
The original project at `C:\Users\tyler\Main Storage\multiplayer_1` remains untouched.

The user resumed all tasks and requested GPT-6.1 Sol delegates. We recovered the
existing authored checkpoint rather than reimplementing it: 194 files were copied
and SHA-256 checked before/copy/after. Evidence: `Artifacts/Resume/recovered-checkpoint.json`.
The original design/pause packet under `docs/handoffs/` remains historical and unchanged.

## Current evidence

- GitHub delivery is authorized: personal private repository `Capsfan900/multiplayer_1`
  and a manual Windows Practice prerelease workflow. [TOOLING](TOOLING.md#github-builds-and-private-releases)
  owns operation and access constraints; [QA](QA.md) records publication evidence.
  The runner registration and first workflow run are not yet verified.

- Unity 6000.5.10f1 licensing, import, Compile and full content preparation now work.
- Fusion 2.1.3 Stable build 2390 is imported; the user-supplied App ID is configured
  in ignored local Photon settings and survives reload. A fresh native compile passes.
  No Photon service connection or gameplay networking has been tested.
- Native EditMode, PlayMode, explicit preparation and fresh managed checks pass;
  [QA](QA.md) records their exact totals and run identities.
- All three maps build and bake in Unity. Axis/anchor/bounds/normals/material checks
  pass; human animation loop contracts and attachment checks pass.
- Four Python map regressions pass. Fourteen model/source hash pairs and all 73
  saved map meshes were verified without regenerating the approved source assets.
- Post-integration Windows build succeeded. Run
  `Artifacts\Unity\Windows-20261027-204502-089\WildstoneArena.exe` for
  **offline Practice**, with the approved equipment, three maps and a stationary target.
  This is a local development build, not an online MVP or release.
- A packaged Practice frame was visually inspected. It renders the arena, weapon,
  hands and HUD; the player log contains no reported exception at that checkpoint.
  Title, online-unavailable/invalid-code feedback, settings resolution/apply,
  Atrium and Split Gallery loading, restart and leave were exercised. A settings
  contrast fix is authored; its rebuilt appearance remains unverified. Computer
  Use was stopped with physical Escape; desktop automation remains paused.
  This is not a completed all-map visual pass or human feel approval.

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

1. Complete the packaged visual/input checks in [QA](QA.md), coordinating with the user
   who is trying the running build. Inspect all maps, menu/settings flow, weapon framing,
   rig animation, pickups, death/respawn and zone completion. Fix actual failures.
2. SDK provisioning is complete: Fusion 2.1.3 Stable build 2390 is imported and the
   user-supplied App ID is configured locally. Native compile and package/file-GUID
   checks pass; evidence is in `Artifacts/Resume/photon-setup.md` and [QA](QA.md).
   Proceed to real service/runner validation; do not request the SDK or App ID again.
3. Initial real SDK adapters now live in `Assets/Wildstone/Networking`: lossless motor
   wire state, a networked movement behaviour, proxy poses and a private runner
   lifecycle. Focused native EditMode 42/42 and PlayMode 9/9 checks pass, including
   a real Single runner; [QA](QA.md) owns exact evidence and limits.
   They are not connected to the game menus or a complete online match. Implement
   host-verified seat admission, lobby/team/ready flow, full match/combat replication,
   actual lag compensation and one whole-match migration checkpoint next.
4. Recoverable PlayFab email login/registration/recovery and Photon token acquisition
   are implemented with bounded HTTPS calls and offline response tests. The user is
   being guided through title/add-on/custom-auth setup; the Title ID and live provider
   verification remain pending. No account request has run. Never substitute a
   client-supplied identity or ship service secrets. See [USER-SETUP](USER-SETUP.md).
5. Run actual client/host tests, then external-network, eight-player, recovery,
   endurance and measured latency/performance gates from the foundation spec.
   Only after acceptance should a release-quality package be produced.

No paid services, accounts, deployments or public releases were created. Do not
re-audit the source projects or regenerate approved art merely because this is a new
worktree. Preserve existing asset GUIDs and editable Blender sources. The accepted
roster, Blink/shield-only consumables, Host Mode and art direction remain in their
[canonical owners](README.md) and [decision record](DECISIONS.md).
