# Session recorder (development add-on)

**Status:** implemented on `idea/session-recorder`; recorder fidelity, analyzer, build and smoke checks pass locally
(see Verification). A human calibration run in a real build is still pending.

## Intent

The developer plays the real Windows player and the game records what they did and what each system
did, so tuning and bug work is driven by real sessions instead of simulated ones. The report turns
"the SMG felt mushy around minute 3" into data: inputs, motor events, combat outcomes, refusals,
movement state and frame time.

## Ownership and boundary

- Owned entirely by `Assets/Wildstone/DevTools/` (assembly `Wildstone.DevTools`) and `Tools/Sessions/`.
  Deleting those two folders and this file removes it; no other file references them.
- It **observes only**. It subscribes to existing `PracticeSession` events (`Stepped`, `ScreenChanged`,
  `SettingsChanged`, `SessionStarted`, `SessionStopped`) and adds extra `performed` listeners to the
  existing input actions. It never sends input, changes rules, time, settings or scenes.
- **Off by default.** Without the `--wildstone-record` launch argument its startup hook returns
  immediately, so normal players behave exactly as before. No core system was changed to add it.

## Inputs

| Argument | Meaning |
|---|---|
| `--wildstone-record` | Turn recording on |
| `--wildstone-record-dir <dir>` | Output folder (default `%USERPROFILE%\AppData\LocalLow\<company>\<product>\Recordings\<stamp>`) |
| F8 (in game) | Mark "something felt wrong here": writes a `mark` row and `mark-N.png` screenshot (encoded off the main thread) |
| `Play-Recorded.ps1 -Expect "<k=v,...>"` | Trust check against the report's Counts |
| `Play-Recorded.ps1 -Video` | Also record the screen (off by default; game data is always recorded) |

## Output: `session.jsonl`

One JSON object per line, `t` = seconds since recording started. Rows:

| type | When | Main fields |
|---|---|---|
| `meta` | once at start | build identity, Unity, GPU, CPU, RAM, screen, refresh, quality, vsync |
| `settings` | start and every settings change | sensitivity, FOV, ADS toggle, reduced motion, bindings |
| `screen` | menu screen change | Title, Playing, Pause, Results, ... |
| `session` | match start/stop | map, tick rate, online flag, phase, winner |
| `input` | every gameplay key press | action name (Forward ... Aim), tick |
| `combat` | every combat event, every tick, never sampled | kind, actor, seq, action, target, weapon, amount (spread for Shot, damage for Hit), head, reason, pos, vec |
| `move` | any tick with motor events, per participant | events (Jump, Dash, Slide, Land, WallStart, WallEnd, WallJump, Perfect, Launch), speed, pos |
| `sample` | every 6th tick (20 Hz) | local pos/vel/speed/yaw/pitch, grounded, sliding, wallrun, dashing, stamina, held buttons, hp, shield, weapon, ammo, charges, reloading, stunned, ADS, bloom, target pos/hp, zone, captor, scores |
| `perf` | each real second | frames, avg/p95/max ms, frames over 25/50 ms, GC count, managed MB, playing flag |
| `mark` | F8 | n, tick, screen |
| `end` | quit | reason, marks |

Edge cases: lines are flushed every second, so a crash loses at most the last second (no `end` row; the
analyzer reports it and skips a truncated last line). Non-finite numbers are written as `null`. Online
matches are recorded through the same session events, but only practice has been checked.

## Use

```powershell
./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows        # once per code change
./Tools/Sessions/Play-Recorded.ps1                         # play, press F8 on bad moments, quit
./Tools/Sessions/Play-Recorded.ps1 -Video                  # same, plus screen video
```

The script writes `Artifacts/Sessions/<stamp>/` (git-ignored): `session.jsonl`, `report.md`,
`player.log`, `mark-N.png`, and, with `-Video`, `session.mkv` + `video.json` + `frames/`.

### Screen video

With `-Video`, the launcher records the main monitor with ffmpeg (`ddagrab` desktop duplication + `h264_nvenc`, 30 fps,
MKV so a cut-off recording stays playable). The recorder's `meta.startedUtc` and `video.json` give the
two wall-clock starts, so the analyzer maps any data time to a video time and extracts `frames/` at
1.5 s and 3 s before each F8 mark and at the mark itself (the bad moment usually happens before the key
press), and at the worst frame-time spikes. Claude reviews those frames next to the data rather than
watching the whole video; more frames for a stretch can be pulled on request with
`ffmpeg -ss <seconds> -i session.mkv -frames:v 1 out.jpg`. Video is off unless `-Video` is passed, so
ordinary runs give clean performance numbers.

### Report

The report covers counts, per-weapon accuracy/damage/kills, accuracy by range
and movement state, pressed-vs-happened gaps, rule refusals by reason, movement time split and speed,
zone time, frame-time summary with worst moments, a 5-second context for each F8 mark, and player-log
exceptions.

## Acceptance

1. **Fidelity (automated):** the file contains every combat event exactly once and in order, every
   refusal reason, a movement row for every motor event tick, and state samples that match the
   simulation. Text escaping and non-finite numbers stay valid JSON.
2. **Analyzer (automated):** counts, accuracy, kill credit, range buckets, ignored presses, zone time and
   the `--expect` check produce the known answers for a synthetic session.
3. **Calibration (human, pending):** in a real build, do this drill in practice and run
   `./Tools/Sessions/Play-Recorded.ps1 -Expect "shots=10,reloads=1,jumps=5,slides=3,dashes=2,marks=2,pauses=1"`:
   fire exactly 10 single shots, reload once, jump 5 times, slide 3 times, dash 2 times, press F8 twice,
   pause once, then quit. Every line must say PASS. A FAIL means the recorder or the drill is off.
   Resolve it before trusting reports.
4. **Usefulness (human, pending):** after one normal session the report must point at at least one
   concrete, actionable thing (an ignored input, a bad accuracy situation, a frame spike at a mark). If
   two sessions in a row produce nothing actionable, reconsider what is recorded.

## Verification

| Check | Command | Result |
|---|---|---|
| Analyzer self-test (incl. video time alignment) | `python -I Tools/Sessions/analyze_session.py --selftest` | passed 2026-10-31, Python 3.12.10 |
| Recorder fidelity | `./Tools/Unity/Invoke-Unity.ps1 -Action Test -TestPlatform EditMode -TestFilter Wildstone.DevTools.Tests` | 2/2 passed 2026-10-31, Unity 6000.5.10f1 |
| Video pipeline | 3 s `ddagrab` + `h264_nvenc` capture and frame extract, ffmpeg 9.0.1, RTX 4070 Ti | worked 2026-10-31 (2560×1440, 30 fps) |
| Player build | `./Tools/Unity/Invoke-Unity.ps1 -Action BuildWindows` | OK 2026-10-31; `git status` afterwards showed only the add-on's new files |
| Player smoke | built exe with `--wildstone-record`, title screen ~5 s, normal window close | meta, settings, screen, perf and `end` rows written; recorder start logged; no exceptions |

## Limits

Video: main monitor only (`output_idx=0`); frame/data alignment is about ±0.5 s (ffmpeg start-up is
not measured); about 12 MB per minute at 2560×1440; NVENC encoding still costs a little GPU time, so
frame-time numbers from a video session are slightly pessimistic. Practice only verified. The single practice target never attacks, so "killed by" is mostly self or
world damage. Shots are tied to hits by action sequence, so the hit% for multi-pellet weapons counts a
shot as a hit if any pellet landed. Zone time uses horizontal distance against `MovingZone.Radius`
and ignores the floor-height check.
