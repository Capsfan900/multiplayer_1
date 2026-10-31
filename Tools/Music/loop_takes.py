# /// script
# requires-python = ">=3.12"
# dependencies = ["numpy==2.5.4", "scipy==1.18.1", "soundfile==0.14.0"]
# ///
"""Cut the chosen ACE-Step takes into seamless game loops (see README: AI takes).

  uv run Tools/Music/loop_takes.py <takes dir>                 # all four tracks, write OGGs, verify
  uv run Tools/Music/loop_takes.py <takes dir> Level_atrium    # one track
  uv run Tools/Music/loop_takes.py <takes dir> --probe         # every seed's beat grid and loop window, no output
  uv run Tools/Music/loop_takes.py --check                     # measure the committed OGGs only (no takes needed)

Each loop is a whole number of 8-bar phrases between two kick-grid downbeats inside the take's groove. The beat
after the loop end is crossfaded over the loop start, so the wrap plays exactly what followed in the take.
"""
import sys

sys.dont_write_bytecode = True
import hashlib
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, sosfiltfilt

import engine as E
from build_music import OUT, finish, measure
from engine import SR

# Track -> (take file, nominal BPM, loop bars, loop samples at 44.1 kHz). Takes were picked by measurement (README);
# bars and samples lock the cut, so a changed take or grid fails the build instead of silently changing the loop.
TAKES = {"MenuTheme": ("menu_s11.wav", 116, 80, 7299512), "Level_atrium": ("atrium_s11.wav", 122, 72, 6246108),
         "Level_split-gallery": ("split_gallery_s33.wav", 124, 72, 6096728),
         "Level_terrace-steps": ("terrace_steps_s66.wav", 120, 48, 4198612)}
HOP = 44  # onset frame, ~1 ms


def onsets(x):
    """Low-band (kick) onset strength per HOP frame."""
    low = sosfiltfilt(butter(4, [40, 140], "bandpass", fs=SR, output="sos"), x.mean(1))
    env = sosfiltfilt(butter(2, 25, fs=SR, output="sos"), np.abs(low))[::HOP]
    return np.maximum(np.diff(env, prepend=env[0]), 0)


def beat_grid(x, bpm):
    """Kick times (samples) on the best constant-tempo grid near `bpm`, snapped to the local onset peak."""
    o = onsets(x)
    t = np.arange(len(o))
    best = (-1, 0, 0)
    for b in np.arange(bpm * .985, bpm * 1.015, .005):
        period = 60 * SR / b / HOP
        bins = np.bincount((np.mod(t, period) / period * 400).astype(int) % 400, o, 400)
        bins = bins + np.roll(bins, 1) + np.roll(bins, -1)
        if bins.max() > best[0]:
            best = (bins.max(), b, bins.argmax() / 400 * period)
    _, b, phase = best
    period = 60 * SR / b / HOP
    beats, strength = [], []
    for k in range(int((len(o) - phase) / period)):
        c = int(round(phase + k * period))
        w = o[max(0, c - 15):c + 16]
        beats.append((max(0, c - 15) + int(w.argmax())) * HOP)
        strength.append(w.max())
    beats, strength = np.array(beats), np.array(strength)
    # Downbeat = the beat phase where the harmony changes most (chords move on the bar line).
    mid = sosfiltfilt(butter(4, [200, 2000], "bandpass", fs=SR, output="sos"), x.mean(1))
    spec = lambda s, e: np.abs(np.fft.rfft(mid[s:e] * np.hanning(e - s), 8192))[40:400]
    change = np.array([np.abs(spec(beats[i], beats[i + 1]) - spec(beats[i - 1], beats[i])).sum()
                       for i in range(1, len(beats) - 1)])
    p = int(np.argmax([change[(q - 1) % 4::4].mean() for q in range(4)]))  # change[k] is at beat k + 1
    return b, beats[p:], (strength > .25 * np.percentile(strength, 90))[p:]


def window(x, beats, kick):
    """Longest loop of whole 8-bar phrases whose first and last bars (and the bar after) carry the groove."""
    bars = len(beats) // 4
    bar_kick = np.array([kick[4 * i:4 * i + 4].all() for i in range(bars)])
    rms = np.array([np.sqrt((x[beats[4 * i]:beats[4 * i + 4] if 4 * i + 4 < len(beats) else len(x)] ** 2).mean())
                    for i in range(bars)])
    live = rms > .3 * np.median(rms)  # not the outro fade (breakdowns stay in)
    for n in range(bars // 8 * 8, 7, -8):
        for a in range(0, bars - n):
            e = a + n
            if e + 1 < bars and bar_kick[a:a + 2].all() and bar_kick[e - 2:e + 1].all() and live[a:e + 1].all() \
                    and 4 * e + 4 < len(beats):
                return a, n
    raise SystemExit("no groove window of 8+ bars")


def loop(name, path, bpm):
    x, sr = sf.read(path, always_2d=True, dtype="float64")
    x = E.resample(x, sr, SR) if sr != SR else x
    x = sosfilt(E.peak(3000, -2.0, .7), x, axis=0)  # keep the bed out of the SFX presence band, as build_music does
    b, beats, kick = beat_grid(x, bpm)
    a, n = window(x, beats, kick)
    t0, t1 = beats[4 * a], beats[4 * (a + n)]
    fade = beats[4 * (a + n) + 1] - t1  # one beat
    drift = (t1 - t0) / SR - n * 240 / b
    print(f"{name}: {path.name} sha256 {hashlib.sha256(path.read_bytes()).hexdigest()[:16]} | grid {b:.3f} BPM, "
          f"{kick.mean():.0%} beats kicked | loop bars {a}-{a + n} ({n} bars, {(t1 - t0) / SR:.2f} s, grid drift "
          f"{drift * 1000:+.1f} ms)")
    y = x[t0:t1].copy()
    ramp = np.sin(np.linspace(0, np.pi / 2, fade))[:, None] ** 2
    y[:fade] = y[:fade] * ramp + x[t1:t1 + fade] * (1 - ramp)
    return y, n * 240 * SR / (t1 - t0), n


def main():
    args = sys.argv[1:]
    names = [a for a in args if a in TAKES] or list(TAKES)
    for name in names:
        file, bpm, bars, samples = TAKES[name]
        if "--check" in args:
            measure(name, OUT / f"{name}.ogg", bars * 240 * SR / samples, bars)
            continue
        takes = Path(args[0])
        if "--probe" in args:
            for take in sorted(takes.glob(file.rsplit("_s", 1)[0] + "_s*.wav")):
                loop(name, take, bpm)
            continue
        y, exact_bpm, cut = loop(name, takes / file, bpm)
        assert (cut, len(y)) == (bars, samples), f"{name}: cut {cut} bars / {len(y)} samples, locked {bars} / {samples}"
        path = OUT / f"{name}.ogg"
        finish(y, path)
        measure(name, path, exact_bpm, cut)


if __name__ == "__main__":
    main()
