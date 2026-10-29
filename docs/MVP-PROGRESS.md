# MVP execution ledger

Plan: `docs/superpowers/plans/2026-10-25-wildstone-mvp.md`.
Goal active; user explicitly resumed and delegated all work possible while away.

## Current position

2026-10-28 delivery: user requested the current-state build and confirmed public
publication. Committed source snapshot `316a00f` built in a separate archive checkout,
passed 15-second headless startup and ZIP/checksum gates, and was released as public
`preview-20261028-316a00f`. Later concurrent edits are excluded and untouched. No
full-suite, visual/input or external-network acceptance was added; see [QA](QA.md).

GitHub delivery (2026-10-27): private `Capsfan900/multiplayer_1` created and a manual
Windows Practice prerelease workflow uploaded. Clean-source commit `e3f5815` built
successfully and its ZIP/checksum were published as a private preview via the exact
workflow packaging/publication steps run locally. Runner registration was blocked
by automatic approval review even after authorization; manual setup and the first
Actions run remain pending. [TOOLING](TOOLING.md#github-builds-and-private-releases)
owns operation, [QA](QA.md) exact evidence and limitations.

Updated 2026-10-28 in `C:\Users\tyler\orca\workspaces\multiplayer_1\mvp-2` (`mvp-2`).
The 2026-10-26 authored checkpoint was recovered read-only from the original project;
194 copied files passed before/copy/after SHA-256 comparison. Original source and
sibling projects were not changed. Historical entries below describe earlier runs.

- Unity licensing and native import now work. Compile and full content preparation pass.
- Native EditMode: 192 passed, zero failed, two explicit checks skipped; those two passed in their deliberate preparation run. Native PlayMode: 62 passed, zero failed. Exact identities and limits are in [QA](QA.md).
- Fresh managed baseline/final: 172 passed, zero failed/skipped. Existing four Python map regressions and asset/source hashes also pass.
- Native fixes cover empty startup scenes, missing Unity components, interrupted controller generation, imported model handedness, initial character capsule query registration, final Blink displacement and cross-update input taps. Replay checks separately bound native floating-point vectors while retaining exact timers and discrete state.
- All three maps have successful native CPU lighting bakes and Windows compilation passes. Sixteen packaged offscreen menu/all-map captures now pass static inspection; physical-input and moving-readability acceptance remain open, with desktop automation paused.
- An independent GPT-6.1 Sol review found one fixture scene-preservation gap; it now uses the shared guard and its focused native tests pass. No material runtime finding remains from that review.
- Fusion 2.1.3 Stable build 2390 and the supplied App ID are configured. Import evidence remains in `Artifacts/Resume/photon-setup.md`; the required Physics 2D module is enabled.
- Real motor/input mappings, networked movement, proxy poses and private runner lifecycle are integrated in the separate `Wildstone.Fusion` assembly. Native focused EditMode 42/42 and PlayMode 9/9 pass after integration, including actual SDK weaving and a real Single runner. [QA](QA.md) owns run identities and boundaries; Single mode is not remote prediction or Photon service acceptance.
- D023 removes PlayFab and all account UI/client code. Photon-only private host/join uses a temporary guest ID and a per-match reconnect credential in memory. No PlayFab Title ID or custom-auth setup is required. Closing the game cannot recover an old guest seat.
- Lobby/team/ready/load, late join, complete match/combat replication, SDK hitscan rewind, shared-orb rollback and whole-match migration hooks are implemented. Focused native checks and real Photon tests pass. Separate Windows processes now verify gameplay, reconnect, movement/fire deduplication, eight-player capacity, late admission, controlled stationary hit replication, graceful host migration and abrupt-crash recovery. External-network/performance acceptance remains open. [QA](QA.md) owns exact runs and limits. Settings contrast is verified in packaged offscreen captures; desktop automation remains paused after physical Escape.
- Latest Photon-only Windows build succeeds at `Artifacts/Unity/Windows-20261028-200856-804/WildstoneArena.exe`; prior builds remain preserved. Fifteen-second headless startup stayed alive with no logged exceptions and both diagnostic drivers excluded. Build source did not change (1,157 inputs; 200 output hashes); it includes uncommitted inputs and remains local-playtest-only, not published. Physical input, moving readability and external-network acceptance remain open. Exact commands/hashes are in [QA](QA.md).
- Unattended polish: menu focus/HUD lifetime, recovery countdown and settings scrollbar fixes; runner-local avatar discovery and stable presentation registry. Focused native 65 EditMode/17 PlayMode checks pass. Five-minute eight-process Photon gameplay, late admission, controlled combat and abrupt host-crash regressions pass. Unity allocation-counter calibration failed, so no measured allocation reduction is claimed.
- D025 implemented: visible animated legs, articulated wrist/finger/sleeve handling and stronger cosmetic slide feedback. Focused native 35/35, thirteen inspected packaged pose fixtures and fresh Photon combat/reconnect checks pass. Human animation/slide feel remains open; QA owns exact evidence.
- Full MVP remains open. Continue the concrete next steps in [HANDOFF](HANDOFF.md), not the historical pause packet. Canonical network targets are unchanged.

## Rulings

- Ruling: work directly in the user's separate `multiplayer_1` project, with a new local MVP branch if permitted; no source worktree is needed because this is already the intended isolated destination.
- Ruling: user approval to build the discussed game supersedes the pause and avoids re-asking design/approach approval. Preserve prior visual and game decisions.
- Ruling: D018 permits independent rules/assets/UI work while SDK/account input is unavailable; this does not waive actual Fusion, identity or internet acceptance.
- Ruling: source laws/prefab tuning and pure geometry are adaptation inputs; do not copy source `Library`, entire manifest, campaign/player prefabs, untracked audio or mutable global managers.

## Preflight interface/conflict scan

| Tasks | Boundary checked | Result |
| --- | --- | --- |
| 1 | project root/package/tool outputs and its own compile gate | Explicit local outputs, no source mutation; SDK is a visible dependency |
| 2 | state/input/tuning and replay tests | State restoration is not source Teleport; includes private traversal timers |
| 3 | session seats/identity/recovery tests | Local identity cannot satisfy verified online admission |
| 4 | action state/phase ordering/test cases | Stable generations/action IDs and published phase order |
| 5 | rig/export/presentation tests | Effects cannot own gameplay, single axis convention |
| 6 | map definitions/build/validators | Three distinct IDs and supported routes, no campaign builder dependency |
| 7 | UI/settings/player flow | Local settings are separate from authoritative state; practice labeled |
| 8 | acceptance/evidence/packaging | Local/offline/online/external gates remain distinct |
| 1 -> 2-7 | Unity packages and runtime assembly boundary | Pin minimal dependencies; confirm APIs in installed Unity |
| 2 -> 3,4 | MotorInput/MotorState/collision | One simulation path consumed by local test driver and actual Fusion |
| 3 -> 4,7 | ownership/session events/match state | UI sends intent; no trusted client damage/team assignments |
| 4 -> 5,6,7 | combat events, catalog IDs, objective state | Presentation and map assets consume stable IDs only |
| 5 -> 6,7 | material/model/socket conventions | Validate roundtrip before expanding assets |
| 6 -> 3,4,7 | map ID, route/spawns, selection | Content compatibility checked before online spawn |
| 2-7 -> 8 | build/log/test artifacts | Capture precise identity and real limits; no inferred passes |

## Source audit evidence

The following entries preserve the execution history. The Current position section
above and final checkpoint entry below supersede earlier in-progress wording and
older run totals.

Independent read-only audits completed for movement/dependencies, art/authoring and requirements/QA. Detailed reuse inventory will be saved alongside the first imports. No source files changed.

- Ruling: parallel asset authoring is isolated to Tools/Assets, ArtSource and Art exports while workspace setup owns Packages/ProjectSettings. This follows the active delegation instruction; only the workspace owner runs Unity. Asset batches require an imported axis fixture first; no shared source or scene mutations.

- Task1 authored; Unity compile blocked by sandbox licensing IPC. Escalated Compile request waited635seconds without approval and was interrupted before execution. No destination Editor remains from that launch. Invalid-argument checks passed; runtime gates pending permission.
- Ruling: D018 permits provisional model exports and production code with tests authored while Unity execution is permission-blocked. No TDD RED/GREEN, imported-axis, runtime or network pass is inferred. Blender/native-script checks continue; mandatory Unity acceptance stays open.

- Root owns Task6 authored map definitions/builder/validators and map-only tests. All Unity execution remains queued; semantic type checks use installed Roslyn/real Unity managed references without license or Editor execution. This is not build or gameplay evidence.

- Task1 fix round1/5: all5reviewfindings addressed; authoredsetup approvedbyindependentreview, Unityruntimegatepending. No committedimplementation yet.
- Task2 authored;40movementmanagedtests pass (39law/input+1testchecking95sourceprefabnumbers);42nativecasesauthoredbutnotrun. Independentreviewnext. Session and map managedtests joinsharedsuite separately.

- Ruling D019: five continuous seconds of restored opposition closes an absence episode; brief reconnect loops cannot reset its original deadline. Canonical foundation updated. This avoids immediate abort for an unrelated disconnect much later in a healthy match. Initial builder default, subject to actual network QA.

- Task2 fix round1/5 authored: explicit sampled edges retained, rollback-safe feedback identities, and ordinary orb pops preserve dash cooldown. Four new pure identity cases observed failing then passing; three native regressions compiled but unrun. Scoped re-review queued.
- Task3 fix round1/5 authored: preserve locally known connection-generation watermark and original disconnect holds through older checkpoint restoration; D019 stable-recovery policy and checkpoint timestamps covered. Thirty-two session cases pass; scoped re-review queued.
- Latest shared managed evidence: Artifacts/ManagedCompile/20261026-001717-443, 118 passed, zero failed. This is a concurrent workspace snapshot, not native Unity or network evidence.
- Task7 delegated to menu_gameflow, owning UI/Game/bootstrap authoring only. Practice uses actual movement/combat rules and native physics when run; online remains unavailable until legitimate SDK/service setup. Root retains global docs/tooling/maps ownership.
- Task2 fix round1/5 reviewed: all three findings addressed, zero open in fix diff. Authored movement approved; native trajectory/feel, CC replay and actual Fusion mapping remain mandatory open gates. Review: task-2-rereview1.md. Implementation still uncommitted.
- Task6 Blender evidence complete: three editable maps/nine previews from exact exported definitions, 73 structural boxes; 1,118 sampled route centres with 49 footprint points each, no support/clearance/opening-LOS flags. This remains sampled geometry math, not native traversal or visual acceptance. Report: task6-preview-report.md.
- Task3 fixround1 review: original2findings addressed; newdifferent-generation disconnect episode could inherit an olderhold. Round2/5 reproduced andfixedincludinglatecheckpointrestore. Scopedround2approved, 0open in fixdiff. Thirty-fivesessionmanagedtests pass; realprovider/Fusion/wholegamecrossprocessgatesremainopen. Reviews task-3-rereview1/2.md.
- Task4 review found three must-fix issues: recurring tick allocations, Pulse disappearing at range before expiry explosion, and unusable restored counters. Original implementer is fixing round1/5 with focused regressions and allocation measurement.
- Task5 review found seven presentation issues: grip alignment, animation selection/death visibility, recurring trail/event allocations, dense dedup eviction, fuse/status readability, overlay FOV updates and culling-mask restoration. Original implementer is fixing round1/5; native visuals remain unverified.
- Task7 full Practice/menu implementation and native tests are authored; map-path mismatch and bootstrap scene preservation were corrected before independent review. Latest managed evidence before review fixes: 145 passing at003015-270. No native UI/frame-rate claim.

## Latest review and integration checks

- Task 4: fix round 2/5 approved, all original findings and the introduced request-counter denial-of-service addressed. Client terminal sequences reject before state mutation and cannot abort the match. Persistent Practice sequences across respawn still work. Report: `task-4-rereview2.md`.
- Task 5: fix round 2/5 approved. Original seven findings addressed; runtime destruction tests now belong to PlayMode, while FBX assertions remain EditMode. No native result is claimed. Report: `task-5-rereview2.md`.
- Task 7: fix round 1/5 approved, six findings addressed and zero introduced issues in the scoped review. Reports: `task-7-report.md`, `task-7-rereview1.md`.
- Task 6: fix round 1/5 has corrected full capsule footprint checks, grading-component persistence and failing lint exit status. The stronger lint also exposed ramp/platform lips and a blocked pickup; minimal layout corrections preserve all three map identities. Code is stable; refreshed Blender evidence and scoped re-review are pending.
- Latest shared rule evidence: `Artifacts/ManagedCompile/20261026-004856-703`, 172 managed cases passed, zero failed. The runtime, editor and 20 test files compiled. Newly authored native map/profile tests were not executed.
- Separate assembly-boundary compilation passed for all five actual asmdefs using only declared direct package/project references: `Artifacts/QA/AssemblyBoundaries-20261026-005136`. This supplements the merged managed-test compile and does not run Unity.
- Fourteen model-export hashes and their editable-source hashes match the manifest. Both PowerShell tools parse; 196 local Markdown links across 30 files resolve. These are authoring checks only.
- Final integrated authored-checkpoint review is active. It includes root tooling and cross-task lifecycle paths, with all native, Fusion and external-machine acceptance gates left open.
- Integrated review found three important setup/lighting gaps: missing Audio module, disabled world-camera post-processing and no additive light-probe refresh. One final fix agent owns all three plus the map persistence fixture's missing output-folder setup. No native failure was claimed from static inspection.
- Task 7: minor (deferred): HUD formats display text every frame. Whole-game allocation/performance profiling must determine its practical cost; the zero-byte claim remains restricted to the measured combat core. This is recorded for the final reviewer and native performance pass.
- Native risk retained: PresentationBuilder temporary prefab construction may dirty the user's active scene before Bootstrap's dirty-scene guard. This has not been reproduced. Native Prepare must verify active-scene preservation, no leftovers and useful failure handling; do not suppress the protection.
- Final handoff trace found an ineffective Music slider with no music source. The same final fix wave hides it while preserving stored settings compatibility; the UI contract now makes this control conditional on actual music. No new music feature is added.

## Final independent checkpoint

- Integrated review: `authoring-final-rereview.md` approves all three integration
  fixes, the map fixture setup and the ineffective slider removal. No new material
  scoped regression; native/full-MVP approval remains explicitly withheld.
- Final evidence: `Artifacts/ManagedCompile/20261026-010458-840`, 172 managed passed,
  zero failed; 43 runtime, eight editor and 22 test files compiled. All five actual
  asmdef boundaries compile at `Artifacts/QA/AssemblyBoundaries-20261026-010618`.
  Engine-module resolution is still a native import gate.
- Map evidence remains `Artifacts/MapFix1`: four Python regressions, three refreshed
  Blender maps/nine previews and 73 saved meshes checked against the corrected JSON.
- Final report/resume entrypoint: [HANDOFF](HANDOFF.md), with [USER-SETUP](USER-SETUP.md)
  and [QA](QA.md). The original pause packet is unchanged. No verified executable,
  Photon app, provider title, internet session or external performance result exists.
