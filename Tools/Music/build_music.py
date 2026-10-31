# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy==2.5.4", "scipy==1.18.1", "soundfile==0.14.0"]
# ///
"""Wildstone chill-house music, synthesized fallback (D030): four seamless loops, no samples, no AI model.
Superseded by loop_takes.py (D032, ACE-Step takes); running this overwrites the shipped OGGs with the synth set.

  uv run Tools/Music/build_music.py                  # render all four, write OGGs, verify
  uv run Tools/Music/build_music.py Level_atrium     # one track
  uv run Tools/Music/build_music.py --check          # only measure the committed OGGs
  uv run Tools/Music/build_music.py --wav DIR        # also write the mastered loops as WAV to DIR

Needs ffmpeg on PATH (ebur128 integrated loudness + true-peak meter). Fixed seeds: same input, same audio.
"""
import sys

sys.dont_write_bytecode = True  # keep Tools/Music free of __pycache__
import re
import subprocess
import time
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import sosfilt

import engine as E
from engine import SR, db, hz, ts

OUT = Path(__file__).resolve().parents[2] / "Assets/Wildstone/Resources/Audio/Music"
TARGET_I, TP_MAX = -15.0, -1.0  # LUFS integrated, dBTP (measured on the encoded OGG)


# ---------------------------------------------------------------- custom instruments (engine signature)

def rhodes(g, m, v, p):
    """FM electric piano: ratio-1 'bark' pair + a fast ratio-14 tine click, pitch-scaled decay, damper
    release at the gate, tempo-synced stereo autopan whose phase follows song time (t0)."""
    f, vel = hz(m), v / 127
    n = int((g + .3) * SR)
    t = ts(n)
    ph = 2 * np.pi * f * t + E.rng.random() * 2 * np.pi
    bark = (.25 + 1.2 * vel) * np.exp(-t / .4) + .3
    y = .8 * np.sin(ph + bark * np.sin(ph)) + .2 * np.sin(ph + .6 * vel * np.exp(-t / .012) * np.sin(14 * ph))
    dec = p.get("decay", 2.2) * (220 / f) ** .5
    amp = np.minimum(1, t / .0015) * (.55 * np.exp(-t / .25) + .45 * np.exp(-t / dec)) * np.clip(1 - (t - g) / .25, 0, 1)
    y = np.tanh(1.4 * y * amp) / np.tanh(1.4)
    pan = p.get("pan_depth", .3) * np.sin(2 * np.pi * (t + p.get("t0", 0)) / (p.get("pan_beats", 2) * p["spb"]))
    return np.stack([y * (1 + pan), y * (1 - pan)], 1)


def mallet(g, m, v, p):
    """Soft FM mallet/kalimba: sine carrier, modulator at `ratio`, index decaying in `bright` s."""
    f, vel = hz(m), v / 127
    n = int(min(g + .6, 2.5) * SR)
    t = ts(n)
    ph = 2 * np.pi * f * t + E.rng.random() * 2 * np.pi
    y = np.sin(ph + (.4 + 1.1 * vel) * np.exp(-t / p.get("bright", .05)) * np.sin(p.get("ratio", 4) * ph))
    return y * np.minimum(1, t / .001) * np.exp(-t / p.get("decay", .45)) * np.clip(1 - (t - g - .4) / .2, 0, 1)


def warm_bass(g, m, v, p):
    """Round house bass: sine + 2nd harmonic sub, low-passed saw for small-speaker presence, soft attack."""
    f = hz(m)
    n = int((g + .08) * SR)
    t = ts(n)
    ph = 2 * np.pi * f * t
    y = np.sin(ph) + .12 * np.sin(2 * ph) + p.get("mid", .6) * E.lp(E.saw(f, n), p.get("cut", 320), 2)
    amp = np.minimum(1, t / .006) * (.75 + .25 * np.exp(-t / .12)) * np.clip(1 - (t - g) / .08, 0, 1)
    return np.tanh(1.3 * y) * amp


def shaker(g, m, v, p):
    t = ts(int(.15 * SR))
    y = E.hp(E.lp(E.noise(len(t), 2, corr=.6), 10000, 2), 5000, 2)
    return y * ((1 - np.exp(-t / .003)) * np.exp(-t / p.get("decay", .035)))[:, None]


def tail_fade(fn, ms=10):
    """Fade the last `ms` of every note: engine kick/pluck/clap buffers stop mid-decay (a small step = a tick)."""
    def inst(g, m, v, p):
        y = np.array(fn(g, m, v, p), float)
        k = min(len(y), int(ms / 1000 * SR))
        y[-k:] = (y[-k:].T * np.linspace(1, 0, k)).T
        return y
    return inst


E.INSTRUMENTS.update(rhodes=rhodes, mallet=mallet, warm_bass=warm_bass, shaker=tail_fade(shaker))
E.INSTRUMENTS.update({k: tail_fade(E.INSTRUMENTS[k]) for k in ("kick", "pluck", "clap")})
E.LOW_END.add("warm_bass")


# ---------------------------------------------------------------- writing helpers

class Writer:
    """Places notes with 16th shuffle (odd 16ths late by `swing` beats), timing jitter and velocity spread
    from its own seeded RNG, so a render is identical every run."""

    def __init__(self, s, seed, swing=.045):
        self.s, self.rng, self.swing = s, np.random.default_rng(seed), swing

    def at(self, beat, jitter_ms):
        step = beat * 4
        if abs(step - round(step)) < 1e-6 and round(step) % 2:
            beat += self.swing
        return max(0.0, beat + self.rng.normal(0, jitter_ms / 1000) / self.s.spb)

    def vel(self, v):
        return int(np.clip(v * (1 + self.rng.normal(0, .05)), 1, 127))

    def note(self, track, beat, dur, midi, vel, jitter_ms=3, **kw):
        b = self.at(beat, jitter_ms)
        self.s.note(track, b, dur, midi, self.vel(vel), t0=b * self.s.spb, **kw)

    def chord(self, track, beat, dur, notes, vel, strum_ms=9, **kw):
        b = self.at(beat, 4)
        for i, m in enumerate(notes):
            bi = b + i * strum_ms / 1000 / self.s.spb
            self.s.note(track, bi, dur, m, self.vel(vel * (1 - .05 * i)), t0=bi * self.s.spb, **kw)

    def drums(self, track, bar, pattern, jitter_ms=2):
        for i, c in enumerate(pattern):
            if c in "Xxo-":
                self.note(track, bar * 4 + i / 4, .25, 60, {"X": 118, "x": 100, "o": 72, "-": 48}[c], jitter_ms)

    def phrase(self, track, bar, notes, vel=92):
        for beat, dur, m in notes:
            self.note(track, bar * 4 + beat, dur, m, vel)


def song(bpm, bars, seed, key, title):
    # warm, quiet master: almost no drive/OTT, lows tilted down, top rolled off; loudness is set after it
    return E.Song(bpm, bars, seed=seed, duck_release=.11, master_drive=1.15, master_lp=14000, low_shelf=-3.0,
                  high_shelf=0.0, side_boost=3.0, master_ott=.05, glue_db=1.0, target_lufs=TARGET_I,
                  key=key, title=title)


def base_tracks(s, kick=-9, clap=-8, hat=-10.5, ohat=-9.5, shaker_=-12.5, bass=-7, rhodes_=-4, pad=-12.5, mallet_=-11,
                ratio=4):
    s.track("Kick", "kick", gain=kick, sidechain_source=True, base=50, sweep=100, pitch_t=.02, decay=.15,
            click=.08, drive=1.5, length=.42)
    s.track("Clap", "clap", gain=clap, plate=.18, duck=.1, decay=.11, eq=[(3000, -6, .7), (9000, -4, .7)])
    s.track("Hat", "hat", gain=hat, width=1.3, eq=[(10000, -3, .7)])
    s.track("OpenHat", "hat", gain=ohat, duck=.3, open=True, decay=.055, eq=[(10000, -3, .7)])
    s.track("Shaker", "shaker", gain=shaker_, pan=.3, group="drums")
    s.track("Bass", "warm_bass", gain=bass, duck=.15, duck_lo=.9, group="bass")
    s.track("Rhodes", "rhodes", gain=rhodes_, duck=.2, plate=.15, hall=.1, group="keys", eq=[(250, -2.5, .8)])
    s.track("Pad", "pad", gain=pad, duck=.45, hall=.3, attack=.6, release=1.4, detune=9, eq=[(300, -3, .7)])
    s.track("Mallet", "mallet", gain=mallet_, delay=.35, plate=.2, widen=.3, ratio=ratio, group="lead",
            eq=[(3000, -3, .8)])


HAT = "o-x-o-x-o-x-o-x-"     # odd 16ths are ghosts and land late: the shuffle
OHAT = "..o...o...o...o."
KICK = "x...x...x...x..."
CLAP = "....x.......x..."


# ---------------------------------------------------------------- the four loops

def menu():
    """118 BPM, D major, 2 bars per chord: Dmaj9 | Bm9 | Gmaj9 | Em9 -> A13sus (same voicing). Calm: pad +
    Rhodes intro/outro, soft kick, sparse mallet melody with dotted-8th echo."""
    s = song(118, 56, 101, "D major", "Wildstone Menu Theme")
    base_tracks(s, kick=-11.5, clap=-9.5, hat=-12, ohat=-11, shaker_=-14, bass=-8, rhodes_=-2.5, pad=-12, mallet_=-9.5)
    w = Writer(s, 101)
    prog = [(38, (54, 57, 61, 64)), (35, (50, 54, 57, 61)), (43, (54, 57, 59, 62)), (40, (55, 59, 62, 66))]
    mel_a = [(0, .75, 78), (1, .5, 81), (1.5, 1.5, 76), (4.5, .5, 73), (5, 1, 74), (6, 1.5, 69),
             (8, .75, 78), (9, .5, 81), (9.5, 1.5, 83), (12.5, .5, 81), (13, 1, 78), (14, 1.5, 76),
             (16, .75, 74), (17, .5, 78), (17.5, 1.5, 81), (20.5, .5, 78), (21, 1, 79), (22, 1.5, 74),
             (24, .75, 76), (25, .5, 79), (25.5, 1.5, 78), (28.5, .5, 74), (29, 2.5, 76)]
    mel_b = [(0, 1.5, 81), (2, .5, 78), (2.5, 1, 76), (6, .5, 74), (6.5, 1, 76),
             (8, 1.5, 83), (10, .5, 81), (10.5, 1, 78), (14, .5, 76), (14.5, 1, 78),
             (16, 1.5, 86), (18, .5, 83), (18.5, 1, 81), (22, .5, 79), (22.5, 1, 78),
             (24, 1.5, 79), (26, .5, 78), (26.5, 1, 76), (29.5, 2, 71)]
    for bar in range(56):
        b0, ci = bar * 4, (bar // 2) % 4
        root, v = prog[ci]
        if ci == 3 and bar % 2:
            root = 33
        groove, full, brk = 8 <= bar < 32 or 40 <= bar < 50, 16 <= bar < 32 or 40 <= bar < 48, 32 <= bar < 40
        if bar % 2 == 0:
            for st, ln, vs in ((0, 12, 1), (14, 3, .7), (18, 13, .85)):  # steps across the 2-bar chord
                w.chord("Rhodes", b0 + st / 4, ln / 4, v, 92 * vs)
            w.chord("Pad", b0, 7.5, [m + 12 for m in v], 80, strum_ms=0, cut=2600 if brk else 2000)
        if groove:
            w.drums("Kick", bar, KICK, 0)
        if full:
            w.drums("Clap", bar, CLAP)
            w.drums("OpenHat", bar, OHAT)
        if 8 <= bar < 48:
            w.drums("Hat", bar, HAT)
        if 4 <= bar < 52:
            w.drums("Shaker", bar, HAT)
        if 8 <= bar < 32 or 40 <= bar < 52:
            for st, ln, iv, vv in ((0, 5, 0, 100), (6, 2, 0, 72), (10, 4, 0, 88), (14, 2, 12, 64)):
                w.note("Bass", b0 + st / 4, ln / 4, root + iv, vv)
        elif brk:
            w.note("Bass", b0, 3.5, root, 80)
        if bar in (16, 24, 32, 40):
            w.phrase("Mallet", bar, mel_a if bar in (16, 32) else mel_b, 66 if brk else 92)
    return s


def atrium():
    """122 BPM, A dorian, 1 bar per chord: Am9 | D9 | Fmaj9 | Em7. Syncopated Rhodes stabs, swung hats + shaker,
    marimba-ish hook in the B and C sections, short kick-less break."""
    s = song(122, 72, 202, "A minor (dorian)", "Wildstone Atrium")
    base_tracks(s)
    w = Writer(s, 202)
    prog = [(33, (52, 55, 59, 60)), (38, (54, 57, 60, 64)), (41, (52, 55, 57, 60)), (40, (52, 55, 59, 62))]
    rh_a = ((0, 5, 1), (6, 2, .7), (10, 3, .85), (13, 3, .7))
    rh_c = ((0, 3, 1), (3, 2, .75), (6, 3, .85), (10, 2, .8), (13, 3, .75))
    bass = ((0, 3, 0, 105), (3, 2, 0, 80), (7, 2, 12, 85), (10, 2, 0, 95), (14, 2, 7, 75))
    hook = [(.5, .5, 76), (1, .5, 79), (1.5, 1, 81), (3, .5, 79), (4.5, .5, 78), (5, .5, 76), (5.5, 1.5, 74),
            (8.5, .5, 76), (9, .5, 79), (9.5, 1, 81), (11, .5, 84), (12.5, .5, 83), (13, .5, 81), (13.5, 1.5, 79)]
    hook2 = [(.5, .5, 84), (1, 1, 83), (2.5, .5, 81), (3, .5, 79), (4.5, .5, 81), (5, 1.5, 78),
             (8.5, .5, 79), (9, .5, 81), (9.5, 1, 84), (11, .5, 81), (12.5, .5, 79), (13, 2, 76)]
    for bar in range(72):
        b0 = bar * 4
        root, v = prog[bar % 4]
        intro, brk, sec_c = bar < 8 or bar >= 64, 40 <= bar < 48, 48 <= bar < 64
        cut = 1300 if intro else 2400 if brk else 2000 if sec_c else 1700
        w.chord("Pad", b0, 3.9, [m + 12 for m in v], 75, strum_ms=0, cut=cut)
        w.drums("Hat", bar, HAT)
        if 8 <= bar < 64:
            w.drums("Shaker", bar, HAT)
        if brk:
            w.chord("Rhodes", b0, 3.8, v, 80)
            w.note("Bass", b0, 3.5, root, 75)
        else:
            w.drums("Kick", bar, KICK, 0)
            w.drums("Clap", bar, CLAP)
            if not intro:
                w.drums("OpenHat", bar, OHAT)
            for st, ln, vs in rh_c if sec_c else rh_a:
                w.chord("Rhodes", b0 + st / 4, ln / 4, v, 96 * vs)
            for st, ln, iv, vv in bass:
                w.note("Bass", b0 + st / 4, ln / 4 * .9, root + iv, vv)
        if 24 <= bar < 40 or 48 <= bar < 64 or bar == 44:
            if bar % 4 == 0:
                w.phrase("Mallet", bar, hook2 if bar % 8 == 4 and bar != 44 else hook, 64 if brk else 92)
    return s


def split_gallery():
    """124 BPM, C minor, 1 bar per chord: Cm9 | Abmaj9 | Fm9 | G7sus4. Moodier and tighter: offbeat pumping
    bass, filtered 16th pluck arps that open across the loop, darker pad, minor-pentatonic hook."""
    s = song(124, 80, 303, "C minor", "Wildstone Split Gallery")
    base_tracks(s, bass=-8, rhodes_=-6, pad=-13, mallet_=-12, ratio=2)
    s.track("Arp", "pluck", gain=-10, delay=.3, plate=.15, duck=.35, group="lead", cut=420, env_oct=1.6, fdecay=.07,
            decay=.14, drive=1.2, eq=[(3000, -3, .8)],
            lp=[(0, 700), (8, 800), (24, 3500), (40, 2200), (48, 3500), (72, 3500), (80, 700)])
    w = Writer(s, 303)
    prog = [(36, (51, 55, 58, 62)), (32, (55, 58, 60, 63)), (41, (51, 55, 56, 60)), (43, (50, 53, 55, 60))]
    arp_steps = (0, 2, 3, 6, 8, 10, 11, 14)
    arp_idx = (0, 2, 1, 3, 2, 0, 3, 1)
    arp_vel = (95, 70, 80, 90, 75, 85, 70, 80)
    hook = [(0, 1, 72), (1.5, .5, 75), (2, 1, 74), (3.5, .5, 70), (4, 1.5, 72), (6, .5, 67), (6.5, 1, 70),
            (8, 1, 68), (9.5, .5, 72), (10, 1, 75), (11.5, .5, 77), (12, 2, 74), (14.5, .5, 72)]
    hook2 = [(0, 1, 79), (1.5, .5, 77), (2, 1, 75), (3.5, .5, 74), (4, 1.5, 75), (6, .5, 72), (6.5, 1, 79),
             (8, 1, 80), (9.5, .5, 79), (10, 1, 77), (11.5, .5, 75), (12, 2, 74), (14.5, .5, 77)]
    for bar in range(80):
        b0 = bar * 4
        root, v = prog[bar % 4]
        intro, brk, sec_c = bar < 8 or bar >= 72, 40 <= bar < 48, 48 <= bar < 72
        w.chord("Pad", b0, 3.9, [m + 12 for m in v], 72, strum_ms=0, cut=2000 if brk else 1400)
        for st, i, vv in zip(arp_steps, arp_idx, arp_vel):
            w.note("Arp", b0 + st / 4, .5, v[i] + 12, vv)
        w.drums("Hat", bar, HAT)
        if 24 <= bar < 72:
            w.drums("Shaker", bar, HAT)
        if brk:
            w.chord("Rhodes", b0, 3.8, v, 78)
        else:
            w.drums("Kick", bar, KICK, 0)
            for st, ln, vs in ((0, 3, .95), (3, 3, .7), (10, 4, .8)):
                w.chord("Rhodes", b0 + st / 4, ln / 4, v, 94 * vs)
            for st, ln, iv, vv in ((2, 2, 0, 105), (6, 2, 0, 92), (10, 2, 0, 100), (14, 2, 12, 85)):
                w.note("Bass", b0 + st / 4, ln / 4 * .9, root + iv, vv)
            if bar % 4 == 3:
                w.note("Bass", b0 + 3.75, .2, root + 12, 70)
            if not intro:
                w.drums("Clap", bar, CLAP)
                w.drums("OpenHat", bar, OHAT)
        if (24 <= bar < 40 or sec_c or bar == 44) and bar % 4 == 0:
            w.phrase("Mallet", bar, hook2 if bar % 8 == 4 and bar != 44 else hook, 62 if brk else 90)
    return s


def terrace_steps():
    """120 BPM, E major, 1 bar per chord with the bass climbing: Emaj9 | G#m7 | Amaj9 | B13sus. Sunny:
    brighter pad, rising 8th-note mallet 'steps' arpeggios, a kalimba melody in the last section."""
    s = song(120, 72, 404, "E major", "Wildstone Terrace Steps")
    base_tracks(s, ratio=3)
    s.track("Steps", "mallet", gain=-10.5, delay=.3, plate=.2, widen=.35, ratio=4, decay=.35, group="lead",
            eq=[(3000, -3, .8)])
    w = Writer(s, 404)
    prog = [(40, (56, 59, 63, 66)), (44, (54, 56, 59, 63)), (45, (56, 59, 61, 64)), (47, (56, 57, 61, 64))]
    rh = ((0, 3, 1), (3, 3, .75), (6, 4, .85), (11, 2, .7), (14, 2, .75))
    bass = ((0, 2, 0, 105), (3, 2, 12, 80), (6, 2, 0, 90), (8, 2, 7, 95), (11, 2, 12, 80), (14, 2, 7, 85))
    hook = [(0, 1.5, 80), (1.5, .5, 78), (2, 2, 75), (4, 1.5, 78), (5.5, .5, 75), (6, 2, 71),
            (8, 1.5, 76), (9.5, .5, 78), (10, 2, 80), (12, 1.5, 81), (13.5, .5, 80), (14, 2, 78)]
    for bar in range(72):
        b0 = bar * 4
        root, v = prog[bar % 4]
        intro, brk, sec_b, sec_c = bar < 8 or bar >= 64, 40 <= bar < 48, 24 <= bar < 40, 48 <= bar < 64
        w.chord("Pad", b0, 3.9, [m + 12 for m in v], 75, strum_ms=0, cut=1500 if intro else 2600 if brk else 2200)
        w.drums("Hat", bar, HAT)
        if 8 <= bar < 64:
            w.drums("Shaker", bar, HAT)
        if brk:
            w.chord("Rhodes", b0, 3.8, v, 80)
            w.note("Bass", b0, 3.5, root, 72)
        else:
            w.drums("Kick", bar, KICK, 0)
            if not intro:
                w.drums("Clap", bar, CLAP)
                w.drums("OpenHat", bar, OHAT)
            for st, ln, vs in rh:
                w.chord("Rhodes", b0 + st / 4, ln / 4, v, 94 * vs)
            for st, ln, iv, vv in bass:
                w.note("Bass", b0 + st / 4, ln / 4 * .9, root + iv, vv)
        if sec_b or brk or 56 <= bar < 64:  # rising 'steps': chord tones climbing in 8ths, then a rest
            up = sorted(v) + [sorted(v)[0] + 12]
            for k, m in enumerate(up):
                w.note("Steps", b0 + k / 2, .5, m + 12, (60 if brk else 78) + 6 * k)
        if sec_c and bar % 4 == 0:
            w.phrase("Mallet", bar, hook, 92)
    return s


TRACKS = {"MenuTheme": (menu, 118, 56), "Level_atrium": (atrium, 122, 72),
          "Level_split-gallery": (split_gallery, 124, 80), "Level_terrace-steps": (terrace_steps, 120, 72)}


# ---------------------------------------------------------------- render, master, encode, verify

def circular(fn, x, pad):
    """Run a stateful processor as if the loop were already playing: pad with its own end/start, keep the middle."""
    return np.asarray(fn(np.concatenate([x[-pad:], x, x[:pad]]))[pad:pad + len(x)], float)


def render_loop(s):
    song_n = s.s(s.bars * 4)
    n = song_n + int(6 * SR)
    sc = s._sidechain(n)  # periodic sidechain: the bar-0 kicks also duck tails that wrap onto the start
    per = sc[:song_n].copy()
    per[:n - song_n] = np.clip(per[:n - song_n] + sc[song_n:], 0, 1)
    sc = np.concatenate([per, per[:n - song_n]])
    mix, groups = np.zeros((n, 2)), {}
    for grp in dict.fromkeys(tr["group"] for tr in s.tracks.values()):
        members = [tr for tr in s.tracks.values() if tr["group"] == grp and tr["notes"]]
        if members:
            groups[grp] = s._mix_group(members, n, sc)
            mix += groups[grp]
    mix = s._post(mix)
    mix = sosfilt(E.peak(3000, -1.5, .7), mix, axis=0)  # keep the bed out of the SFX presence band
    loop = mix[:song_n].copy()
    loop[:n - song_n] += mix[song_n:]  # linear stages: wrapping the tail = playing the loop forever
    print("  group LUFS (pre-master): " + "  ".join(f"{k} {E.lufs(g[:song_n]):.1f}" for k, g in groups.items()))
    return circular(s._master, loop, int(4 * SR))


def write_ogg(path, x):
    # ffmpeg libvorbis q6 with bitexact muxing: identical bytes every build (libsndfile randomises the Ogg serial)
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2",
                    "-i", "pipe:", "-c:a", "libvorbis", "-q:a", "6", "-fflags", "+bitexact", "-flags:a", "+bitexact",
                    "-map_metadata", "-1", str(path)], input=np.ascontiguousarray(x, np.float32).tobytes(), check=True)


def ebur128(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-filter_complex", "ebur128=peak=true",
                        "-f", "null", "-"], capture_output=True, text=True, check=True)
    txt = r.stderr[r.stderr.rfind("Summary:"):]
    grab = lambda pat: float(re.search(pat, txt)[1])
    return grab(r"I:\s+(-?[\d.]+) LUFS"), grab(r"LRA:\s+([\d.]+) LU"), grab(r"Peak:\s+(-?[\d.]+) dBFS")


def finish(x, path):
    """Gain to TARGET_I and a circular look-ahead limiter, both judged on the encoded OGG with ffmpeg."""
    ceil = -1.8
    for _ in range(6):
        y = circular(lambda z: E.limit(z, db(ceil)), x, int(SR))
        write_ogg(path, y)
        i, _, tp = ebur128(path)
        if abs(i - TARGET_I) <= .2 and tp <= TP_MAX - .1:
            return y
        x = x * db(TARGET_I - i)
        ceil -= max(0.0, tp - (TP_MAX - .1)) + (.2 if tp > TP_MAX - .1 else 0)
    raise SystemExit(f"{path.name}: could not reach {TARGET_I} LUFS / {TP_MAX} dBTP (last {i:.2f} / {tp:.2f})")


def measure(name, path, bpm, bars):
    x, sr = sf.read(path, always_2d=True)
    assert sr == SR and x.shape[1] == 2, f"{name}: {sr} Hz, {x.shape[1]} ch"
    expect = int(round(bars * 4 * 60 / bpm * SR))
    step = np.abs(np.diff(x, axis=0)).max(1)
    seam = np.abs(x[0] - x[-1]).max()
    d2 = lambda y: np.abs(y[2:] - 2 * y[1:-1] + y[:-2]).max(1)  # curvature: a click is a spike in it
    seam_d2 = d2(np.concatenate([x[-2:], x[:2]])).max()
    rms = lambda y: 20 * np.log10(np.sqrt((y ** 2).mean()) + 1e-12)
    m = x.mean(1)
    spec, f = np.abs(np.fft.rfft(m)) ** 2, np.fft.rfftfreq(len(m), 1 / SR)
    share = lambda lo, hi: 10 * np.log10(spec[(f >= lo) & (f < hi)].sum() / spec.sum())
    i, lra, tp = ebur128(path)
    r = dict(seconds=len(x) / SR, samples=len(x), delta=len(x) - expect, I=i, LRA=lra, TP=tp, seam=seam,
             p999=np.percentile(step, 99.9), seam_d2=seam_d2, p999_d2=np.percentile(d2(x), 99.9),
             end_db=rms(x[-SR:]), start_db=rms(x[:SR]), harsh=share(2000, 5000), body=share(200, 1000))
    print(f"{name}: {r['seconds']:.3f}s {len(x)} smp ({r['delta']:+d} vs {bars} bars @ {bpm}) | I {i:.1f} LUFS, "
          f"LRA {lra:.1f} LU, TP {tp:.1f} dBTP | seam step {seam:.4f} vs p99.9 {r['p999']:.4f}, seam curvature "
          f"{seam_d2:.4f} vs p99.9 {r['p999_d2']:.4f}, last/first 1 s {r['end_db']:.1f}/{r['start_db']:.1f} dBFS | "
          f"2-5 kHz {r['harsh']:.1f} dB, 200-1000 Hz {r['body']:.1f} dB of energy")
    assert r["delta"] == 0, f"{name}: length off by {r['delta']} samples"
    assert seam <= r["p999"] and seam_d2 <= r["p999_d2"], f"{name}: seam click"
    assert -16 <= i <= -14 and tp <= TP_MAX, f"{name}: loudness {i} LUFS / {tp} dBTP"
    return r


def main():
    args = sys.argv[1:]
    wav = Path(args[args.index("--wav") + 1]) if "--wav" in args else None
    names = [a for a in args if a in TRACKS] or list(TRACKS)
    OUT.mkdir(parents=True, exist_ok=True)
    for name in names:
        fn, bpm, bars = TRACKS[name]
        path = OUT / f"{name}.ogg"
        if "--check" not in args:
            t = time.time()
            s = fn()
            print(f"{name}: rendering {s.title}, {bpm} BPM, {s.key}, {bars} bars, seed {s.seed}")
            y = finish(render_loop(s), path)
            if wav:
                wav.mkdir(parents=True, exist_ok=True)
                sf.write(wav / f"{name}.wav", y, SR, subtype="PCM_24")
            print(f"  rendered in {time.time() - t:.0f}s")
        measure(name, path, bpm, bars)


if __name__ == "__main__":
    main()
