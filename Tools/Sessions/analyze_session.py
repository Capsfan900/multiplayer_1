"""Turn a Wildstone session recording (session.jsonl) into a readable report.

Usage:
  python -I analyze_session.py <session dir or session.jsonl> [--expect shots=10,jumps=5,...]
  python -I analyze_session.py --selftest

--expect is the trust check: do a known drill, then confirm the recording counted it. Keys are the
"Counts" names in the report. Stdlib only. Development add-on; see docs/SESSION-RECORDER.md.
"""
import json, math, os, sys
from collections import Counter, defaultdict

LOCAL = 1


def load(path):
    if os.path.isdir(path):
        path = os.path.join(path, "session.jsonl")
    rows = []
    with open(path, encoding="utf-8") as f:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                # A crash can cut the last line short; everything before it is still good.
                print(f"warning: line {n} is not valid JSON (truncated?), skipped", file=sys.stderr)
    return rows


def clock(t):
    return f"{int(t // 60)}:{t % 60:04.1f}"


def pct(a, b):
    return f"{100 * a / b:.0f}%" if b else "-"


def dist(a, b):
    return math.dist(a, b) if a and b else None


def percentile(values, q):
    if not values:
        return 0
    s = sorted(values)
    return s[min(len(s) - 1, int((len(s) - 1) * q))]


def analyze(rows):
    by = defaultdict(list)
    for r in rows:
        by[r["type"]].append(r)
    for rows_of_type in by.values():
        rows_of_type.sort(key=lambda r: r.get("t") or 0)  # lookups below bisect by time
    combat, samples, moves, inputs = by["combat"], by["sample"], by["move"], by["input"]
    mine = [c for c in combat if c.get("actor") == LOCAL]

    # Movement counts come from the motor's own events, so they show what actually happened, not what was pressed.
    move_counts = Counter()
    for m in moves:
        if m.get("who") == LOCAL:
            for e in m["events"].split(", "):
                move_counts[e] += 1
    pressed = Counter(i["action"] for i in inputs)

    shots = [c for c in mine if c["kind"] == "Shot"]
    hits = [c for c in mine if c["kind"] == "Hit" and c.get("target") not in (None, 0, LOCAL)]
    hit_seqs = {c.get("seq") for c in hits}
    deaths = [c for c in combat if c["kind"] == "Death"]

    # Death events name the victim only; credit the last hit on that victim.
    last_hitter, kills, killed_by = {}, Counter(), Counter()
    for c in combat:
        if c["kind"] == "Hit":
            last_hitter[c.get("target")] = (c.get("actor"), c.get("weapon"))
        elif c["kind"] == "Death":
            actor, weapon = last_hitter.pop(c.get("target"), (None, None))
            if c.get("target") == LOCAL:
                killed_by["self/world" if actor in (None, LOCAL) else f"player {actor}"] += 1
            elif actor == LOCAL:
                kills[weapon or "?"] += 1

    # Nearest earlier state sample for each shot gives range, speed and ADS at the moment of firing.
    sample_ticks = [s["tick"] for s in samples]

    def sample_at(tick):
        import bisect
        i = bisect.bisect_right(sample_ticks, tick) - 1
        return samples[i] if i >= 0 else None

    weapons = defaultdict(lambda: Counter())
    by_range, by_motion = defaultdict(Counter), defaultdict(Counter)
    for s in shots:
        w, hit = s.get("weapon") or "?", s.get("seq") in hit_seqs
        weapons[w]["shots"] += 1
        weapons[w]["hit shots"] += hit
        st = sample_at(s["tick"])
        if st:
            d = dist(st.get("pos"), st.get("targetPos"))
            if d is not None:
                bucket = "<10 m" if d < 10 else "10-25 m" if d < 25 else "25 m+"
                by_range[bucket]["shots"] += 1
                by_range[bucket]["hits"] += hit
            motion = "sliding" if st.get("sliding") else "airborne" if not st.get("grounded") else "moving" if st.get("speed", 0) > 1 else "still"
            by_motion[motion]["shots"] += 1
            by_motion[motion]["hits"] += hit
            aim = "ADS" if st.get("ads", 0) > 0.5 else "hip"
            by_motion[aim]["shots"] += 1
            by_motion[aim]["hits"] += hit
    for h in hits:
        w = h.get("weapon") or "?"
        weapons[w]["damage"] += h.get("amount", 0)
        weapons[w]["headshots"] += bool(h.get("head"))
    for w, n in kills.items():
        weapons[w]["kills"] += n

    refused = Counter((c.get("action"), c.get("reason")) for c in mine if c["kind"] == "Rejected")

    playing = [s for s in samples if s.get("alive")]
    n = len(playing) or 1
    states = Counter("wallrun" if s.get("wallrun") else "sliding" if s.get("sliding") else "dashing" if s.get("dashing")
                     else "ground" if s.get("grounded") else "air" for s in playing)
    speeds = [s.get("speed", 0) for s in playing]
    travelled = sum(dist(a["pos"], b["pos"]) or 0 for a, b in zip(playing, playing[1:]) if b["tick"] - a["tick"] <= 12)
    from_zone = [math.dist(s["pos"][::2], s["zone"][::2]) for s in playing if s.get("zone")]
    in_zone = sum(1 for d in from_zone if d <= 4)  # MovingZone.Radius; height tolerance ignored

    perf = [p for p in by["perf"] if p.get("playing")]
    frame_avg = [p["avgMs"] for p in perf if p.get("frames")]
    worst = sorted(perf, key=lambda p: -p.get("maxMs", 0))[:5]

    counts = {
        "shots": len(shots),
        "hits": len(hit_seqs & {s.get("seq") for s in shots}),
        "reloads": sum(1 for c in mine if c["kind"] == "Accepted" and c.get("action") == "Reload"),
        "grenades": sum(1 for c in mine if c["kind"] == "Accepted" and c.get("action") == "Grenade"),
        "blinks": sum(1 for c in mine if c["kind"] == "Blink"),
        "shields": sum(1 for c in mine if c["kind"] == "ShieldActivated"),
        "pickups": sum(1 for c in mine if c["kind"] == "Pickup"),
        "jumps": move_counts["Jump"], "dashes": move_counts["Dash"], "slides": move_counts["Slide"],
        "landings": move_counts["Land"], "wallruns": move_counts["WallStart"], "walljumps": move_counts["WallJump"],
        "kills": sum(kills.values()), "deaths": sum(1 for d in deaths if d.get("target") == LOCAL),
        "marks": len(by["mark"]), "pauses": sum(1 for a, b in zip(by["screen"], by["screen"][1:]) if a.get("screen") == "Playing" and b.get("screen") == "Pause"),
    }
    # Pressed vs happened: a gap is input the game ate (cooldown, stamina, buffer, state) - often the feel problem.
    eaten = {
        "Jump": (pressed["Jump"], move_counts["Jump"] + move_counts["WallJump"]),
        "Dash": (pressed["Dash"], move_counts["Dash"]),
        "Slide": (pressed["Slide"], move_counts["Slide"]),
        "Reload": (pressed["Reload"], counts["reloads"]),
        "Blink": (pressed["Blink"], counts["blinks"]),
        "Shield": (pressed["Shield"], counts["shields"]),
    }
    return dict(by=by, counts=counts, weapons=weapons, refused=refused, states=states, n=n, speeds=speeds,
                travelled=travelled, in_zone=in_zone, zone_samples=len(from_zone), perf=perf, frame_avg=frame_avg,
                worst=worst, by_range=by_range, by_motion=by_motion, killed_by=killed_by, eaten=eaten, pressed=pressed,
                samples=samples, combat=combat, moves=moves)


def report(a):
    by, c = a["by"], a["counts"]
    meta = (by["meta"] or [{}])[0]
    settings = (by["settings"] or [{}])[-1]
    end_t = max((r.get("t") or 0) for rows in by.values() for r in rows) if by else 0
    sessions = [s for s in by["session"] if s.get("event") == "start"]
    out = ["# Wildstone session report", ""]
    out.append(f"- Recorded **{end_t / 60:.1f} min**, {len(sessions)} match(es): {', '.join(s.get('map', '?') for s in sessions) or 'none'}")
    out.append(f"- Build `{meta.get('build', '?')}`, {meta.get('gpu', '?')}, {meta.get('screen', '?')} @ {meta.get('refreshHz', '?')} Hz, quality {meta.get('quality', '?')}")
    out.append(f"- Sensitivity {settings.get('sensitivity', '?')}, FOV {settings.get('fov', '?')}, ADS toggle {settings.get('adsToggle', '?')}")
    if not by["end"]:
        out.append("- **No end record**: the game crashed or was killed; data up to the last second is kept.")
    out += ["", "## Counts", "", "Use these for the trust check: do a known drill and compare.", ""]
    out.append(" | ".join(f"{k} **{v}**" for k, v in c.items()))

    out += ["", "## Weapons", "", "| weapon | shots | hit% | damage | headshots | kills |", "|---|---|---|---|---|---|"]
    for w, s in sorted(a["weapons"].items(), key=lambda kv: -kv[1]["shots"]):
        out.append(f"| {w} | {s['shots']} | {pct(s['hit shots'], s['shots'])} | {s['damage']:.0f} | {s['headshots']} | {s['kills']} |")
    out += ["", "Accuracy by situation: " + ", ".join(f"{k} {pct(v['hits'], v['shots'])} of {v['shots']}"
                                                   for k, v in {**a["by_range"], **a["by_motion"]}.items())]
    if a["killed_by"]:
        out.append("Deaths: " + ", ".join(f"{k} x{v}" for k, v in a["killed_by"].items()))

    out += ["", "## Pressed vs happened", "", "A gap means the game ignored the press (cooldown, stamina, state). Big gaps are feel problems.", ""]
    why = why_ignored(by)
    for k, (p, h) in a["eaten"].items():
        if p or h:
            reasons = ", ".join(f"{r} {n}" for r, n in why.get(k, Counter()).most_common())
            out.append(f"- {k}: pressed {p}, happened {h}" + (f" -> **{p - h} ignored**" if p > h else "") + (f" ({reasons})" if reasons else ""))
    if a["refused"]:
        out += ["", "Refused by the rules (action: reason x count):"]
        for (act, reason), k in a["refused"].most_common(10):
            out.append(f"- {act}: {reason} x{k}")

    st, n, sp = a["states"], a["n"], a["speeds"]
    out += ["", "## Movement", ""]
    out.append("Time: " + ", ".join(f"{k} {pct(v, n)}" for k, v in st.most_common()))
    out.append(f"Speed: avg {sum(sp) / len(sp) if sp else 0:.1f} m/s, p95 {percentile(sp, .95):.1f}, max {max(sp, default=0):.1f}. Travelled {a['travelled']:.0f} m.")
    out.append(f"In zone: {pct(a['in_zone'], a['zone_samples'])} of alive time.")

    out += ["", "## Performance (while playing)", ""]
    fa = a["frame_avg"]
    if fa:
        avg = sum(fa) / len(fa)
        out.append(f"Avg {1000 / avg:.0f} fps ({avg:.1f} ms). Worst-second p95 {max(p['p95Ms'] for p in a['perf']):.1f} ms. "
                   f"Frames >25 ms: {sum(p['over25ms'] for p in a['perf']):.0f}, >50 ms: {sum(p['over50ms'] for p in a['perf']):.0f}. GC: {sum(p['gc'] for p in a['perf']):.0f}.")
        out.append("Worst moments: " + ", ".join(f"{clock(p['t'])} max {p['maxMs']:.0f} ms" for p in a["worst"]))
    else:
        out.append("No gameplay frames recorded.")

    if by["mark"]:
        out += ["", "## Your F8 marks (5 s before each)", ""]
        for m in by["mark"]:
            t0, t1 = m["t"] - 5, m["t"]
            near = [x for x in a["combat"] if t0 <= x["t"] <= t1 and x.get("actor") == LOCAL]
            mv = Counter(e for x in a["moves"] if t0 <= x["t"] <= t1 and x.get("who") == LOCAL for e in x["events"].split(", "))
            s = [x for x in a["samples"] if t0 <= x["t"] <= t1]
            pf = [x for x in a["perf"] if t0 <= x["t"] <= t1 + 1]
            kinds = Counter(f"{x['kind']} ({x.get('action')}: {x.get('reason')})" if x["kind"] == "Rejected" else x["kind"] for x in near)
            out.append(f"- **Mark {m['n']}** at {clock(t1)} (mark-{m['n']}.png): "
                       f"{dict(kinds) or 'no combat'}; moves {dict(mv) or 'none'}; "
                       + (f"weapon {s[-1].get('weapon')}, hp {s[-1].get('hp') or 0:.0f}, speed {max(x.get('speed', 0) for x in s):.1f} max; " if s else "")
                       + (f"worst frame {max(x['maxMs'] for x in pf):.0f} ms" if pf else ""))
    return "\n".join(out) + "\n"


def check(counts, expect):
    ok = True
    for pair in expect.split(","):
        k, v = pair.split("=")
        got = counts.get(k.strip())
        good = got == int(v)
        ok &= good
        print(f"{'PASS' if good else 'FAIL'} {k}: expected {v}, recorded {got}")
    return ok


def why_ignored(by):
    """Match each Jump/Dash/Slide press to the motor event it caused; label the unmatched ones from the state at that
    moment (readiness fields mirror ArenaMotor's checks). Returns {action: Counter(reason)}."""
    import bisect
    meta = (by["meta"] or [{}])[0]
    samples = by["sample"]
    times = [x["t"] for x in samples]
    events = {"Jump": ("Jump", "WallJump"), "Dash": ("Dash",), "Slide": ("Slide",)}
    out = {}
    for action, kinds in events.items():
        happened = [m["t"] for m in by["move"] if m.get("who") == LOCAL and any(k in m["events"].split(", ") for k in kinds)]
        reasons, j = Counter(), 0
        for press in (i["t"] for i in by["input"] if i["action"] == action):
            while j < len(happened) and happened[j] < press - 0.02:
                j += 1
            if j < len(happened) and happened[j] <= press + 0.3:  # input is consumed on the next 120 Hz tick(s)
                j += 1
                continue
            k = bisect.bisect_right(times, press) - 1
            st = samples[k] if k >= 0 else {}
            if "dashReady" not in st:
                reasons["unknown (older recording)"] += 1
            elif not st.get("alive", True) or st.get("stunned"):
                reasons["dead or stunned"] += 1
            elif action == "Dash":
                reasons["cooldown" if not st["dashReady"] else "air dash already used" if not st.get("grounded") and st.get("airDashUsed")
                        else "low stamina" if st.get("stamina", 0) < meta.get("dashCost", 0) else "other"] += 1
            elif action == "Slide":
                reasons["already sliding" if st.get("sliding") else "cooldown" if not st["slideReady"] else "in the air" if not st.get("grounded")
                        else "low stamina" if st.get("stamina", 0) < meta.get("slideCost", 0) else "other"] += 1
            else:
                reasons["in the air" if not st.get("grounded") else "other"] += 1
        out[action] = reasons
    return out


def moments(a):
    """Times worth looking at: just before each F8 mark (the problem happens before the key press) and the worst spikes."""
    out = []
    for m in a["by"]["mark"]:
        out += [(f"mark{m['n']}-{lead}s", m["t"] - lead) for lead in (3, 1.5, 0)]
    for p in a["worst"][:3]:
        if p.get("maxMs", 0) > 25:
            out.append((f"spike-{p['t']:.0f}s-{p['maxMs']:.0f}ms", p["t"] - 0.5))
    return out


def video_offset(by, folder):
    """Seconds into session.mkv where recording t=0 is, from the two wall-clock starts; None without video."""
    from datetime import datetime
    video, info = os.path.join(folder, "session.mkv"), os.path.join(folder, "video.json")
    meta = (by["meta"] or [{}])[0]
    if not (os.path.exists(video) and os.path.exists(info) and meta.get("startedUtc")):
        return None
    with open(info, encoding="utf-8-sig") as f:
        started = json.load(f)["startedUtc"]
    def parse(s):  # .NET "o" has 7 fraction digits; Python wants at most 6
        import re
        s = re.sub(r"(\.\d{6})\d+", r"\1", s.replace("Z", "+00:00"))
        return datetime.fromisoformat(s)
    return (parse(meta["startedUtc"]) - parse(started)).total_seconds()


def extract_frames(a, folder):
    import shutil, subprocess
    offset, ffmpeg = video_offset(a["by"], folder), shutil.which("ffmpeg")
    if offset is None or not ffmpeg:
        return []
    os.makedirs(os.path.join(folder, "frames"), exist_ok=True)
    made = []
    for label, t in moments(a):
        name = os.path.join("frames", label + ".jpg")
        subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-ss", f"{max(0, offset + t):.2f}",
                        "-i", os.path.join(folder, "session.mkv"), "-frames:v", "1", "-vf", "scale=1280:-1", "-q:v", "4",
                        os.path.join(folder, name)], check=False)
        if os.path.exists(os.path.join(folder, name)):
            made.append(name)
    return made


def selftest():
    rows = [{"type": "meta", "t": 0, "build": "test"}, {"type": "screen", "t": 0, "screen": "Playing"},
            {"type": "screen", "t": 0, "screen": "Pause"}, {"type": "screen", "t": 0, "screen": "Settings"},
            {"type": "screen", "t": 0, "screen": "Pause"}, {"type": "screen", "t": 0, "screen": "Playing"},
            {"type": "session", "t": 0, "event": "start", "map": "atrium"}]
    for i in range(40):  # 2 s of samples at 20 Hz, local at origin, target 12 m away
        rows.append({"type": "sample", "t": i / 20, "tick": i * 6, "pos": [0, 0, 0], "targetPos": [12, 0, 0], "zone": [0, 0, 0],
                     "speed": 0, "grounded": True, "alive": True, "ads": 0, "hp": 100, "weapon": "LineDriver",
                     "dashReady": True, "airDashUsed": False, "slideReady": True, "stamina": 100})
    for seq in range(1, 5):  # 4 shots, 3 hit, the last hit kills
        rows.append({"type": "combat", "t": seq / 4, "tick": seq * 30, "kind": "Shot", "actor": 1, "seq": seq, "weapon": "LineDriver"})
        if seq < 4:
            rows.append({"type": "combat", "t": seq / 4, "tick": seq * 30, "kind": "Hit", "actor": 1, "seq": seq, "target": 2,
                         "weapon": "LineDriver", "amount": 40, "head": seq == 3})
    rows.append({"type": "combat", "t": 1, "tick": 90, "kind": "Death", "actor": 2, "target": 2})
    rows.append({"type": "combat", "t": 1.2, "tick": 140, "kind": "Rejected", "actor": 1, "action": "Fire", "reason": "Cooldown"})
    rows += [{"type": "input", "t": t, "action": "Jump"} for t in (1, 1, 1.5)]  # jumps happen at 1 and 1.5: one press ignored
    rows += [{"type": "input", "t": 1, "action": "Dash"}, {"type": "input", "t": 1.58, "action": "Dash"}]
    next(r for r in rows if r["type"] == "sample" and r["tick"] == 186)["dashReady"] = False  # t=1.55: dash on cooldown
    rows += [{"type": "move", "t": 1, "tick": 120, "who": 1, "events": "Jump, Dash"}, {"type": "move", "t": 1.5, "tick": 180, "who": 1, "events": "Jump"}]
    rows += [{"type": "perf", "t": 1, "frames": 60, "avgMs": 10, "p95Ms": 12, "maxMs": 30, "over25ms": 1, "over50ms": 0, "gc": 0, "playing": True},
             {"type": "mark", "t": 1.8, "n": 1, "tick": 216}, {"type": "end", "t": 2, "reason": "quit"}]
    a = analyze(rows)
    c = a["counts"]
    assert (c["shots"], c["hits"], c["kills"], c["jumps"], c["dashes"], c["marks"], c["pauses"]) == (4, 3, 1, 2, 1, 1, 1), c
    w = a["weapons"]["LineDriver"]
    assert (w["damage"], w["headshots"], w["kills"]) == (120, 1, 1), w
    assert a["by_range"]["10-25 m"] == Counter(shots=4, hits=3), a["by_range"]
    assert a["eaten"]["Jump"] == (3, 2) and a["refused"][("Fire", "Cooldown")] == 1
    assert a["in_zone"] == 40
    text = report(a)
    assert "Mark 1" in text and "LineDriver | 4 | 75%" in text and "1 ignored" in text, text
    assert check(c, "shots=4,jumps=2") and not check(c, "shots=5")
    assert [label for label, _ in moments(a)] == ["mark1-3s", "mark1-1.5s", "mark1-0s", "spike-1s-30ms"], moments(a)
    import tempfile
    with tempfile.TemporaryDirectory() as folder:  # Unity writes 7 fraction digits; PowerShell writes 7 plus an offset
        open(os.path.join(folder, "session.mkv"), "w").close()
        with open(os.path.join(folder, "video.json"), "w", encoding="utf-8-sig") as f:
            json.dump({"startedUtc": "2026-10-31T17:00:00.0000000Z"}, f)
        offset = video_offset({"meta": [{"startedUtc": "2026-10-31T17:00:02.5000000Z"}]}, folder)
        assert abs(offset - 2.5) < 1e-6, offset
        with open(os.path.join(folder, "video.json"), "w", encoding="utf-8-sig") as f:
            json.dump({"startedUtc": "2026-10-31T17:00:00.0000000+00:00"}, f)
        assert abs(video_offset({"meta": [{"startedUtc": "2026-10-31T17:00:02.5000000Z"}]}, folder) - 2.5) < 1e-6
    why = why_ignored(a["by"])
    assert why["Dash"] == Counter({"cooldown": 1}) and why["Jump"] == Counter({"other": 1}), why
    assert clock(51.06) == "0:51.1" and clock(61.7) == "1:01.7", clock(51.06)
    print("selftest passed")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args == ["--selftest"]:
        selftest()
        sys.exit(0)
    if not args:
        sys.exit(__doc__)
    path = args[0]
    result = analyze(load(path))
    text = report(result)
    out_dir = path if os.path.isdir(path) else os.path.dirname(path)
    frames = extract_frames(result, out_dir)
    if frames:
        text += "\n## Video frames\n\nFrom session.mkv, just before each mark and at the worst spikes:\n\n" + "\n".join(f"- {f}" for f in frames) + "\n"
    with open(os.path.join(out_dir, "report.md"), "w", encoding="utf-8") as f:
        f.write(text)
    print(text)
    if "--expect" in args:
        sys.exit(0 if check(result["counts"], args[args.index("--expect") + 1]) else 1)
