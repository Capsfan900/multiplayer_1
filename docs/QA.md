# Verification and remaining acceptance

Updated 2026-10-29. Active project: `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2`.
Authored code, native Unity behavior, a Windows build and real internet play remain
separate evidence levels. The full MVP is not complete.

## Current observed evidence

### 2026-10-31 feel-pass public preview release

User-requested [Better Bean Battles preview](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261031-31e86da) of `main` at `31e86da`. It adds the
[feel pass (D041)](#2026-10-31-feel-pass-d041) and the four checkpoint commits to
[`190bb5e`](#2026-10-31-current-state-public-preview-release). Built with the
`release-build` skill.

- Snapshot: 1,879 nonignored files from this checkout at
  `31e86da81beb50d8867a86dbfc7974d38b6be017`, captured `2026-10-31T17:09:09Z` with
  per-file SHA-256. The capture flagged the tree dirty; the only untracked file, an
  empty stray `untitled.md`, is not in the snapshot.
- Build root: `D:/WildstoneBuilds/release-20261031-130855/source`, Unity `Library`
  copied from the Main Storage checkout. Public Photon app settings included; local
  service credentials excluded.
- Command: `./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`,
  passed; log `Artifacts/Unity/BuildWindows-20261031-130909-615.log` under that build
  root. Content identity `f30ea00d…`.
- Fresh headless startup stayed alive for 15 seconds with zero exception/assertion
  matches (`release-startup.log` beside the build root).
- `BetterBeanBattles-Windows-x64-preview-20261031-31e86da.zip`: 76620307 bytes, 200
  files, ZIP CRC passed. SHA-256:
  `fa23d473c8149a3b8579912d3e863b203ebf8935d3d7deec15b2edfad30adbae`.
- Published as a prerelease on tag `preview-20261031-31e86da` (only the tag was
  pushed). An anonymous download matched that SHA-256 and the GitHub asset digest
  (`public-access-check.json` beside the build root, `2026-10-31T17:51:58Z`).
- Not repeated on this exact build: full suites, captures and Photon runs. They passed
  on the same game code in the [feel pass](#2026-10-31-feel-pass-d041). External
  networks, the two-hour soak and a human playtest remain open.

### 2026-10-31 current-state public preview release

User-requested [Better Bean Battles preview](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261031-190bb5e)
of the latest work, so playtests stop using the 2026-10-30 preview. It adds health packs
([D039](#2026-10-31-health-packs-d039)), the [playtest fixes (D040)](#2026-10-31-playtest-fixes-d040)
and the [zone waypoint chip](#2026-10-31-zone-waypoint-chip) to
[`07d8cc8`](#2026-10-30-current-state-public-preview-release).

- Snapshot: `capture.py` from that release copied 1,881 nonignored files from this
  checkout at `fca428e` plus its uncommitted work, with source/copy SHA-256 equality at
  `2026-10-31T14:10:06Z`. An empty stray `untitled.md` was left out. Game source
  commit: `190bb5eb454dd5828248134ad240e406fe6196ee`, on top of `25e6943` (the
  2026-10-30 release record). This checkout's files, index and branch were not changed.
- Build root: `D:/WildstoneBuilds/release-20261031-100938/source`, a clone of the
  2026-10-30 release checkout with its Unity `Library` copied to skip a fresh import.
  Existing public Photon app settings copied separately (same SHA-256 as here); local
  service credentials excluded.
- Command: `./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`,
  `WILDSTONE BUILD OK`; log `Artifacts/Unity/BuildWindows-20261031-101034-731.log` under
  that build root. The tracked tree stayed clean after the build.
- Against the local `b563bab3…` playable build, 192 of 200 files are identical. The 8
  that differ are build-stamped or compiled per project path: `boot.config`,
  `globalgamemanagers`, `resources.assets`, the Burst library and four managed DLLs
  (`Wildstone.Runtime.dll`: same size, 220 bytes differ). Content identity
  `36fd444d…`.
- Fresh headless startup stayed alive for 15 seconds with zero exception/assertion
  matches (`startup-result.json` beside the build root).
- `BetterBeanBattles-Windows-x64-preview-20261031-190bb5e.zip`: 75122208 bytes, 200
  files, ZIP CRC passed, Unity backup/debug folders excluded. SHA-256:
  `dfbf6d6e8d103666abaa72803baae0862d4918463597c5c3c2af2f9c68963e09`.
- Published as a prerelease on tag `preview-20261031-190bb5e` at release-record commit
  `6bc8ba5` (only the tag was pushed). An anonymous download matched that SHA-256 and the
  GitHub asset digest (`public-access-check.json` beside the build root).
- Not repeated on this exact build: full suites, captures and Photon runs. They passed
  on the same game code earlier ([D040](#2026-10-31-playtest-fixes-d040),
  [waypoint](#2026-10-31-zone-waypoint-chip)). External networks, the two-hour soak and
  a human playtest remain open.

### 2026-10-30 current-state public preview release

Recorded in release commit `25e6943`; copied here because that record never reached this
checkout. User-requested [Better Bean Battles preview](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261030-07d8cc8), captured while another agent
continued authoring. Game source commit: `07d8cc8176c5328f556f008a40a3de2862b78a32`.
Snapshot: 1865 nonignored files, with source/copy SHA-256 equality
verified before acceptance at `2026-10-31T02:52:30Z`. Later edits are excluded.
No live authoring file, index, branch or running Editor was modified by this release
operation.

- Build root: `D:/WildstoneBuilds/release-20261030-225213/source`.
- Command: `./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`.
  Unity `6000.5.10f1` succeeded with `WILDSTONE BUILD OK`; log:
  `Artifacts/Unity/BuildWindows-20261030-225312-389.log` under that build root.
- Existing public Photon app settings were supplied separately. Ignored local
  service credentials were excluded; no Host/Join connection was tested here.
- Fresh headless startup stayed alive for 15 seconds with zero runtime
  exception/assertion matches.
- `BetterBeanBattles-Windows-x64-preview-20261030-07d8cc8.zip`: 75098659 bytes, 200 files, ZIP CRC
  passed. SHA-256: `7f1859522cbea94b406652112430dd5d92044bd5d24579f0b2b74fffcf12825d`.
  Anonymous download matched that SHA-256 (`public-access-check.json` beside the build root).
- Full suites, physical-input/visual/listening checks and external-network
  multiplayer acceptance were not repeated.

### 2026-10-29 menu backdrop, motion and music (managed only)

`pwsh -File Tools/QA/Compile-Managed.ps1 -Scope All -RunManagedTests` on the
uncommitted `mvp-2` working tree: Runtime (51 files), Editor (10) and Tests (37)
type checks pass; **237 passed, 0 failed, 0 skipped** managed tests, including the
seven new `MenuMusicBackdropTests` (track selection per map id, equal-power 1.5 s
crossfade, mid-fade reversal, pause duck and invalid deltas, reduced-motion still and
bounded drift speed, dive ease/lens widen/scrim ramp, MusicVolume across settings
versions) and 42 `MenuSettingsTests`. Evidence:
`Artifacts/ManagedCompile/20261029-224254-998/`. An earlier run in this pass
(`20261029-224105-830`) had one unrelated failure in
`PresentationEventHistoryTests.OpponentLocomotionIsMeasuredInTheFacingFrame`, fixed
by its owning lane before the rerun. No Unity import, EditMode/PlayMode, build,
capture or listening check was run: USS/Barlow import, backdrop composition on all
three maps, soldier animation, dive/curtain timing, click and music playback remain
unverified.

### 2026-10-29 grenade, disc and stun continuation

Base `b33ba79` plus diagnostic changes, verified in
`D:/WildstoneBuilds/resume-20261029`. New `grenade-combat` and
`special-projectiles` scenarios use real Photon RPCs and production combat rules
with explicit synthetic geometry and initial player placement. They are compiled
only into the diagnostic player. Production balance and public builds are unchanged.

Grenade build `Artifacts/Unity/BuildSmoke-20261029-164838-553.log` passes.
Both runs below pass gameplay, peer identity, profile/RTT, player-exit and log gates:

| Run under `Artifacts/Smoke/` | Profile | Mean SDK RTT host/client | Result |
| --- | --- | --- | --- |
| `20261029-164929-424-grenade-combat-baseline` | No injection | 13.25 / 14.55 ms (117/117 samples) | Both throws pass |
| `20261029-165045-509-grenade-combat-adverse150` | Added 150 ms RTT, +/-20 ms one-way jitter, 2% receive loss at each peer | 161.93 / 161.98 ms (183/182 samples) | Both throws pass |

Each peer observes gravity, a backstop bounce and explosion exactly at the
192-tick fuse (1.6 seconds at the runner's 120 Hz). The uncovered target takes
31.5281 damage at 4.2879 m from the actual explosion center; the second target at
the same distance takes zero behind verified cover. Both are inside the 5.5 m
blast radius. Grenade ammo falls from two to one to zero; each throw has exactly
one Shot/Explosion with matching action/event identities. Clients record 29 flight
samples per throw. Only pre-throw placement/health reset and diagnostic geometry
are injected; projectile motion and damage are not injected.

Failed special-projectile attempts are retained:

- `20261029-164929-424-special-projectiles-baseline`: startup serialized an absent
  peer array as `[]`; indexing before checking length raised `IndexOutOfRangeException`.
  The diagnostic now waits for an empty array and rejects malformed nonempty data.
- Rebuild `BuildSmoke-20261029-165101-377.log` passes. Runs
  `20261029-165200-176-special-projectiles-baseline` and
  `20261029-165200-201-special-projectiles-adverse150` reach the third stun shot but
  fail verification: the diagnostic cached the previous stun timers when a new
  event arrived ahead of its avatar snapshot. Disc and first two stun verdicts
  pass within these failed runs. Timer sampling now waits for matching state;
  exact duration, damage, ammo and immunity checks remain required.
- Rebuild `BuildSmoke-20261029-165531-256.log` passes. Run
  `20261029-165642-021-special-projectiles-adverse150` passes, including all stun
  timers (mean SDK RTT 162.59/162.32 ms). Its baseline sibling verifies all weapon
  verdicts but fails the overall deadline while completing stability/peer cleanup.
  Source review identifies a completion path that could return false on an
  unavailable peer status file and restart stability even after bilateral weapon
  verification. Completed grenade and special scenarios now retain completion;
  the outer live-match, tick-progress, peer-success and clean-exit gates still apply.

- Rebuild `BuildSmoke-20261029-165931-986.log` passes. Special baseline
  `20261029-170011-572-special-projectiles-baseline` passes in 36.4 seconds.
  Grenade baseline `20261029-170011-577-grenade-combat-baseline` fails before
  gameplay: client receives `Requested` disconnect after player admission starts,
  then cancellation and a closed-session error. Host logs do not identify the
  rejection cause. Repeat `20261029-170137-927-grenade-combat-baseline` passes.
  This failure is retained as unresolved startup evidence, not attributed to the
  user's earlier team-selection report or counted as grenade acceptance.
- `20261029-170137-927-special-projectiles-adverse150` fails its second stun
  shot's flight-sampling gate: the client sees only one projectile snapshot despite
  matching damage/events/timers. Stun-only fixture distance increases from 6 m to
  12 m within the disc's verified clear corridor. Two distinct flight samples,
  exact damage, legitimate cooldowns and immunity behavior remain required.

Final build `Artifacts/Unity/BuildSmoke-20261029-170332-094.log` passes. Both
`20261029-170427-699-special-projectiles-baseline` and
`20261029-170427-699-special-projectiles-adverse150` pass all wrapper gates and exit
cleanly. Mean SDK RTT is 10.98/13.46 ms (79/79 samples) and 166.29/161.39 ms
(84/83 samples), respectively. Client stun-flight samples are 5/5/5 baseline and
4/5/5 adverse. Disc spends one round, deals 35 damage once across the return path,
records two ricochets and disappears before range/lifetime expiry. Three stun shots
spend one round each and deal 15 damage each; stun events are 1/0/1, with exact
54-tick suppression and 210-tick subsequent immunity at 120 Hz. Host observes active
suppression; both peers observe expiry and agree on event IDs and timers.
Final grenade adverse rerun `20261029-170534-432-grenade-combat-adverse150`
also passes on this build (164.45/159.56 ms mean SDK RTT, 79/77 samples), retaining
the same exact fuse, positive falloff and zero covered-damage verdicts. The latest
grenade baseline above passes with 10.14/13.82 ms mean RTT (76/76 samples).

These controlled, short, two-process tests
do not establish natural moving counterplay, motor/input suppression, deflection,
rewind accuracy, external-network reliability or performance. SDK RTT includes
processing time and configured loss probability is not a measured loss rate.
No normal-player rebuild or full test-suite rerun was needed for compile-time
diagnostic changes. Input hashes, build identities, all failed/passing run paths
and a subsequent blank-EOF-line cleanup are recorded in
`Artifacts/Resume/grenade-specials-20261029.json`.

### 2026-10-29 projectile and injected-network continuation

Base `b401a93` plus the diagnostic delta, verified in
`D:/WildstoneBuilds/resume-20261029`. Pulse Lobber coverage now requires observed
flight on both peers and matching terminal state, including a within-radius
covered target. Added SDK profiles use copied process-local config and are absent
from normal players. No service, firewall or public-release change is involved.

- Initial `BuildSmoke-20261029-110711-763.log` fails: SDK `NetworkProjectConfig.Copy`
  exists in XML/IL but is internal, so the diagnostic cannot call it directly.
  The corrected copy preserves nonserialized prefab/execution-order fields via
  `MemberwiseClone`, replaces only network conditions, and relies on the verified
  SDK runner initialization copy for simulation state. Native build
  `BuildSmoke-20261029-110934-195.log` passes.
- Baseline `Artifacts/Smoke/20261029-111014-922-projectile-combat/result.json`
  passes. Direct Pulse damage is 57.3335, matching actual explosion-center falloff;
  cover keeps health 100 at 2.195 m from the explosion (inside its 4 m radius),
  where unobstructed damage would be 38.05. Both peers observe flight, matching
  action/event identities, terminal removal and exactly one ammo spend per shot.
- The simultaneous `20261029-111014-922-laser-moving-cover` regional80 attempt
  completes all gameplay verdicts but the wrapper correctly fails its RTT evidence
  gate. Host mean SDK RTT is 95.0 ms; client samples are absent because the SDK
  requires a client to query its own PlayerRef. Corrected sampling uses own player
  on clients and remote players on hosts; this failed run is not promoted to a pass.
- Normal compilation and focused session checks pass in
  `Artifacts/Unity/EditMode-20261029-111042-884.xml`: **9/9**, zero failures/skips.
  Final diagnostic rebuild `BuildSmoke-20261029-111252-821.log` passes.

The following rebuilt runs pass all gameplay, profile-report, RTT-sampling,
player-exit and log gates. Paths are under `Artifacts/Smoke/` in the D: root:

| Run | Configured injection | Mean SDK RTT host/client | Result |
| --- | --- | --- | --- |
| `20261029-111633-346-projectile-combat-baseline` | None; final wrapper/profile-named folder rerun | 10.92 / 11.25 ms (222/222 samples) | Pulse flight, damage, cover and terminal identities pass |
| `20261029-111410-545-laser-moving-cover` | Added 80 ms RTT, +/-10 ms one-way jitter, 1% receive loss at each peer | 96.72 / 94.82 ms (125/123 samples) | All three moving laser hits and cover shots pass |
| `20261029-111410-540-laser-moving-cover` | Added 150 ms RTT, +/-20 ms jitter, 2% receive loss at each peer | 160.75 / 163.28 ms (303/301 samples) | All three moving laser hits and cover shots pass |
| `20261029-111410-544-projectile-combat` | Same adverse150 profile | 162.20 / 162.90 ms (280/279 samples) | Pulse flight, damage, cover and terminal identities pass |

Each peer reports one application of the requested profile and Debug SDK mode.
In the adverse projectile run the client samples seven direct-flight and five
cover-flight snapshots, with exact matching final damage/ammo/event identities.
RTT includes SDK processing time; it is not raw ICMP ping or input latency.
The profiles configure probability, not a measurement of the realized loss rate.
These concurrent same-PC, two-player, short functional runs do not cover three-
player fairness, asymmetric paths, prediction error, rewind age, grenade/disc/stun,
deflection, external networks, endurance or performance budgets. No production
behavior changed and no public build was published.
Source parity, final build/test/run identities and these result summaries are
recorded in `Artifacts/Resume/projectile-profiles-20261029.json`.

### 2026-10-29 moving laser and continuous presentation follow-up

Source base `be0a352` plus the diagnostic delta, verified in
`D:/WildstoneBuilds/resume-20261029`. Production gameplay and the public preview
are unchanged. `BuildSmoke-20261029-103115-545.log` passes.

- `Artifacts/Smoke/20261029-103148-656-laser-moving-cover/result.json` passes:
  the client picks up, equips and fires pistol, shotgun and sniper at a host target
  moving through production network input. Target speed at each fire is 2.2 m/s;
  displacement exceeds 0.27 m. Both peers agree on 32/72/80 damage, respectively,
  exact 1/8/1 hit events and one round spent. A follow-up shot through explicit
  diagnostic world cover spends one more round with zero target hits/damage.
  Both players finish and exit cleanly; log exception gates pass. This remains
  same-PC Photon evidence with initial placement/health resets, no injected faults
  or measured SDK rewind age. Projectile and adverse-network acceptance remain open.
- The new continuous first-person sequence initially times out before capture in
  `Artifacts/Visual/20261029-103202-687`. Existing `-Motion` reproduces the shared
  setup failure in `20261029-103355-054`. Context-only rebuild
  `BuildSmoke-20261029-103440-423.log` and sequence `20261029-103527-943` identify
  a clamped 1024x720 player window while readiness demanded 1280x720. The map is
  loaded and Playing; no fixture has yet been created. These are failed checks,
  not animation acceptance.
- Corrected diagnostic readiness checks the created offscreen texture, camera
  binding/aspect, render request and UI target. PNG headers independently verify
  actual image dimensions; window dimensions are recorded separately. Rebuild
  `BuildSmoke-20261029-103721-284.log` passes. Sequence
  `Artifacts/Visual/20261029-103814-106/run.json` passes all 32 captures and gates:
  slide-to-rest hand displacement 0.1012 m, recoil 0.0403 m, reload 0.0600 m,
  with zero recorded residual hand displacement after recoil/reload settle.
  The 3.6167 simulated seconds use fixed 1/60 updates and matching 120 Hz action
  ticks; capture wall time is separate and is not a framerate measurement.
  Two contact sheets were inspected: slide legs appear, recoil/reload poses
  change and return, with no missing geometry or stale accumulated frame in this
  first-person starter-weapon sample. This is retained synthetic presentation
  state, not natural traversal, remote readability, human feel or all-weapon motion.
- Existing pose mode `20261029-103832-879` passes all 13 captures. Default mode
  `20261029-103854-736` passes 16 captures, including actual 1280x720, 1920x1080
  and 1920x810 output despite clamped windows. Its contact sheet was inspected for
  menu/HUD framing and projection regressions. These modes were rerun because
  offscreen readiness is shared; no full test suite or production rebuild was
  needed for this compile-time-only change. Independent Sol source review found
  no blocking issues. Source parity and build/run paths are recorded in
  `Artifacts/Resume/moving-visual-20261029.json`.

### 2026-10-29 resumed acceptance and connection follow-up

The user reports that playtesting worked and felt good overall, with some initial
connection issues. Follow-up confirms the other player was in Texas on a different
ISP, and the issue occurred after joining and clicking a team. This is user-reported
independent-network play, not an instrumented network/performance acceptance run.
The user subsequently identified the symptom as a disconnect or return to a menu
after clicking a team. Exact builds and error wording remain unconfirmed.
Follow-up focused check: `EditMode-20261029-074112-638.xml`, **61 passed, zero
failures/skips**, covering `SessionRulesTests`, `FusionLobbyAdmissionTests` and
`OnlineMenuTests` in the same D: verification workspace. Existing live smoke uses
the production team-selection bridge/RPC before ready/start. This does not reproduce
the reported click symptom or prove it fixed; the exact external failure remains
unreproduced.
New investigation found ordinary smoke targets the teams automatically assigned
at admission, so it may never issue a real team-change RPC. Diagnostic `team-switch`
now deliberately moves the guest four times, checks readiness clearing on both
peers and retained runner/player/connection identity, then starts gameplay.
The command initially rejected this unsupported scenario before implementation.
`BuildSmoke-20261029-100914-057.log` succeeds and
`Artifacts/Smoke/20261029-101012-977-team-switch/result.json` passes on both peers:
four real moves, four readiness-clear acknowledgements, five ready acknowledgements,
unchanged guest connection and ten seconds of advancing gameplay. Required player
exits/log checks pass. These paths are under the D: verification root. Only a SHA256
identity fingerprint is serialized; the earlier unredacted diagnostic build was
not run. No production gameplay behavior changed, and this test uses the production
UI bridge/RPC rather than physical pointer clicks or external-network latency.
Process inspection found the older public `316a00f` executable
running locally; mixed builds are rejected, but that is not a diagnosed cause of
the report. The latest local player log separately records a rejected recovery
checkpoint and repeated Fusion allocator errors during host-migration disposal.
The invalid checkpoint's cause remains unknown; rollback limits were not relaxed.

Work remains in `mvp-2`. Verification runs use `D:/WildstoneBuilds/resume-20261029`
from base `5a8348a` plus the recorded local delta. Source manifest:
`Artifacts/Resume/resume-20261029-source-manifest.json`. It records 1,176 tracked/
new build inputs, their workspace/build hashes and 233 Git-archive line-ending-only
differences; no other source differences were found. No original project, account
provider or public release was modified. No firewall allow rules were added:
read-only inspection found four Public inbound **Block** query-user rules for the
two new diagnostic player paths. Earlier test-build allow rules remain unchanged.

- **Aborted results:** regression failed before the fix in
  `Artifacts/Unity/EditMode-20261029-000736-508.xml` (1/1 failed: expected Results,
  got Online). An aborted authoritative round now keeps an independent validated
  score/player summary after transport and live presentation retirement; Back
  clears it, and connection failures retain retry/back. First focused green:
  `EditMode-20261029-000949-200.xml`, **16 passed**.
- **Recovery callback boundary:** current and `316a00f` source both threw checkpoint
  validation errors out of Fusion's synchronous resume callback. SDK IL inspection
  showed that this bypasses following migration-disposal/initialization work.
  `EditMode-20261029-001159-411.xml` failed the new exception-containment test
  (9 tests, 1 failure). The callback now faults the owning completion task and
  returns; the owner still aborts invalid recovery and shuts down the replacement.
  No claim is made that every reported SDK allocator failure is resolved.
- **Remote weapon handling:** four initial pose regressions failed on the missing
  behavior in `PlayMode-20261029-001041-874.xml`. Third-person shots/reloads now
  drive bounded wrist/weapon motion, with grip preservation, lifecycle resets,
  reduced motion and shared event dedup. Six final tests include the actual
  presentation bridge and valid historical committed shots across recovery.
- **Final focused native checks:** `EditMode-20261029-001415-546.xml`, **49 passed**
  across session boundaries, recovery state/rules, aborted summaries and online UI;
  `PlayMode-20261029-001508-847.xml`, **32 passed** across remote/first-person
  handling, network presentation, practice session and recovery lifecycle/hooks.
  No failures or skips. These are focused runs, not a fresh full-suite claim.
- **Three-laser live Photon fixture:** first successful run
  `Artifacts/Smoke/20261029-000736-522-laser-combat` exercises real client pickup,
  equip and fire RPCs for pistol/shotgun/sniper. Both peers agree on 32/72/80 damage,
  one shot each, 1/8/1 hit events, one special-ammo spend and matching event/life
  identities. Host placement and health resets construct stationary targets.
  Final rebuilt-player rerun `20261029-001951-390-laser-combat` also passes all three
  verdicts and cleanup/log gates after the integrated lifecycle/presentation fixes.
- **Final-build recovery smoke:** `20261029-001729-065-migration-crash` and
  `20261029-001729-084-reconnect` pass separate-process Photon recovery, with
  clean required exits/log checks. Host-kill is intentional in the crash scenario.
  Diagnostic build log: `BuildSmoke-20261029-001624-978.log`.
- **Normal player:** `Artifacts/Unity/BuildWindows-20261029-001729-059.log` reports
  successful Windows compilation under the D: verification root. A hidden 15-second
  startup stayed alive without logged exceptions/assertions; owned player stopped.
  Normal `Wildstone.Fusion.dll` contains neither diagnostic driver type name.
  Output hashes and startup record: source workspace
  `Artifacts/Resume/resume-20261029-normal-build.json`. The first metadata check used
  the wrong assembly filename and failed after the successful startup; the corrected
  check inspected the actual assembly. This is a local unpublished continuation;
  public `e9e091f` remains unchanged.

Evidence paths in this section are under the D: verification root unless explicitly
listed as source manifests. Independent review found no blocking code regression.
Moving opponent/readability captures, full packaged physical-input flow, listening,
weapon balance, moving-target/adverse-network behavior, performance percentiles,
eight-machine and long endurance/reconnect matrices remain open. Same-PC Photon
tests and the user's qualitative report do not close those gates.

### 2026-10-28 laser arsenal and first-person polish public release

The user requested the current version be built and released. Source snapshot
`e9e091fe9a4f1b75800082cc141d20bacaf9bdfc` includes the completed D025/D026 pass.
Source was pushed to `mvp-2`; no main-branch merge or original-project modification
was performed. The earlier preview remains available.

- Initial isolated build at `C:/Users/tyler/orca/builds/wildstone-e9e091f` failed
  during package import with `ENOSPC`. Automatic approval review blocked deletion
  of that attempt's incomplete cache; it was left in place. Build moved to D:.
- Successful isolated source archive: `D:/WildstoneBuilds/wildstone-e9e091f`.
  Command: its `Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`.
  Unity 6000.5.10f1 returned zero with `WILDSTONE BUILD OK`; log:
  `Artifacts/Unity/BuildWindows-20261028-215203-120.log` under that build root.
  The source archive SHA-256 matched, and imported tracked Assets/Packages/
  ProjectSettings files remained byte-identical to the archive during compilation.
- Existing public Photon app settings were copied separately; credential-bearing
  local configuration was neither committed nor packaged. Diagnostic/debug folders
  are excluded from the player archive. No content regeneration was performed.
- Fresh normal player remained alive for a 15-second hidden headless startup with
  no exception/assertion match in `Artifacts/Unity/release-startup.log`; only the
  owned smoke process was stopped. Existing game windows were untouched.
- Player ZIP contains 198 files. Every archived file SHA-256 matches the build
  output; required player EXE/DLL/data were checked. ZIP checksum:
  `a16010e5e8661a1e9ae2d8f3089c764d9b003c4b79923772a1dea3dac703b4a5`.
  GitHub's uploaded asset digest and an anonymous downloaded copy both match.
- Public [preview-20261028-e9e091f](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261028-e9e091f)
  contains the Windows ZIP, checksum and release manifest. Anonymous release page
  returned HTTP 200. Share this exact URL; `/releases/latest` excludes prereleases.
- Local records: `Artifacts/Release/current-release.json`, `public-access-check.json`
  and `Package-CurrentPreview.ps1`. Local build/upload, not GitHub Actions.
  Full tests, rendered/input playtesting and external-network acceptance were not
  rerun for this package. Historical gameplay evidence below is separate. Sniper
  zoom, human feel/audio acceptance, external-network and eight-machine performance/
  endurance gates remain open; this is a development preview.

### 2026-10-28 current-state public preview release

The user requested a fresh build/release of the current state and explicitly
confirmed keeping the now-public repository public. Snapshot source commit:
`316a00f36524a823bd92e77c5741d43d87ad7c19`. It captures the worktree at build start;
concurrent edits made afterward are untouched and excluded from this release.
No gameplay changes or content regeneration were performed for delivery.

- Built from `git archive` in `C:\Users\tyler\orca\builds\wildstone-316a00f`,
  using its `Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`.
  Unity 6000.5.10f1 returned zero and `WILDSTONE BUILD OK`; log:
  `Artifacts/Unity/BuildWindows-20261028-202235-794.log` under that build root.
- The existing public Photon app settings were copied separately into the isolated
  build so Host/Join remains configured. No credential-bearing local service config
  was copied or committed. This configuration is not part of the source archive.
- The normal player stayed alive for a 15-second hidden headless startup, with no
  exception/assertion match in `Artifacts/Unity/release-startup.log`, then only the
  owned process was stopped. This does not verify rendered visuals or network play.
- Packaged ZIP: 200 entries, required EXE/DLL/player data verified, Unity backup/debug
  folders excluded. SHA-256:
  `dcd1c21839e73446c15467370758eb92ec7b3d03cab70dd69cce011efe1a2ae7`.
  GitHub's asset digest matches. ZIP, checksum and `release-manifest.json` uploaded.
- Public prerelease: [preview-20261028-316a00f](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-20261028-316a00f).
  Normal gameplay code is the captured work in progress, including D026 changes
  present at capture. Full tests, visual/input checks and external-network acceptance
  were not rerun for this release. Historical tests below are not new release tests.

Local delivery record: `Artifacts/Release/current-release.json`. This was a local
build/upload, not a GitHub Actions run. No runner setup was attempted.

### GitHub delivery

Personal private repository `Capsfan900/multiplayer_1` was created on 2026-10-27.
Source commit `e3f5815c1e863a32a23ed7c482e2efc48a324557` is uploaded to `main`.
The manual Windows preview workflow is active on GitHub. YAML/manual-trigger assertions,
PowerShell parsing of every run step and actionlint 1.7.12 pass (custom runner label
explicitly allowed). Account settings, local service config, Library and Artifacts
are absent from the tracked snapshot; a filename-only credential-pattern scan found
no candidate secrets in the published source trees.

A clean `git archive` of that commit was expanded to
`C:\Users\tyler\orca\builds\wildstone-e3f5815`, with no copied Library or local config.
Running its `Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows -TimeoutMinutes 30`
passed with exit zero and `WILDSTONE BUILD OK`. Evidence:
`Artifacts/Unity/BuildWindows-20261027-205939-312.log` under that clean build root.
First import emitted SDK API-updater assembly-resolution and config-initialization
exceptions before completing import and a successful build; this was not a clean-log
runtime test. The generated Photon account settings contain no Fusion App ID.

The exact workflow packaging and publication PowerShell steps were then executed
locally. The ZIP's 200 entries include the executable, UnityPlayer DLL and player
data; Unity backup/debug folders are absent. Published private prerelease:
[preview-local-20261027-e3f5815-1](https://github.com/Capsfan900/multiplayer_1/releases/tag/preview-local-20261027-e3f5815-1).
ZIP SHA-256: `caee19d7ed6e91b0ac5ea79c383884fb96eb6378b1cfc24bb56798e0e1f39f0f`.
The release has a player ZIP and a checksum file. This new player was not launched
or visually inspected; historical visual evidence describes the earlier local build.

**Automation remains unverified:** automatic approval review blocked runner registration
and startup twice, including after explicit user authorization; the reported reason
was only `blocked by policy`. GitHub lists zero registered runners. Runner 2.338.2
is downloaded with its published SHA-256 checked, but must be registered/launched
manually using [TOOLING](TOOLING.md#github-builds-and-private-releases). No Actions run
has built this release. The first automated run remains pending. Source publication
and packaging are not online gameplay acceptance.

SDK provisioning checkpoint (2026-10-27): Fusion 2.1.3 Stable build 2390 imported
under `Assets/Photon`, with the supplied App ID in ignored local Photon settings.
`Compile-20261027-202021-250.log` exits zero with `WILDSTONE CONFIGURED`, no compiler
errors or exceptions, and the App ID persists after reload. The earlier native
test suites and Windows executable below predate SDK import and were not rerun or
rebuilt for this provisioning step. No Photon connection was attempted.

The first import failed on the missing Physics 2D module. Adding
`com.unity.modules.physics2d` 1.0.0 resolved compilation. The next initialization
run created Fusion's config assets and logged a transient config-load exception;
the final clean wrapper run above contains no exception. Exact commands, package
hash and imported-asset checks are in `Artifacts/Resume/photon-setup.md` and
`Artifacts/Resume/fusion-sdk-import.json`. All 241 file payloads/file GUIDs match
the archive; Unity omitted eight empty folders and generated the Resources folder
GUID. This is SDK provisioning/compile evidence, not runtime/network acceptance.

The native commands below use `Tools/Unity/Invoke-Unity.ps1` in this worktree.
Run files are under `Artifacts/Unity/`. Historical 2026-10-26 paths in the execution
ledger refer to the original authoring checkout and were not copied as fresh evidence.

| Check | Result | Evidence |
| --- | --- | --- |
| Checkpoint recovery | 194 source/copy hashes verified; original read-only | `Artifacts/Resume/recovered-checkpoint.json` |
| Managed compile/rules | 172 passed, 0 failed/skipped; 43 runtime, 8 editor, 24 test source files typechecked | `Artifacts/ManagedCompile/20261027-200257-396` |
| Native Compile | Passed | `Compile-20261027-194530-787.log` |
| Native EditMode suite | 194 total: 192 passed, 0 failed, 2 explicit skipped | `EditMode-20261027-195009-614.xml` |
| Explicit full Prepare + interrupted controller repair | 2 passed, including open-scene/root/dirty-state preservation | `EditMode-20261027-194924-596.xml` |
| Reviewed setup-fixture safety follow-up | 2 passed after shared scene-guard reuse | `EditMode-20261027-200122-833.xml` |
| Native PlayMode suite | 62 passed, 0 failed/skipped | `PlayMode-20261027-195621-403.xml` |
| Input causal check | Recovered original code fails only the new cross-update tap test; restored fix passes all 3 input cases | `PlayMode-20261027-195720-081.xml`, `PlayMode-20261027-195816-338.xml` |
| CPU lightmaps/probes | All 3 maps baked; command exits successfully | `Bake-20261027-200209-874.log` |
| Windows standalone build | Passed; fresh executable and required scenes produced | `BuildWindows-20261027-200340-100.log` |
| Existing Python/Blender/map checks | 4 regressions pass; 14 model/source pairs match; 73 saved map meshes and 9 previews verified | `Artifacts/Resume/asset-verification.md` |
| Independent resume-delta review | One fixture scene-safety finding fixed; no other material runtime finding | `Artifacts/Resume/resume-review.md`, `native-readiness.md` |
| Native Fusion/provider EditMode checks after SDK import | 42 passed, 0 failed/skipped: exact mappings, actual SDK weaving, input validation, transport boundaries and offline identity responses | `EditMode-20261027-204049-361.xml` |
| Native Fusion PlayMode checks | 9 passed, 0 failed/skipped: real Single runner spawn/ticks/teleport/cleanup, proxy collider/visual boundaries, missing-input timeout and credential-free lifecycle | `PlayMode-20261027-204334-222.xml` |
| Post-integration core managed check | 172 passed, 0 failed/skipped, using this project's own package assemblies | `Artifacts/ManagedCompile/20261027-204725-610` |
| Post-integration Windows build | Success; includes SDK and settings contrast fix; not launched or visually rechecked | `FusionWindows-20261027-204502-089.log` |

Latest executable: `Artifacts/Unity/Windows-20261027-204502-089/WildstoneArena.exe`.
This is an **offline, local development build**; the online gameplay path remains
unwired and cannot host/join a match. The old `Windows/WildstoneArena.exe` was left
running and unchanged. No release archive or public upload was made. Exact build
arguments are in `Artifacts/Resume/fusion-windows-build.json`; post-build source
and output hashes (1,044 inputs, 200 output files) are in `fusion-build-manifest.json`.
Dirty source inputs mean this is not a release/reproducibility claim.
A packaged Practice frame from the earlier executable was observed rendering arena geometry, hands, weapon and
HUD; `Artifacts/Resume/standalone.log` had no reported exception at that checkpoint.
Title/online-unavailable/invalid-code feedback, settings resolution/apply, Atrium
and Split Gallery loading, restart and leave were exercised. Settings input/dropdown
contrast was fixed in source; rebuilt appearance is not yet verified. Computer Use
was stopped with physical Escape, and desktop automation remains paused. Full visual,
all-map interaction, human feel and performance acceptance remain open.

The new `Wildstone.Fusion` assembly is woven by the actual SDK. Imported project
configuration now explicitly selects eight players, 120 Hz host/client simulation
and 60 Hz sends. These settings and woven layout checks are not latency/bandwidth
measurements or evidence of remote prediction. The separate networking tests do
not belong to the historical managed-only compiler's runtime/test source scan.

The real Single-runner fixture bakes/registers a real network prefab, starts Fusion
without contacting Photon Cloud, initializes its authoritative spawn/life/tick
origin and observes motor advancement/gravity, teleport state and shutdown. The
initial fixture incorrectly copied ArenaMotor's pre-placement Awake position from
the prefab; the corrected spawn explicitly supplies the host's intended pose and
asserts both state and collider position. Proxy interpolation is tested through
the real pose application boundary, not through a remote snapshot connection.
Host/client reconciliation, authenticated connection, two remote peers, hitbox rewind,
full-match replication and migration are still untested/uncomposed.

Initial integration failures caught missing test assembly/plugin references, the
SDK/NUnit Assert name collision, a motor fixture incorrectly run in EditMode where
Awake does not initialize it, Unity JSON nested-object defaults, and cancellation
exception subtype assertions. The final EditMode run above covers the corrected
EditMode set. Identity tests make no HTTP requests and do not establish provider
configuration, credential validity or successful login.

Native PlayMode covers actual CharacterController/physics queries, local motor replay,
Blink/cover/support, input edges, presentation lifecycle, probe refresh, scene flow
and Practice coordination. This does not validate Fusion prediction, hitbox rewind,
identity, host migration, external-network behavior, cross-machine determinism or FPS.
The warmed coordinator allocation assertion passes without presentation subscribers;
this is not a whole-frame allocation result.

PhysX replay vector limits and measured maxima are in the foundation owner and
`Artifacts/Resume/motor-replay-verification.md`; timers/discrete fields remain exact.
These tightly bounded same-process checks do not change the network budgets.
The input negative control restored the fixed file byte-for-byte, then reran its tests.

## Failures found and resolved

Initial native Compile failed because additive scene creation cannot start from an
untitled default scene. Setup now replaces only disposable empty startup state and
preserves/refuses unsaved author work. Native fixtures use the same preservation guard.
A reviewer found and closed one fixture that had bypassed that guard.

The first EditMode run exposed imported-axis mismatch plus scene-fixture/preparation
dependencies. The authored FBX whitelist retains Unity's root axis conversion and
applies the required X reflection; exact native bounds, anchors, normals/materials,
hand attachment and prepared animation loops pass. No Blender meshes were regenerated.

Prepare exposed Unity fake-null component fallbacks and a zero-layer animation
controller left by an interrupted run. Explicit native component checks and repair
of the generated controller fix both. The suspected temporary-prefab dirty-scene
problem did not reproduce; no speculative isolation code was added.

PlayMode exposed nonqueryable startup capsules, negligible floor-adjusted Blink
movement, batch input-focus routing and input edges lost across update steps. Capsule
configuration now rebuilds its native shape while preserving disabled state. Blink
checks final displacement. Test input routing is restored after each fixture;
production press callbacks retain taps until consumption.

The initial bake saved all maps but failed restoring an empty batch scene setup.
Its successful rerun covers the teardown fix; nonempty untitled/dirty author scenes
are rejected before bake scene replacement.

## Reproduce native checks

Close only this worktree's interactive Editor before batch commands. Do not operate
another project's Editor. The historical licensing/approval block is resolved.

```powershell
& ./Tools/Unity/Invoke-Unity.ps1 -Action Compile
& ./Tools/Unity/Invoke-Unity.ps1 -Action Prepare
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform EditMode
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestFilter Wildstone.Tests.ContentPreparationTests
& ./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode
& ./Tools/Unity/Invoke-Unity.ps1 -Action Bake -TimeoutMinutes 60
& ./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows
```

Prepare and its explicit regression regenerate content and invalidate old lighting;
run them before Bake. Build packages saved scenes without rebuilding geometry.
Only the wrapper's own launched process is terminated on timeout. Preserve logs and
NUnit XML; neither a marker in a failed log nor a compile alone establishes a pass.

## Standalone Practice pass

After a successful build, choose **Practice**, then an arena. Default controls:
WASD move, mouse look, Space jump, Left Shift dash, Left Ctrl slide, left mouse fire,
R reload, 1 starter, 2 special, G grenade, Q Blink, E shield, F pickup and Escape menu.
Bindings can be changed in Settings. This mode has one strafing target fixture that never fires;
it does not supply opponents or demonstrate multiplayer.

Exercise title, settings/apply/cancel/defaults, keyboard focus/rebinding, Practice
selection on all three maps, loading cancel/error/retry, pause, focus loss, death,
respawn, results/rematch and leave. Pause must leave simulation running and release
input cleanly. Confirm 720p/1080p/1440p/ultrawide layouts and reduced-motion behavior.

Traverse every ramp, wall seam, gallery and arrival screen with the actual motor.
Measure all four starts' routes to central and side pickups. Follow the entire
zone footprint across ramps and stacked floors; verify floor-qualified occupancy,
contesting and fixed team scoring. Record native failures and opening advantages.

Use the explicitly labeled strafing target to exercise each weapon, pickup
replacement/ammunition, reload, grenade fuse/self-damage/cover, disc ricochets,
Push Lens displacement, stun/immunity, sword windup/coverage, blocked/clear Blink,
shield spillover/expiry, death cleanup and respawn. Check model/weapon framing,
animation and effects in moving first-/third-person captures against lit/shaded
surfaces. An unattended still or scripted target is not human feel approval.

## Real network and performance acceptance

The legitimate Fusion SDK and app are configured. D023 Photon guest admission,
actual lobby/player/match adapters, whole-game recovery checkpoints and SDK
historical hitscan are implemented and have the local evidence recorded below.
No PlayFab or additional identity provisioning is pending. Continue measured
prediction/lag-compensation and recovery acceptance using the actual SDK; do not
repeat the superseded offline-only adapter milestone.

Run host plus clients first, then independent-home networks and direct/relay
paths. Exercise stale/malformed/duplicate actions, ownership/life changes, losses
during attacks/pickups/scoring, cancellation, disconnect holds, recovery flicker,
graceful departure and host crash. Verify the whole transaction rather than only
session or combat metadata. Local multi-process tests cannot replace this evidence.

The [foundation](superpowers/specs/2026-10-25-multiplayer-foundation-design.md)
owns the exact latency/loss, correction, CPU/GPU, bandwidth, migration, eight-machine,
30-minute profile, two-hour soak and reconnect matrix. Establish service eligibility
before high-volume backend tests. All 120 Hz / 60 Hz snapshot / 144 FPS values are
targets, not measured results. Do not describe player-hosted networking as lag-free
or esports proven without the specified measurements and host-trust limits.

The HUD's per-frame formatted display text remains a recorded optimization candidate.
Measure it in the complete native frame profile; the core zero-byte checks exclude it.

Release-quality acceptance still requires these gates. Separately authorized
development previews may be packaged with exact source/build identity, verified
file hashes and explicit remaining limits; public previews above were requested
by the user. Preserve previous builds and never describe publication as completion
of runtime or internet acceptance.

# Networking continuation, 2026-10-27

- `PlayMode-20261027-212938-098.xml`: broader focused integration passes 22/22.
- `EditMode-20261027-213932-695.xml`: 217/218 pass after recovery integration. The existing recovery test needed loaded acknowledgements for the new mandatory recovery-ready gate; corrected with an explicit assertion that warmup cannot end before those acknowledgements.
- `PlayMode-20261027-214027-588.xml`: 56/57 pass, including motor regressions. The local recovery fixture used object-wide state copying between different SDK IDs, which Fusion correctly rejected.
- `PlayMode-20261027-214259-821.xml`: corrected local recovery hook fixture passes. It uses SDK behaviour-state copying under Single mode, validates complete gameplay/motor restoration, clears authority and tests warmup. Object-ID preservation with an actual migration token remains a live gate. Production follows the SDK resume-object flow, documented in [Photon's recovery sample](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect).

Native integration is ongoing; these runs do not establish live provider or internet acceptance.

- `Compile-20261027-211110-663.log`: integrated menu/lobby/match code compiles.
- `NetworkPrepare-20261027-211132-647.log`: registered network lobby/match/player prefabs and Bootstrap composition generated successfully.
- `EditMode-20261027-211946-281.xml`: 154/154 focused networking, identity, menu and combat rules checks pass, no skips.
- `PlayMode-20261027-212008-854.xml`: 19/22 pass. Three new multi-prefab fixtures used the same asset path; unique fixture paths corrected registration.
- `PlayMode-20261027-212139-442.xml`: three match spawns exposed actual SDK per-object limit (41,164 bytes versus 32,768). The presentation-only event ring was reduced to 128 entries, with a woven-state size regression.
- `PlayMode-20261027-212401-201.xml`: all three reruns pass: authoritative combat/movement plus canonical checkpoint restore, event delivery/eviction, and actual SDK hitscan damage during disconnect grace.

Initial `EditMode-20261027-211244-497.xml` had 79/82 passing: invalid fixture zone route, NUnit attribute encoding replacing an unpaired surrogate, and an outdated expected player wire size were corrected. The surrogate is now constructed at runtime so the rejection case remains meaningful.

No PlayFab HTTP request, authenticated Photon room, external-client session, packaged visual pass or performance acceptance was performed by these checks. Desktop automation remains paused.


- Pre-identity-change final focused runs: `EditMode-20261027-220250-290.xml` **242/242**, `PlayMode-20261027-220339-162.xml` **58/58**, no failures/skips. Networking preparation passed in `PrepareNetworking-20261027-220543-063.log`.
- Core managed checks at `Artifacts/ManagedCompile/20261027-214840-881`: **174/174**. These precede the final guest/migration validation changes and do not prove service behavior.
- D023 subsequently removed PlayFab; earlier identity tests and configuration requirements above are historical. Current private matches use temporary Photon guests and held-seat credentials. Focused guest binding check **11/11** at `Artifacts/Resume/photon-guest-check/managed-check/results.xml`; native guest-flow checks follow separately.


## Photon-only verification, 2026-10-27

- `EditMode-20261027-221901-948.xml`: **231/231** focused native checks, no failures/skips. Removed PlayFab fixtures; covers guest startup validation, copied-ID/wrong-credential rejection, menu actions, session/recovery and the committed-attack rebind regression.
- `PrepareNetworking-20261027-221954-358.log`: regenerated registered prefabs and online build fingerprint successfully.
- Explicit live service test `PlayMode-20261027-222118-458.xml`: **1/1**, no skips. Used the configured Fusion App ID in region `us`, created a transient private Host room, admitted its actual SDK guest peer, changed team, readied and shut down cleanly. No PlayFab or account request. This proves one host's cloud connection and lobby commands, not remote-client, direct/relay, match or migration acceptance.
- First live run `PlayMode-20261027-222009-059.xml` reached the ready-state assertion but failed because NUnit compared boxed Fusion `NetworkBool` to a CLR bool. Casting the assertion to `bool` corrected the fixture; production behavior was unchanged for the passing rerun.
- Review found a pending committed attack retained its original connection generation across migration while snapshot validation required the replacement generation. Validation now permits positive historical generations for an already committed same-life attack; new commands still require the current generation. Focused baseline regression failed before the fix; **51/51** managed combat checks passed after it at `Artifacts/Resume/pending-attack-check/managed-check/results.xml`.

- Map retention and presentation: `PlayMode-20261027-222538-355.xml` **10/10**. The new fixture keeps the same scene and floor collider through 180 sequential suppressed motor ticks; different/stale/pending maps reject reuse.
- Online cancellation/failure UI: `EditMode-20261027-222706-395.xml` **13/13**. Tests exercise actual menu handlers and rendered Retry gating while network loading has no attached external simulation. Physical Escape now delegates to the same menu cancellation path.

- Final graceful-departure freeze integration: `EditMode-20261027-223104-070.xml` **238/238**, no failures/skips. Includes validated frozen-tick/clock-anchor behavior and all current guest/menu/session/combat checks. The preceding compile attempt `Test-20261027-223037-314.log` exposed missing test-assembly internal access; the existing assembly friend list now includes the PlayMode test assembly, without broadening production APIs.

- Final focused native PlayMode `PlayMode-20261027-223211-340.xml`: **59/59**, no failures/skips. Includes actual SDK freeze primitive with coherent final motor state while transport ticks continue, map retention, movement, match/events/orbs/hitscan and recovery hooks. Explicit live service test is excluded from this run.
- Final content preparation `PrepareNetworking-20261027-223313-704.log` passed. Independent Sol source review of the graceful-freeze delta found no confirmed important defect; this is not cloud migration evidence.


## Earlier local Windows playtest checkpoint

`Artifacts/Unity/Windows-20261027-223327-783/WildstoneArena.exe` built successfully
with Unity 6000.5.10f1; log `GuestWindows-20261027-223327-783.log`. Reproducible command
and exact arguments are saved in `Artifacts/Resume/Build-GuestPreview.ps1` and
`guest-build-20261027-223327-783.json`. `guest-manifest-20261027-223327-783.json`
records HEAD `8d88f1569f39a53208c4c184530457bd325509a2`, all 1,131 source hashes
before/after (zero changes), the generated content identity and 200 output hashes.
This preserves uncommitted inputs, is **local-playtest-only**, and was not published
or substituted for the earlier private offline preview.

A separate owned player process ran `-batchmode -nographics` for 15 seconds, remained
alive, and logged no exceptions/errors before the tool stopped it. Evidence:
`Artifacts/Resume/guest-startup-20261027-223327-783.json` and
`Artifacts/Unity/GuestStartup-20261027-223327-783.log`. This verifies headless standalone
startup only, not rendering, input, gameplay, remote transport or cloud migration.
The earlier running build was not touched. Desktop automation remains paused.

Documentation link check: 89 local links, zero missing targets at this checkpoint.
`git diff --check` reports only two Unity-generated empty `m_Name` field trailing
spaces in Bootstrap; no gameplay source whitespace defects. Remaining full gates:
remote clients, real cloud migration/reconnect, external direct/relay networks,
eight-player load/endurance/performance, and the packaged visual/human-feel pass.


## Separate Windows process continuation

Diagnostic build `Artifacts/Unity/Smoke-20261028-025035-388/WildstoneArena.exe`
(first exact path is owned by `Artifacts/Smoke/latest-build.json`) was built through
`BuildSmoke-20261027-225025-891.log`. The first actual host/client run at
`Artifacts/Smoke/20261027-225108-139-basic` reached two-player gameplay in both
processes with replicated avatars and advancing match ticks for ten continuous
seconds. However, the client log contains two sequential movement-tick exceptions.
The initial state-only pass was invalidated in `result.json`; this run is **failed**.
The runner now additionally rejects runtime exceptions/assertions in either player
log. Root cause and a clean rerun are required before treating this as accepted.

- Diagnostic rerun `Artifacts/Smoke/20261027-225528-626-basic` correctly failed the new log gate: first client forward prediction received motor tick 9 while the current callback expected tick 15 (`runner=335`, `origin=319`). One-time confirmed-snapshot catch-up now replays the missing steps with the existing missing-input policy, preserving tick origin and strict subsequent checks.
- Focused native bootstrap/orb/match tests: `PlayMode-20261027-230343-217.xml` **4/4**; boundary cases `EditMode-20261027-230432-831.xml` **8/8**, no failures. A clean separate-process rerun remains required; these tests alone do not close the integration failure.


### Confirmed separate-process results

- `Artifacts/Smoke/20261027-230638-668-basic`: **passed** actual two-process Host/Join/team/ready/load and ten seconds of running gameplay, with no runtime exceptions/assertions in either player log. Closes the initial prediction-arrival regression for this setup.
- `Artifacts/Smoke/20261027-231445-459-reconnect`: **passed** host-triggered client disconnect and retained-session retry. Both peers observed connection generation 2 -> 3 in about 1.41 seconds, preserving seat, life, health 100 and starter ammo 12, followed by ten seconds of stable play and clean cleanup.
- Earlier reconnect runs `20261027-230714-754-reconnect` and `20261027-231018-570-reconnect` remain failed: gameplay recovery succeeded but atomic status-file reporting hit Windows sharing errors. Shared-delete readers and bounded diagnostic-file replacement retries fixed the harness; the passing rerun above also had no runtime log exceptions. A focused Windows file-sharing reproduction confirmed restrictive readers block replacement.
- `Artifacts/Smoke/20261027-232436-756-combat`: **passed** real SDK input and duplicate fire RPCs. Both processes observed local and remote horizontal displacement of about 2.809 m, starter clips 12 -> 11, and exactly one replicated shot per action with matching event IDs. This does not prove hit damage against a controlled moving target.
- `Artifacts/Smoke/20261027-232806-735-capacity`: **passed** eight separate Windows processes through real Photon admission. All eight independently confirmed eight connected players, eight avatars and four teams with sizes `[2,2,2,2]`, ten seconds of advancing gameplay, no logged runtime exceptions/assertions and clean exits. This is same-PC functional capacity evidence, not the eight-machine performance/endurance or external-network matrix.
- Migration runs `20261027-231543-174-migration`, `20261027-232236-475-migration` and `20261027-232614-831-migration` **failed**. The original host left; survivors changed runners, but the elected host returned `StartGame.Ok=true` without completing the restore callback. Strict state/content checks were not waived. Current diagnostics distinguish missing uploaded snapshots, callback timing and actual restore failures; migration acceptance remains open.
- `Artifacts/Smoke/20261027-233354-813-migration`: **passed** graceful host departure with two surviving Windows processes. The SDK can return a successful `StartGame` before invoking its resume callback; startup now awaits that callback within the existing recovery deadline and retains its cancellation source until restoration finishes. Both survivors replaced their runner, kept the same match/content/seat, advanced authority 1 -> 2 and observed exactly one new host, zero lost match ticks and ten further seconds of running play. All three exited cleanly with no runtime exception/assertion logs. The recorded migration duration includes the ten-second stability observation; it is not a pure recovery latency measurement.
- `Artifacts/Smoke/20261027-234225-897-migration-crash`: **failed** after the orchestrator forcibly terminated its owned original-host process following a confirmed periodic snapshot. Both survivors entered session failure around the transport's ten-second disconnect timeout, before runner replacement. Graceful migration does not cover this failure. The original host's unfinished/unsuccessful status and expected killed exit are preserved rather than relabeled as a pass.
- The timeout path previously called `StopAsync` immediately, closing the cloud connection before the migration token could arrive. Only an active client transport timeout with a valid recovery baseline now preserves that connection, freezes old-runner simulation and awaits the token within one non-extending recovery deadline. Explicit kicks still fail immediately and expose retained-session retry. `EditMode-20261027-234813-738.xml`: **248/248**, no failures/skips, including the disconnect-policy boundary checks and safe diagnostic formatting.
- `Artifacts/Smoke/20261027-234918-922-migration-crash`: **passed** actual owned-host process termination after periodic snapshot publication. Both survivors restored the same match/seat/content with authority 1 -> 2, exactly one new host, 17 match ticks of observed rollback and ten further seconds of stable running gameplay. Both survivors exited zero; the killed host remains unfinished and is explicitly marked as the expected crash. All three logs pass the runtime exception/assertion gate. This run proves the scenario, not every transport callback ordering.
- Post-fix reconnect `Artifacts/Smoke/20261027-235017-897-reconnect`: **passed**, connection generation 2 -> 3 in about 1.11 seconds and ten seconds of further play. Post-fix graceful migration `Artifacts/Smoke/20261027-235112-556-migration`: **passed**. Both preserve clean runtime logs and normal owned-process cleanup.
- Sol review found the log gate omitted bare `Exception:` records. Its pattern now covers those, with a focused PowerShell self-check. Rechecking all five earlier accepted live runs with the corrected pattern found zero runtime exceptions/assertions. Routine periodic snapshot success logs are diagnostic-build-only.

## Focused checks and pre-fixture Windows build

- `EditMode-20261027-234813-738.xml`: **248/248** focused checks, zero failures/skips.
- `PlayMode-20261027-235152-137.xml`: **61/61** focused checks, zero failures/skips. Includes SDK movement/match/orb/recovery fixtures, initial prediction bootstrap, map retention and presentation composition; excludes the explicit service test already recorded separately.
- `PrepareNetworking-20261027-235249-558.log`: network content preparation passed.
- Fresh normal Windows player: `Artifacts/Unity/Windows-20261027-235301-648/WildstoneArena.exe`; build log `GuestWindows-20261027-235301-648.log`. `Artifacts/Resume/guest-build-20261027-235301-648.json` records exact arguments and HEAD; `guest-manifest-20261027-235301-648.json` records 1,143 source hashes before/after with zero changes and 200 output hashes. Built using `Artifacts/Resume/Build-GuestPreview.ps1`.
- `Artifacts/Resume/guest-startup-20261027-235301-648.json`: fifteen-second owned headless startup remained alive with zero runtime exception/assertion records. The shipped `Wildstone.Fusion.dll` contains no `FusionSmokeDriver` type name; the normal build excludes the diagnostic define. Startup log: `GuestStartup-20261027-235301-648.log`.

The new normal build contains the production prediction, migration-callback and
transport-timeout fixes exercised by the preceding diagnostic players. It preserves
uncommitted inputs and is **local-playtest-only**, not published or a clean release.
The earlier private offline preview and earlier player directories are preserved.
Same-PC cloud tests do not establish external direct/relay paths, eight-machine
performance, adverse latency/loss profiles or endurance. Controlled moving-target
hit verdicts and the paused full packaged visual/human-feel pass remain open.

## 2026-10-28 additional local acceptance and current build

- `Artifacts/Smoke/20261028-000141-951-late-join`: **passed**. Two original clients independently verified ten seconds of play before the runner launched a third process. All three then confirmed three connected players/avatars/teams, coherent lobby bindings and existing seats, unchanged authority, and ten more seconds of gameplay. All exited zero with clean runtime logs.
- `Artifacts/Smoke/20261028-000141-946-combat`: **passed** actual movement/deduplication followed by controlled stationary hit replication. Host-authoritative test placement is explicitly recorded; the client then sent one real fire RPC. Both peers observed health 100 -> 74, shooter ammo 11 -> 10, target ammo unchanged at 11, one shot and one hit with matching identities and target life, and unchanged shooter health. This is not moving-target/adverse-latency acceptance. Review found the final distance metrics included the later fixture teleport; the movement threshold itself had been verified beforehand. Do not quote this run's final distances as input-only movement.
- Current normal Windows build: `Artifacts/Unity/Windows-20261028-000246-678/WildstoneArena.exe`, log `GuestWindows-20261028-000246-678.log`. Exact command/HEAD: `Artifacts/Resume/guest-build-20261028-000246-678.json`; source/output manifest: `guest-manifest-20261028-000246-678.json`. All 1,145 source inputs stayed unchanged during build; 200 output files were hashed.
- `Artifacts/Resume/guest-startup-20261028-000246-678.json`: fifteen-second owned headless startup stayed alive, no runtime exceptions/assertions, no smoke driver in the shipped assembly. Startup log: `GuestStartup-20261028-000246-678.log`. The subsequent movement-metric correction changes only code excluded from normal builds.

This remains an unpublished local playtest with uncommitted inputs. The full MVP
still needs independent external networks, eight-machine latency/performance,
adverse network profiles/endurance, moving-target combat acceptance and the paused
packaged visual/input/human-feel pass. No PlayFab setup or account dependency exists.
- Corrected-distance combat rerun `Artifacts/Smoke/20261028-000411-316-combat`: **passed** all movement, deduplication and controlled-hit gates with clean logs/exits. Input-only horizontal distances were about 2.28-2.62 m at each process's completion observation, retained before fixture teleport; both peers again observed exactly 26 damage and one hit/shot.
- Final documentation check: 83 local links resolve. PowerShell smoke-runner syntax passes. `git diff --check` reports only the same two Unity-generated empty `m_Name` trailing spaces in Bootstrap; no new source whitespace issue.

## 2026-10-28 unattended polish and current build

This batch preserves the approved game/art and Photon-only flow. Menu focus survives
same-screen updates; settings starts on sensitivity rather than a binding action;
the same match retains its HUD through pause/settings. The crosshair is centered,
hit cue starts hidden, zone distance is horizontal, and enlarged HUD labels wrap.
Recovery occupies the foreground with the original deadline and suppresses stale
lobby/reconnect actions. Settings has a readable scrollbar and keyboard focus.
Coordinator discovery uses reusable runner-local SDK lists at the existing cadence
and rebuilds presentation only after participant/avatar/network-ID/render-root change.

### Native and live evidence

- `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform EditMode -TestFilter 'Wildstone.Tests.Menu;Wildstone.Tests.LocalSettings;Wildstone.Tests.OnlineMenu;Wildstone.Tests.FusionSessionBoundary' -TimeoutMinutes 5`: **65/65**, no skips, `Artifacts/Unity/EditMode-20261028-081207-323.xml`.
- `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode -TestFilter 'Wildstone.Tests.FusionPlayerRunnerTests;Wildstone.Tests.NetworkPresentationBridgeTests;Wildstone.Tests.PracticeSessionTests;Wildstone.Tests.FusionSessionLifecycleTests' -TimeoutMinutes 5`: **17/17**, no skips, `Artifacts/Unity/PlayMode-20261028-102420-846.xml`. Includes real SDK spawn/despawn and changed visual-root propagation through actual presentation.
- Earlier `PlayMode-20261028-081315-197.xml` failed one allocation-comparison assertion (16/17). The BCL counter reported zero even for known allocations. The corrected fixture retains a 64 KiB calibration allocation and legacy query arrays; calibration still reports zero, so measurement is explicitly unavailable. Behavioral assertions remain active. Do not claim zero allocations or quantified savings; 20 Hz render snapshots still allocate.
- Final diagnostic build: `Artifacts/Unity/Smoke-20261028-143650-489/WildstoneArena.exe`, built with `./Tools/Unity/Invoke-Unity.ps1 -Action BuildSmoke -TimeoutMinutes 10`; log `BuildSmoke-20261028-103642-789.log`. All final live/capture runs below used it.
- `./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity -SoakSeconds 300`: **passed**, `Artifacts/Smoke/20261028-103753-715-capacity/result.json`. All eight processes held coherent eight-player/eight-avatar/four-team `[2,2,2,2]` gameplay for at least 300 seconds, entirely Running (zero Finished time), with advancing SDK ticks, clean runtime logs and zero exit codes.
- Windows-owned process memory samples: 550–556 observations per role, spanning 299.26–299.99 seconds of the hold. Per-process endpoint private-byte changes were +76 to +2,252 KiB; working-set changes +5.30 to +7.70 MiB. These are short-run observations, not leak absence, frame budgets or long endurance acceptance. Exact timestamps/bytes are in `result.capacityMemory`.
- Earlier `Artifacts/Smoke/20261028-102443-743-capacity` remains **failed**: gameplay held for five minutes, but Unity/Mono process counters returned zero. The runner now measures its actual Windows processes; zero/missing/incomplete evidence still fails. Focused runner duration/bounds/memory gate self-checks passed under `Artifacts/Resume/capacity-soak-check/`.
- `./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario late-join -TimeoutSeconds 210`: **passed**, `Artifacts/Smoke/20261028-104654-655-late-join`. Existing two peers continued through third-player admission with coherent seats, avatars and authority.
- Same command with `-Scenario combat`: **passed**, `Artifacts/Smoke/20261028-104654-659-combat`, including actual movement, duplicate-fire rejection and controlled stationary-hit replication.
- Same command with `-Scenario migration-crash`: **passed**, `Artifacts/Smoke/20261028-104654-656-migration-crash`. The owned original host was forcibly terminated; both survivors restored authority 2 with exactly one new host, 18 ticks of rollback and ten seconds of continued play. Reported migration duration includes that stability observation. Expected host kill is distinguished from survivor zero exits; all log gates passed.

### Packaged visual evidence

`./Tools/QA/Invoke-VisualCheck.ps1` generated sixteen D3D11 camera/UI captures in
`Artifacts/Visual/20261028-103753-693/`; all sixteen were inspected. Title, settings
top/controls, online, practice selection, pause and all three map spawn views show
readable text, scrollbar contrast, centered crosshair and no observed HUD overflow.
Map views cover 720p, 720p at UI scale 1.4 and 1080p; title additionally covers
1920x810. This is real offscreen rendering, not concept images. The driver uses
existing screen/map transitions, without desktop input or saved preference changes.
Normal builds exclude it. Static images do not establish keyboard/mouse operation,
full-map traversal, animation/moving readability or human game feel.

Earlier capture runs are preserved: `20261028-081714-524` failed with black frames
and timeout; `20261028-102622-032` was rejected after image inspection because the
batch camera was not rendering, accumulating UI over old frames. Explicit render
requests fixed that. `20261028-102830-235` rendered correctly but exposed target
release warnings on resize; final cleanup clears camera references before release.
The final log has no warning/exception/assertion lines. It does include Unity
shutdown memory telemetry (about 0.95 MB, no allocation stack), which is not enough
to attribute a leak to this batch and does not establish performance acceptance.
Independent GPT-6.1 Sol read-only review found no actionable defect in menu/recovery,
HUD, discovery or the compile-time visual driver.

### Current normal Windows player

`Artifacts/Unity/Windows-20261028-103823-536/WildstoneArena.exe` built through
`./Artifacts/Resume/Build-GuestPreview.ps1`; log `GuestWindows-20261028-103823-536.log`.
`Artifacts/Resume/guest-build-20261028-103823-536.json` records exact arguments and
HEAD `8d88f1569f39a53208c4c184530457bd325509a2`;
`guest-manifest-20261028-103823-536.json` records 1,149 source inputs unchanged during
compilation and 200 output hashes. `guest-startup-20261028-103823-536.json` records
fifteen seconds alive, zero runtime exceptions/assertions and neither diagnostic
driver in the shipped assembly. This is **unpublished, local-playtest-only** with
uncommitted inputs; prior builds and the separate private offline preview remain intact.

Full acceptance still needs independent external networks/direct-relay paths,
eight machines, latency/loss profiles, moving-target combat, long endurance/frame
budgets and physical-input/human-feel checks. No PlayFab setup is needed.

Final documentation check: 85 local links across ten current documents resolve.
`git diff --check` reports only the two pre-existing Unity-generated empty `m_Name`
trailing spaces in Bootstrap (lines 367 and 379); no new source whitespace issue.

## 2026-10-28 first-person body, hands and slide (D025)

The requested local body/handling continuation is implemented with original
Wildstone geometry. Source `vibegame1` was inspected read-only: its slide simulation
values already match this project. No source assets/dependencies or gameplay tuning
were copied. Camera eye and aim remain authoritative; leg animation, wrist/finger
articulation, sleeves, confirmed action feedback and slide FOV/roll are cosmetic.

- Final command: `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform PlayMode -TestFilter 'Wildstone.Tests.FirstPersonBodyTests;Wildstone.Tests.FirstPersonWeaponMotionTests;Wildstone.Tests.FirstPersonSlideViewTests;Wildstone.Tests.PresentationRuntimeTests;Wildstone.Tests.NetworkPresentationBridgeTests;Wildstone.Tests.PracticeSessionTests' -Graphics -TimeoutMinutes 5`.
- `Artifacts/Unity/PlayMode-20261028-200805-947.xml`: **35 passed, zero failed/skipped**. Coverage includes original six skin renderers and real D3D11 backface-culling visibility, look-down/slide framing, imported cuff mesh contact, grip/support alignment, confirmed-event recoil, real action duration on slow frames, lifecycle/short-Blink reset, reduced motion, exact camera eye/aim and frame partition behavior.
- Earlier 193606 run failed one new camera test because it assumed 90 FOV while its camera fixture defaults to 60. It now asserts relative to the captured baseline. Earlier 200355 run failed its new cuff test because the imported mesh is not CPU-readable; editor read-only mesh data fixed the test without changing import settings. Final run above supersedes these failures.
- `./Tools/Unity/Invoke-Unity.ps1 -Action BuildSmoke -TimeoutMinutes 10` passed; log `Artifacts/Unity/BuildSmoke-20261028-200829-311.log`, executable `Artifacts/Unity/Smoke-20261029-000836-240/WildstoneArena.exe`.
- `./Tools/QA/Invoke-VisualCheck.ps1 -Motion` passed in `Artifacts/Visual/20261028-200856-832/`. All thirteen real D3D11 pose fixtures were inspected: forward/down, two gait phases, airborne, slide entry/sustain/down/base-FOV60/exit, fire, reload and sword. Connected legs/boots and distinct slide framing are visible; hand/weapon action poses differ. Low-poly original geometry remains. These are explicitly sampled presentation states, not actual input or human animation-feel proof.
- Rejected captures remain preserved: `193457-428` aligned leg segments along the viewing ray; `195511-468` tried a waist cap that occluded the legs. Final code removes that geometry and places hips behind/under the lens. Intermediate `194808-055` and `200309-732` captures/builds are superseded. Their successful screenshot production is not visual acceptance.
- Fresh `./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario combat -TimeoutSeconds 210` and `-Scenario reconnect` both pass on the final diagnostic build: `Artifacts/Smoke/20261028-200856-824-combat/` and `20261028-200856-824-reconnect/`. Actual separate Photon processes verify movement/fire deduplication/controlled stationary hits and retained-seat reconnect; runtime log and exit gates pass.
- Independent GPT-6.1 Sol final read-only body/integration review found no actionable defect. Hand review exposed a reflected-import-axis assumption; the production basis now derives from authored grip points and the rendered cuff regression passes.

Fresh normal player: `Artifacts/Unity/Windows-20261028-200856-804/WildstoneArena.exe`,
built by `./Artifacts/Resume/Build-GuestPreview.ps1`. Manifest
`Artifacts/Resume/guest-manifest-20261028-200856-804.json` records HEAD
`8d88f1569f39a53208c4c184530457bd325509a2`, **1,157 unchanged inputs / 200 output hashes**.
Startup record `guest-startup-20261028-200856-804.json` reports fifteen seconds alive,
zero runtime exceptions/assertions and both diagnostic drivers excluded. This is
**unpublished, local-playtest-only**, preserving uncommitted inputs. Human animation
feel, physical input, adverse/external networks and performance acceptance remain open.

## 2026-10-28 expanded laser arsenal and weapon expression (D026)

The user selected sci-fi arena weapons plus recognizable laser pistol, shotgun and
sniper. Catalog revision3 appends those three identities, keeps earlier IDs, adds
map-revision2 pickups on all three arenas and changes starter to held rapid fire.
The combat owner contains initial tuning; balance is not accepted by these checks.

### Native implementation evidence

- `./Tools/Unity/Invoke-Unity.ps1 -Action PreparePresentation -TimeoutMinutes 5` imports the three original models and rebuilds only presentation assets. Map geometry/lightmaps were preserved; scene diffs add only revision/pickup metadata.
- `-Action Test -TestPlatform EditMode -TestFilter 'Wildstone.Tests.CombatRulesTests;Wildstone.Tests.MenuPresentationTests;Wildstone.Tests.FusionCombatMappingTests;Wildstone.Tests.FusionCombatEventTests;Wildstone.Tests.FusionCombatWorldTests;Wildstone.Tests.PresentationImportTests;Wildstone.Tests.ArenaDefinitionTests;Wildstone.Tests.ArenaWorldTests' -TimeoutMinutes 5`: `Artifacts/Unity/EditMode-20261028-202656-758.xml`, **155 passed, zero failures/skips**. Includes all new damage/cadence/ammo/clip/snapshot rules, eight deterministic shotgun rays and pellet event IDs, SDK adapter dispatch/mapping, release-timed Lens/sword events and cancellation, ten weapon attachment imports, all map pickup support/clearance.
- `-Action Test -TestPlatform PlayMode -TestFilter 'Wildstone.Tests.PracticeInputTests;Wildstone.Tests.FirstPersonWeaponMotionTests;Wildstone.Tests.ArenaWeaponAudioTests;Wildstone.Tests.WeaponEffectsTests;Wildstone.Tests.PresentationRuntimeTests;Wildstone.Tests.NetworkPresentationBridgeTests;Wildstone.Tests.PracticeSessionTests;Wildstone.Tests.FusionDisconnectedHitscanRunnerTests;Wildstone.Tests.FusionMatchRunnerTests' -Graphics -TimeoutMinutes 5`: `Artifacts/Unity/PlayMode-20261028-202728-298.xml`, **51 passed, zero failures/skips**. Real Input System tap/hold/release/focus/life handling, bounded reusable muzzle/audio/world effects, imported cuffs/grips, final-pose muzzle routing and event dedup, ten distinct non-silent synthesized shot waveforms, Fusion lifecycle/hitscan integration.
- After reducing the three new first-person weapon models to70% for framing, `PlayMode-20261028-203322-539.xml` passed **35/35** for FirstPersonWeaponMotionTests, NetworkPresentationBridgeTests and WeaponEffectsTests with graphics enabled. No simulation scale or hitbox changed.
- Earlier `PlayMode-20261028-201946-237.xml` was38/42: three new input fixtures accidentally rebound Fire to the existing Pickup key; fixed fixtures use unusedJ. One projectile fixture omitted required action direction; fixed fixture supplies forward. Production validators were preserved. Initial EditMode66/66 run omitted two mistakenly qualified Fusion filters; the final155 run above uses correct namespace and covers them.
- Sol peer review found ring creation radius did not match animation age0 and local world beams sampled muzzle before the final weapon pose. Both were corrected, with regression tests. No remaining actionable gameplay/SDK/handling finding was reported.

### Source and live limits

Three original Blender4.2 exports were generated with the surgical `laser-kit` mode:
LaserPistol984, LaserShotgun1212, LaserSniper1300 triangles. Editable .blend, FBX,
previews and `ArtSource/laser-kit-export-manifest.json` are retained. Existing
fourteen FBX exports still match their earlier manifest hashes. No source-project
asset writes, new dependencies or GPU generation.

Actual separate Photon processes passed `Invoke-NetworkSmoke.ps1 -Scenario combat`
and `-Scenario reconnect` (both `-TimeoutSeconds 210`) on diagnostic
`Smoke-20261029-002944-426`: `Artifacts/Smoke/20261028-203036-206-combat/` and
`20261028-203036-198-reconnect/`. These cover actual movement, starter-fire dedup,
controlled stationary hit replication and retained-seat reconnect with clean runtime
logs/exits. New pistol/shotgun/sniper behavior is covered by native authoritative,
SDK-dispatch and wire tests; these two live scenarios do not claim three-weapon
external-network playtesting. Subsequent changes only affect color, framing and
excluded capture fixtures.

Early packaged captures `Artifacts/Visual/20261028-203036-200/` were superseded for
oversized new viewmodels, dull effects and stale fixture HUD. The next26-frame run
`20261028-203504-583` improved framing but was superseded for washed-out colors,
a grenade gun-style muzzle flash and fixture blast occlusion behind spawn cover.
Review findings led to saturated HDR weapon colors, no grenade muzzle burst and
an open-space diagnostic viewpoint. Screenshot generation alone is not acceptance.

### Final visual and normal-player evidence

- Final material/framing/muzzle changes pass **35/35** focused graphics-enabled PlayMode tests in `Artifacts/Unity/PlayMode-20261028-203813-949.xml` (WeaponEffectsTests, FirstPersonWeaponMotionTests, NetworkPresentationBridgeTests), with no failures/skips.
- `Artifacts/Visual/20261028-203918-040/` completed26 captures. Root inspected six effect/charge/overlap cases; two Sol reviewers inspected fourteen earlier-weapon views and six new-gun views. **25 cases accepted** for static recognition, color, muzzle placement and aim visibility. Pistol/shotgun/sniper HUD labels and reduced viewmodel framing are correct. Hands remain partly hidden by weapon geometry; these samples do not prove moving arm/reload feel.
- The overlap fixture placed synthetic peer eyes on the observer camera plane, producing misleading near-plane bands. Only this excluded diagnostic placement changed: eight origins now sit separately three metres ahead. `BuildSmoke-20261028-204144-533.log` passed, and `Artifacts/Visual/20261028-204215-286/` completed26 captures. Root inspected the corrected `weapon-overlapping-lasers.png` and a shotgun framing spot check; both accepted. The other25 cases retain the immediately previous inspected evidence rather than being claimed reinspected. All26 weapon/effect cases therefore have explicit accepted static evidence across these two runs. No eight-player performance claim follows from the synthetic overlap case.
- Color treatment uses saturated HDR weapon materials with a separate bright beam core; grenade throws retain projectile/fuse cues without a gun muzzle burst. Captures establish visible differentiated color/geometry, not a measured bloom-softness or human quality rating. Shared96 transient lines/64 projectiles/16 audio voices remain bounded; shotgun uses9 lines per trigger before impacts. Previous allocation calibration remains unavailable, so no measured allocation/performance claim is added.

Final normal player: `Artifacts/Unity/Windows-20261028-203918-018/WildstoneArena.exe`.
`./Artifacts/Resume/Build-GuestPreview.ps1` succeeded; log
`Artifacts/Unity/GuestWindows-20261028-203918-018.log`. Manifest
`Artifacts/Resume/guest-manifest-20261028-203918-018.json` records source HEAD
`e654b3a9b87c274115383aff086ccde78e6aa206`, **1,173 inputs unchanged during build and
200 output hashes**. `guest-startup-20261028-203918-018.json` verifies15 seconds alive,
zero runtime exceptions/assertions and both diagnostic drivers excluded. The later
overlap-fixture-only adjustment is excluded from this normal player.

This local player includes uncommitted weapon changes and is **not a new published
release**. Concurrent delivery committed/published an earlier snapshot; its records
were preserved. No public-release edit was performed by this weapon continuation.
Use F to collect a weapon and2 to select its special slot; hold Fire for the starter.
Sniper currently fires unscoped; the scope model does not add a zoom binding.
Remaining acceptance: human aim/animation/weapon balance/audio listening, moving-target
and adverse/external-network cases, eight-machine rendering/frame budgets and long
endurance. The full MVP remains open; no PlayFab setup is needed.

Documentation check:87 current local links resolve. `git diff --check` passes for
this final workspace state. Prior failed/superseded runs remain preserved.

## 2026-10-30 quality pass integration (D028–D031)

Scope: weapon orientation and viewmodel framing, D031 map look, the two Sol art lanes
(six special weapons, soldier team readability), slide body framing, the quality
default and diagnostic fixes. Source HEAD `fca428e` plus uncommitted work; nothing
was committed or published.

### Native

- Full suites on the final source: **EditMode 451/453** in
  `Artifacts/Unity/EditMode-20261030-003100-031.xml` and **PlayMode 148/149** in
  `Artifacts/Unity/PlayMode-20261030-003115-628.xml`. Skips are the three explicit-only
  tests: content regeneration, controller regeneration and the live Photon host room.
- The first full EditMode run of this pass failed two tests, and both were real:
  - `ApplyingEveryActualQualityPresetPreservesIndependentVSync`: an earlier edit had turned
    the VSync and FPS-cap statements in `LocalSettings.Apply` into a comment.
  - `GradeOverridesSurviveReloadAndASecondBuilderRun`: the D031 grade rewrote authored
    profiles on every build. The grade is now written once on creation.
  Both are fixed. The focused rerun passes 11/11; see the full runs above.
- New regressions:
  - `FirstPersonBodyTests.SlideKneesStayClearOfTheLens`. It failed first: the nearest
    visible vertex was 0.18 m from the lens, at eye height. After the torso fold it
    measures 0.42 m, 0.28 m below the eye.
  - `MenuSettingsTests`: v2 Medium migrates to High, a chosen tier is kept, and level 3 is "High".
- `-Action PreparePresentation` imported the six rebuilt special weapons and the soldier
  team-mark export, and rebuilt the team prefabs with the neutral ivory `SoldierMark`.

### Photon (real service, separate processes)

On build `Smoke-20261030-041924-609` (content `439472e9ca20d657…`). Networking code is
unchanged in the final build.
- `20261030-002051-091-gunplay-baseline` passed.
- `20261030-002236-276-gunplay-adverse150` passed. This verifies the earlier profile
  allow-list fix.
- `20261030-002319-189-combat-baseline` passed.
- `20261030-002418-542-reconnect-baseline` passed.
- `20261030-002450-317-laser-moving-cover-baseline` passed.

### Packaged visual and frame time

Final build: `Smoke-20261030-042926-704`, content
`06e20a24029daec674ebb64f1f56051a99c79cd3a5d062ea04105f6571301531`.
- All four modes pass with zero render-graph errors:
  - static, 16 captures: `Artifacts/Visual/20261030-003109-569`
  - `-Motion`, 13 captures: `20261030-003137-086`
  - `-Weapons`, 26 captures: `20261030-003159-582`
  - `-Sequence`, 32 captures: `20261030-003243-733`
- The 2026-10-29 static run `20261029-235739-643` had **failed**, which an earlier note
  misreported as a pass. Its player logged 24 URP `ReflectionProbeManager.UpdateGpuData`
  NREs from forced render requests during map load/unload frames. The capture driver
  now skips render requests until every scene is loaded. Normal play submits no such
  requests, but a windowed transition was not separately observed.
- The `-Sequence` gate read zero hand motion because it sampled `Viewmodel` child 0. The
  soldier arms rig now comes first, so the driver now reads `Viewmodel/Weapon pivot`.
  Final observations: slide 0.101 m, recoil 0.040 m and reload 0.060 m, all returning
  to rest.
- Inspected captures:
  - **Viewmodel:** with the guns facing the right way, they drew about twice life size
    and too high. The Line Driver sight sat above the eye line. Fixed: guns now draw at
    0.6 scale and sit lower-right; the measured grip, sight and muzzle positions are in
    the character spec. Holo, red dot and iron sights read in ADS.
  - **Slide:** before the fix, the torso armour filled a quarter of the screen. It now
    shows the hips and legs only. Looking down mid-slide still shows the open belt ring
    from above (known gap).
  - **Spawn view:** spawn views in every map face the inside corner of the charcoal
    arrival screens. This is pre-existing map design, and the 10/28 captures show the
    same view.
- Editor viewmodel framing tool: `Viewmodel-20261030-000917` (10 weapons, hip and ADS),
  produced by `-Action CaptureViewmodel`.
- Frame time: `Tools/QA/Invoke-PerfSample.ps1`, evidence in
  `Artifacts/Visual/Perf-20261030-002944-176`. Setup: 1920x1080 offscreen through the
  normal render loop on an RTX 4070 Ti, D3D11. The scene has eight soldiers, beams every
  0.1 s each and a grenade blast every 1.2 s.

  | Tier | Mean | p95 | p99 | Max |
  | --- | ---: | ---: | ---: | ---: |
  | Ultra (High pipeline) | 1.60 ms | 1.79 ms | 2.04 ms | 4.78 ms |
  | Low | 1.52 ms | 1.69 ms | 1.97 ms | 4.44 ms |

  The two tiers are nearly equal, so this scene is not GPU-bound on this card. The
  sample is same-PC evidence only. Lower-end GPUs, the eight-machine networked load and
  allocations are unmeasured.

### Art lanes (GPT-6.1 Sol, Blender only)

- **Weapons:** rebuilt LaserShotgun, PulseLobber, ReboundDisc, PushLens, StunGun and
  LaserSword. Each has 2,888–5,132 tris with identity sockets and its named moving part.
  Contact sheet: `ArtSource/Previews/WeaponSpecials-ContactSheet.png`. Hashes:
  `ArtSource/weapon-specials-export-manifest.json`.
- **Team readability:** new teal ▲, gold ■, crimson ◆ and violet ○ palette, with
  ivory symbols on chest, back and both pauldrons.
  - The palette has luminance separation, and symbols are 0.6–1.8 mm surface decals.
  - Minimum greyscale pixel separation at a simulated 30 m (front / side / back):
    palette plus symbols 4/0/1 → **55/38/48**; symbols alone 4/0/1 → 11/5/18.
  - Simulated colour-vision-deficiency RGB minimums are ≥ 60 for every view.
  - Evidence: `ArtSource/Previews/Soldier_Readability30m*.png` and
    `ArtSource/Characters/soldier-export-manifest.json`.
- Neither lane ran Unity. Integration and the checks above were done in Unity.

### Remaining acceptance

Human feel and balance remain open. That covers ADS, spread, headshots, the SMG, the
viewmodel and slide feel, and 30 m team recognition in motion. External networks,
eight machines and endurance are also open. Captures and tests are not playtests.
Music is synthesized in-repo because the FL Studio MCP was not connected (D030).

## 2026-10-30 presentation follow-up: spawn view, shot reads, HUD, slide body

Scope:
- spawn facing
- beam taper, wall strikes and the muzzle star
- the HUD layout
- the Sol starter laser-kit art pass
- Pulse Lobber hip framing
- slide-entry and slide-sustain body fixes
- diagnostic fixes in the capture and smoke drivers

Source HEAD `fca428e` plus uncommitted work; nothing was committed or published.
Final build: `Smoke-20261030-053238-835`, content
`135c9b2d0af9b112509e6ce0e9960a87a6356af071536af4841065fb25baf6b8`
(log `BuildSmoke-20261030-013230-294`). After the build, only a code comment changed.

### Native

- Full suites on the final source: **EditMode 454/456** in
  `Artifacts/Unity/EditMode-20261030-013931-913.xml` and **PlayMode 150/151** in
  `Artifacts/Unity/PlayMode-20261030-013948-744.xml`. The skips are the same three
  explicit-only tests as before.
- New regressions:
  - `ArenaWorldTests.SpawnsFaceTheirShelterOpening`, one case per map.
  - `ContentPreparationTests.ShippedArenaCatalogMatchesTheAuthoredLayouts` now also
    compares spawn positions and yaws.
  - `WeaponEffectsTests.BeamWidensAwayFromTheViewerAndSparksOnTheWallItStrikes`.
  - `FirstPersonBodyTests.SlideEntryKeepsTheBodyOutOfTheLens` checks every frame
    from a run into a slide.
- The slide-entry test **failed first**, on frame 1: a knee was 0.08 m in front of
  the lens. Its log measured the cause. The lens drops to 0.75 m at once, while
  SlideEnter still holds the hips at 0.95 m. With `HipsUnderEye` the body is hidden
  for about 0.15 s, then appears clear.
- Settled slide, after the pelvis and thigh fold: the nearest visible vertex is
  0.62 m from the lens (it was 0.42 m), and the highest is 0.21 m under the eye.

### Packaged visual

All four modes pass on the final build, with zero render-graph or null-reference
lines in the player logs:
- static, 16 captures: `Artifacts/Visual/20261030-014012-031`
- `-Weapons`, 26 captures: `20261030-014039-000`
- `-Motion`, 13 captures: `20261030-013309-904`
- `-Sequence`, 32 captures: `20261030-014122-707`

Editor viewmodel framing: `Viewmodel-20261030-005911`.

Inspected:
- **HUD:** chips, clock, zone pill, vitals, weapon panel and ability chips at 720p,
  1080p and the largest UI scale.
  - Two layout defects were found and fixed. The ability chips sat under the gun at
    bottom centre; they moved above the vitals. The top-left hint ran into the score
    chips at the largest scale.
  - The first fix capped the hint at 20% width, which squeezed it into a one-word
    column. Stacking the hint with the chips resolved it.
- **Weapons:** all ten weapons now frame like the editor capture.
  - In the first run, the disc, stun gun and sword showed as slivers at the bottom
    edge. The fixture fired 0.035 s after switching weapons, mid way through the
    0.3 s equip raise. `FusionVisualCheckDriver` now lets the raise finish first.
  - Beams keep their width at range, and muzzle rings are gone. Wall strikes and the
    star flare read.
- **Slide:**
  - Entry is clear; before, a large dark piece of pelvis armour covered the top half
    of the view.
  - The open belt "bowl" is gone, and so are the hollow thigh ends that one
    intermediate fold showed. While sliding, the lens now sees a shin and boot
    thrown forward. Standing and walking legs are unchanged.
- **Spawn view:** the player looks out of the arrival screen along the exit alley,
  instead of into its inside corner.

### Photon (real service, separate processes)

Final runs, on the final build, run one at a time with nothing else on the machine.
Paths are under `Artifacts/Smoke/`; the adverse150 runs measured 161–162 ms mean SDK RTT.

| Run | Seconds | Result |
| --- | ---: | --- |
| `20261030-013342-172-gunplay-baseline` | 37 | pass |
| `20261030-013419-342-gunplay-adverse150` | 54 | pass |
| `20261030-013513-782-combat-baseline` | 42 | pass |
| `20261030-013555-885-reconnect-baseline` | 31 | pass |
| `20261030-013627-187-laser-moving-cover-baseline` | 48 | pass |
| `20261030-013714-916-laser-moving-cover-adverse150` | 61 | pass |
| `20261030-013816-045-special-projectiles-adverse150` | 30 | pass |
| `20261030-013846-337-grenade-combat-adverse150` | 29 | pass |

Failed attempts during this work, and the fixes. All fixes are to diagnostics;
gameplay and networking code did not change.
- `20261030-010539-159-gunplay-baseline` **failed**: "verdict mismatch: LaserPistol
  step 1 … head False". Step 1 reused sequence 6, so the host graded step 0's body
  shot as the headshot step; the shot IDs were identical. The client read its own
  `LastActionSequence` before replication had caught up. It now waits for the
  replicated state to acknowledge the previous step.
- `20261030-012344-194-gunplay-adverse150` **failed**: the scoped sniper shot
  (sequence 3) was sent but never resolved on either peer, and the fixture timed out.
  - Inferred cause: the host rejects fire before `FireReadyTick`, and the client's
    predicted tick leads the host's. Rejections are not replicated, so this was not
    observed directly.
  - The client now waits 0.4 s past the cooldown before sending.
- `20261030-012707-678-laser-moving-cover-adverse150` **failed** `DeadlineExceeded`.
  - All three moving and cover verdicts had verified on both peers. The host was at
    10.006 s of its 10 s stability window when the 120 s hang guard fired. The client
    then attempted a host migration and could not resume, and the match aborted.
  - That run's match lasted 118 s; the passing rerun took 61 s. The cause of the
    slow run is not known.
  - Injected-profile runs now get 180 s in the player and 210 s in the wrapper.
    Baseline budgets are unchanged.
- `20261030-010835-306-gunplay-adverse150` and
  `20261030-010954-058-laser-moving-cover-adverse150` failed while editor test runs
  shared the machine. Both were rerun above and are not counted.

### Limits

Captures and tests are not playtests. Human feel is still open for the HUD layout,
shot reads, the slide legs and the spawn view. The slide legs fold the third-person
pose and are not a true first-person pose. Smoke runs are same-PC, in-process
latency injection, not external networks.

## 2026-10-30 music: ACE-Step chill-house loops (D032)

Scope: the four loops for the menu and the three maps were replaced by instrumental
takes from a local ACE-Step 1.5 run, cut into loops by `Tools/Music/loop_takes.py`.
[ASSET-PIPELINE](ASSET-PIPELINE.md#music) owns the provenance, licence, take hashes
and per-file measurements. Before this, music playback had never been checked in a
native player.

### Generation and selection

- **Generation:** 15 takes from `Tools/Music/ai_takes.py`: 3 seeds per track, plus 3
  extra terrace-steps seeds. Each took 36–41 s on the RTX 4070 Ti, and every caption
  was used verbatim (caption lock). Logs are `D:\WildstoneArchive\music\gen.log`
  and `gen2.log`.
- **Probe:** `loop_takes.py --probe` found a groove window in 13 of 15 takes. The
  exceptions are `menu_s33` (27% of beats kicked) and `terrace_steps_s22` (48%).
- **Vocal check:** RoFormer stems for the candidates gave vocal-stem energy relative
  to the mix of:

  | Take | Vocal stem | Notes |
  | --- | --- | --- |
  | menu s11 | −63.0 dB | |
  | atrium s11 | −61.5 dB | |
  | atrium s33 | −54.8 dB | |
  | split gallery s33 | −53.7 dB | |
  | terrace steps s11 | −40.0 dB | ten 2 s windows above −40 dB, peak −27 dB: rejected |
  | terrace steps s44 | −65.6 dB | |
  | terrace steps s55 | −64.6 dB | |
  | terrace steps s66 | −55.2 dB | one window at −38 dB |

- **Picks:**
  - **Atrium:** s11. The s33 take opens 6 dB quieter, which would dip at every wrap.
  - **Split Gallery:** s33, which has the lowest 2–5 kHz energy.
  - **Terrace Steps:** s66, the longest clean window. s44 and s55 only allowed 32
    bars.

### Evidence

- **Loops:** `uv run Tools/Music/loop_takes.py D:/WildstoneArchive/music/takes`.
  - Every file passes: exact locked length, −15.0 LUFS, true peak ≤ −1.2 dBTP, and
    seam step and curvature below each file's p99.9.
  - A second full run reproduced all four OGG sha256 values (`ed55a66b…`, `b748bae1…`,
    `c4357769…`, `4b8e1acf…`). `--check` passes on the committed files.
- **Unity import:**
  - `MusicImportTests` (4 new, Native) and `MenuMusicBackdropTests` (7) pass, 11/11:
    `Artifacts/Unity/EditMode-20261030-071942-683.xml` after the final terrace take.
    Each imported clip is 44.1 kHz stereo Streaming, and its sample count equals the
    OGG granule position.
  - Full EditMode suite: 458/460, with 2 explicit skips
    (`Artifacts/Unity/EditMode-20261030-072643-264.xml`).
- **Packaged player:** build `Artifacts/Unity/Smoke-20261030-112221-169` (content
  `92dff50d6ed17c67eed95b7e84095786bb2e764edf916898ae8636ddec24abda`, log
  `BuildSmoke-20261030-072213-221`).
  - `Invoke-VisualCheck.ps1 -Music` passes (`Artifacts/Visual/20261030-072244-398`):

    | Screen | Track | Playing at | Source RMS |
    | --- | --- | --- | --- |
    | Title | `MenuTheme` | 2.11 s | 0.081 |
    | Atrium | `Level_atrium` | 2.09 s | 0.038 |
    | Split Gallery | `Level_split-gallery` | 2.05 s | 0.047 |
    | Terrace Steps | `Level_terrace-steps` | 2.09 s | 0.037 |

    All at volume 0.40, with zero RenderGraph or NRE lines.
  - The static mode also passes (`Artifacts/Visual/20261030-072301-230`) and records
    music as not checked in batch mode.
- **Failure found and fixed in the check:** the first static run with a music
  assertion (`Artifacts/Visual/20261030-072037-888`) failed with "Music is MenuTheme
  stopped at 0.00 s". The batch-mode player has no audio. Music is now asserted only
  by the windowed `-Music` mode.

### Limits

- No one has listened to the new music. Its mix against weapon SFX, its mood and
  any AI artefacts need the user's ears.
- The wrap was measured offline, and Unity's import length was asserted. A full
  in-player loop wrap (95–166 s) was not observed.
- The terrace steps loop is 48 bars (1:35); the others are 72–80.
- The FL Studio MCP was still not connected, so an FL rerender stays open (D030, D032).

## 2026-10-30 recorded weapon and movement audio

Scope: every combat cue was a synthesized sine sweep at one level, and players made
no movement sound. `ArenaAudio` now plays recorded CC0 banks copied from `vibegame1`
([SOURCE-REUSE](SOURCE-REUSE.md#recorded-weapon-and-movement-audio-2026-10-30)) and
adds footsteps, jump, land, dash, slide and reload cues. The
[character and weapon spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
owns the behavior. Presentation only; no rule, network or input code changed.

### Gain basis

Loudest 50 ms RMS per bank, measured on the copied OGGs (dBFS):

| Bank | Level | Bank | Level |
| --- | --- | --- | --- |
| Land | −7.6 | Footstep | −16.5 |
| Swing | −10.3 | Hit | −16.7 |
| Block | −11.7 | DeflectorOpen | −17.9 |
| LaserFire | −12.4 | DeflectorParry | −20.2 |
| Dash | −12.9 | Click | −21.6 |
| SpellFireImpact | −13.3 | Stun | −21.9 |
| LaserReload | −14.5 | Jump | −24.6 |

The per-cue gains in `ArenaAudio` start from these levels. They are proposals until
someone listens.

### Evidence

- **Copy:** all 70 clips are byte-identical to `vibegame1` `2cb6e576`. Every folder
  and clip GUID matches its source, and no meta keeps `preloadAudioData: 0`.
- **Compile:** `Artifacts/Unity/Compile-20261030-073500-204.log`, no warnings in the
  changed files.
- **Native tests:** the rewritten `ArenaWeaponAudioTests` pass 2/2
  (`Artifacts/Unity/PlayMode-20261030-073523-777.xml`). Full PlayMode passes 151/152
  with 1 explicit skip (`PlayMode-20261030-073547-190.xml`). Full EditMode passes
  458/460 with 2 explicit skips (`EditMode-20261030-073635-970.xml`).
- **Packaged player:** build `Artifacts/Unity/Smoke-20261030-113935-132` (content
  `bb4764367cca11de6f147a100ce1bc47b38772db9c3f3a5a0548e098de5f6b07`, log
  `BuildSmoke-20261030-073927-423`).
  - `-Music` passes with its new recorded-cue step (`Artifacts/Visual/20261030-073956-890`).
    `laserfire_02` played at pitch 1.01 and volume 0.83, with output RMS 0.143, over
    `Level_terrace-steps`. The four music steps repeat the D032 result (RMS
    0.082 / 0.080 / 0.030 / 0.073).
  - Static (`20261030-074028-573`, 16 captures) and `-Weapons`
    (`20261030-074055-551`, 26 captures) pass on the same build.
  - All three player logs have zero exception, NRE or RenderGraph lines.
- **Playable build:** `-Action BuildWindows` rebuilt `Artifacts/Unity/Windows/WildstoneArena.exe`
  from the same content (log `BuildWindows-20261030-074554-523`). The first attempt
  (`BuildWindows-20261030-074456-742`) built successfully but failed the wrapper's
  freshness check: the incremental build left the unchanged launcher with its old
  timestamp. The wrapper now deletes the launcher before a build, so only that run
  can recreate it.

### Limits

- Nobody has listened. Levels, weapon identity by ear, the music-to-SFX balance and
  whether positional steps help find opponents all need the user.
- Batch-mode tests assert which clip, pitch and volume each voice gets, not the sound.
- Footsteps were tested on a scripted straight run. Natural traversal, wall runs,
  stairs and eight players at once were not observed.
- No Photon run was repeated. Audio reads the same committed events and motor state
  as the effects that the earlier runs exercised.

## 2026-10-30 laser beams: barrel start and daylight contrast

Scope: a review of the packaged weapon captures found two beam defects. Neither
was recorded before. The
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md) owns the
contract. Presentation only.

### Findings

- **Beams started short of the barrel.** The viewmodel draws at 58° vertical FOV and
  the world at a wider one, but beams started at the muzzle socket's raw world
  position. The world camera projects that point nearer the screen centre, so a gap
  opened between the drawn barrel and the beam. In the weapon fixture (a 90° world
  camera) the visible beam began at 57 % of the barrel's screen offset (predicted
  55 %). At the default horizontal 100° it would be about 83 %.
- **Beams washed out in daylight.** Additive cyan on ivory stone and sky read as a
  pale line.

### Fix and evidence

- `WeaponView.TryGetMuzzle` maps the socket through the viewmodel camera's viewport
  into the world camera, keeping screen point and depth. Local beams and the world
  muzzle flash use it.
- `ArenaEffects` adds a dark premultiplied sheath under every laser beam and shotgun
  pellet, queued one before the glow.
- **Tests:**
  - `FirstPersonWeaponMotionTests.MuzzleUsesAuthoredSocketAndFourReusedElementsExpireAndReset`
    now checks a 90° world camera against the 58° viewmodel: same screen point within
    0.0005 and same depth.
  - `WeaponEffectsTests.EveryWeaponHasBoundedDistinctShotGeometry` checks one sheath
    per laser ray, queued before its glow; the shotgun bound rises from 12 to 16 lines.
  - `NetworkPresentationBridgeTests` follows the new burst order.
  - Focused 40/40 (`Artifacts/Unity/PlayMode-20261030-075232-299.xml`). Full PlayMode
    151/152 with 1 explicit skip (`PlayMode-20261030-075422-390.xml`).
- **Packaged build:** `Artifacts/Unity/Smoke-20261030-115257-177`, built by log
  `BuildSmoke-20261030-075251-445`.
  - `-Weapons` passes (`Artifacts/Visual/20261030-075312-732`). In `weapon-linedriver-1`,
    `weapon-laserpistol-1`, `weapon-lasersniper-1` and `weapon-lasershotgun-1`, the
    beams now leave the drawn barrel. Before the fix, in `20261030-074924-212`, they
    started about 85 px short at 1280×720.
  - Static (`20261030-075445-574`), `-Sequence` (`20261030-075512-512`) and `-Music`
    (`20261030-075517-662`) also pass, with zero exception lines.
- **Playable build:** `Artifacts/Unity/Windows/WildstoneArena.exe`, rebuilt with these
  fixes (log `BuildWindows-20261030-075641-945`, content
  `c4d3d6b658f5097ba2f3c26d567070b84f38a42af11bcc1208a7d427fb0d3b3b`).
- **Checked, not defects:**
  - Sequence frames 000–024 are washed out because the ivory curtain opens over
    0.6 s after loading, per the UI spec.
  - The sequence fire frames show no beam because that fixture plays only the
    viewmodel's own muzzle effects.

- **Frame time:** `Invoke-PerfSample.ps1` on the same build
  (`Artifacts/Visual/Perf-20261030-075901-998`), in the eight-soldier beam and blast
  scene of the earlier sample, now with sheaths. Ultra: mean 1.55 ms, p95 1.72,
  p99 2.04, max 4.45. Low: mean 1.46 ms, p95 1.63, p99 2.01, max 4.37. Both are
  within 0.1 ms of the earlier sample. The perf scene does not drive `ArenaAudio`,
  so the new voices are not in these numbers.

### Limits

- The sheath's darkness and width were judged on still frames only. Whether beams
  read well in motion, against every map surface and at 30 m needs a human look.

## 2026-10-30 maps, weapons and fire feedback

Scope: the user asked for the `visual-polish` look merged into the game, and for
better and cooler maps, weapons, effects and gunplay feel (D034). Contracts are in
the [rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md#6-d028-quality-stack)
and the [weapon workflow](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#weapon-energy-pass-2026-10-30).
Presentation only: arena geometry, rules, authority and aim are unchanged.

### Maps

- **Change.** `ArenaBuilder` palette, daylight and grade moved to the
  `visual-polish` values; the three grade profiles were deleted and regenerated.
  - Before: `Artifacts/Visual/20261030-075445-574`. Walls read as tan sandstone
    brick (4 × 2 m) with near-black charcoal and an amber tape-like inlay.
  - After: clean ivory and cool-grey 4 m panels under a pale haze, matching the
    `visual-polish` captures in that worktree's
    `Artifacts/QualityGoal/BeforePulseFeed/Quality/`.
- **Build.** `Prepare-20261030-084253-480`, then `Bake-20261030-084308-075` (all
  three maps).
- **Map views.** `CaptureViewmodel` now also renders an overview and an eye-height
  view per map (`Artifacts/Visual/Viewmodel-20261030-084710/map-*.png`). These
  overviews were used to compare against `visual-polish`.

### Weapons and fire feedback

- **Models.** The weapon art pass covers all eleven weapons. It ran in a separate
  Codex lane with Blender only; its report and measurements are in
  [SOURCE-REUSE](SOURCE-REUSE.md) and `ArtSource/weapon-energy-export-manifest.json`.
  - Sockets are exactly unchanged and `.meta` files are untouched.
  - Previews: `ArtSource/Previews/FirstPerson/WeaponSpecials-ContactSheet.png`.
- **Root cause of the flat look.** `Signal*` materials had no emission, so
  nothing on the guns glowed or bloomed. They now emit.
- **Tuning on captures.**
  - At ×2 emission, and at ×1.3 with a full-colour albedo, amber rendered yellow and
    coral pink.
  - At ×1.2 over a 35 % albedo, with each flare capped at 1.6, the hues hold. See
    the editor captures `Viewmodel-20261030-085104`, then `-085141`, then `-090036`.
  - Packaged fire frames: `20261030-085411-879`, then `-090107-687`, then
    `-090306-160`.
- **Laser burns.** Beams that stop on cover leave a burn and a cooling ember.
  Players are excluded.
- **View punch.** Local shots add a cosmetic view punch.

### Checks

- **New tests.**
  - `FirstPersonWeaponMotionTests.SignalPartsFlareOnShotsAndCoolBack`.
  - `FirstPersonSlideViewTests.ShotPunchRecoversWithoutMovingAim`.
  - `WeaponEffectsTests.LaserBurnsAreBoundedAndNeverEvictBlastScorches`.
  - `WeaponEffectsTests.BeamWidensAwayFromTheViewerAndSparksOnTheWallItStrikes` now
    also asserts a burn on the wall face along its normal, and none for a shot into
    open air.
- `PracticeSceneTests` follows the new fog end (230 m).
- **Full suites on the final code:**
  - EditMode 458/460 with 2 explicit skips (`Artifacts/Unity/EditMode-20261030-090706-655.xml`).
  - PlayMode 154/155 with 1 explicit skip (`PlayMode-20261030-090721-444.xml`).
- **Packaged build** `Artifacts/Unity/Smoke-20261030-130250-158`, log
  `BuildSmoke-20261030-090244-603`. All five modes pass with zero exception lines:
  - static `Artifacts/Visual/20261030-090749-424`;
  - `-Weapons` `20261030-090306-160`;
  - `-Sequence` `20261030-090816-576`;
  - `-Motion` `20261030-090821-956`;
  - `-Music` `20261030-090844-308`.
- **Frame time** (`Artifacts/Visual/Perf-20261030-090405-054`, same scene as earlier
  samples):
  - Ultra: mean 1.61 ms, p95 1.78, p99 2.11, max 4.51.
  - Low: mean 1.52 ms, p95 1.67, p99 2.01, max 3.45.
  - Both are within 0.1 ms of the previous sample.
- **Playable build.** `Artifacts/Unity/Windows/WildstoneArena.exe` (log
  `BuildWindows-20261030-090442-599`). Its content hash is
  `ad6f1841cf9aa95440594b322c4b1c81c7be1df613a9254944d6889f9f25a9fb`: SHA-256 over
  the sorted relative path and per-file SHA-256 of the build's 200 shipped files,
  excluding `*DoNotShip` and Burst debug folders.
- No Photon run was repeated: no network, authority or simulation code changed.

### Limits

- **Captures.** The static captures still open in the walled spawn alley, so in-play
  map views come from the editor overviews and the weapon fixture. Widening the
  spawn exits is a layout change that waits on the user.
- **Judged on stills only.** Glow strength, the flare and heat curve, burn size and
  lifetime, and the view punch angles were judged on still frames, not in motion.
  Whether firing feels better needs a human playtest.
- **Bloom.** Bloom on the viewmodel's signal parts is subtle at the current grade
  (threshold 1.05, intensity .6).
- **Amber.** Amber still reads golden rather than deep orange in full sun.

### Follow-up: hit flash and ceiling light strips

- **Hit flash.** Remote soldiers flash white on a confirmed hit, brighter on a
  headshot (contract in the
  [weapon workflow](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#weapon-energy-pass-2026-10-30)).
- **Root cause found on the way.** The first full PlayMode run failed the new test
  (`PlayMode-20261030-091651-600.xml`): the armour material had lost `_EMISSION`.
  - Diffing the material across runs showed a plain editor launch keeps the keyword,
    but `CaptureViewmodel` removes it. That run saves the arms' materials on quit.
  - It removed the keyword with GI flags EmissiveIsBlack or None, and even with a
    non-black colour. It keeps it with RealtimeEmissive, as on the weapons' `Signal*`
    materials.
  - So the team glow materials had been going dark after every capture run until
    the next `PreparePresentation`. `SoldierBuilder` now flags every soldier
    material realtime emissive. After a capture run the keyword is still present on
    Armour, Suit, Trim, Accent 1 and Glow 1.
- **Ceiling light strips.** Under each overhang that has a baked bounce light, glowing strips now show
  where the light comes from, so the dark charcoal ceilings read as lit.
  - `Prepare-20261030-091453-397`, then `Bake-20261030-091503-722`.
  - Editor views: `Artifacts/Visual/Viewmodel-20261030-091618/map-*.png`. The
    split-gallery ground view shows the strips.
- **Full suites on the final code:**
  - EditMode 458/460 with 2 explicit skips (`Artifacts/Unity/EditMode-20261030-092157-560.xml`).
  - PlayMode 155/156 with 1 explicit skip (`PlayMode-20261030-092212-210.xml`).
- **Packaged build** `Artifacts/Unity/Smoke-20261030-132249-365` (log
  `BuildSmoke-20261030-092243-581`). All five modes pass with zero exception lines:
  - static `Artifacts/Visual/20261030-092315-853`;
  - `-Weapons` `20261030-092436-393`;
  - `-Sequence` `20261030-092522-880`;
  - `-Motion` `20261030-092528-069`;
  - `-Music` `20261030-092550-141`.
  - `-Weapons` then gained `weapon-remote-hit`: a remote soldier 7 m down a Line
    Driver beam at the peak of its hit flash. It is the first packaged view of a
    remote soldier being hit. The change touches only the smoke-only
    (`WILDSTONE_SMOKE`) driver.
    - Rebuilt smoke: `Artifacts/Unity/Smoke-20261030-133111-723`
      (`BuildSmoke-20261030-093104-021`).
    - `-Weapons` passed with 27 captures and zero exception lines
      (`Artifacts/Visual/20261030-093134-000`).
    - The body reads as a solid white silhouette for the flash. The ceiling strips
      show under the far gallery in the same frame.
    - The other four modes above ran on the previous smoke build, which differs
      only in this driver.
- **Frame time** (`Artifacts/Visual/Perf-20261030-092622-341`):
  - Ultra: mean 1.62 ms, p95 1.81, p99 2.13, max 4.16.
  - Low: mean 1.51 ms, p95 1.67, p99 2.05, max 8.47.
  - Means and percentiles are within 0.03 ms of the previous sample. The Low max is
    a single frame.
- **Playable build.** `Artifacts/Unity/Windows/WildstoneArena.exe` (log
  `BuildWindows-20261030-092652-950`). Its content hash, computed the same way as
  above over 200 files, is
  `3139b40aeb40a49db2429bf523e96b34dc600d99bd22e49d45ebd96959a8dad7`. It replaces
  the earlier build. The later smoke-only driver change does not compile into it.
- **Beams.** Beam width stops growing with distance at 6× (42 m) instead of
  2.5× (17.5 m), so long shots keep their on-screen width. The sniper rail is heavier
  and lingers longer (values in the
  [rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md#6-d028-quality-stack)).
  - `WeaponEffectsTests` 18/18 (`PlayMode-20261030-093455-283.xml`).
  - Packaged `-Weapons` passed with 27 captures (`Artifacts/Visual/20261030-093537-307`,
    smoke `BuildSmoke-20261030-093515-659`).
  - Measured beam widths in the sniper and Line Driver frames barely changed (1–3 px
    of bright core). The strip texture is bright only in its middle quarter.
    Fixture frames are also read at least 40 ms after the shot, partway through
    the quadratic fade. So these stills cannot show the moment of firing, and beam
    weight needs a look in play.
  - Full suites after the beam change: EditMode 458/460 with 2 explicit skips
    (`EditMode-20261030-093820-087.xml`), PlayMode 155/156 with 1 explicit skip
    (`PlayMode-20261030-093834-515.xml`).
  - The playable build was rebuilt (`BuildWindows-20261030-093857-363`). Its
    content hash over 200 files is
    `51decefa611e56b73aca6ab63b4301b55c35d95e28967e41fb8e2c969cda6307`, which
    replaces the hash above.
- **Limit.** Flash brightness was judged on one still frame and its 0.12 s length
  only by the test; neither has been seen in motion.

## 2026-10-30 host removal reasons

Scope: backlog item 1, the external player's disconnect or return to the menu after
joining and choosing a team. The local `Requested` disconnect seen once in
`20261029-170011-577-grenade-combat-baseline` gave no host-side cause. Contract: the
[foundation spec](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#7-sessions-seats-and-recovery).

- **Change.** Every host removal of a peer now logs one reason-only warning. That
  covers a refused admission at startup or join, and a load timeout. Previously
  each failure path in `FusionLobbyAdmission.Admit` and `FusionLobby.Admit` returned a
  bare `false`. No admission rule changed.
- **Tests.**
  - `FusionLobbyAdmissionTests` 13/13 (`Artifacts/Unity/EditMode-20261030-094155-982.xml`),
    including the new `RefusedAdmissionsNameAFixedReason`.
  - Full suites: EditMode 459/461 with 2 explicit skips
    (`EditMode-20261030-094530-883.xml`), PlayMode 155/156 with 1 explicit skip
    (`PlayMode-20261030-094545-826.xml`).
- **Real Photon, separate processes** (smoke `BuildSmoke-20261030-094216-432`). All
  pass with zero exception lines and no removal line in any log:
  - `team-switch` (`Artifacts/Smoke/20261030-094247-746-team-switch-baseline`);
  - `late-join` (`20261030-094316-108-late-join-baseline`);
  - three `grenade-combat` baselines (`20261030-094354-194`, `-094422-126`,
    `-094450-839`).
  - The intermittent startup `Requested` disconnect did not recur, so its cause is
    still unknown.
- **Playable build.** `Artifacts/Unity/Windows/WildstoneArena.exe`
  (`BuildWindows-20261030-094608-839`). Its content hash over 200 files is
  `ca1a81ea1cbc67bf17c62b373edc2b20237a3ac630b041810b5bb1f15aee0a79`, which
  replaces the earlier hashes today.
- **Next time it happens.** Copy the host's `Player.log`. Since the D038 rename it is
  at `%USERPROFILE%\AppData\LocalLow\Wildstone\Better Bean Battles\Player.log`;
  builds from before the rename use `...\Wildstone\Wildstone Arena\Player.log`.
  Search it for `WILDSTONE HOST REMOVED PEER`. A reason names the refusing rule.
  No such line means the host did not remove the player, which points to the
  client or the transport instead.

## 2026-10-30 firefight frame sequences

Scope: the user asked for continued improvement while at work. Captures had shown
weapons only at one moment after a shot. A new packaged mode follows real shots
frame by frame. Mode contract: [TOOLING](TOOLING.md).

- **First run** (`Artifacts/Visual/20261030-100138-388`, 32 captures). At 1:1 the
  Line Driver, SMG and shotgun beams read from muzzle to target, and the struck
  soldier shows its white hit flash. Problems found:
  - **Sniper rail.** It was the weakest read. Its glow (.09 m) was thinner than the
    Line Driver's (.11 m), and its first-person flare was still violet after the art
    pass made the sniper cyan.
  - **Support hand on frame 0.** In every sequence, frame 0 showed the support hand
    off the weapon or missing, often where the previous weapon had held it.
  - **Shotgun framing.** Its twin barrels sat near the centre of view, crowding the
    target.
- **Causes and fixes.**
  - Sniper: glow .15 m and core .045 m (rendering spec). Its viewmodel flare uses
    the beam colour.
  - Support hand: a capture artifact. Forced captures render from `LateUpdate`,
    before the frame's skinning pass, so skinned meshes drew the previous frame's
    bones. Fixture captures now set `forceMatrixRecalculationPerRender`, and the
    run `20261030-101358-874` shows the hand on the grip in every frame 0.
    `FamiliarLaserWeaponsHaveAuthoredContactsDistinctMechanismsAndBoundedFlashes`
    now also asserts both hands keep contact at peak kick (pistol, shotgun, sniper,
    SMG). A body reset test passed before any fix, so the first-person body was
    ruled out.
  - Shotgun: hip pose (.27, −.38, .50), which puts the settled muzzle where the Line
    Driver's sits.
  - Capture clock: `Time.captureDeltaTime` holds fast frames to 1/60 s, but the
    unscaled effect clock advances by real time on the slow shot-setup frame.
    Frame 1 of the first sequence landed at 25.7 ms. Each capture now records its
    true age, and the wrapper allows one frame of error.
- **Incoming rail** (`20261030-101538-012`, added as a fifth sequence). A remote
  sniper rail from the right flank sweeps past the lens: a strong cyan read whose
  white core lasts to frame 14 (about 0.23 s).
  - Near the lens it widens to about 50 px at 720p, because the width taper stops
    shrinking below 2.45 m. It is left as is; judge it in a playtest.
- **Final evidence** (smoke `BuildSmoke-20261030-101639-489`):

  | Mode | Run | Result |
  |---|---|---|
  | `-Firefight` | `Artifacts/Visual/20261030-101702-899` | 40 captures |
  | `-Weapons` | `20261030-101713-646` | 27 captures |
  | `-Motion` | `20261030-101939-175` | 13 captures; mean difference from `092528-069` under 0.4 grey levels, so the skinning change did not move settled poses |
  | `-Sequence` | `20261030-102001-299` | pass |
  | static | `20261030-102006-483` | pass |

- **Tests.**
  - Focused PlayMode 48/48 (`PlayMode-20261030-101149-049.xml`).
  - EditMode 459/461 with 2 explicit skips (`EditMode-20261030-101826-012.xml`).
  - PlayMode 155/156 with 1 explicit skip (`PlayMode-20261030-101840-618.xml`).
- **Playable build.** `Artifacts/Unity/Windows/WildstoneArena.exe`
  (`BuildWindows-20261030-101907-577`). Its content hash over 200 files is
  `5cb1330a9675e82e83c741cbfb848b3ac53bbef04fd52a539e7cdc51e93a9d3b`, which
  replaces `ca1a81ea…` above.
- **Limits.**
  - The frame-time sample was not rerun. These changes are two beam widths, a
    flare material and a pose constant inside the existing pools
    (`Perf-20261030-092622-341` stands).
  - Captures are labeled fixtures with synthetic hits, not networked play.
  - Whether the rails, flash and kick feel good needs a human playtest.

## 2026-10-30 map traversal and travel distance

Scope: backlog item 4, pickup reachability and route fairness. Contract and measured
table: the [map spec](superpowers/specs/2026-10-25-wildstone-modern-map-design.md).

- **Change.** `ArenaValidation.ValidateTraversal` runs inside `ValidateWorld`, so the
  arena build and the existing world tests now fail on any pickup or zone point that
  a start cannot reach, or cannot return from, by plain movement.
- **Tests** (`EditMode-20261030-102724-263.xml`: 463/465, 2 explicit skips).
  - `EveryStartReachesEveryPickupAndZonePointByPlainMovement` passes for all three
    maps and logs the per-target distance report.
  - `RemovingTheGalleryRampsStrandsItsPickups` deletes Atrium's two ascents and
    must report the gallery sniper as unreachable.
  - The saved-catalog world test now covers traversal too.
- **Findings.**
  - Everything is reachable on all three maps, including the 4 m gallery and
    terrace pickups, and no start is cut off from the zone route.
  - No boundary-wall top is reachable by plain jumps. The highest plain-reachable
    floor is 4 m on every map. (Wrong: this grid missed most ledge jumps. See
    [ledge jumps and outer walls](#2026-10-30-ledge-jumps-and-outer-walls).)
  - Split Gallery's distances are balanced: mean 48-50 m to weapons and exactly 50 m
    to zone points for every team.
  - Terrace Steps fails the spec's comparable-travel requirement. Teams 1 and 3
    spawn under the upper court and west terrace and must walk south before climbing.
    Team 3 walks about 97 m to the sniper against 65 m for team 0, and its zone-point
    mean is 73 m against 44 m.
  - Atrium shows a smaller lean of 6-8 m against its northern teams, which spawn
    under the north gallery.
- **Limits.**
  - Walking distance on a grid, not travel time. Slides, dashes, wall-runs and
    launches shorten some routes.
  - Exposure, escape options and the "more than one approach" property are not
    measured.
  - No geometry changed. A Terrace Steps fix (for example, opening the northern
    arrival courts to a short ramp) changes spawn-area geometry and needs a rebake,
    so it waits for the user.

## 2026-10-30 ledge jumps and outer walls

Scope: a review of the first traversal check. Contract: the
[map spec](superpowers/specs/2026-10-25-wildstone-modern-map-design.md).

- **Flaw.** The first grid linked jumps and drops only between neighbouring 0.5 m
  cells. A cell within 0.38 m of a ledge face is never standable, so most ledges had
  no jump at all. Its "no wall top reachable" result could not be trusted.
- **Fix.**
  - Jumps and drops now land up to 1.15 m away. A capsule sweep checks the rise and
    the move over the ledge.
  - A drop must land on the first surface below the ledge, not pass through a
    lower step.
- **Regression test.** `APickupOnACrateNeedsAJumpWithinReach` puts the pistol on a
  crate whose faces sit 0.1 m past a grid cell.
  - Limited to neighbouring cells (`EditMode-20261030-103316-920.xml`), the 1.6 m
    crate failed as unreachable.
  - With the fix, the 1.6 m crate is reachable and a 2.6 m crate is not.
- **Finding: outer-wall escape.** With real ledge jumps, the whole top of Atrium's
  6 m perimeter wall (1012 cells) was reachable from the 4 m gallery. The same is
  true by arithmetic on the other maps. A jump takes off at 12 m/s (2.4 m apex under
  30 m/s² gravity) and moves at up to 11 m/s, so the feet stay above +2 m for about
  2.6 to 6.2 m of travel.
  - Floors that run flush into a wall, 2 m below its top: Atrium's gallery and
    Terrace Steps' upper court.
  - Floors that stop 3 m short of a wall: Split Gallery's crossover and Terrace
    Steps' north edge.
- **Change.**
  - A new rule in `ValidateTraversal`: no reachable floor within 8 m of an outer
    wall may sit less than 2.8 m below its top.
  - `SixMetreOuterWallsAreAnEscape` restores 6 m walls and expects the rule to fail
    on each map.
  - Map revision 4 raises all outer walls to 8 m. It ran `Prepare`,
    `PrepareNetworking` and `Bake` (`Bake-20261030-103825-983`, GPU lightmapper,
    all three maps).
- **Results.**
  - EditMode 468/470 with 2 explicit skips (`EditMode-20261030-104011-034.xml`).
  - PlayMode 155/156 with 1 explicit skip (`PlayMode-20261030-104033-824.xml`).
  - The highest plain-reachable floor is now Atrium's 5.4 m parapet top and Terrace
    Steps' 5.8 m upper-court cover. Travel distances are in the map spec. With real
    ramp-side jumps, Atrium's northern lean drops to 2-3 m. Terrace Steps' remains.
  - A throwaway probe (`Test-20261030-104516-684.log`, test file removed) measured
    one Terrace Steps option: northern starts on the +4 m courts. Results are in the
    map spec. It was not adopted.
  - Map overviews (`Artifacts/Visual/Viewmodel-20261030-104118`) show the taller
    walls. The skyline still reads above them and nothing else moved.
  - Smoke `BuildSmoke-20261030-104148-776`: static captures
    `Artifacts/Visual/20261030-104218-201` (16) and `-Firefight`
    `20261030-104249-285` (40) pass.
  - Playable build `BuildWindows-20261030-104307-489`. Its content hash over 200
    files is `889c10b8b581640d99b34c96648ba442073e3dabec134c198c4ba013ba45defb`,
    which replaces `5cb1330a…` above.
- **Limits.**
  - Wall-run chains, dashes and launches are not modelled. They may still reach
    places plain movement cannot.
  - The 8 m reach and 2.8 m clearance come from motor arithmetic, not a measured
    jump.
  - Whether the taller walls change the feel of the arenas needs a playtest.
- **Frame time after the rebake** (`Artifacts/Visual/Perf-20261030-105354-982`, same PC and smoke player as the blast
  sequences):
  - Ultra: mean 1.65 ms, p95 1.87, p99 2.20, max 4.30.
  - Low: mean 1.52 ms, p95 1.71, p99 1.99, max 18.07 (one frame).
  - Ultra percentiles are within 0.07 ms of `Perf-20261030-092622-341`, so the extra
    lightmap atlases cost nothing measurable here.

## 2026-10-30 blast frame sequences

Scope: the requested grenade and explosion revamp had only single-frame captures.
`-Firefight` now adds grenade and Pulse Lobber blast sequences on the court 8 m
ahead, beside a soldier (mode contract: [TOOLING](TOOLING.md)).

- **First run** (`Artifacts/Visual/20261030-104901-356`). Both blasts showed only a
  dark full-size smoke cloud and the radius dome, with no fireball or flash on any
  frame.
- **Cause: a capture artifact.** The particle systems run on unscaled time. Each
  blast fired on the frame after the previous sequence's PNG encoding, so the first
  particle update aged it by most of a second. That killed the 0.4-0.6 s fireball and
  the 0.12 s flash, and left the 2 s smoke at full size. The driver now fires only
  after a normal-length frame. The hitscan sequences are unchanged, because their
  impacts are timed line effects.
- **Final run** (smoke `BuildSmoke-20261030-105143-140`,
  `Artifacts/Visual/20261030-105206-234`, 56 captures, ages within a frame of
  nominal):
  - Grenade: a yellow-white fireball over frames 0-4 (to about 70 ms). The soldier
    flashes white on frames 0-2. Dark smoke rises and is still a large cloud at frame
    50 (0.83 s). The orange radius dome and ground ring read to frame 8.
  - Pulse Lobber: an orange fireball with red ground glow, embers to frame 8, then
    smoke. Its colour separates it from the grenade.
- **Not changed.** The game's blast effects needed no fix. Whether the short
  fireball pop feels strong enough is a playtest question. A real hitch of most of a
  second just before a blast would also skip its fireball. No such hitch has been
  measured.
- **Playable build.** Unchanged (`889c10b8…`), because the capture driver compiles
  only into smoke builds.

## 2026-10-30 soldier outline

Scope: the user asked for continued graphics and VFX improvements, with every change
tested so it does not make the game worse. The firefight sequences showed the target
soldier at 10-15 m as an ivory figure on ivory stone. On a confirmed hit, its white
flash erased its edge entirely. The fix is a dark inverted-hull outline on remote
soldiers (contract: [rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md#6-d028-quality-stack)).

- **First version** (1.6 px, `Artifacts/Visual/20261030-115709-439`). The silhouette
  read, but the hulls of overlapping armour plates left dark blotches on the chest
  and arms. Pushing the hull 6 cm back from the camera removed most of them
  (`20261030-115819-269`). At 720p, 1.6 px became about one pixel, so the width is
  now 2 px at 1080p.
- **Final** (smoke `BuildSmoke-20261030-120009-950`), compared against the baseline
  `20261030-105206-234` at the same frames:
  - Line Driver frame 14: the strafing soldier reads against the wall at native size.
  - Sniper frame 2: the white hit-flash body now keeps its edge.
  - In the `-Motion` and `-Weapons` captures, the first-person body, arms and
    weapons are not outlined.
  - The title backdrop squad also uses `PlayerVisual`, so it is outlined too
    (`20261030-121549-176/title-720.png`). It reads crisper there, so this is kept.
- **Checks:**
  - EditMode 469/471, 2 skipped (`EditMode-20261030-115911-548`). The new
    `RenderingTierTests.BothTiersOutlineSoldiersOnTheEnemyLayer` checks both tiers.
  - PlayMode 155/156, 1 skipped (`PlayMode-20261030-115940-404`). `DerezTests` now
    checks that the outline layer is left during the dissolve and restored on respawn.
  - All five packaged capture modes pass: default `20261030-120025-237`, firefight
    `120055-610`, motion `120104-639`, weapons `120126-661`, sequence `120211-950`.
  - Frame time (`Perf-20261030-120241-180`): Ultra mean 1.64 ms and p99 2.09 ms
    (previous sample 1.65 and 2.20); Low mean 1.54 ms and p99 2.08 ms (previous 1.52
    and 1.99). That is within run-to-run noise.
- **Limits.** Gaps can remain where hard-edged plates split their normals. Whether
  2 px reads as clean or cartoonish in motion needs a human look.

## 2026-10-30 pickup pads

Scope: pickups were a floating ivory item over ivory stone, with no marker while ready
and nothing at all during the 15 s respawn. They now get a floor pad (contract:
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md#6-d028-quality-stack)).
`-Weapons` gained a `weapon-pickups` capture: five pads on Atrium's court, the
shotgun's 6 s into its respawn.

- **Iterations**, all with packaged `-Weapons`:
  - Additive ring plus a soft light shaft (`Artifacts/Visual/20261030-120722-539`).
    The rings were nearly invisible, and the shafts read as hazy glass boxes. The
    shafts were dropped. The same capture also showed the previous step's hit target
    still standing in frame, so the fixture now hides it.
  - Flat floor-aligned rings (`121127-422`). Fainter at grazing angles than
    camera-facing ones, so they were dropped too.
  - A ring painted in the item's hue over a dark track (`121319-024`, final widths in
    `121651-268`). The pistol's blue ring reads clearly. The pale shield ring reads
    least.
- **Checks:**
  - EditMode 469/471, 2 skipped (`EditMode-20261030-121437-580`).
  - PlayMode 156/157, 1 skipped (`PlayMode-20261030-121458-239`). The new
    `WeaponEffectsTests.PickupPadsShowReadinessAndRefillWithTheRespawn` checks the full
    ring, the dim partial arc, that it grows as the respawn runs down, and that other
    pads are unaffected.
  - Smoke `BuildSmoke-20261030-121527-095`. All five packaged modes pass: default
    `20261030-121549-176`, firefight `121620-203`, motion `121629-260`, weapons
    `121651-268` (28 captures), sequence `121738-249`.
  - Frame time (`Perf-20261030-121743-370`): Ultra mean 1.65 ms and p99 2.12 ms; Low
    mean 1.55 ms and p99 1.99 ms. That is unchanged.
- **Limits.** Readability from across a real court in motion, and whether the
  breathing ring distracts, need a human look. The shield and blink rings use their
  pale ability colours.

## 2026-10-30 remote soldier T-pose frame

Scope: frame 0 of the Line Driver firefight sequence and every `weapon-remote-hit`
capture showed the target soldier in its T-pose, arms straight out.

- **Reproduced.** The new `DerezTests.FirstFrameAndRespawnAreAlreadyPosed` failed
  (`PlayMode-20261030-121926-070`): after the first `Present` and `PresentHandling`,
  the left hand sat exactly at shoulder height (drop 1.2e-07 m).
- **Wrong first fix.** Warming the animator up with `Update(0)` on creation and after
  respawn did not help (`PlayMode-20261030-121958-778`), and was reverted.
- **Cause.** The soldier prefab uses `AnimatorCullingMode.CullUpdateTransforms`. The
  bones of a renderer that has not yet been drawn are not written, so in real games a
  soldier's first frame, and its first frame after each respawn reactivates it,
  rendered the bind pose. The same culling left off-screen shooters with a stale
  muzzle position for their beams.
- **Fix.** `PlayerVisual` sets remote soldiers to `AlwaysAnimate`. There are at most
  eight of them.
- **Checks:**
  - The focused test passes (`PlayMode-20261030-122032-730`).
  - EditMode 469/471, 2 skipped (`EditMode-20261030-122058-334`).
  - PlayMode 157/158, 1 skipped (`PlayMode-20261030-122119-821`).
  - Smoke `BuildSmoke-20261030-122143-108`, compared with `20261030-121620-203` and
    `121651-268`: firefight frame 0 (`Artifacts/Visual/20261030-122204-497`) and
    `weapon-remote-hit` (`122217-671`) now show the soldier posed with its rifle.
- **Limits.** A one-frame flash was easy to miss in play. No human report connects
  it to anything else.

## 2026-10-30 shield shell and stun halo

Scope: an active shield and a stun were the requested "stronger shield and stun
reads". `-Weapons` gained `weapon-status-shield` and `weapon-status-stun`: a remote
soldier 6 m ahead on Atrium's court (contract:
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md#6-d028-quality-stack)).

- **Baseline** (`Artifacts/Visual/20261030-122508-995`). The shield's 12-point ring
  at chest height could not be found. The stun halo was a thin "W" above the helmet.
  The fixture also left the previous step's hit flash on the soldier, so it was all
  white. It now clears the hit before each status step.
- **First shell** (`122836-296`). It was twice the intended size, because the scale
  ignored the built-in sphere's 1 m diameter. It now divides by the mesh bounds.
- **Final** (smoke `BuildSmoke-20261030-123016-857`, `123038-907`). A pale-cyan rim
  outlines the soldier with a clear centre, from the helmet top to the boots. The
  stun halo is a green zigzag around the helmet. It reads well against the dark
  window band, and is fainter against bright stone.
- **Checks:**
  - EditMode 469/471, 2 skipped (`EditMode-20261030-123147-132`).
  - PlayMode 158/159, 1 skipped (`PlayMode-20261030-123208-125`). The new
    `WeaponEffectsTests.ShieldShellAndStunHaloFollowTheirPlayers` checks that only
    the shielded player gets a shell, centred over them. It dims as the shield
    drains, shrinks with a slide capsule and hides when the shield ends. The stun
    halo is a closed 16-point loop around the head.
  - All five packaged modes pass: default `20261030-123231-089`, firefight
    `123258-019`, motion `123307-053`, weapons `123038-907` (30 captures), sequence
    `123329-108`.
  - Frame time (`Perf-20261030-123334-185`): Ultra mean 1.65 ms and p99 2.14 ms; Low
    mean 1.54 ms and p99 2.02 ms. That is unchanged.
- **Limits.** Whether the shell reads as too much of a bubble in play, and whether the
  halo is strong enough on bright stone, need a human look. The shell's break and
  expiry still use the existing event cues.

### Shield break pop (follow-up)

- A broken shield's shell now flares to strength 2 and swells 35% as it fades over
  0.22 s. Reduced motion skips it. Before, the shell vanished on the break and only
  the ring, sparks and flash marked it.
- `-Weapons` gained `weapon-status-shield-break` (31 captures, smoke
  `BuildSmoke-20261030-124304-304`, `Artifacts/Visual/20261030-124333-398`). The
  flaring shell reads as a brighter white rim, with the soldier still clear inside.
  The 0.1-0.18 s ring, sparks and flash had expired by capture time.
- **Checks:**
  - The new `WeaponEffectsTests.BrokenShieldShellPopsThenClears` checks that reduced
    motion skips the pop, the shell flares above full-shield strength, swells, then
    clears.
  - EditMode 469/471, 2 skipped (`EditMode-20261030-124445-646`).
  - PlayMode 159/160, 1 skipped (`PlayMode-20261030-124507-114`).
  - All five packaged modes pass: default `20261030-124530-550`, firefight
    `124557-497`, motion `124606-505`, weapons `124333-398`, sequence `124628-619`.
  - Frame time (`Perf-20261030-124633-693`): Ultra mean 1.65 ms and p99 2.13 ms; Low
    mean 1.55 ms and p99 2.12 ms. That is within the range of today's runs.
- **Not in the playable build.** `d6d14b5a…` predates the pop.

## 2026-10-30 movement lens and dash streaks

Scope: the user put weapons, gunplay, movement and the game's vibe first. Movement
had a slide FOV hold and wall-run/slide roll, but dashes, jumps and landings moved
nothing in the view. The contract is the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
(movement lens, dash streaks).

- **Adapted from vibegame1**, read-only, with values from its `PlayerFeedback`,
  `GameFeelSettings`, `CameraFX`, `DashFx` and `DashImpulse`:
  - FOV kicks: dash 5, jump 2.5, wall jump 4;
  - landing: −3·k FOV and a 0.22 m eye dip, with k = fall / 22 m/s;
  - slide exit −2.5; wall-run hold 3.5;
  - dash streaks: ten streaks, 0.22 s, (1−t)² fade.

  The kicks were scaled to this ~68° vertical lens. No code was copied.
- **Changed during the work.** The first version dropped the eye 0.2 m on a hard
  landing, as vibegame1 does. The character spec requires the camera eye to stay the
  authoritative shot origin, so it became a 2.5° downward nod instead.
- **Sequence evidence.** `-Sequence` now dashes at frame 11 (smoke
  `BuildSmoke-20261030-125857-778`, `Artifacts/Visual/20261030-130158-167`, compared
  with `20261030-124628-619`):
  - Sample 12: camera FOV 92.77 (was 90), with the streaks visible as thin cool
    lines radiating clear of the crosshair.
  - Sample 24: FOV 90.31, and the streaks are gone.
  - Sample 60, the slide exit: FOV 93.24 (was 94.90), from the −2° punch.
  - Sample 72: settled at 90.26 (was 90.44).
  - Eye height is unchanged at every sample (1.67 standing, 0.77 sliding). The
    wrapper's slide, recoil and reload gates pass.
- **Checks:**
  - New `FirstPersonSlideViewTests.MovementKicksWidenTheLensThenSettle` covers the
    dash kick and its settle, the wall-run hold, ADS removing the lens, reduced
    motion, and aim angles that never move.
  - New `HardLandingsNodTheViewAndKerbStepsDoNot` covers an eye that stays exact, a
    nod and a narrower lens on a 22 m/s landing, no response to a 1 m/s step, and
    the settle.
  - New `FirstPersonWeaponMotionTests.DashStreaksFlowAgainstTheTravelAndClear`
    covers the streak count, clearance from the reticle, a strafe streaming the
    other way, no refire, expiry and reduced motion.
  - The muzzle-pool test now counts muzzle lines by name, and its no-growth check
    includes the streak pool.
  - EditMode 469/471, 2 skipped (`EditMode-20261030-125919-942`).
  - PlayMode 162/163, 1 skipped (`PlayMode-20261030-125940-923`).
  - All five packaged modes pass: default `20261030-130004-259`, firefight
    `130035-287`, motion `130044-306`, weapons `130106-410`, sequence `130158-167`.
  - Frame time (`Perf-20261030-130203-218`): Ultra mean 1.65 ms and p99 2.16 ms; Low
    mean 1.55 ms and p99 2.04 ms. That is unchanged.
- **Limits.** Whether the kicks feel punchy or nauseating, and whether the streaks
  are too faint against bright ivory, can only be judged in play. Landings, jumps and
  wall runs are covered by tests, not captures. Not in the playable build
  `d6d14b5a…`.

## 2026-10-30 crosshair rim and movement follow-ups

Scope: reticle legibility, and three more movement reads. The contracts are the
[UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md) (dynamic crosshair,
hitmarkers) and the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
(movement lens).

- **Crosshair.** In default captures the ivory ticks almost disappeared on ivory
  stone. Each tick and the dot is now a 2 px ivory core in a 1 px near-black rim at
  0.8 alpha. The core keeps its old position, so the cone gap is unchanged. The hit
  cue gets a 1.5 px dark text outline.
  - A crop of the 1080p atrium and Terrace Steps captures (900,480 to 1020,600),
    compared with `20261030-130004-259`, shows the rimmed ticks clear on the same
    stone.
- **Movement.**
  - A perfect wall jump adds a +2° FOV kick. Before this, nothing showed its
    stamina refund.
  - A strafe dash banks 1.4° into its travel.
  - A slide entry nods 1.2°.
- **Checks:**
  - New `MenuPresentationTests.ReticleAndHitmarkerKeepADarkEdge` covers a dark,
    opaque 1 px rim on every reticle part, an ivory core, and the dark cue outline.
  - New `FirstPersonSlideViewTests.PerfectWallJumpKicksWiderThanAPlainOne` and
    `StrafeDashBanksAndSlideEntryNods`. These check the bank direction, the nod, the
    settle, and that aim angles never move.
  - Smoke `BuildSmoke-20261030-130651-741`.
  - EditMode 470/472, 2 skipped (`EditMode-20261030-130713-883`).
  - PlayMode 164/165, 1 skipped (`PlayMode-20261030-130735-120`).
  - All five packaged modes pass: default `20261030-130758-726`, firefight
    `130829-706`, motion `130838-730`, weapons `130900-847`, sequence `130952-583`.
  - Frame time (`Perf-20261030-130957-671`): Ultra mean 1.65 ms and p99 2.17 ms; Low
    mean 1.56 ms and p99 2.10 ms. That is unchanged.
- **Limits.** Whether the rim looks too heavy, and how the bank and nod feel, need a
  playtest. Wall jumps and slide entries are covered by tests, not captures.

## 2026-10-30 review fixes: kill credit and pellet cues

Scope: an independent read-only review of the afternoon's runtime changes:
- `FirstPersonBody`;
- the `ArenaHud` markers, feed and banner;
- the `ArenaAudio` remote scrape and hit dedupe;
- `ArenaPresentation`;
- `FusionPlayer.AcceptsCombatTick`.

Each finding was checked against the code before any change.
- **Fixed.**
  - **Kill credit.**
    - `ArenaMatch.Kill` runs only at the end of the tick, so `Hit` events at
      0 damage keep arriving for a target already at 0 health.
    - `ArenaHud` credited the last hit, which could name the wrong killer. A trade
      in the same tick, or a later projectile, did this, and it moved the kill
      banner and streak to that player.
    - It could also drop or invent `HEADSHOT` on shotgun kills.
    - `RecordHit` now ignores hits that dealt no damage. `Killer` reads the result.
    - The [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md) kill feed
      line says this.
  - **Pellet cues.**
    - The hit dedupe remembered only the last target, so pellets alternating
      between two neighbours re-cued on every switch.
    - `ArenaAudio.Play` now keeps a target bitmask per participant, sequence and
      tick.
  - **Knee tuck.** It now runs only while `Sliding` and not during the fold's
    release tail, when the lens is already back up.
  - **Kill banner.** `Confirm` no longer shows the banner itself. `Refresh` alone
    does, so a kill landing after your own death cannot flash it for a frame.
- **Not changed.**
  - A weaker hit during a stronger marker's display stays silent. The UI spec
    chooses that ("A stronger marker on display still suppresses a weaker one and
    its tick").
  - The streak survives your own death (respawn takes 3 s, the window is 4 s). That
    matches the stated rule and is left for the playtest.
  - A player who disconnects mid-slide keeps scraping until the replicated slide
    ends.
  - `FusionPlayer.AcceptsCombatTick` and `ArenaPresentation` were found clean.
- **Checks.**
  - New `MenuPresentationTests.TheLethalHitNamesTheKillerNotALaterZeroDamageHit`.
  - `ArenaWeaponAudioTests.PelletHitsOfOneShotPlayOneCue` now alternates two
    targets.
  - EditMode 486/488, 2 skipped (`EditMode-20261030-155850-686`).
  - PlayMode 176/177, 1 skipped (`PlayMode-20261030-155915-513`).
  - Smoke `BuildSmoke-20261030-155944-639`.
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines:
    - weapons `20261030-160007-520`
    - default `160106-776`
    - firefight `160133-678`
    - motion `160142-678`
    - sequence `160204-694`
    - music `160209-765`

    `weapon-kill-banner` and every motion and sequence frame are pixel-identical to
    the previous run. The rest differ only by animated menu, pickup and blast noise,
    at most 0.59 mean.
  - Frame time (`Perf-20261030-160223-997`): Ultra mean 1.64 ms and p99 2.13 ms; Low
    mean 1.54 ms and p99 2.02 ms.
  - Baseline two-process gunplay `20261030-160250-589` passes.
  - On the same smoke build, the rest of the Photon set passes too:
    - baseline: combat `160751-169`, team switch `160836-792`, reconnect
      `160859-772`, laser combat `160931-033`;
    - adverse150: gunplay `161006-477`, laser moving cover `161103-017`, grenade
      combat `161234-225`, special projectiles `161303-998`.
- **Disk.** At the user's request, 55 stale `C:/Temp/claude` folders from other
  sessions were deleted after checking for running sessions, Unity processes, git
  checkouts and writes in the last 48 h. That freed about 85 GB on C:.
- **Playable build.**
  - `BuildWindows-20261030-160627-717` wrote `Artifacts/Unity/Windows`.
  - Its content hash over 200 files is
    `d838ac03ac8287760aaa83baea3ccc5b9fbbf54f5152b58d3a534b78755d8c56`.
  - It overwrote the unplayed 15:40 `e732dfed…` build from the
    [kill banner](#2026-10-30-kill-banner).
  - A 15-second headless start (`Artifacts/Unity/WindowsStartup-20261030-1607.log`)
    initialised the engine with no logged exception. That proves startup only.

## 2026-10-30 SMG held-fire verdict

Scope: the intermittent `SMG held fire verdict mismatch` in two-process gunplay. It
was first seen under adverse150 and failed once at baseline at 15:42.
- **Cause.**
  - Baseline gunplay `20261030-154234-042` (smoke `BuildSmoke-20261030-153824-894`)
    failed with `shots 5 hits 5 damage 50 spacing 16 ammo 27`.
  - Both peers agree on the shot ticks 925, 941, 957, 976 and 992. The emulated
    client sent one fire request every four to five ticks (about 25 fps), so held
    fire was accepted only every 16 to 19 ticks rather than the SMG's 10.
  - Bloom decays only after 0.1 s (12 ticks) without a shot. After the 19-tick gap it
    shrank, which broke the harness's spread-grows-every-shot check.
  - The rules behaved as specified, so this was harness timing, not a gameplay fault.
  - The adverse failures fit the same cause: late `Fire` RPCs that are dropped
    (D035) open gaps longer than the bloom delay.
- **Fix.** In `FusionSmokeDriver.Combat`, bloom growth is now required only across
  shot gaps of up to `Ticks(BloomDelaySeconds)`. Count, damage, ammo, minimum
  spacing, first-shot spread and peer agreement are unchanged.
- **Checks.**
  - Before the change, two baseline reruns passed with exact 10-tick spacing
    (`20261030-154353-031`, `154448-329`).
  - With it (smoke `BuildSmoke-20261030-154621-427`), baseline `154651-559` and
    adverse150 `154816-350` pass.
  - Baseline combat `154122-454` passed on the earlier smoke build.
  - Smoke-only harness code, so the suites and captures were not rerun.
- **Limits.**
  - Held fire on a slow client still fires below the SMG's rate: a shot is accepted
    only when a request arrives after the interval.
  - Each shot waits at most one frame for the next request: up to 2 ticks at 60 fps,
    and none when the frame period divides the 10-tick interval. A slow frame rate
    costs more.
  - Fixing it would mean scheduling shots by the client's fire tick, which is a
    combat-authority change not made here.

## 2026-10-30 kill banner

Scope: a kill confirmed only with the red ✕, its tone and a corner feed line. Nothing
named the victim where you were looking, or rewarded a quick follow-up. The
[UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md) (Kill banner) owns
the contract. Presentation only.
- **Change.**
  - `ArenaHud.Confirm` runs on your own attributed kill.
  - `KillBanner` formats the text, and the `.hud-kill-banner` style in `ArenaMenu.uss`
    draws it.
  - `ArenaMenu.Hud` exposes the playing HUD to the smoke-only capture driver.
- **Rejected look.**
  - The first capture drew red text over the floor with only an outline. It was
    muddy on ivory.
  - The head glyph `◎` rendered like the violet team's `●`.
  - The capture after `weapon-kill-banner` still showed the banner fading, so the step
    is now last.
- **Checks.**
  - New `MenuPresentationTests.QuickKillsCountUpUnderTheCrosshair` checks:
    - single, double, triple and ×4 text;
    - the headshot wording;
    - the 4 s window boundary;
    - the reset after a longer pause.
  - EditMode 485/487, 2 skipped (`EditMode-20261030-153326-833`).
  - PlayMode 176/177, 1 skipped (`PlayMode-20261030-153347-797`).
  - Weapons capture `20261030-153839-558` (smoke `BuildSmoke-20261030-153824-894`) has
    33 frames:
    - `weapon-kill-banner` reads `DOUBLE · ELIMINATED ● P4 · HEADSHOT` under the
      target;
    - only `weapon-pickups` differs from `20261030-152148-869`, by 0.10 mean
      (pickup animation).
  - The other five modes were captured on smoke `BuildSmoke-20261030-153411-481`,
    before the step reorder. They pass, with no exception, NRE or RenderGraph lines:
    - default `20261030-153525-450`
    - firefight `153552-340`
    - motion `153601-311`
    - sequence `153623-391`
    - music `153628-454`

    Against the knee-tuck runs, motion and sequence are pixel-identical. The others
    differ only in animated menu, title and blast areas, by at most 0.84 mean.
  - Frame time (`Perf-20261030-153642-682`): Ultra mean 1.65 ms and p99 2.21 ms; Low
    mean 1.55 ms and p99 2.14 ms.
- **Limits.**
  - No online kill has shown it.
  - Whether the streak window feels right is for the playtest.
  - The disk filled during one smoke build (`BuildSmoke-20261030-153748-133`). Moving
    old artifacts to `D:/WildstoneArchive` fixed it.
- **Playable build.**
  - `BuildWindows-20261030-154022-532` wrote `Artifacts/Unity/Windows`.
  - Its content hash over 200 files is
    `e732dfed9df1be38bbbeec428f82d12b1b1b2c0c231e906d5ff20ba95aa7c227`.
  - It overwrote the unplayed 15:24 `545fe335…` build from the
    [slide knee tuck](#2026-10-30-slide-knee-tuck).
  - A 15-second headless start (`Artifacts/Unity/WindowsStartup-20261030-1540.log`)
    initialised the engine with no logged exception. That proves startup only.

## 2026-10-30 reconnect cycles and late peer identity

Scope: the foundation spec's repeated-reconnect check, using the new
`Invoke-ReconnectCycles.ps1` ([TOOLING](TOOLING.md)). Each cycle is a full
`reconnect` smoke run in fresh processes: a Photon join, ten seconds of play, a
host-forced disconnect and a credential-bound return to the held seat. Contract: the
[foundation spec](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#7-sessions-seats-and-recovery).
- **Pre-fix run.** On `Smoke-20261030-233842-901`, summary
  `Artifacts/Smoke/20261030-194523-041-reconnect-cycles.json`: 14 of 15
  passed before I stopped it to test the fix. Cycle 8
  (`20261030-194914-538-reconnect-baseline`) failed. The host logged
  `WILDSTONE HOST REMOVED PEER reason=invalid-peer phase=Running` when the client
  rejoined. The client's start was then cancelled, and it reported "The reconnect
  attempt failed".
- **Cause.** The connect token was readable, which fixes the credential at 32 bytes.
  So `invalid-peer` there means `NetworkRunner.GetPlayerUserId` returned no ID for
  the joining peer. The SDK documents that it returns null when no valid player is
  found. Here it raised `PlayerJoined` before it could report that peer's ID. The
  host refused and disconnected the peer in that same callback, with no retry.
- **Fix.** `FusionLobby.Admit` now reports `identity-pending` for a remote peer with
  no ID yet. The startup, join and host-recovery admission paths keep that peer
  connected. The lobby retries it every tick for up to 5 seconds, then refuses it as
  `invalid-peer`. The host's own identity never waits. A pending peer has no seat,
  so its RPCs and inputs bind to nothing.
- **Checks.**
  - New EditMode test
    `FusionSessionBoundaryTests.APeerWithoutAUserIdIsRetriedUntilItsDeadlineInsteadOfRemoved`.
  - `Check-NetworkEvidence.ps1` type checks pass with and without `WILDSTONE_SMOKE`.
  - Suites: EditMode 499/501, 2 explicit skips (`EditMode-20261030-195413-647`).
    PlayMode 169/170, 1 explicit skip (`PlayMode-20261030-195445-247`).
- **Fixed-build run.** On `BuildSmoke-20261030-195516-201`
  (`Smoke-20261030-235524-341`), summary `Artifacts/Smoke/20261030-195608-398-reconnect-cycles.json`: 100 of 100 passed, with no host removal.
  - Cycle duration 31.4–39.3 s (mean 32.8).
  - Reconnect time, from the host's forced disconnect until the host sees the
    client back in its seat: 1.1–1.6 s (mean 1.2). Pre-fix passing cycles:
    1.2–2.2 s (mean 1.3).
  - The identity wait logs nothing, so how many of these cycles hit the race and
    were absorbed by it is unknown. The pre-fix rate was 1 in 15.
- **Related.** The earlier unexplained startup `Requested` disconnect in
  `20261029-170011-577-grenade-combat-baseline`
  ([host removal reasons](#2026-10-30-host-removal-reasons)) could be this race:
  that run predates removal logging. Its logs no longer exist, so this is
  unconfirmed. The intermittent host-recovery `DeadlineExceeded` (`012707`,
  `183442`) is a different failure: neither log has a removal line.
- **Limits.**
  - Same-PC baseline profile.
  - Fresh processes per cycle, so this does not measure accumulation inside one
    long-lived host.
  - The 5-second wait is a builder value.

## 2026-10-30 online rematch and night playable build

Scope: the backlog's online rematch. After a normally finished match, the host's
Results offers Rematch, which returns the same room to its lobby. Contracts: the
[foundation spec](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#7-sessions-seats-and-recovery)
Rematch block and the UI spec's Online rematch. This build also carries the
[late peer identity fix](#2026-10-30-reconnect-cycles-and-late-peer-identity) and
the D038 rename to Better Bean Battles ([DECISIONS](DECISIONS.md)).
- **Rules and menu checks.** New: `SessionRulesTests.Rematch*` (2),
  `MenuPresentationTests.OnlineResultsOfferRematchOnlyToTheHostOfALiveFinishedMatch`
  (4 cases), `AbortedOnlineResultsTests.FinishedRoundRematchNeedsTheHostAndPeersFollowTheLobbyInTheSameSession`
  and an `OnlineMenuTests` bridge assert.
- **Suites.** EditMode 506/508 with 2 explicit skips
  (`EditMode-20261030-201127-752`). PlayMode 169/170 with 1 explicit skip
  (`PlayMode-20261030-201200-564`).
- **First online run.** On smoke `BuildSmoke-20261030-201232-566`
  (`Smoke-20261031-001241-195`), the `rematch` scenario passed at its first run,
  `20261030-201327-704-rematch-baseline`. Both peers agree on every point
  ([TOOLING](TOOLING.md) has the full verdict):
  - two rounds were played and finished, host team winning both;
  - the client's own rematch press changed nothing;
  - after the host's rematch, both peers were in the same room `7B43DEF0` with the
    same match ID, seats, teams and generations (host 1, client 2), and ready was
    cleared;
  - round 2 had a new match object (1027 then 1030), zero score, no projectiles,
    full health, 13/13 pickups and a new HUD;
  - the host published a round-2 checkpoint (tick 1183, after 702 at the rematch).
- **Photon regression set** on the same smoke build:
  - rematch baseline: passed `20261030-201425-083`;
  - rematch baseline: passed `20261030-201443-301`;
  - combat baseline: passed `20261030-201501-311`;
  - laser-combat baseline: **failed** `20261030-201527-020`;
  - reconnect baseline: passed `20261030-201728-388`;
  - team-switch baseline: passed `20261030-201800-861`;
  - late-join baseline: passed `20261030-201825-003`;
  - gunplay adverse150: passed `20261030-201856-290`;
  - projectile-combat adverse150: passed `20261030-202020-251`;
  - grenade-combat adverse150: passed `20261030-202247-378`;
  - special-projectiles adverse150: passed `20261030-202318-163`;
  - deflection adverse150: passed `20261030-202349-302`;
  - laser-moving-cover regional80: passed `20261030-202429-449`.
  - The `laser-combat` failure (`DeadlineExceeded`, every laser verdict reached)
    overlapped the Windows build. Its cause is in the
    [smoke harness](#2026-10-30-smoke-harness-peer-reads-stun-probe-and-late-attacks) section.
  - Injected latency profiles do not apply to `rematch`; it runs at baseline only.
- **Playable build.**
  - `BuildWindows-20261030-201525-504` wrote `Artifacts/Unity/Windows`. Its content
    hash over 200 files is
    `058fa3ddd042e9b2f9df0d0049677e9ecac4a5628c393d80d1034b410be6f5cc`.
  - Its `app.info` names the product "Better Bean Battles".
  - It replaces the unplayed 19:44 `625026d1…` build.
  - A 15-second headless start (`Artifacts/Unity/WindowsStartup-20261030-2016.log`)
    initialised with no logged exception. That proves startup only.
- **Limits.**
  - The rematch has not been played by a person.
  - Each round is shortened by `SmokeFinish`, not a 480 s match.
  - From code reading and unmeasured:
    - A host migration before round 2's first checkpoint should abort that round.
    - A peer that drops while Results is showing still faces the 30-second absence
      abort, because the session stays `Running`.
  - The D038 rename resets each player's saved settings once, because Unity keys
    them by product name.

## 2026-10-30 smoke harness: peer reads, stun probe and late attacks

Scope: why `laser-combat` hit `DeadlineExceeded` in the
[rematch regression set](#2026-10-30-online-rematch-and-night-playable-build), plus
the stun-probe follow-up. All changes are smoke-only (`WILDSTONE_SMOKE`); the
playable `058fa3dd…` build has none of this code. Commands are in [TOOLING](TOOLING.md).
- **Stage timeline.** Each peer's JSON now has `stageTimes`, every driver stage with
  the second it began.
- **Peer reads.** Three passing `laser-combat` runs on `Smoke-20261031-002532-909`
  took 26, 76 and 17 s from `playing` to `verified` on the host (`202606-849`,
  `202657-912`, `202833-393`); the stable phase needs 10 s.
  - Cause: the peer's status file is briefly unreadable while it is rewritten. That
    read returned no peer, the driver returned false before its completion check,
    and the 10-second stability window started over.
  - Fix: `ReadPeer` keeps the last valid peer status when the file is locked or
    missing.
  - On `BuildSmoke-20261030-202923-386` (`Smoke-20261031-002935-030`), three
    `laser-combat` runs took 10, 10 and 10 s (`203108-760`, `203137-586`,
    `203207-418`).
- **Stun probe.** On the third stun shot the host holds forward input on its stunned
  target. Both peers sampled the target 62–64 times while stunned with 0.00 m of
  travel, then saw 3.47 m by 0.4 s after expiry: baseline `202958-661` and
  adverse150 `203035-266`. This covers the host's own avatar only; a predicting
  client target is still unproven.
- **Regression set** on `Smoke-20261031-002935-030`:
  - special-projectiles baseline: passed `202958-661`;
  - special-projectiles adverse150: passed `203035-266`;
  - laser-combat baseline: passed `203108-760`;
  - laser-combat baseline: passed `203137-586`;
  - laser-combat baseline: passed `203207-418`;
  - combat baseline: passed `203236-806`;
  - reconnect baseline: passed `203302-525`;
  - rematch baseline: passed `203335-400`;
  - team-switch baseline: passed `203353-379`;
  - gunplay adverse150: passed `203416-976`;
  - projectile-combat adverse150: **failed** `203450-307`;
  - grenade-combat adverse150: **failed** `203521-671`;
  - deflection adverse150: passed `203553-037`;
  - laser-moving-cover adverse150: passed `203633-556`.
- **Late attacks.** The projectile and grenade failures, and the rerun failure
  `204035-322`, have the same cause. Each host log shows
  `WILDSTONE COMBAT RPC EXPIRED` (`kind=Fire sequence=2 age=25`, or `kind=Grenade
  sequence=1 age=24`).
  - The reliable RPC was lost, resent and arrived about 200 ms late, past the
    18-tick attack window, so the host dropped it before validation. This is the
    known limit of the [resent inventory actions](#2026-10-30-resent-inventory-actions)
    fix; the fixtures fired once and never re-sent.
  - These runs overlapped the 100-cycle reconnect run, so four player processes
    shared the CPU. That may explain why it happened in 3 of 8 runs here and in none of 11 on
    earlier builds. Not measured.
  - Fix (harness only): if the host has not acknowledged the fire or throw after 1 s,
    the client sends the same sequence again, as a player would press again. The
    host rejects a duplicate as stale, so it cannot spend twice. `resends` counts it.
  - Not changed: the 150 ms attack window. A player whose attack RPC is lost and
    resent under similar loss still loses that press. Widening the window together
    with the [rewind cap](#2026-10-30-measured-rewind-age) remains the user's decision.
- **After the fix**, `BuildSmoke-20261030-205054-770` (`Smoke-20261031-005106-182`):
  all 10 passed. No host logged an expired RPC and no fixture re-sent, so the
  re-send path is type-checked but not yet exercised. These runs did not overlap
  other Photon runs.
  - projectile-combat adverse150: passed `205129-166`;
  - grenade-combat adverse150: passed `205203-285`;
  - projectile-combat adverse150: passed `205233-233`;
  - grenade-combat adverse150: passed `205301-141`;
  - projectile-combat adverse150: passed `205331-495`;
  - grenade-combat adverse150: passed `205359-263`;
  - projectile-combat baseline: passed `205429-626`;
  - grenade-combat baseline: passed `205456-875`;
  - special-projectiles adverse150: passed `205526-203`;
  - laser-combat baseline: passed `205557-603`.
- **Earlier hang-guard failures.** `012707` (laser moving cover) and `183442` (D036
  A/B) failed with `DeadlineExceeded` after every verdict had verified, the host
  stuck in its 10 s stability window (`012707` was at 10.006 s). That matches the
  peer-read restart above, so it is the likely cause; the old logs cannot show the
  unreadable reads. On the same smoke build, 10 more runs all passed:
  - laser-moving-cover adverse150: `205839-049`, `210017-050`, `210204-847`;
  - gunplay adverse150: `205944-107`, `210131-315`;
  - migration baseline: `205915-307`, `210240-880`;
  - migration-crash baseline: `210053-726`, `210309-245`;
  - late-join baseline: `210347-924`.

  Every moving-cover and gunplay run held its stability phase for exactly 10 s on
  both peers.

## 2026-10-30 weapon read fixes: grenade in hand, sniper rail, sword slash, title fit

Scope: clear defects from the 19:20 captures (`Artifacts/Visual/20261030-192033-554`
weapons, `192136-972` default) and from the first captures of the fixes. All of this
is presentation only.
- **Grenade in hand.** In `weapon-grenade-0/1` the viewmodel stayed on the Line
  Driver with no grenade.
  - While the throw key is held with a grenade, the support hand now leaves the gun
    and holds the grenade, scaled to 0.08 m. The host's `Accepted` still starts the
    throw, and the grenade leaves the hand during the swing.
  - The first capture (`223550-837`) showed the grenade half behind the HUD vitals
    panel, so the hold point moved up and toward the centre. Values are in the
    [character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
    arms row.
  - In `224547-655` the grenade sits in the palm with the fingers wrapped over it,
    clear of the vitals panel, in `weapon-grenade-aim` (key held, arc shown, nothing
    thrown) and the throw frames. The armoured forearm crosses the ability chips.
  - `weapon-grenade-blast` also shows a grenade in hand. That frame plays the throw's
    `Accepted` and the explosion together; in play they are at least 1.5 s apart.
- **Muzzle lamp.** `weapon-pulselobber-0` and `weapon-plasma-impact` showed a pink
  wash on the floor under the Lobber. Every muzzle lamp is dimmer and shorter now
  ([rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md) effects
  row). In daylight the Line Driver frames are visually unchanged.
- **HUD backing.** In `weapon-lasersniper-0` the scope and cyan ring showed through
  the ammo panel. Both bottom panels now use the score chips' 0.72 backing
  ([UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md)).
- **Sniper rail.** The shooter's own rail read as a hairline. The beam texture is
  above half brightness across only the middle 22% of its width, so the visible core
  was about 2.5 px. Glow and core went from .15/.045 m to .26/.075 m (rendering spec
  "Shot reads" row).
  - `weapon-lasersniper-0`: about 7 px at the muzzle, was 5.
  - `firefight-lasersniper-00` (`224651-778` against `192204-695`): a broader band
    with a visible sheath from muzzle to head.
  - `firefight-incoming-sniper`: heavier, a bright band about 60 px wide where it
    passes the lens. It reads as a strong threat without hiding the view.
- **Sword slash.** The swing drew two constant-width lines across the whole view.
  Both arcs are now crescents, pointed at the tips and full width mid-swing, with a
  wider glow (rendering spec sword row).
  - Weapon-mode frames are taken at least 0.04 s after the effect starts, plus a slow
    setup frame, so a 0.2 s arc is captured already thinned. Measured on the same
    delay, the arc is thinner toward the screen edges and 2–4 px thicker mid-swing.
  - Its full width has not been seen. The firefight mode, which records true ages,
    has no sword sequence.
- **Title fit after the rename.** The two-line "BETTER BEAN / BATTLES" title made the
  720p title column scroll. On ultrawide it pushed Quit below the fold; there the
  footer note was already hidden before the rename.
  - The hero face went from 80 to 64 px, and the menu column's vertical padding from
    4% to 3% of the width.
  - In `225632-729`, `title-720` fits with no scroller. `title-ultrawide` shows Quit
    again; the footer note still needs a scroll, as before the rename.
- **Kill banner.** At 63% down, in 15 px text on a 0.62 pill, the banner ran into the
  Line Driver's muzzle and the red streak word was faint. It now sits at 59% in 17 px
  text on a 0.8 pill ([UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md)).
  In `weapon-kill-banner` (`233051-805`) it clears the muzzle and `DOUBLE` reads
  clearly.
- **Pulse Lobber lines (not changed).** Thin orange lines from the bell and across the
  plasma blast are the faint 4-point star in `T_VfxFlare`, enlarged by big flashes:
  0.55 m at the near-camera bell and about 3.5 m for the blast. They last 0.09–0.12 s
  and were already in the 19:20 frames.
- **Checks.**
  - New `FirstPersonWeaponMotionTests.HeldGrenadeShowsInTheSupportHandAndLeavesItDuringTheThrow`:
    - no grenade without the key;
    - while held, a 0.08 m grenade centred on the left grip, left of centre and
      between 20% and 60% of the view height (clear of the HUD vitals), with the hand
      off the gun;
    - still held 0.2 s after release, gone mid-swing;
    - both hands back on the gun afterwards.
  - The first run also failed `MuzzleUsesAuthoredSocketAndFourReusedElementsExpireAndReset`:
    the grenade model's own `Muzzle` marker answered a lookup over the whole camera.
    The held copy's parts are renamed. Production lookups were already scoped to the
    weapon.
  - `WeaponEffectsTests.EveryWeaponHasBoundedDistinctShotGeometry(LaserSword)` now
    asserts the crescent: zero width at both tips, full width mid-swing.
  - `Invoke-VisualCheck.ps1 -Weapons` expects 35 captures (was 34).
  - Suites on the final source: EditMode 506/508, 2 explicit skips
    (`EditMode-20261030-232935-787`); PlayMode 170/171, 1 explicit skip
    (`PlayMode-20261030-233001-749`). Smoke build `BuildSmoke-20261030-233029-250`.
    The title captures (`225632-729`) ran on `BuildSmoke-20261030-225428-178`, before
    the kill-banner change.
    The weapons (`224547-655`) and firefight (`224651-778`) captures ran on
    `BuildSmoke-20261030-224504-369`, which differs from the final source only in the
    title size and menu padding.
  - Frame time on `224504-369`, idle GPU, Chrome and MSI Afterburner running
    (`Perf-20261030-224917-718` and `224952-250`): Ultra mean 1.96/2.02 ms, p99
    2.78/2.99 ms; Low mean 1.71/1.68 ms, p99 2.48/2.34 ms. Against the last valid pair
    (`182822-933`/`182849-555`: Ultra 1.77/1.86, p99 2.62/3.15; Low 1.61/1.66, p99
    2.19/2.35), Ultra means are about 0.15 ms higher and p99 is in the same range.
- **Limits.** Captures are fixtures, not play. The grenade hold, rail and slash
  widths are proposed values pending a human look in motion.

## 2026-10-30 late-night playable build

Scope: a playable build with the
[weapon read fixes](#2026-10-30-weapon-read-fixes-grenade-in-hand-sniper-rail-sword-slash-title-fit)
on top of the [20:15 build](#2026-10-30-online-rematch-and-night-playable-build).
- **Build.** `BuildWindows-20261030-233237-714` wrote `Artifacts/Unity/Windows`, from
  the same source as smoke build `BuildSmoke-20261030-233029-250` and its suites. Its
  content hash over 200 files is
  `db84d55e47435b57190fccab8036e5c9e0a7100864615a6bf64a0f18ee63a0fb`. Its `app.info`
  names "Better Bean Battles".
- **Archive.** The 20:15 build `058fa3dd…` it replaced is kept at
  `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-058fa3dd`.
- **Startup.** A 15-second headless start
  (`Artifacts/Unity/WindowsStartup-20261030-2334.log`) logged no exception or error.
  That proves startup only.
- **Limits.** Not yet played.

## 2026-10-30 30-minute capacity soak

Scope: the first full-length run of the eight-process connection and memory soak
(`./Tools/QA/Invoke-NetworkSmoke.ps1 -Scenario capacity -SoakSeconds 1800 -TimeoutSeconds 2100`).
Smoke build `BuildSmoke-20261030-225428-178`, baseline profile, real Photon, one PC.
Nothing else used the CPU during the run.
- **Result.** Passed, `Artifacts/Smoke/20261030-225821-320-capacity-baseline`. All
  eight peers kept coherent players, seats and four teams of two for 1800 s with no
  recovery or authority change. Each ran 216,005 SDK ticks (120 Hz).
- **Time split.** 480 s Running, then 1320 s on Results. The peers send no gameplay
  actions, so this is not a gameplay soak.
- **Memory** (Windows process counters, start to end of the hold, about 3,030
  samples each):
  - host: working set +5.5 MB (324 to 329 MB), private bytes flat (447 MB);
  - clients: working set +4.5 to +8.0 MB, private bytes +1.0 to +11.0 MB, from about
    440 MB.
  - Only the start and end values are kept, so whether this growth levels off or
    keeps climbing is not known.
- **Traffic** (Running): host 125 KB/s out and 34 KB/s in; each client 17.8 KB/s in
  and 4.8 KB/s out. The Results figures are the same. SDK remote RTT averaged 11 ms
  (4.6–87.6 ms), 13,272 samples.
- **Limits.** One PC, baseline profile, no gameplay actions and mostly Results time.
  This is not the backlog's gameplay soak, a two-hour soak or external-network
  evidence.
- **Rerun with the memory series** (`20261030-233817-422-capacity-baseline`): stopped
  by hand after about 2 minutes, because a Better Bean Battles window was open and
  eight peers would have competed with it. No result.

## 2026-10-30 stun end on a predicting client

Scope: the open question from the
[stun probe](#2026-10-30-smoke-harness-peer-reads-stun-probe-and-late-attacks),
whether a stunned player on a client machine unfreezes on time.
- **Cause.** The host sent only a "suppressed" flag in the movement snapshot. A
  client predicts its own player from the last snapshot, so it stayed frozen after the
  stun ended until the host's unsuppressed snapshot arrived, about one round trip
  late, and then jumped forward by the movement it had missed.
- **Fix.** The snapshot now also carries the movement tick the stun ends
  (`SuppressedUntil`). The motor clears a stun-only suppression on that tick by itself,
  so the client's prediction releases on the same tick as the host. Death,
  disconnection, warm-up and match end still lift only from the host. Host movement
  is unchanged. The movement snapshot grows by one 4-byte word (99 to 100; the
  player object 105 to 106).
- **Checks.**
  - `ArenaMotorTests.StunSuppressionLiftsAtItsEndTickWithoutAHostUpdate`: with no
    host update, input is ignored before the end tick and moves the player from it.
  - `FusionMatchRunnerTests`: on a real single-peer runner the host's end tick
    matches the stun's remaining match ticks, and both values clear when the stun
    ends.
  - Suites: EditMode 506/508 (`EditMode-20261030-234337-141`), PlayMode 171/172
    (`PlayMode-20261030-234403-158`), smoke build `BuildSmoke-20261030-234431-909`.
- **Real Photon regressions** on that build, all passed:
  - special-projectiles baseline `234543-472` and adverse150 `234618-583`. The host
    target sampled 60–64 times while stunned with 0.00 m of travel, then 3.47 m by
    0.4 s after the stun, the same as before the change;
  - combat baseline `234649-950`, reconnect baseline `234715-153`, rematch baseline
    `234747-520`;
  - gunplay adverse150 `234805-553`, projectile-combat adverse150 `234839-026`.
- **Limits.** No run makes a client the stun target, so the on-time release on a
  client is shown by the motor and host checks, not by a two-process measurement.
  The start of a stun still corrects a client back to where the host stunned it;
  only the host decides a hit.

## 2026-10-31 stunned client, two-process proof

Scope: the open limit of the [stun end fix](#2026-10-30-stun-end-on-a-predicting-client),
whether a stunned player on a client machine releases on time. New smoke scenario
`stun-client` (smoke builds only).
- **Fixture.** The host moves the client's player onto open ground at (-30, 0, -15).
  The client then holds forward input. Once the client reports more than 1 m of
  movement, the host applies the production stun rule (`CombatRules.Stun`) to it,
  standing in for a Stun Gun bolt; the bolt's flight is not under test. The client
  reads its own predicted movement state every frame and the host reads its
  authoritative copy.
- **Gates.**
  - Release tick (client): the last tick it saw stunned is before the stun's end
    tick, and the first tick it saw free is at or after it. This holds at any frame
    rate, because one frame can advance several ticks.
  - Travel while stunned (both peers): under 0.05 m in the last 0.15 s before the
    end tick. A stun removes input, not momentum, so a full-speed runner coasts
    about 8 cm in the first 0.15 s. The first run measured from 0.15 s after the
    stun and failed on that coast: host 0.066 m, client 0.13 m.
  - After release (both peers): over 0.3 m by 0.4 s after the stun ends.
  - Snap (client): its largest per-frame jump after release is under 0.5 m.
- **Results.** Smoke build `BuildSmoke-20261031-000845-774`, both runs passed:

  | Run | Measured RTT | Client end / last stunned / first free tick | Stunned travel | Travel 0.4 s after | Largest frame step |
  |---|---|---|---|---|---|
  | baseline `000914-501` | 10–14 ms | 140 / 139 / 140 | 0.00 m both | 3.86 m both | 0.092 m |
  | adverse150 `000941-364` | 163–164 ms | 117 / 116 / 117 | 0.00 m both | 3.86 / 3.95 m | 0.092 m |

  At 150 ms the client still releases on the stun's last tick, not a round trip
  late. Its largest step after release is one 120 Hz tick at run speed, so there
  is no catch-up jump.
- **Harness fixes found by the first runs.** The worker's fixture was missing from
  the player's injected-profile allow-list (`FusionSmokeNetworkProfile`), so the
  adverse150 host refused to start. The release check is now frame-rate
  independent and the travel window is measured as described above.
- **Limits.** The stun is injected on the host, not fired as a bolt. At 150 ms the
  start of a stun still pulls the client back to where the host stunned it. In
  this run the client had predicted more than 1 m of travel and was put back
  0.045 m from its start (`movingTravel`), because the host's copy had barely
  moved. Only the host decides a hit. This is not external-network acceptance.

## 2026-10-31 gameplay soak

Scope: BACKLOG row 6's gameplay soak, with the
[allocation figures](TOOLING.md) added to the capacity runner. Eight real Photon
processes on one PC, `capacity -Gameplay`. Every peer moves, fires at the nearest
enemy every 0.4 s and reloads, and the host rematches 3 s into Results.
- **100 s, 30 s rounds** (`-SoakSeconds 100 -RoundSeconds 30`):
  - `20261030-235611-905` passed: 4 rounds, 169–180 shots, 4–7 deaths per peer.
    About 16 GC collections/s, most of them from the harness reading every peer's
    status file and searching the scene every frame.
  - The harness now reads peer files twice a second and counts avatars from a
    reused list. The next run (`20261031-000031-778`) failed: the host asked a
    stopped runner for its objects (`NullReferenceException` in
    `NetworkRunner.GetAllBehaviours`). The count now runs only while the runner is
    running.
  - `20261031-000157-468` passed: 4 rounds (3 rematches); 169–183 shots, 1–4
    reloads and 5–8 deaths per peer; 90 s running and about 1 s between rounds.
    - GC: 0.39–0.43 collections/s per peer.
    - Managed heap: 17.6–23.0 MB at the start, 19.9–23.6 MB at the end.
    - Working set: +14.4 to +17.4 MB over 100 s (319–327 MB at the start).
    - Traffic: host 431 KB/s out and 43 KB/s in while running; each client
      about 62 KB/s in and 6 KB/s out.
- **Two hours** (`-SoakSeconds 7200`, full 480 s rounds, pinned smoke build
  `Smoke-20261031-040856-508`): failed at about 46 min
  (`20261031-001150-322-capacity-gameplay-baseline`). At host tick 330145 the host
  threw "Eight-player capacity coherence was lost during the soak"
  (`FusionSmokeDriver.Capacity.cs:104`) outside a round transition. No host peer
  removal was logged. The run overlapped Unity batch work on the same PC from
  about 00:37.
  - **Diagnosis (2026-10-31, from the peer logs and status files).** Seven of the
    eight `Connected` flags went false together. The host status shows 1 connected
    of 8 players, 8 avatars and the session still running. The clients' runner
    ticks froze at 329787–329788, the host's recovery publish missed around tick
    330012, and every client then threw "SDK ticks stopped advancing". The peak
    remote RTT reached about 6.9 s. This points to a machine-wide stall of about
    3 s while the Unity batch ran, rather than a game desync (moderate confidence).
    Why a 3 s stall dropped peers when `ConnectionTimeout` is 10 s is still open.
  - **Diagnostics added.** The host logs `WILDSTONE PEER LEFT participant=…
    generation=… tick=…` (no identity or credential data) when it retires a peer.
    The capacity runner records the largest frame gap (`maxFrameGapSeconds`). A lost
    coherence after a gap over 2 s fails as "environment stalled"; otherwise the
    error includes the connected count and the largest gap. Next: rerun the two
    hours with nothing else running on the PC.
- **Limits.** One PC and loopback-near latency. Bots fire at the nearest enemy with
  perfect aim; they are not human movement. Working set is still rising at 100 s,
  so only the two-hour series can show whether it levels off.

## 2026-10-31 firefight sword and range captures, fixture FOV

- **Captures.** `Invoke-VisualCheck.ps1 -Firefight` (`20261031-000530-632`) passed
  with 67 PNGs.
  - The Laser Sword slash at true ages: `firefight-lasersword-00/01/02/04/06/09/14/20`.
  - A strafing soldier straight ahead at 15, 30 and 45 m, Line Driver held,
    nothing fired: `firefight-range-15m/30m/45m`.
- **Sword read.** The slash read as a thin cyan streak, not a blade: one line
  ribbon drawn with the laser beam material, whose mask is a narrow core.
  - A Codex worker added a smear with the in-repo `T_VfxBlade` mask and a white
    core, revealed left to right.
  - It still read as a wisp, for two reasons. The mask is a comet with its bright
    head 30% along, mapped start to end, so the tail led the swing. The 2.1 m arc
    also passed behind the target, which stands inside the 2.3 m reach.
  - The fringe is now drawn front to back, so the head leads, and the smear sits
    1 m from the eye at the same angles.
  - `20261031-002956-147` shows the cyan head crossing in front of the target's
    helmet with the white edge trailing; compare `20261031-000530-632`. The values
    are in the [rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md).
  - Suites after the change: EditMode 507/509 (`EditMode-20261031-002215-192`) and
    PlayMode 172/173 (`PlayMode-20261031-002854-060`). PlayMode adds
    `SwordBladeSweepsThenExpiresAndReducedMotionStaysStill`.
- **Capture warm-up.** Twice under the soak's CPU load, the first recorded shot's
  next frame took 38 ms and failed the age gate. The firefight mode now fires one
  unrecorded Line Driver shot during its settle time.
- **Range read.** The soldier measures about 43, 22 and 15 px tall at 720p. It stays
  a dark, visible silhouette against the ivory stone at all three ranges.
- **Fixture FOV.** These and all the motion, weapon and firefight fixtures were taken
  at a 90-degree vertical FOV, the value before the FOV setting became 100 degrees
  horizontal (67.7 degrees vertical at 16:9). The world in those captures is drawn
  smaller than players see it; the perf fixture already used the real default.
  Fixtures now use the game's default. Captures before this change, including the
  numbers above, understate on-screen size by about 1.5x. `-Weapons`
  (`20261031-001737-238`) and `-Firefight` (`20261031-002956-147`) passed at the
  corrected view. The new map card also appears in weapon fixtures, whose clock
  stays at 8:00.

## 2026-10-31 map name card

The tester asked whether the build had "the same map 3 times". It has three maps,
but they share one stone kit and the same corner spawn structure, so the first view
of every map is nearly identical. The default static captures show this
(`20261031-001003-622`: `atrium-720`, `split-gallery-1080`, `terrace-steps-720-large-ui`).
- **Change.** Each round opens with the map's name in a graphite card under the
  score strip for 3 s. It fades over the last 0.5 s; with reduced motion it cuts
  instead. The bottom-left hint line keeps the name ("SPLIT GALLERY · ESC MENU").
  Both strings are cached per map, so nothing allocates per frame.
- **Checks.** `MenuPresentationTests.EachRoundOpensOnTheMapNameThenClears`;
  `atrium-720` shows the card at 7:59.
- **Limits.** This names the map; it does not make the maps look different. A
  per-map identity pass (accents, landmarks, spawn views) is a proposal awaiting the
  owner.

## 2026-10-31 playable build

Scope: a playable build with the
[stunned-client release fix](#2026-10-31-stunned-client-two-process-proof), the
[sword slash smear and fixture FOV fix](#2026-10-31-firefight-sword-and-range-captures-fixture-fov)
and the [map name card](#2026-10-31-map-name-card), on top of the
[late-night build](#2026-10-30-late-night-playable-build).
- **Build.** `BuildWindows-20261031-003132-589` wrote `Artifacts/Unity/Windows` from
  the same source as smoke build `BuildSmoke-20261031-002925-892`. Its content hash
  over 200 files is
  `5ac42eedb29413c5a134573dd7414ac8ac1ec66d00c8f636d76090b52583410b`. Its `app.info`
  names "Better Bean Battles".
- **Suites on that source.** EditMode 507/509, 2 explicit skips
  (`EditMode-20261031-003057-977`). PlayMode 172/173, 1 explicit skip
  (`PlayMode-20261031-002854-060`).
- **Captures on that source.** Firefight `20261031-002956-147` (67 PNGs). The
  weapons (`001737-238`) and default (`001003-622`) runs and the frame-time sample
  (`Perf-20261031-001030-612`) came from earlier smoke builds the same night; the
  later changes were the sword smear tuning and the firefight warm-up shot, which
  only the firefight run shows.
- **Archive.** The 23:33 build `db84d55e…` it replaced is kept at
  `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-db84d55e`; the copy re-hashes
  to the same value.
- **Startup.** A 15-second headless start
  (`Artifacts/Unity/WindowsStartup-20261031-0032.log`) logged no exception or error.
  That proves startup only.
- **Compatibility.** The stun fix adds a networked field, so both players should use
  this build. Mixing it with `db84d55e…` is untested.
- **Limits.** Not yet played.

## 2026-10-31 HUD string allocations

Scope: the HUD rebuilt about 20 strings every frame (scores, clock, health, ammo,
item labels, zone distance, kill feed) and walked pickups through a boxed
enumerator. Each label now rebuilds its string only when the value it shows
changes. Not in the `5ac42eed…` build.
- **Checks.** New `PracticeSessionTests.SteadyHudRefreshAllocatesNoManagedBytes`:
  64 refreshes of an unchanged HUD allocate 0 bytes, and a health change still
  updates the label. EditMode 507/509 (`EditMode-20261031-003809-710`), PlayMode
  173/174 (`PlayMode-20261031-003840-078`), smoke `BuildSmoke-20261031-003917-438`.
- **Captures.** Default `20261031-004013-893` against `001003-622`: the in-game
  frames differ in under 0.01% of pixels, the menu frames by the usual idle-pose
  0.3–0.7%. Firefight `003957-269`: 67 PNGs.
- **Frame sample** `Perf-20261031-004041-234` against `Perf-20261031-001030-612`:
  - managed allocation 1,460 → 560 bytes per frame, gen0 collections 8 → 3 per tier;
  - Ultra mean 1.74 → 1.98 ms, p99 2.49 → 3.27 ms; Low mean 1.60 → 1.81 ms,
    p99 2.21 → 2.94 ms.
  - The later sample ran while the eight-process two-hour soak loaded the CPU and
    the earlier one did not, so the frame times are not comparable; a clean
    resample follows the soak.

## 2026-10-31 playtest fixes (D040)

Scope: the user asked for the root-cause fixes in
[PLAYTEST-FIX-LATER](PLAYTEST-FIX-LATER.md) to be implemented. Every value is a
proposal ([D040](DECISIONS.md)). Contracts are in the
[combat spec](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md#2026-10-31-playtest-fixes-catalog-revision-5-d040),
the [foundation spec](superpowers/specs/2026-10-25-multiplayer-foundation-design.md),
the [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md), the
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md) and the
[weapon spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md).
None of this is in the `5ac42eed…` build.
- **Content.** `PreparePresentation-20261031-012811-444` regenerated the zone
  materials. `PrepareNetworking-20261031-012821-499` rebuilt the avatar prefab with
  the narrower torso box. Maps are unchanged, so there was no rebake.
- **Suites.** EditMode 522/524, 2 explicit skips (`EditMode-20261031-013034-659`).
  PlayMode 184/185, 1 explicit skip (`PlayMode-20261031-013100-314`).
  - The first runs (`EditMode-20261031-012832-906`, `PlayMode-20261031-012901-980`)
    failed 9 and 1 tests that pinned the old values: the 150 ms rewind interval, the
    18-tick attack window, a one-magazine bound, and a disconnected-avatar fixture
    that shot level at eye height. That fixture has no head hitbox, and its shot now
    passes over the shorter torso box. The fixture now aims at the chest. The other
    tests now assert the new bounds.
  - The first `BuildSmoke-20261031-012931-011` failed to compile three smoke-only
    lines with an out-of-scope `weapon`. `BuildSmoke-20261031-013128-803` passes.
- **New checks.**
  - Rules: `TorsoBoxStopsUnderTheHeadAndTurnsWithTheAvatar`,
    `BlinkFollowsTheHorizontalAim`, `HostAcceptsAPickupWithinTheLagMargin`, the
    scope-threshold asserts, and two-magazine pickup and restore.
  - Practice: `NativeRaysUseTheOnlineTorsoAndHeadInsideTheCapsule`.
  - Input: `EarlySemiAutomaticPressFiresOnceAtReady` and
    `ApplyingSettingsKeepsTheOnlineMovementSequenceRising`.
  - HUD: `DamageDirectionIsAFarArcNotAReticleMark`, `BlinkPickupPromptSaysWhatItDoes`,
    `ZoneWaypointStaysOnScreenAndPointsTheWayToTurn`,
    `RefusedBlinkShowsADenyCueButASpentOneDoesNot`.
  - Weapon motion: `ShotCameraPunchWithoutMouseInputLeavesSwayAtZero` and
    `MouseFlickSettlesWithoutOvershoot`.
  - Zone: four `WeaponEffectsTests` on colour, blend, queue and reduced motion,
    plus `HealthPackStandsOutFromOtherPickups`.
  - Online: same-room seat reclaim, refused-checkpoint tolerance and the host-left
    text.
- **Final source.** The health pack scale and ring (see
  [health packs](#2026-10-31-health-packs-d039)) and the soak diagnostics landed after
  those suites. One batch reran everything on the final source (01:37–01:41):
  - Compile, EditMode 522/524 with 2 explicit skips (`EditMode-20261031-013806-483`),
    PlayMode 184/185 with 1 explicit skip (`PlayMode-20261031-013831-299`), smoke
    `BuildSmoke-20261031-013859-019` (content identity `33076a46…`).
  - Captures, all passing with clean player logs: weapons `20261031-013924-452`,
    firefight `014027-390` (70 PNGs), motion `014037-971` and default `014100-485`.
  - Frame sample `Perf-20261031-014127-553`, taken after the two-hour soak had
    stopped: Ultra mean 1.71 ms, p99 2.71 ms; Low mean 1.55 ms, p99 2.39 ms; about
    560 bytes managed allocation per frame and 3 gen0 collections per tier. The last
    clean sample (`Perf-20261031-001030-612`, before the HUD fix) had Ultra 1.74 /
    2.49 ms and Low 1.60 / 2.21 ms with 1,460 bytes. Means are slightly lower and p99
    about 0.2 ms higher, within run-to-run spread; the allocation drop holds.
- **What the captures show.**
  - The free zone reads as a magenta floor and curtain on ivory stone
    (`weapon-pickups`, `weapon-kill-banner`). A pickup behind the curtain takes its
    tint: the health pack in `weapon-pickups` reads purple through it.
  - The zone waypoint shows over the zone and at the screen edge (`atrium-1080`,
    `split-gallery-720`). Its 1.5 px dark text outline ate the 14 px glyphs, so
    "40 m" read as hollow magenta strokes. It now sits on a graphite chip, 44 px
    in from the edge; see [the rerun](#2026-10-31-zone-waypoint-chip).
  - The damage arc and the Blink refusal have no capture.
- **Photon regression set** on smoke `Smoke-20261031-053905-337` (01:49–02:09). Final
  verdict: all pass.
  - Passed first time: gunplay baseline `20261031-014944-423` and adverse150
    `015016-378`; laser moving cover regional80 `015049-253` and adverse150
    `015124-199`; combat `015159-622`; laser combat `015223-778`; grenade combat
    adverse150 `015321-911`; deflection adverse150 `015424-131`; stun-client
    adverse150 `015503-622`; team switch `015536-057`; rematch `015559-626`; late
    join `015617-090`; migration `015646-416`; migration crash `015714-110`.
  - Projectile combat (`015253-527`) and special projectiles (`015352-247`) at
    adverse150 failed on the harness, not the game. `Invoke-NetworkSmoke.ps1` still
    pinned one-magazine ammo (Pulse Lobber 9 then 8, Disc 9, Stun 7–5), and both
    peers agreed on the two-magazine counts (19/18, 19, 15–13). The deflection check
    pinned 9 and 5 the same way. The script now derives all three from
    `$specialMagazines` (mirrors `WeaponCatalog.SpecialMagazines`; grenades keep
    one). Reruns pass: `015801-406` and `015828-604`. Deflection had already passed
    with the corrected check.
  - Reconnect failed 3 of 4 runs between 01:55 and 01:59 (`015526-707`,
    `015859-431`, `015914-266`), each before any game logic. Photon answered
    "Game does not exist" (32758) to a room the host had just created, or the
    connect itself failed (`ExceptionOnConnect`). No other run tonight logged
    either. Interleaved reruns then passed 3 of 3 on this build and 3 of 3 on the
    pre-D040 smoke `Smoke-20261031-050730-903`, and `Invoke-ReconnectCycles.ps1
    -Cycles 10` passed 10 of 10 (`20261031-020449-685-reconnect-cycles.json`). We
    read it as a transient Photon cloud or network fault, not a D040 regression;
    the seat-credential change never ran in the failed runs.
  - **Rewind age with the 250 ms cap.** At regional80 no query is clamped any more
    (0 of 20; requested 170–184 ms, before: all 20 clamped to 150 ms). At adverse150
    every query is still clamped, now to 250 ms (requested 255–293 ms; before,
    150 ms against 283 ms).

    | Run | Profile | Queries | Clamped | Requested mean / max | Applied |
    | --- | --- | --- | --- | --- | --- |
    | gunplay `20261031-014944-423` | baseline | 11 | 0 | 74–78 / 78 ms | as requested |
    | gunplay `20261031-015016-378` | adverse150 | 11 | 11 | 267–287 / 293 ms | 250 ms |
    | laser moving cover `20261031-015049-253` | regional80 | 20 | 0 | 170–184 / 184 ms | as requested |
    | laser moving cover `20261031-015124-199` | adverse150 | 20 | 20 | 255–280 / 283 ms | 250 ms |
- **Limits.** Same-PC Photon with injected latency, not external networks. No human
  playtest yet. Tests prove the rules and wiring, not feel.

## 2026-10-31 health packs (D039)

Scope: the user asked for health packs around each map. Not in the `5ac42eed…`
build. Rules are in the
[combat spec](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md#2026-10-31-health-packs-d039)
and positions in the [map spec](superpowers/specs/2026-10-25-wildstone-modern-map-design.md).
- **Model.** `build_assets.py health-pickup` built `HealthPickup.fbx` (972
  triangles; Blender re-import checks pass; hashes in
  `ArtSource/health-pickup-export-manifest.json`).
- **Content.** `Prepare-20261031-010228-772`, `PrepareNetworking-20261031-010241-074`,
  `Bake-20261031-010252-221` (map revision 6).
- **Suites.** EditMode 511/513, 2 explicit skips (`EditMode-20261031-010414-649`),
  including the new `HealthPackRestoresUpToFullHealth` (two cases),
  `FullHealthLeavesTheHealthPackInPlace` and
  `EveryArenaOffersFourSpacedHealthPacks`, plus the traversal gate reaching every
  pack from every start. PlayMode 173/174, 1 explicit skip
  (`PlayMode-20261031-010438-361`). The first run (`EditMode-20261031-005155-744`)
  failed the spacing test: Atrium's north pack sat 4.5 m below the sniper. It moved
  to (0, 0, 14).
- **Captures.** Default `20261031-010631-470` against `004013-893`: in-game frames
  differ in at most 0.01% of pixels after the rebuild and rebake. Weapons
  `20261031-010747-957`: `weapon-pickups` shows the pack about 10 m ahead.
- **Finding.** At 10 m the pack read poorly: the 0.29 m canister was a few
  pixels wide, its mint plus showed only side-on, and its pad ring was a thin
  green-grey line beside the brighter shotgun refill disc. The model now draws at
  1.8× with a .2 m ring (the [rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md)
  has the values; `WeaponEffectsTests.HealthPackStandsOutFromOtherPickups`). In
  weapons `20261031-013924-452` the pack is a box with a readable plus at about
  10 m, compare `010747-957`. In that fixture the zone curtain is in front of it,
  so it reads purple there.
- **Limits.** No online pickup run and no human play yet. The 50 heal and 15 s
  respawn are proposals.

## 2026-10-31 zone waypoint chip

Scope: the [D040 captures](#2026-10-31-playtest-fixes-d040) showed the zone
waypoint's 14 px label drawn hollow: its 1.5 px dark text outline covered most of
each glyph. The label now sits on an 80 px graphite chip (0.72, the bottom panels'
backing) with no outline, and the edge margin is 44 px so the whole chip stays on
screen. The contract is in the [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md).
- **Checks.** `MenuPresentationTests.ZoneWaypointStaysOnScreenAndPointsTheWayToTurn`
  now also asserts a dark, mostly opaque chip. Compile
  `Compile-20261031-021021-548`; EditMode 522/524 with 2 explicit skips
  (`EditMode-20261031-021049-949`); PlayMode 184/185 with 1 explicit skip
  (`PlayMode-20261031-021113-831`); smoke `BuildSmoke-20261031-021139-534`
  (content identity `14d7f9da…`).
- **Captures.** Weapons `20261031-021206-095` and default `20261031-021308-822`
  pass with clean player logs. `atrium-1080` shows a solid magenta "◆ 40 m" on the
  chip at the right edge; `weapon-pickups` shows "◆ 32 m" over the zone.
- **Limits.** Presentation only; no Photon rerun, since no network code changed.

## 2026-10-31 D040 playable build

Scope: a playable build with everything since `5ac42eed…`: the
[HUD string allocations](#2026-10-31-hud-string-allocations) fix,
[health packs (D039)](#2026-10-31-health-packs-d039),
[playtest fixes (D040)](#2026-10-31-playtest-fixes-d040), the
[zone waypoint chip](#2026-10-31-zone-waypoint-chip) and the host's `WILDSTONE PEER
LEFT` log line ([gameplay soak](#2026-10-31-gameplay-soak)).
- **Build.** `BuildWindows-20261031-021446-901` wrote `Artifacts/Unity/Windows` from
  the same source as smoke `BuildSmoke-20261031-021139-534`. `app.info` names
  "Better Bean Battles".
- **Hash.** The earlier hashes' exact recipe was not scripted and could not be
  reproduced. From this build on: SHA-256 over one `relative/path  sha256` line per
  file (lower-case hex, `\n` after each), sorted ordinally by forward-slash path,
  skipping `*DoNotShip` folders. This build: 200 files,
  `b563bab3ea2a4694f0af845e962d361fa8338041f5a4535390f903a2c4b96980`. By the
  same method, the replaced `5ac42eed…` build is `42a88f1e…` and `db84d55e…` is
  `c06166f4…`.
- **Archive.** The replaced `5ac42eed…` build, never played, is kept at
  `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-5ac42eed`.
- **Startup.** A 15-second headless start
  (`Artifacts/Unity/WindowsStartup-20261031-0215.log`) logged no exception or
  error. That proves startup only.
- **Compatibility.** Catalog revision 5 changes networked rules (ammo, hit shapes,
  rewind and attack windows), so both players must use this build.
- **Limits.** Not yet played. Every D040 and D039 value is a proposal for the
  user's playtest.

## 2026-10-31 feel pass (D041)

Scope: the gun-feel and movement-feel audit
(`Artifacts/Audit/2026-10-31-feel-audit.md`) implemented under
[D041](DECISIONS.md): air strafing with a movement revision in the join token, a
Practice speed readout, predicted local hitscan shots online, eased recoil with
lower viewmodel pitches, damage-scaled body hit markers, a speed lens, wider wind
thresholds and an ADS bloom ring. Rules: [combat spec](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md#2026-10-31-feel-pass-d041),
[foundation spec §5](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#5-simulation-input-and-movement-replay),
[weapon spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md),
[UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md). Every value is a
proposal; nothing here is human-accepted feel.
- **Compile.** `Compile-20261031-112350-494` (clean, after one shadowed-local fix in
  `ArenaHud`).
- **Native suites on the final source.** EditMode
  `Artifacts/Unity/EditMode-20261031-113329-457.xml`: 528 total, 526 passed, 2
  explicit skips (the two `ContentPreparationTests` scene-regeneration cases, as
  before). PlayMode `Artifacts/Unity/PlayMode-20261031-113244-337.xml`: 194 total,
  193 passed, 1 explicit skip (`ExplicitPhotonPrivateHostCreatesAdmittedGuestLobby`).
  Earlier runs on the way: `EditMode-20261031-112421-158` (1 failure: the
  source-parity baseline still pinned `airStrafeWishSpeed` 2; the row is removed
  because the value is no longer retained), `PlayMode-20261031-112718-992` (7
  failures: the effects refactor fed hitscan shots into the generic spark branch,
  fixed; the strafe script faced backwards, fixed; the SMG and beam-count
  assertions were wrong, fixed) and `PlayMode-20261031-113125-818` (1: the strafe
  test re-captured the motor state between runs, fixed).
- **New tests (20).** Movement: `MovementLawTests.AirStrafeGainPerTickIsBoundedByTheWishSpeed`,
  `ForwardHeldDoesNotRotateAHeldStrafe`, `SidewaysStrafeCanOnlyAddItsTwoMetreWishSpeed`
  (updated to 2.5 m/s and 18 m/s²); `ArenaMotorTests.ScriptedStrafeBuildsSpeedWithinOneJumpAndStaysUnderTheSoftCap`,
  `AirStrafeReplayFromMidFlightProducesIdenticalEntireState`;
  `FusionSessionBoundaryTests.MovementRevisionIsPartOfTheRulesetAndAMismatchRefusesTheJoin`.
  Prediction: `NetworkPresentationBridgeTests.PredictedShotPlaysOnceAndTheHostShotDoesNotRepeatIt`,
  `RejectedPredictedShotPlaysNothingFurther`, `PredictedBeamIsCorrectedToTheHostRay`.
  Recoil: `FirstPersonWeaponMotionTests.RecoilPeaksWithinThirtyMillisecondsAndReturnsWithoutOvershoot`,
  `HeldSmgRecoilSettlesToASteadyBand`. Lens and HUD:
  `FirstPersonSlideViewTests.SpeedWidensTheLensAndAdsFadesIt`,
  `MenuPresentationTests.BodyMarkerSizeFollowsDamageAndTheBloomRingWaitsForAds`,
  `PracticeSessionTests.PracticeSpeedReadoutShowsHorizontalSpeedUnlessReduced`,
  and `ArenaWeaponAudioTests.SpeedWindRisesWithSpeedOnOneLoopingVoice` (updated to
  the 9–20 m/s thresholds). `PelletHitsInOneFrameSoundOnce` passes the new damage
  argument.
- **Measured strafe.** A scripted A-key strafe through one held jump (88 ticks from
  11 m/s, facing turned so the sideways wish leads the velocity by the gain angle)
  ends at 12.98 m/s; with forward held, 13.09 m/s. The audit's 13.5 m/s assumed its
  deferred second phase (a slower carry decay); the test asserts at least 12.8 m/s,
  the soft cap plus 0.5 m/s never exceeded, and the two runs within 0.2 m/s.
- **Smoke build.** `BuildSmoke-20261031-113409-641`, content identity
  `26b97d49cd2a9629bcce64a752b7b4542852df1e1d354d2a506d7fad2e99de24`,
  `Artifacts/Unity/Smoke-20261031-153416-198`.
- **Photon smokes (separate host/client processes, all pass).**
  `20261031-113516-223-gunplay-baseline`, `20261031-113551-990-gunplay-adverse150`,
  `20261031-113624-440-laser-moving-cover-regional80`, `20261031-113659-406-combat-baseline`,
  `20261031-113724-051-late-join-baseline`. Shot and hit identities still match
  across peers with the predicted-shot code in the client. The smoke client log
  carries one `WILDSTONE SHOT CUE host <participant:life:sequence>` line per
  accepted local shot (gunplay baseline: 11 lines for the 11 shots in
  `client.json`, sequences 2, 3, 6, 7 and 10–16; adverse150: 11; moving cover: 6),
  none repeated.
- **Captures (pass, clean player logs).** Weapons `Artifacts/Visual/20261031-113822-433`,
  Firefight `20261031-113921-085`, Motion `20261031-113931-240`. Inspected:
  `fixture-standing-forward.png` and `fixture-fire.png` show the Practice chip
  `SPEED 0.0 m/s` under the map hint, left column, and the fired Line Driver sits
  lower and further back than at rest with its muzzle flash; `weapon-linedriver-1.png`
  shows the beam from the barrel with the chip in place. Firefight frames carry no
  HUD, so the marker size and bloom ring are test evidence only.
- **Playable build.** `BuildWindows-20261031-114011-116` wrote `Artifacts/Unity/Windows`
  from the smoke build's source. 200 files, content hash (same recipe as the
  [D040 build](#2026-10-31-d040-playable-build))
  `80f56a7c3454b7bdb727d97f3026f7e31716909605be97e052270cb6aa78c794`. A 15-second
  headless start (`Artifacts/Unity/WindowsStartup-20261031-1140.log`, 24 lines)
  logged no exception, error or assertion. The replaced `b563bab3…` build, never
  played, is archived at `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-b563bab3`
  (verified to the same 200-file hash before the rebuild).
- **Compatibility.** The ruleset token now carries `movement-1`, so peers on an
  older build are refused at the lobby. Both players must use this build.
- **Docs.** Every relative link and anchor under `docs/` resolves (scratch
  checker, 537 links).
- **Limits.** Not yet played by a human. The smoke drivers send the combat RPC
  directly, so the predicted cue (`WILDSTONE SHOT CUE predicted`) appears only
  when a real client presses fire; the prediction path itself is covered by the
  three bridge tests. Prediction covers hitscan weapons only; a press the host
  refuses (empty magazine, cooldown) shows one false flash and sound. Online
  peers never receive `Rejected`, so the 0.3 s timeout is the refusal path there.
  The strafe gain is below the audit's 13.5 m/s target until the deferred carry
  decay change. No perf sample was rerun; the HUD adds one struct copy and one
  label per frame.

## 2026-10-30 evening playable build

Scope: a playable build with everything since the 16:06 build the user played.
- **Contents.**
  - [D036 online held-fire cadence](#2026-10-30-online-held-fire-cadence-d036);
  - [no legs in view while sliding](#2026-10-30-no-legs-in-view-while-sliding);
  - [D037 Push Lens deflection](#2026-10-30-push-lens-deflection-d037), including its
    pooled-projectile fix;
  - [rewind-age recording](#2026-10-30-measured-rewind-age). It adds a few float
    operations per host hitscan query; the smoke-only event is compiled out.
- **Build.**
  - `BuildWindows-20261030-194409-129` wrote `Artifacts/Unity/Windows`. Its content
    hash over 200 files is
    `625026d13ed875ae1ae1cc6cfe13fe244ac8be79689c6d35f197006040b576e8`.
  - It overwrote the unplayed `BuildWindows-20261030-191835-213` build
    (`ece027dcd32da22be07b0cc188242691c464225b6d7b2d169cbcf05bebe1bbd2`), which
    lacked the pooled-projectile fix.
  - The 16:06 build the user played, `d838ac03…`, is kept at
    `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-d838ac03`.
  - 15-second headless starts (`Artifacts/Unity/WindowsStartup-20261030-1920.log`,
    `-1945.log`) initialised the engine with no logged exception. That proves
    startup only.
- **Suites on the final source.** EditMode 498/500, 2 explicit skips
  (`EditMode-20261030-194304-957`). PlayMode 169/170, 1 explicit skip
  (`PlayMode-20261030-194338-254`). PlayMode has 14 fewer tests than the D036 run
  because the slide-leg tests went with the behaviour they covered.
- **Captures** on smoke `BuildSmoke-20261030-191805-255`: weapons
  `20261030-192033-554`, default `192136-972`, firefight `192204-695`, motion
  `192214-713`, sequence `192237-274`, music `192243-060`. That build had D037
  before its pooled-projectile fix, which is not visual.
  - All six counts are correct and the player logs are clean.
  - Against the D036 set (`190151-059` and the rest), the intended change is that
    the slide frames (`sequence-060`, `-061`, `-062`, `fixture-slide-sustain`,
    `fixture-slide-fov60`) no longer show legs.
  - The other differences are time-driven: pickup models spin with wall-clock
    time, plus the menu soldiers' idle phase, speed streaks and smoke puffs.
- **Photon regression set** on that smoke build, all passing:
  - laser moving cover at regional80 `20261030-192355-147` and adverse150
    `192538-720`;
  - gunplay at adverse150 `192801-045` and baseline `192836-652`;
  - adverse150 projectile combat `192938-332`, grenade combat `193048-300` and
    special projectiles `193119-124`;
  - baseline combat `193150-638`, laser combat `193231-301`, reconnect
    `193336-142` and team switch `193408-589`.
- **Perf.** Samples `Perf-20261030-194144-818` and `194212-001` are invalid: the
  user started Deadlock on the same GPU during them. The last valid sample is in
  the [steep slide leg hide](#2026-10-30-steep-slide-leg-hide) section. A fresh
  sample needs an idle GPU.
- **Limits.** Not yet played.

## 2026-10-30 Push Lens deflection (D037)

Scope: the backlog's unimplemented projectile deflection. The contract is the
combat spec's
[Push Lens deflection (D037)](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md#2026-10-30-push-lens-deflection-d037)
section, and [DECISIONS](DECISIONS.md) records D037. Feel and balance are the
user's to judge.
- **Rules checks.**
  - Managed run `Artifacts/ManagedCompile/20261030-191355-065`. New EditMode tests:
    - `PushReflectsEnemyOrbOnceAndItsBlastCreditsThePusherAgainstTheShooter`: the
      orb is relaunched at its own speed, keeps its fuse and transfers credit. The
      same result holds on a replica restored from a snapshot. The blast hits the
      original shooter once for 60.
    - `PushReflectsOnlyAVisibleEnemyProjectileInItsConeOnce`: a grenade in the cone
      is re-thrown along the push. Projectiles that are out of the cone, out of
      range, behind cover, friendly or already deflected are untouched.
    - `LatePushNeitherResurrectsNorRecreditsAConfirmedBlast`: a push resolving on or
      after the fuse tick does nothing.
    - `DeflectionHasItsOwnParryCue`, and the networking `ProjectileCuesReachClients`.
  - Native suites: see the [evening playable build](#2026-10-30-evening-playable-build).
- **Online check.** A new `deflection` scenario runs two rounds per session, host
  pushing then client pushing (verdict in [TOOLING](TOOLING.md)).
  - The first runs on `BuildSmoke-20261030-193452-010` (baseline
    `20261030-193604-543`, adverse150 `193634-031`) passed round 0 and failed round 1.
    The host threw "Duplicate/wrong original orb" at the client-push round's fire.
  - Cause: a gameplay bug, not the harness. `ArenaMatch` reuses pooled projectile
    objects and did not reset `Deflected` at launch. After any deflection, the next
    orb, disc, bolt or grenade that reused that object was born deflected: it could
    not be deflected, and it read as the pusher's catch.
  - Fix: launch resets `Deflected`. The orb test now also launches a second orb
    after the blast and asserts that it is the shooter's own and not deflected.
    Without the fix that assertion fails (`EditMode-20261030-194115-444`); with it,
    `CombatRulesTests` passes 104/104 (`EditMode-20261030-194047-432`).
  - After the fix, on `BuildSmoke-20261030-193834-745`, both rounds pass on both
    peers:

    | Run | Round | Pusher | Lead ticks | Seen at | Resolved at | Damage (expected) |
    | --- | --- | --- | --- | --- | --- | --- |
    | baseline `20261030-193907-907` | 0 | host | 22 | 7.80 m | 4.50 m | 57.33 (57.33) |
    | | 1 | client | 27 | 8.55 m | 4.65 m | 57.33 (57.33) |
    | adverse150 `20261030-193953-875` | 0 | host | 22 | 7.80 m | 4.50 m | 57.33 (57.33) |
    | | 1 | client | 45 | 10.80 m | 4.20 m | 57.33 (57.33) |

    The deflected orb's velocity is exactly reversed (dot −1) in every round.
- **Limits.**
  - Each run is one stationary, scripted catch per round.
  - Natural play, other projectile kinds online and a late-push online variant are
    not covered.
  - One catch per push is a builder choice, explained in the spec.

## 2026-10-30 measured rewind age

Scope: the foundation spec's "Record accepted shot tick, rewind age, weapon/sequence
and hit verdict". `FusionCombatWorld` now keeps, for every host hitscan query, the
applied rewind age and the SDK-requested view age before the
[150 ms cap](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#6-combat-lag-compensation-and-fairness).
Smoke results report them per weapon (see [TOOLING](TOOLING.md)). The type and
rule check is `Check-NetworkEvidence.ps1`.

| Run | Profile | Queries | Clamped | Requested mean / max | Applied |
| --- | --- | --- | --- | --- | --- |
| gunplay `20261030-192836-652` | baseline (same PC) | 11 | 0 | 77 / 85 ms | as requested |
| laser combat `20261030-193231-301` | baseline | 10 | 0 | 81 / 86 ms | as requested |
| laser moving cover `20261030-192355-147` | regional80 | 20 | 20 | 184 / 239 ms | 150 ms |
| laser moving cover `20261030-192538-720` | adverse150 | 20 | 20 | 283 / 289 ms | 150 ms |
| gunplay `20261030-192801-045` | adverse150 | 11 | 11 | 285 / 337 ms | 150 ms |

- **Finding.** With no added latency the view age is already about 80 ms
  (interpolation and tick delay). At the regional80 profile (80 ms RTT with jitter)
  every query was clamped, 34 ms short on average. The cap therefore
  under-compensates moving targets for an ordinary regional ping, not only for
  high ping. That is consistent with the earlier
  [laser moving-cover investigation](#laser-moving-cover-investigation).
- **Not changed.** The 150 ms cap is the foundation spec's accepted initial value.
  Raising it (for example to about 250 ms, which covers the regional80 maximum) is
  a fairness trade-off for the user to decide.
- **Limits.** Same-PC SDK-injected profiles, a handful of scripted shots per run.

## 2026-10-30 no legs in view while sliding

Scope: the user's report, "when you slide, the new full body gets in the fov we need
to make it so when we slide that does not happen". The fresh capture review found the
same thing in the current build: `sequence-060` (smoke `BuildSmoke-20261030-185422-057`,
run `20261030-190345-950`) shows a loose shin and boot mid-screen during a slide. It
supersedes the [slide knee tuck](#2026-10-30-slide-knee-tuck) and the
[steep slide leg hide](#2026-10-30-steep-slide-leg-hide). The contract is the character
spec's Local body row (section 6).
- **Change.** `FirstPersonBody` hides the visible body from the first slide frame
  until the slide spring has settled after the slide ends, at every look pitch. The
  shadow-only copy keeps rendering. The knee tuck, steep-glance hide, pelvis/shin fold,
  spine fold, slide hip offset and hips-under-eye gate are removed, since nothing they
  shaped is visible any more. `Present` no longer takes the lens.
- **Checks.**
  - PlayMode `FirstPersonBodyTests` 5/5 (`PlayMode-20261030-191217-835`).
  - New `SlideHidesTheVisibleBodyAndKeepsItsShadow` (normal and reduced motion). It
    asserts that running shows the legs. On every one of 120 slide frames, from the
    run into the slide, the visible body is hidden and the shadow renders. The motor
    state is unchanged, the legs stay hidden on the first frame after the slide ends,
    and they show again once the slide has settled.
  - `DeathTeleportAndNewLifeClearTheSlideHide` replaces the slide-pose reset check.
  - The slide boot, knee-tuck, steep-glance, knee-clearance, slide-entry and
    reduced-motion offset tests are removed with the behaviour they covered.
- **Captures.** Sequence `20261030-192237-274` and motion `20261030-192214-713`
  (smoke `BuildSmoke-20261030-191805-255`): `sequence-060`, `-061`, `-062`,
  `fixture-slide-sustain` and `fixture-slide-fov60` no longer show a shin or boot.
  The slide shows only the weapon, the view and the world.
- **Limits.** The user has not judged the change.

## 2026-10-30 online held-fire cadence (D036)

Scope: the limit left by the [SMG held-fire verdict](#2026-10-30-smg-held-fire-verdict).
Online, held automatic fire (Laser SMG, Line Driver) waited up to one client frame per
shot after each interval. The client asked again only after it saw the replicated
cooldown, and the host rejected a request that arrived a tick early. The contract is
the combat spec's
[held automatic cadence online (D036)](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md#2026-10-30-held-automatic-cadence-online-d036)
section, which owns the timing values.
- **Change.**
  - `WeaponCatalog.FireSlackSeconds` is a fire slack for automatic weapons only. The
    pistol and the other non-automatic weapons get none.
  - Host: `ArenaMatch.Reserve` accepts an automatic shot up to the slack early and
    keeps the schedule. Within the slack, `FireReadyTick` advances from the previous
    ready tick; otherwise it advances from now.
  - Client: `PracticeInput.ConsumeActions` keeps its own held-repeat schedule against
    the replicated cooldown. It predicts the clip refilled only once the reload end
    plus the slack has passed. It sends the empty-trigger Reload only when no reload
    is running.
  - `FusionMatch.NextActionTick` stamps client requests with the client's predicted
    next action tick. It holds through backward steps smaller than one second of
    ticks, which warm-up recovery produces.
- **Harness.**
  - `FusionSmokeDriver.Combat.cs` gunplay auto-fire now drives a real `PracticeInput`
    through a synthetic keyboard (`SmokeTrigger`, J bound to Fire). The input
    background behaviour is set to IgnoreFocus and restored afterwards.
  - Verdict over a 0.6 s hold: 7–8 shots, every shot hits, minimum spacing at least
    interval − 2·slack, span at least (shots − 1)·interval − slack, and mean spacing
    at most interval + 1 tick.
- **Before.** The new harness on the old code (smoke build `180607`) fired 4 SMG
  shots in the 0.6 s hold:
  - baseline `20261030-180638-455`: mean spacing 17.3 ticks;
  - adverse150 `20261030-180658-658`: mean 19.7 ticks, with 1 of 5 requests rejected.
- **After.**
  - Baseline `20261030-180756-196`: 7 of 7 shots at exactly 10-tick spacing.
  - Adverse150 `20261030-180830-881`: 7 of 7 shots, mean 9.83 ticks, no expired
    requests.
  - Final smoke `BuildSmoke-20261030-184357-026`, with the review fixes: gunplay 7 of 7
    at baseline and adverse150, mean 10.0 ticks. `autoStampOffset=-1`: the host stamp
    lands one tick before the client's requested tick.
- **Review.** An independent review of the change:
  - Fixed:
    - `NextActionTick` stepped backwards during warm-up recovery;
    - the client could send a Fire the host would reject while predicting a reload
      end;
    - the harness did not restore the input background setting.
  - Documented, not changed: after a weapon switch an automatic weapon can borrow the
    slack once (at most 25 ms). The previous weapon's ready tick is not replicated per
    weapon.
- **Checks.**
  - Compile `Compile-20261030-180221-859`.
  - Rewritten EditMode `CombatRulesTests.StarterCadenceClipReloadAndRevisionStayConsistent`
    at 60 Hz, where the slack is 2 ticks:
    - an early shot is rejected at tick 11 and accepted at 12, with ready tick 23;
    - a late shot carries 25 → 32;
    - a later one resets 40 → 49;
    - the pistol has no slack at 17 ticks.
  - New PlayMode `PracticeInputTests.HeldRepeatsKeepTheirScheduleAgainstReplicatedCooldown`,
    including a reload ending at 300: still empty at 302, fires at 303.
  - EditMode 486/488, 2 skipped (`EditMode-20261030-184259-685`).
  - PlayMode 183/184, 1 skipped (`PlayMode-20261030-184325-629`).
  - The Photon regression set on the final smoke build:
    - baseline: combat `20261030-184641-194`, team switch `184732-170`, reconnect
      `184755-339`, laser combat `184827-700`;
    - adverse150: grenade combat `184957-625`, special projectiles `185027-446`.

    All pass. Adverse150 laser moving cover `184921-657` failed with shotgun 5/8. That
    run used an experimental fixture aim; see the investigation below.
  - Captures on the reverted-fixture smoke `BuildSmoke-20261030-185422-057`: weapons
    `20261030-190151-059`, default `190247-926`, firefight `190314-860`, motion
    `190323-874`, sequence `190345-950`, music `190351-089`. All six counts are
    correct and the player logs are clean. Against the steep-slide set below, the
    only pixel differences are soldiers' idle phase (menus and `weapon-pickups`) and
    the randomised smoke puffs in `firefight-grenade-blast-50`.
- **Limits.**
  - Same-PC Photon with SDK-injected latency, not an external network.
  - Nobody has judged held fire by feel.

### Laser moving-cover investigation

- **Misses.** Adverse150 laser moving cover missed shotgun pellets (7/8, then 6/8)
  between 18:11 and 18:32.
- **D036 is not implicated.** An interleaved A/B from 18:33 to 18:42 compared the
  D036-reverted smoke `Smoke-20261030-221728-328` with the D036 smoke
  `Smoke-20261030-222214-089`. All 8 runs, on both builds, landed shotgun 8/8.
  - One D036 run (`20261030-183442-770`) failed with DeadlineExceeded `Host recovery
    failed` after all lasers had verified. That is a known intermittent, also seen at
    `012707`.
- **Cause.**
  - The fixture aims at the 20 Hz model position. The host rewinds to the client's
    SDK view, capped at 150 ms (`FusionCombatWorld.MaximumRewindSeconds`).
  - The shotgun pellet ring is about 0.187 m in radius at 2.14 m, against a 0.35 m
    hitbox half-width. A strafing target therefore leaves edge pellets outside.
- **Experiment.** The fixture aimed at the rendered (interpolated) target instead:
  - adverse150 landed 5, 5, 8 and 5 of 8, which is worse;
  - regional80 passed 8/8 twice (`20261030-185132-449`, `185316-997`).

  So lag compensation matches the rendered view within the cap. At 150 ms RTT the
  view age (RTT plus interpolation, about 190–230 ms) exceeds the 150 ms cap that the
  [foundation spec](superpowers/specs/2026-10-25-multiplayer-foundation-design.md#6-combat-lag-compensation-and-fairness)
  accepts ("High ping does not earn unlimited historical hits"). Strafing targets are
  under-compensated at that latency by design. That trade-off is the user's to
  revisit; it is not changed.
- **Reverted.** The fixture is back to the model aim. On the rebuilt smoke
  `BuildSmoke-20261030-185422-057`, adverse150 laser moving cover passed twice
  (`20261030-185447-836`, `185543-965`): pistol 1, shotgun 8/8, sniper 1.

## 2026-10-30 steep slide leg hide

*Historical: superseded the same day by [no legs in view while sliding](#2026-10-30-no-legs-in-view-while-sliding).*

Scope: the limit left by the [slide knee tuck](#2026-10-30-slide-knee-tuck), which
covers glances down to about 20°. Looking down further mid-slide showed a shin cut
off at the knee mid-screen (the 40° capture). The contract is in the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
("Local body" row and the "Slide legs" limit).
- **Change** (`Assets/Wildstone/Runtime/Presentation/FirstPersonBody.cs`).
  - `TuckKnees` now returns the knee's remaining overshoot into the frame once the
    tuck has faded. The tuck still fades out between 20° and 35° pitch.
  - While sliding, the visible legs hide when that overshoot exceeds 0.04 m and show
    again below 0.01 m. The shadow-only body stays, so the player's shadow is whole.
  - The legs show at 20° and hide from 25°.
- **Rejected values.** Widening the tuck to 30°/45° failed lens clearance: the nearest
  visible vertex was 0.26 m from the eye at 25°. It was not kept.
- **Checks.**
  - New `FirstPersonBodyTests.SteepSlideGlanceShowsNoKneeCut` runs at 25°, 30°, 40°
    and 70°.
  - `SlideKneesStayClearOfTheLens` now also runs at 25° and 30°.
  - Both are in the PlayMode 183/184 run of the
    [online held-fire cadence](#2026-10-30-online-held-fire-cadence-d036).
  - All six packaged modes pass on smoke `Smoke-20261030-222214-089`, with the
    expected frame counts and no exception, NRE or RenderGraph lines in the player
    logs:
    - weapons `20261030-182528-272` (34 frames)
    - default `182625-178` (16)
    - firefight `182652-020` (56)
    - motion `182701-042` (13)
    - sequence `182723-147` (32)
    - music `182728-238` (5)
  - Against the [remote strafe](#2026-10-30-remote-strafe-and-backpedal-cadence) runs,
    the pixel diff shows only intended changes:
    - `fixture-slide-down`, `sequence-042` and `sequence-054` lose the floating cut
      shin;
    - `weapon-remote-wallrun` shows the `Stone-Charcoal` wall;
    - default and music differ only by the spawn alternation.
  - Frame time (`Perf-20261030-182822-933` and `182849-555`): Ultra mean 1.77/1.86 ms
    and p99 2.62/3.15 ms; Low mean 1.61/1.66 ms and p99 2.19/2.35 ms. Chrome and MSI
    Afterburner were busy in the background, so treat it as an upper-bound sample.
- **Limits.** These are offscreen captures and tests. A steep slide glance shows no
  legs, only their shadow. Nobody has judged it in motion.

## 2026-10-30 remote strafe and backpedal cadence

Scope: other players' back and side runs, from the game plan's remote-animation
item. The contract is the "Controller" row and the "Back and side runs" limit in the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md).
- **Cause.**
  - Every direction moves at the 11 m/s ground speed.
  - RunB and RunL/R were authored at 5.48 and 5.81 m/s, so `LocomotionScale` played
    them at 2.0× and 1.9×. That is about 6 steps/s, against 4.2 for the forward run.
  - The wall-run state had no rate at all. At its 13.75 m/s top speed its 7.94 m/s
    stride slid.
- **Change.**
  - `soldier_clips.py`: RunB contact (duty) .17 → .12 and RunL/R .145 → .105, with
    the hips 3 cm lower. The stance sweep is unchanged.
  - Measured speeds: RunB 7.83 and RunL/R 8.02 m/s, so 11 m/s plays at 1.40× and
    1.37×, about 4.2 and 4.1 steps/s.
  - `SoldierRig` speed constants follow the sidecar.
  - `SoldierBuilder` drives the WallRun state with `LocomotionRate`.
- **Rejected.** Longer strides at the original contact overextended the legs. At
  1.1–1.3× sweep, the ankle IK missed by 1.5–6.4 cm and the clips failed the gate.
- **Checks.**
  - `soldier_clips.py build`, `verify` (`VERIFY OK`, forge quality gates, 52/52 rest
    bones) and `previews` pass. The RunB and RunR contact sheets read as bounding runs.
  - `PresentationImportTests.LocomotionBlendPositionsMatchTheAuthoredStrideSpeeds` now
    also requires the WallRun state's rate parameter. It passes, and the tree positions
    and runtime constants match the new sidecar speeds.
  - `PreparePresentation-20261030-171256-319`.
  - EditMode 486/488, 2 skipped (`EditMode-20261030-171329-976`).
  - PlayMode 176/177, 1 skipped (`PlayMode-20261030-171356-160`).
  - Smoke `BuildSmoke-20261030-171421-037`.
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - weapons `20261030-171502-480`
    - default `171603-479`
    - firefight `171630-868`
    - motion `171641-005`
    - sequence `171703-377`
    - music `171709-047`
  - Against the [wall-run lean](#2026-10-30-remote-wall-run-lean) runs:
    - The firefight target's strafe pose changed (grenade-blast and incoming-sniper
      frames, 0.4–0.6 mean).
    - `weapon-remote-wallrun` caught a later stride phase from the new rate. Its walls
      also drew another stone tone, because the fixture took whichever stone material
      it found first; it now takes `Stone-Charcoal`.
    - Motion frames are pixel-identical. Menu frames differ by the idle soldiers'
      timing (1.1–2.3 mean), and sequence frame 12 by the random dash streaks.
  - Frame time was not measured. Deadlock started on the same GPU during these runs.
    `Perf-20261030-171723-572` and `171750-456` read Ultra means of 9.8 and 14.6 ms,
    which reflect that load rather than this change.
  - The later samples `Perf-20261030-182822-933` and `182849-555` include this change
    and supersede them: Ultra mean 1.77/1.86 ms and p99 2.62/3.15 ms; Low mean
    1.61/1.66 ms and p99 2.19/2.35 ms. See the
    [steep slide leg hide](#2026-10-30-steep-slide-leg-hide) for their limits.
- **Limits.** Cadence is judged from numbers and contact sheets. Nobody has watched a
  remote strafe or backpedal in motion online.

## 2026-10-30 remote wall-run lean

Scope: other players' wall runs (WallRunL/R), from the game plan's remote-animation
item. The contract is the "Wall runs" limit in the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md).
- **New capture.** `weapon-remote-wallrun` (smoke builds only) shows two remote
  soldiers wall-running toward the lens beside diagnostic walls 0.4 m off their
  capsules. The left-wall run is on the right of the frame. The weapons count is now 34.
- **Before.** Weapons run `20261030-165905-542`.
  - Both runners floated in the corridor with a mild lean.
  - The clip rolled the run cycle 24° about the root. Measured over the cycle in the
    clip model, the wall-side boot stayed 0.31 m off the wall, the hips sat 0.37 m and
    the head 0.60 m from the capsule axis.
- **Change.** `soldier_clips.py wall_run` rolls the cycle 30° and slides it 0.285 m
  toward the wall.
  - The pivot is the point whose pure roll lands that shift, so the clip gates still
    measure foot contact in the rolled frame.
  - The wall-side boot centreline is now 0.36 m out, so the boot edge meets the wall.
    The hips are 0.18 m and the head 0.46 m from the capsule axis.
  - WallRunR mirrors WallRunL exactly.
  - A 35° roll was rejected because it put the head 0.57 m out.
- **Checks.**
  - `soldier_clips.py build`, `verify` and `previews` pass. `verify` includes the forge
    animation-quality gates, the `VERIFY OK` result and 52/52 rest-compatible bones.
    Both wall runs show no gate failures and 0.45 cm slip.
  - `PreparePresentation-20261030-170153-001` reimported `SoldierAnimations.fbx` and
    regenerated `Soldier.controller`.
  - Smoke `BuildSmoke-20261030-170202-230`.
  - EditMode 486/488, 2 skipped (`EditMode-20261030-170345-903`).
  - PlayMode 176/177, 1 skipped (`PlayMode-20261030-170409-804`).
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - weapons `20261030-170224-013`
    - default `170437-783`
    - firefight `170504-889`
    - motion `170513-981`
    - sequence `170536-099`
    - music `170541-344`
  - Weapons after: both runners' boots meet their walls and the lean reads stronger.
    The other weapons frames differ only by pickup bob and shield-break spark timing,
    with means of 0.6 or less.
  - Motion frames are pixel-identical to the 16:01 runs. Firefight differs only in
    three Pulse Lobber blast frames, by 0.1 mean (blast debris). In the default
    and music modes, the spawn view alternates between two spawns from run to run.
    These runs match the 15:35 run exactly.
  - Sequence frame 12 differs only in the randomly placed dash streaks.
  - Frame time: the first three samples read Ultra mean 1.75–2.02 ms while the machine
    settled. `Perf-20261030-170735-743` and `Perf-20261030-170802-251` read Ultra mean
    1.68 ms and p99 2.22–2.27 ms, and Low mean 1.55–1.56 ms and p99 2.03–2.14 ms.
    That is within run-to-run noise of the 16:02 sample. The perf scene plays no wall
    runs.
- **Limits.**
  - The soles do not plant flat on the wall.
  - The upper body still comes from the stance hold layer.
  - The capture is a static frame of a scripted fixture. Nobody has judged it
    online in motion.

## 2026-10-30 slide knee tuck

*Historical: superseded by [no legs in view while sliding](#2026-10-30-no-legs-in-view-while-sliding).*

Scope: the shin cut off at the knee that floated mid-screen when you glanced down
during a slide. The contract is in the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md)
("Local body" row and the "Slide legs" limit).
- **Change.**
  - `FirstPersonBody.Present` takes the lens camera, from `ArenaPresentation` and from
    the smoke-only pose fixture in `FusionVisualCheckDriver`.
  - While sliding, `TuckKnees` draws the visible legs back along the lens heading
    until both knees sit below the bottom frame edge.
  - It applies fully up to 20° below level and fades out by 35°.
- **Rejected values.**
  - A full tuck at every pitch passed the knee-cut check. The packaged
    `fixture-slide-down` (40°) capture then showed a large knee pad over the crosshair,
    and the nearest visible vertex was 0.22 m from the eye.
  - A rule keeping each knee 0.3 m ahead of the eye could not hide the cut even at 15°.
- **Checks.**
  - `FirstPersonBodyTests.SlideGlanceDownKeepsTheKneeCutsBelowTheFrame` runs at 10° and
    20°. Both knees are below the frame, the lead boot shows, the boots stay above the
    floor, and the lead boot shows looking straight ahead.
  - `SlideKneesStayClearOfTheLens` now also runs with the lens at 40° and 70°. The
    nearest visible vertex stays more than 0.35 m from the eye.
  - PlayMode 176/177, 1 skipped (`PlayMode-20261030-152049-411`).
  - EditMode 484/486, 2 skipped (`EditMode-20261030-152028-171`).
  - Smoke `BuildSmoke-20261030-151913-110`.
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - motion `20261030-151935-443`
    - sequence `152001-679`
    - default `152112-784`
    - firefight `152139-799`
    - weapons `152148-869`
    - music `152242-261`
  - Compared with the [resent inventory actions](#2026-10-30-resent-inventory-actions)
    runs:
    - Only `fixture-slide-sustain` and `fixture-slide-fov60` changed among the motion
      and sequence frames. Both now show the boots rising from the bottom edge with no
      cut top.
    - `fixture-slide-down` and every sequence frame are pixel-identical.
    - The other modes differ only by small animated menu, pickup and blast
      differences under 0.7 mean.
  - Frame time (`Perf-20261030-152256-414`): Ultra mean 1.64 ms and p99 2.10 ms; Low
    mean 1.54 ms and p99 2.13 ms.
- **Playable build.**
  - `BuildWindows-20261030-152412-335` wrote `Artifacts/Unity/Windows`.
  - Its content hash over 200 files is
    `545fe33516e7b39ea1b05d8763aa70fe8bcf26a40b2891d70d3178c3bebb914c`.
  - It replaces `76f03229…`, which is kept at
    `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-76f03229`.
  - A 15-second headless start (`Artifacts/Unity/WindowsStartup-20261030-1524.log`)
    initialised the engine and was still running at 15 s, with no logged exception.
    That proves startup only, not play.
- **Limits.** A steep look down mid-slide still shows the cut. Nobody has judged the
  tuck in motion.

## 2026-10-30 second playable build refresh

Scope: a fresh playable build with the afternoon's work, for the user's playtest.

- `BuildWindows-20261030-150840-215` wrote `Artifacts/Unity/Windows`.
  - Its content hash over 200 files is
    `76f0322925b1807d13864d19e54c3cd2810354e0963dbfa2a062d4b8fb2566c5`.
  - It replaces `d6d14b5a…`, which is kept at
    `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-d6d14b5a`.
  - Four interim builds were overwritten before anyone played them:
    - 14:07 `a51aebc4…` (`BuildWindows-20261030-140753-040`, start log
      `WindowsStartup-20261030-1410.log`), without the pistol beam;
    - 14:14 `72a99a9e…` (`BuildWindows-20261030-141444-525`, start log
      `WindowsStartup-20261030-1416.log`), without the remote scrape;
    - 14:30 `8d2446a2…` (`BuildWindows-20261030-143051-409`, start log
      `WindowsStartup-20261030-1432.log`), without the shotgun hit sound fix;
    - 14:39 `8d5aa8c7…` (`BuildWindows-20261030-143927-078`, start log
      `WindowsStartup-20261030-1440.log`), without the D035 pickup fix.
- It adds everything since the 12:39 build:
  - shield break pop, movement lens and dash streaks, crosshair rim;
  - weapon voices and empty-trigger reload, strafing target;
  - speed wind, hit tick, slide scrape, near-miss zip, the low-ammo warning, the
    pistol beam, the remote slide scrape, the shotgun hit sound fix and the D035
    resent-pickup fix.
- After `BuildSmoke-20261030-150420-863`, only docs changed.
  - That build adds just the smoke-only `weapon-low-ammo` capture to the
    [resent inventory actions](#2026-10-30-resent-inventory-actions) source.
  - Its six-mode cycle and both suites pass. The Photon runs used
    `BuildSmoke-20261030-144825-883`.
- A 15-second headless start (`Artifacts/Unity/WindowsStartup-20261030-1510.log`)
  initialised the engine and was still running at 15 s, with no logged exception.
  That proves startup only, not play.

## 2026-10-30 resent inventory actions

Scope: the unexplained sniper-pickup timeout in the
[Photon regression runs](#2026-10-30-photon-regression-runs).
- **Logging.** Smoke builds now log `WILDSTONE COMBAT RPC EXPIRED` whenever the host
  drops a combat RPC for age. It is compiled only under `WILDSTONE_SMOKE`.
- **Cause.** Smoke `BuildSmoke-20261030-144217-804` ran four adverse150 gunplay runs.
  - `20261030-144247-819` failed at `gunplay_pickup_LaserSMG`.
  - Its host log shows `kind=Pickup sequence=8 age=21`: the reliable RPC arrived 21
    ticks (175 ms) old, past the 18-tick window, and was dropped.
  - The other three runs passed.
- **Fix (D035).**
  - `FusionPlayer.AcceptsCombatTick` gives Pickup, Reload, SelectStarter and
    SelectSpecial 60 ticks (500 ms).
  - The other actions keep 18. The host already judges these four on current state.
  - The contract is the
    [foundation spec](superpowers/specs/2026-10-25-multiplayer-foundation-design.md).
- **Checks:**
  - New `FusionCombatTickTests.InventoryActionsSurviveAResendButAttacksKeepTheirWindow`
    covers:
    - pickups accepted at 60 ticks and rejected at 61;
    - reload and both selections accepted at 21 or 60 ticks;
    - fire, grenade, Blink and shield rejected at 19 ticks, and shield accepted at 18;
    - future ticks rejected.
  - The original cases are unchanged.
  - EditMode 484/486, 2 skipped (`EditMode-20261030-144740-937`).
  - PlayMode 172/173, 1 skipped (`PlayMode-20261030-144802-040`).
  - Smoke `BuildSmoke-20261030-144825-883`.
  - Adverse150 gunplay passes 5 of 5 (`20261030-144853-798`, `144953-476`, `145041-917`,
    `145145-143`, `145239-570`).
    - Run `145041-917` still dropped six late SMG `Fire` RPCs (ages 19–23) as
      intended, and passed.
  - The other Photon scenarios all pass:
    - baseline combat `145329-051`
    - baseline gunplay `145508-353`
    - baseline team switch `145557-657`
    - baseline reconnect `145620-242`
    - baseline laser combat `145651-590`
    - adverse150 laser moving cover `145816-263`
    - adverse150 grenade combat `145928-626`
    - adverse150 special projectiles `145957-426`
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - default `20261030-150034-424`
    - firefight `150101-373`
    - motion `150110-386`
    - weapons `150132-524`
    - sequence `150224-279`
    - music `150229-343`
  - Frame time (`Perf-20261030-150243-530`): Ultra mean 1.65 ms and p99 2.13 ms; Low
    mean 1.55 ms and p99 2.07 ms.
- **Limits.**
  - Late attacks are still dropped. Under loss, held SMG fire can lose shots, which
    may explain the intermittent SMG verdict.
  - The fault profile is in-process injection, not an external network.

## 2026-10-30 shotgun hit sounds

Scope: `ArenaMatch` emits one Hit event per shotgun pellet. A full blast therefore
played up to eight identical HUD ticks (or head dings) in one frame, which sum
coherently and clip. It also played eight target cues, which took half of the 16
round-robin cue voices and could cut other tails.

The contracts are the [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md)
(hitmarkers) and the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
(mix). Damage and events are unchanged; only sound playback changed.

- **Checks:**
  - New `MenuPresentationTests.PelletHitsInOneFrameSoundOnce`: eight body marks in
    one frame sound once, a kill in that frame still sounds, and the next shot
    sounds again.
  - New `ArenaWeaponAudioTests.PelletHitsOfOneShotPlayOneCue`: eight pellet hits on
    one target play one `Block` cue. A second target and the next shot each get
    their own.
  - Smoke `BuildSmoke-20261030-143415-140`.
  - EditMode 473/475, 2 skipped (`EditMode-20261030-143314-396`).
  - PlayMode 172/173, 1 skipped (`PlayMode-20261030-143335-485`).
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - default `20261030-143444-397`
    - firefight `143515-381`
    - motion `143524-426`
    - weapons `143546-494`
    - sequence `143638-216`
    - music `143643-293`
  - Frame time (`Perf-20261030-143657-445`): Ultra mean 1.65 ms and p99 2.16 ms; Low
    mean 1.56 ms and p99 2.12 ms. That is unchanged.
  - Real Photon combat and gunplay at baseline pass:
    `Artifacts/Smoke/20261030-143724-036-combat-baseline` and
    `20261030-143811-790-gunplay-baseline`.
  - Superseded capture runs from earlier cycles were moved to
    `D:/WildstoneArchive/mvp-2/Artifacts/Visual/` under the same names.
- **Limits.**
  - One frame is the dedupe window. Pellets reported by two simulation steps in
    different frames would still sound twice, but one shot resolves in one step.
  - Nobody has heard it.

## 2026-10-30 remote slide scrape

Scope: an opponent's slide played only its entry whoosh. The contract is the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
(slide scrape). `ArenaAudio` shares one gain law between the centred local scrape and
the positional remote loops. `ArenaPresentation` cuts a remote loop when that player
dies or leaves.

- **Checks:**
  - New `ArenaWeaponAudioTests.RemoteSlidesScrapeFromTheSlider` covers:
    - no voice before a slide;
    - one looping positional voice at the slider's feet, at the local law's gain;
    - the cut from `StopScrape` and from a zero dt;
    - no remote voice for the local player.
  - `FootstepsFollowDistanceAndMovementEdgesPlayOnce` now expects 31 voices: the
    fixed 30 plus the loop for the one remote that slid. Its first run failed at
    30 before that update.
  - Smoke `BuildSmoke-20261030-142639-125`.
  - EditMode 472/474, 2 skipped (`EditMode-20261030-142506-021`).
  - PlayMode 171/172, 1 skipped (`PlayMode-20261030-142605-113`).
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - default `20261030-142708-374`
    - firefight `142739-400`
    - motion `142748-418`
    - weapons `142810-474`
    - sequence `142902-214`
    - music `142907-288`
  - Motion captures are pixel-identical to the pistol cycle. Weapons differ only in
    grenade particles and pickup spin.
  - Frame time (`Perf-20261030-142921-457`): Ultra mean 1.64 ms and p99 2.12 ms; Low
    mean 1.54 ms and p99 2.08 ms. That is unchanged.
  - Real Photon combat at baseline passes:
    `Artifacts/Smoke/20261030-142948-048-combat-baseline`.
- **Limits.**
  - The practice target never slides, so only online play makes the sound.
  - No packaged run plays it, and nobody has heard it.

## 2026-10-30 Photon regression runs

Scope: the afternoon changes touched production input (empty-trigger reload and
special drop), so real Photon runs were repeated. The smoke build was
`BuildSmoke-20261030-140950-472`, the same source as the playable build.

- **Baseline, all passed:**
  - combat `Artifacts/Smoke/20261030-141632-498-combat-baseline`;
  - gunplay `141724-529`;
  - team switch `141814-375`;
  - reconnect `141836-966`;
  - laser combat `141949-900`.
- **Adverse150 gunplay passed one of three runs.**
  - `20261030-141913-747`: the client timed out after 30 s at
    `gunplay_pickup_LaserSniper`.
    - The client sent its pickup RPC with sequence 0, the same first sequence a
      passing baseline uses.
    - The host never observed the sniper in the shooter's inventory.
    - The host log records no rejection reason, so the cause is unknown.
    - The empty-trigger change cannot take that sequence: it needs an empty
      magazine, and the starter holds 24 rounds at that step.
  - `20261030-142151-368`: `SMG held fire verdict mismatch: shots 5 hits 5 damage 50
    spacing 10 ammo 27`.
    - The same check failed at 01:08 (`20261030-010835-306`), before this
      afternoon's changes.
    - The counts, damage and ammunition agree. A remaining condition is the
      spread-grows-every-shot check, which an injected input loss can break by
      letting bloom decay.
    - This was confirmed later and the check was narrowed; see
      [SMG held-fire verdict](#2026-10-30-smg-held-fire-verdict).
  - `20261030-142207-999`: passed.
- **Limits.** Adverse gunplay is intermittent: the five earlier runs since 2026-10-29
  23:22 include two passes. These runs neither prove nor exclude a network fault. The pickup
  timeout was later traced to the RPC age window and fixed; see
  [resent inventory actions](#2026-10-30-resent-inventory-actions).

## 2026-10-30 pistol beam

Scope: the pistol's beam (.045 m glow, .014 m core, .11 s) nearly vanished on ivory
stone in `weapon-laserpistol-0/1`. That is weak for a 32-damage shot. The
[rendering spec](superpowers/specs/2026-10-25-unity-rendering-design.md) (shot reads)
now records .075 m, .022 m and .13 s. The dark sheath scales with the glow.

- **Checks:**
  - Weapons capture `20261030-141021-152` against `140040-956`: the beam is visibly
    wider and darker-edged in both pistol frames, but still the slimmest hitscan
    beam.
  - Smoke `BuildSmoke-20261030-140950-472`.
  - EditMode 472/474, 2 skipped (`EditMode-20261030-141145-701`).
  - PlayMode 170/171, 1 skipped (`PlayMode-20261030-141206-949`).
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - default `20261030-141230-428`
    - firefight `141257-377`
    - motion `141306-410`
    - weapons `141021-152`
    - sequence `141328-477`
    - music `141333-573`
  - Firefight differences are at most 0.19 mean, within grenade-blast frames.
  - Frame time (`Perf-20261030-141347-698`): Ultra mean 1.65 ms and p99 2.16 ms; Low
    mean 1.55 ms and p99 2.05 ms. That is unchanged.
- **Limits.** The pistol beam still brightens rather than darkens ivory, so its
  contrast rests on the sheath. Nobody has judged it in motion.

## 2026-10-30 low-ammo warning

Scope:
- Running dry mid-fight had no warning before the empty trigger.
- The practice menu and HUD hint still called the target stationary after it began
  strafing.
- A slide-leg trial was reverted.

The contract is the [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md)
(weapon panel). `ArenaHud.LowAmmo` decides the range. `ArenaAudio.MakeDry` builds the
click, which plays on the existing HUD feedback voice, so no voice was added.

- **Checks:**
  - New `MenuPresentationTests.LastQuarterOfAMagazineWarnsWithADryClickUnderTheHitTick`
    covers:
    - range edges for the Line Driver (6 low, 7 not), sniper (1 / 2) and an empty
      SMG;
    - no warning for the sword or an empty hand;
    - a 0.85-normalised click of at most 0.06 s that decays without a click;
    - a zero-crossing rate under 0.7 of the hit tick's;
    - a playback scale under the tick's.
  - Smoke `BuildSmoke-20261030-135912-056`.
  - EditMode 472/474, 2 skipped (`EditMode-20261030-135819-484`).
  - PlayMode 170/171, 1 skipped (`PlayMode-20261030-135843-543`).
  - All six packaged modes pass, with no exception, NRE or RenderGraph lines in the
    player logs:
    - default `20261030-135935-096`
    - firefight `140009-693`
    - motion `140018-845`
    - weapons `140040-956`
    - sequence `140132-722`
    - music `140137-808`
  - Against the near-miss cycle, pixel differences sit on the HUD hint line, apart
    from the practice menu.
    - The first menu text wrapped to a third line and moved the arena buttons down.
    - It was shortened. Smoke `BuildSmoke-20261030-140232-260` and default capture
      `20261030-140255-853` show the original two-line layout.
  - Frame time (`Perf-20261030-140643-436`): Ultra mean 1.66 ms and p99 2.10 ms; Low
    mean 1.56 ms and p99 2.07 ms. That is unchanged.
    - The first attempt, `Perf-20261030-140326-958`, exited 1 because drive C: had
      0 MB free.
    - Older capture runs from this session were moved to
      `D:/WildstoneArchive/mvp-2/Artifacts/Visual/`, which freed 0.9 GB.
- **Slide legs.** Restoring the thighs in place of the shins removed the floating shin.
  - The open thigh tops then faced the lens as dark tubes in `fixture-slide-down`,
    `slide-sustain` and `slide-fov60`.
  - Motion run `20261030-135413-067` is the trial and `135008-549` the baseline. The
    trial looked worse and was reverted. The limit stays in the
    [character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md).
- **Low-ammo capture (added later the same day).** `-Weapons` now ends with
  `weapon-low-ammo` (32 captures).
  - Run `20261030-150444-257` (smoke `BuildSmoke-20261030-150420-863`) shows the
    Line Driver's count as a red `5` beside the ivory `24` of `weapon-linedriver-0`.
  - EditMode 484/486 (`EditMode-20261030-150557-488`) and PlayMode 172/173
    (`PlayMode-20261030-150618-538`).
  - The other five modes pass: default `150641-938`, firefight `150708-897`, motion
    `150717-912`, sequence `150739-951`, music `150744-992`.
  - `Perf-20261030-150759-225`: Ultra 1.65/2.14 ms, Low 1.55/2.09 ms.
- **Limits.** Nobody has heard the click; batch players have no audio.

## 2026-10-30 near-miss zip

Scope: being shot at made no sound unless the shot hit. The contract is the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
(near miss). `ArenaPresentation` passes each enemy shot to
`ArenaAudio.PlayNearMiss` with the camera position. The segment is the one the beam
effect draws, so sound and picture agree. It reuses the `Swing` bank, so no new
asset was added.

- **Checks:**
  - New `ArenaWeaponAudioTests.NearMissZipsOnlyWhenTheBeamPassesTheEar`. A beam 0.8 m
    from the ear zips once, from the passing point, at 0.5 × (1 − 0.8/1.6). Nothing
    plays for a beam 3 m wide, a listener behind the shooter, muted effects, a beam
    whose Hit event ends at the listener, or a wall cube between shooter and
    listener.
  - Smoke `BuildSmoke-20261030-134821-978`.
  - EditMode 471/473, 2 skipped (`EditMode-20261030-134844-033`).
  - PlayMode 170/171, 1 skipped (`PlayMode-20261030-134905-227`).
  - All six packaged modes pass: default `20261030-134928-454`, firefight
    `134959-541`, motion `135008-549`, weapons `135030-598`, sequence `135122-340`,
    music `135127-411`. The player logs have no exception, NRE or RenderGraph lines.
  - Frame time (`Perf-20261030-135141-558`): Ultra mean 1.65 ms and p99 2.15 ms; Low
    mean 1.55 ms and p99 2.07 ms. That is unchanged.
- **Limits.**
  - Only online play has enemies who fire; the practice target never shoots. No
    packaged or Photon run exercised it, and nobody has heard it.
  - Shotgun pellets zip once per shot, along the centre pellet.

## 2026-10-30 hit tick and slide scrape

Scope: two missing sounds in the gunplay and movement loop.
- A body hit had a hitmarker but no sound unless it was a head hit or a kill. The
  positional "Block" cue at the target is inaudible at range.
- A slide had only its entry whoosh.

The contracts are the [UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md)
(hitmarkers) and the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
(slide scrape). The scrape ports vibegame1's `SlideFx` law and noise recipe, with no
code copied ([SOURCE-REUSE](SOURCE-REUSE.md)). Both games use the same 8/22 m/s
slide speeds.

- **Checks:**
  - New `MenuPresentationTests.BodyHitTickIsShorterAndQuieterThanTheHeadConfirm`
    checks the tick is at most 0.06 s and under half the head ding, normalised to
    0.85, decays without a click, and plays at a scale below 0.6.
  - New `ArenaWeaponAudioTests.SlideScrapeRidesSlideSpeedAndReleases` covers silence
    off a slide, full gain and 1.32 pitch at 22 m/s, 0.78 pitch at 8 m/s, a ramped
    release that ends by 0.18 s, the immediate cut on mute or pause, and a loop seam
    no larger than any sample step.
  - Voice totals rise from 29 to 30.
  - Smoke `BuildSmoke-20261030-134220-557`.
  - EditMode 471/473, 2 skipped (`EditMode-20261030-134242-654`).
  - PlayMode 169/170, 1 skipped (`PlayMode-20261030-134303-695`).
  - All six packaged modes pass: default `20261030-134327-013`, firefight
    `134358-072`, motion `134407-114`, weapons `134429-123`, sequence `134520-917`,
    music `134525-998`. The player logs have no exception, NRE or RenderGraph lines.
  - Frame time (`Perf-20261030-134540-158`): Ultra mean 1.64 ms and p99 2.11 ms; Low
    mean 1.55 ms and p99 2.09 ms. That is unchanged.
- **Limits.** Nobody has heard either sound. The tick's wiring in `ArenaHud.Mark` has
  no packaged check, because batch players have no audio and the `-Music` mode fires
  no hits. Other players' slides make no scrape.

## 2026-10-30 speed wind

Scope: fast movement had footsteps and edge cues but no sense of speed by ear. The
contract is the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
(speed wind). It ports vibegame1's `CameraBeats` wind law (concepts and values; no
code copied) with that game's CC0 `SpeedWind` loop
([SOURCE-REUSE](SOURCE-REUSE.md#recorded-weapon-and-movement-audio-2026-10-30)). Both
games use the same motor tuning (`MotorTuning.Source`: 11 m/s ground, 22 m/s dash),
so the 14–24 m/s range carries over unchanged.

- **Checks:**
  - New `ArenaWeaponAudioTests.SpeedWindRisesWithSpeedOnOneLoopingVoice` covers one
    looping centred voice, silence at an 11 m/s run and in a straight fall, a level
    between 0.1 and 0.3 at 19 m/s, the cap times effects volume, pitch 1.1 at full
    speed, and mute.
  - The voice totals in the two existing audio tests rise from 28 to 29.
  - Smoke `BuildSmoke-20261030-133459-099`.
  - EditMode 470/472, 2 skipped (`EditMode-20261030-133521-516`).
  - PlayMode 168/169, 1 skipped (`PlayMode-20261030-133542-444`).
  - All six packaged modes pass: default `20261030-133605-893`, firefight
    `133636-881`, motion `133645-929`, weapons `133707-986`, sequence `133759-754`,
    music `133804-868`. The player logs have no exception, NRE or RenderGraph lines.
  - `-Music` now also requires the wind voice. It recorded "laserfire_00 at pitch
    0.99, volume 0.83, output RMS 0.1219; lasercellnova_01 at pitch 0.98, volume 0.44,
    output RMS 0.0466; wind speedwind_00 looping".
  - Frame time (`Perf-20261030-133818-997`): Ultra mean 1.64 ms and p99 2.12 ms; Low
    mean 1.54 ms and p99 2.05 ms. That is unchanged.
- **Limits.** Nobody has heard it. The packaged check proves the loop ships and
  plays, not its level at speed or its balance with the music.

## 2026-10-30 strafing practice target

Scope: Practice had one frozen target, so gunplay could only be judged against a
still body. The contract is the
[UI spec](superpowers/specs/2026-10-25-ui-gameflow-design.md) (Practice).
`PracticeSession` now drives participant 2's motor with `PracticeSession.Drill`, a
pure function of the leg index, the tick within the leg, the target's position, its
spawn and the player. Its aim follows its yaw. Online play is unaffected.

- **Checks:**
  - New `MotorCollisionTests.PracticeDrillStrafesJumpsDashesAndStaysNearHome` runs
    20 s of the drill on a real motor. It travelled 111.7 m and never went more than
    5.26 m from its spawn. Jump and Dash both fired, it faced the player, and every
    leg was 60–156 ticks.
  - New `NetworkPresentationBridgeTests.PracticeTargetFixtureStrafesFacingThePlayer`
    steps a real offline practice match for 3 s. The target leaves its spawn and its
    combat aim faces the player.
  - Smoke `BuildSmoke-20261030-132720-657`.
  - EditMode 470/472, 2 skipped (`EditMode-20261030-132742-721`).
  - PlayMode 167/168, 1 skipped (`PlayMode-20261030-133218-096`, rerun after the
    second test was added).
  - All five packaged modes pass: default `20261030-132826-907`, firefight
    `132857-977`, motion `132907-012`, weapons `132929-069`, sequence `133020-867`.
  - Against the previous run, pixel differences are only the animated title and
    settings backdrop, one blast's particles, and the pickups' bob and spin. The
    target is not in any still.
  - Frame time (`Perf-20261030-133025-965`): Ultra mean 1.64 ms and p99 2.09 ms; Low
    mean 1.54 ms and p99 2.06 ms. That is unchanged.
- **Limits.**
  - No packaged capture shows the target moving.
  - Whether its speed and rhythm make good practice needs the playtest.
  - Nothing stops it stepping off a ledge near its spawn. It would then stay below
    until it dies. This has not been observed.

## 2026-10-30 weapon voices and empty-trigger reload

Scope: the user put weapons and gunplay first.
- Six guns shared four `LaserFire` clips and differed only by pitch. The Line Driver
  and pistol were 12% apart.
- An empty trigger did nothing until R or 1 was pressed. The host rejected the shot,
  and nothing else was sent.

The contracts are the
[character spec](superpowers/specs/2026-10-25-characters-weapons-design-workflow.md#audio-2026-10-30)
(audio) and the
[combat spec](superpowers/specs/2026-10-25-arena-combat-items-zone-design.md)
(default controls).

- **Banks.** `TurretAnswer` (3 clips) and `LaserCellNova` (2) were copied
  byte-identical from `vibegame1` `2cb6e57` (clean), with GUIDs preserved
  ([SOURCE-REUSE](SOURCE-REUSE.md#recorded-weapon-and-movement-audio-2026-10-30)).
  They were chosen by measurement over the other unused shot-like banks:
  - `TurretAnswer` has an instant attack, about 120 ms long, with 60–70% of its
    energy at 250 Hz–2 kHz. `LaserFire` puts 75% of its energy under 250 Hz.
  - `LaserCellNova` has an instant attack and a 0.5 s low tail.
  - `SpellFireCast` and `SpellVoidCast` were rejected because they swell 60–500 ms
    after the trigger, which would sound late on a shot.
- **Levels.** Gains match each weapon's previous K-weighted (BS.1770) loudest-50 ms
  level at its playback pitch:
  - pistol 0.78 becomes 0.80, and SMG 0.62 becomes 0.64;
  - the nova tail sits 6 dB under the sniper's shot (0.44) and 8 dB under the
    shotgun's (0.28, at 1.5 pitch).
- **Empty trigger.** `PracticeInput` replaces a shot on an empty starter clip with a
  reload request, retried at most every 0.25 s. On a spent special it sends a starter
  select and blocks the held trigger. The sword is unaffected. The host rules are
  unchanged, and online play uses the same `ConsumeActions` path.
- **Checks:**
  - `ArenaWeaponAudioTests` now checks the tail voice's bank, pitch and volume, that
    guns sharing a bank are at least 20% apart in pitch, and that the guns use at
    least three banks.
  - New `PracticeInputTests.EmptyTriggerReloadsTheStarterOrDropsASpentSpecial` covers
    no futile shot, the retry throttle, no request mid-reload, the special swap with
    the held trigger blocked, and the sword.
  - The repeat-stop test now expects one reload request on an empty clip.
  - Smoke `BuildSmoke-20261030-131802-772`.
  - EditMode 470/472, 2 skipped (`EditMode-20261030-131826-402`).
  - PlayMode 165/166, 1 skipped (`PlayMode-20261030-131847-414`).
  - All six packaged modes pass: default `20261030-131910-724`, firefight
    `131941-742`, motion `131950-755`, weapons `132012-849`, sequence `132104-553`,
    music `132109-634`. The player logs have no exception, NRE or RenderGraph lines.
  - `-Music` now also requires the nova tail. It recorded `laserfire_01` at pitch
    1.00, volume 0.83, output RMS 0.1251, and `lasercellnova_01` at pitch 1.01,
    volume 0.44, output RMS 0.0513.
  - Frame time (`Perf-20261030-132123-730`): Ultra mean 1.64 ms and p99 2.13 ms; Low
    mean 1.54 ms and p99 2.06 ms. That is unchanged.
- **Limits.** Nobody has listened. The timbres and levels are measured, not heard.
  No Photon run was repeated: the reload and select requests use the existing,
  network-tested actions, and the host rules are unchanged.

## 2026-10-30 playable build refresh

Scope: a fresh playable build with the day's presentation work, for the user's
playtest.

- `BuildWindows-20261030-123946-374` wrote `Artifacts/Unity/Windows`. Its content
  hash over 200 files is
  `d6d14b5af7c3a3735a58a555f274fa4ede761801b31a3b7f7274c77774646fcd`, which replaces
  `889c10b8…`. The previous build is kept at
  `D:/WildstoneArchive/mvp-2/Artifacts/Unity/Windows-889c10b8`.
- It includes the soldier outline, pickup pads, the T-pose fix, the shield shell and
  the stun halo. The smoke build that passed every capture mode for them is
  `BuildSmoke-20261030-123016-857`, from the same source.
- A 15-second headless start (`Artifacts/Unity/WindowsStartup-20261030-1243.log`)
  initialised the engine with no logged exception. That proves startup only, not play.
