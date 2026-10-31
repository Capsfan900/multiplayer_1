"""Code-only synth + mixer. No DAW, no samples: every sound is synthesized here.

A song script does:
    s = Song(bpm=141, bars=96, seed=1)
    s.track("Kick", "kick", gain=0, sidechain_source=True)
    s.track("Bass", "bass_roll", gain=-4, duck=.85)
    s.note("Bass", beat, dur_beats, midi, vel)          # extra kwargs reach the instrument
    s.render("my_song", sections={"Intro": (0, 16), ...})

Times are in beats (quarter notes); automation points are (bar, value).
Run a song with: uv run <song>.py
"""
import hashlib
import json
import math
import sys
import time
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.ndimage import maximum_filter1d, minimum_filter1d, uniform_filter1d
from scipy.signal import butter, lfilter, oaconvolve, sosfilt, sosfiltfilt

SR = 44100
BLOCK = 128  # samples per coefficient update in time-varying filters (~3 ms)
OUT = Path(__file__).parent / "out"
VOCALS = Path(__file__).parent / "vocals"  # <song>_tuned.wav from autotune.py is mixed in automatically
rng = np.random.default_rng(0)


# ---------------------------------------------------------------- dsp helpers

def hz(m): return 440.0 * 2 ** ((np.asarray(m, float) - 69) / 12)
def db(g): return 10 ** (g / 20)
def lp(x, f, order=2): return sosfilt(butter(order, min(f, SR * .45), "low", fs=SR, output="sos"), x, axis=0)
def hp(x, f, order=2): return sosfilt(butter(order, f, "high", fs=SR, output="sos"), x, axis=0)
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x, axis=0)
def noise(n, ch=1, corr=0.0):
    """White noise; stereo with `corr` L/R correlation (0 = fully independent/wide, 1 = mono). Drums use .5 -
    independent noise per channel loses 3 dB when summed to mono and cancels once the side is boosted."""
    if ch == 1:
        return rng.standard_normal(n)
    y = rng.standard_normal((n, ch))
    return math.sqrt(corr) * rng.standard_normal(n)[:, None] + math.sqrt(1 - corr) * y if corr else y
def stereo(y): return np.repeat(y[:, None], 2, 1) if y.ndim == 1 else y
def ts(n): return np.arange(n) / SR


def shelf(kind, f, gain_db):
    """RBJ low ('ls') / high ('hs') shelf, slope 1, as a one-section sos array."""
    A, w = 10 ** (gain_db / 40), 2 * np.pi * f / SR
    c, al = np.cos(w), np.sin(w) / 2 * math.sqrt(2)
    s = 1 if kind == "ls" else -1  # high shelf = low shelf with the (A-1)cos terms flipped
    k = 2 * math.sqrt(A) * al
    b = [A * ((A + 1) - s * (A - 1) * c + k), 2 * s * A * ((A - 1) - s * (A + 1) * c), A * ((A + 1) - s * (A - 1) * c - k)]
    a = [(A + 1) + s * (A - 1) * c + k, -2 * s * ((A - 1) + s * (A + 1) * c), (A + 1) + s * (A - 1) * c - k]
    return np.array([[*(np.array(b) / a[0]), 1.0, a[1] / a[0], a[2] / a[0]]])


def peak(f, gain_db, q=1.0):
    """RBJ peaking EQ bell as a one-section sos array (cut harsh 2-5 kHz, boxy 200-400 Hz, resonances)."""
    A, w = 10 ** (gain_db / 40), 2 * np.pi * f / SR
    c, al = np.cos(w), np.sin(w) / (2 * q)
    a0 = 1 + al / A
    return np.array([[(1 + al * A) / a0, -2 * c / a0, (1 - al * A) / a0, 1.0, -2 * c / a0, (1 - al / A) / a0]])


def biquad(kind, f, q=.707):
    """RBJ cookbook biquad as a one-section sos array."""
    w = 2 * np.pi * min(max(f, 20.0), SR * .45) / SR
    c, a = np.cos(w), np.sin(w) / (2 * q)
    b = {"lp": [(1 - c) / 2, 1 - c, (1 - c) / 2],
         "hp": [(1 + c) / 2, -(1 + c), (1 + c) / 2],
         "bp": [a, 0.0, -a]}[kind]
    a0 = 1 + a
    return np.array([[b[0] / a0, b[1] / a0, b[2] / a0, 1.0, -2 * c / a0, (1 - a) / a0]])


def vfilter(x, kind, cutoff, q=.707, poles=2):
    """Filter whose cutoff follows `cutoff` (scalar or per-sample array), updated every BLOCK samples.
    poles=2: one biquad (12 dB/oct). poles=4: two cascaded (24 dB/oct, Butterworth-flat at q=.707; higher q
    resonates) - the steep, driven filter modern synths use."""
    cut = np.broadcast_to(np.asarray(cutoff, float), (len(x),))
    y = np.empty(x.shape)
    secs = 1 if poles == 2 else 2
    zi = np.zeros((secs, 2) + x.shape[1:])
    for i in range(0, len(x), BLOCK):
        c = cut[i]
        sos = biquad(kind, c, q) if poles == 2 else np.vstack([biquad(kind, c, .541), biquad(kind, c, 1.307 * q / .707)])
        y[i:i + BLOCK], zi = sosfilt(sos, x[i:i + BLOCK], axis=0, zi=zi)
    return y


def widen(x, ms=12, amount=.4):
    """Mono-compatible width for a mono signal: a delayed copy becomes the side (L = x + a*d, R = x - a*d).
    The side is high-passed at 200 Hz (Strobe widens bass only above 200 Hz) so the low end stays mono."""
    d = max(1, int(ms / 1000 * SR))
    dl = hp(np.r_[np.zeros(d), x[:-d]], 200, 4)
    return np.stack([x + amount * dl, x - amount * dl], 1)


def _follow(x, attack, release):
    """Level follower: power smoothed with `attack`, then amplitude smoothed with `release` (seconds)."""
    a, r = np.exp(-1 / (attack * SR)), np.exp(-1 / (release * SR))
    pw_ = lfilter([1 - a], [1, -a], x ** 2, axis=0)
    return lfilter([1 - r], [1, -r], np.sqrt(np.maximum(pw_, 0)), axis=0)


def ott(x, depth=.5):
    """OTT-style 3-band (88 Hz / 2.5 kHz) upward + downward compression: pushes quiet detail up and loud
    peaks down in every band - the dense, 'in your face' 2010s EDM sound. depth = wet mix 0..1.
    Look-ahead peak detector: a lagging follower let the upward boost fire on every note onset (+10 dB
    crest, so OTT'd stems came out ~10 dB quiet after peak-normalisation)."""
    lo = lp(lp(x, 88), 88)
    hi = hp(hp(x, 2500), 2500)
    mid = x - lo - hi
    ref = np.abs(x).max() + 1e-12
    wet = np.zeros_like(x)
    for band, look, rel, tilt in ((lo, .010, .2, 0), (mid, .005, .12, 0), (hi, .002, .06, 3)):
        L = max(1, int(look * SR))
        pk = maximum_filter1d(np.abs(band) / ref, 2 * L + 1, axis=0)  # sees each transient L samples early
        r = np.exp(-1 / (rel * SR))
        lvl = np.maximum(pk, lfilter([1 - r], [1, -r], pk, axis=0))  # instant attack, smooth release
        lvl = 20 * np.log10(lvl + 1e-9)
        down = -np.maximum(lvl + 11, 0) * (1 - 1 / 8)
        up = np.clip((-27 - lvl) * (1 - 1 / 4), 0, 18) * np.clip((lvl + 67) / 15, 0, 1)
        gain = uniform_filter1d(down + up + tilt, L + 1, axis=0)  # gain settles before the transient arrives
        wet += band * db(gain)
    wet *= np.sqrt((x ** 2).mean() / ((wet ** 2).mean() + 1e-12))
    return (1 - depth) * x + depth * wet


def limit(x, ceiling=db(-.3), look=.004, release=.06):
    """Look-ahead peak limiter: gain ramps down over `look` s before each peak, recovers over `release` s."""
    L = int(look * SR)
    g = np.minimum(1, ceiling / np.maximum(np.abs(x).max(1), 1e-9))
    g = uniform_filter1d(minimum_filter1d(g, 2 * L + 1), L + 1)
    r = np.exp(-1 / (release * SR))
    g = np.minimum(g, lfilter([1 - r], [1, -r], g - 1) + 1)
    return np.clip(x * g[:, None], -ceiling, ceiling)


def glue(x, ratio=2.0, attack=.03, release=.1, gr_db=1.5):
    """Master-bus glue: 2:1, 30 ms / 100 ms, threshold placed so the loud (95th percentile) passages get
    ~gr_db of gain reduction - 'gentle 1-2 dB' regardless of the mix level."""
    lvl = 20 * np.log10(_follow(x.mean(1), attack, release) + 1e-9)
    loud = lvl[lvl > lvl.max() - 40]
    thresh = np.percentile(loud, 95) - gr_db / (1 - 1 / ratio)
    return x * db(-np.maximum(lvl - thresh, 0) * (1 - 1 / ratio))[:, None]


def lufs(x):
    """ITU-R BS.1770 K-weighted loudness (ungated) of a stereo signal."""
    kw = sosfilt(np.vstack([shelf("hs", 1681, 4.0), biquad("hp", 38, .5)]), x, axis=0)
    return -.691 + 10 * np.log10((kw ** 2).mean(0).sum() + 1e-12)


def resample(x, sr_from, sr_to):
    """Polyphase resample along axis 0 (e.g. 48 kHz AI takes -> SR)."""
    from math import gcd
    from scipy.signal import resample_poly
    g = gcd(int(sr_from), int(sr_to))
    return resample_poly(x, int(sr_to) // g, int(sr_from) // g, axis=0)


def nightcore(x, ratio=173.25 / 141):
    """Nightcore edit: play faster, tempo and pitch rise together. Default = the user's reference upload
    (141 -> 173.25 BPM, +3.57 semitones, measured by refanalyze.py --test)."""
    from fractions import Fraction
    fr = Fraction(ratio).limit_denominator(1000)
    return resample(x, fr.numerator, fr.denominator)


def env(n, gate, a=.002, d=.1, s=1.0, r=.05):
    """Attack (s), decay time-constant (s) to sustain level, linear release (s) starting at gate (s)."""
    t = ts(n)
    e = np.minimum(1, t / a) * (s + (1 - s) * np.exp(-np.maximum(t - a, 0) / d))
    return e * np.clip(1 - (t - gate) / r, 0, 1)


def crush(x, bits=8, factor=4):
    """Bit depth reduction + sample-and-hold downsampling (factor 4 = ~11 kHz)."""
    q = 2 ** (bits - 1)
    x = np.round(x * q) / q
    return np.repeat(x[::factor], factor, axis=0)[:len(x)]


def glide(m, n, p):
    """Per-sample frequency; slides from p['slide_from'] (midi) over p['glide'] seconds if given."""
    f = np.full(n, hz(m))
    if p.get("slide_from") is not None:
        f += (hz(p["slide_from"]) - hz(m)) * np.exp(-ts(n) / p.get("glide", .06))
    return f


# ---------------------------------------------------------------- oscillators (band-limited)

def _blep(t, dt):
    y = np.zeros_like(t)
    a = t < dt
    x = t[a] / dt[a]
    y[a] = x + x - x * x - 1
    b = t > 1 - dt
    x = (t[b] - 1) / dt[b]
    y[b] = x * x + x + x + 1
    return y


def _phase(f, n):
    dt = np.broadcast_to(np.asarray(f, float), (n,)) / SR
    return (rng.random() + np.cumsum(dt) - dt) % 1.0, dt


def saw(f, n):
    t, dt = _phase(f, n)
    return 2 * t - 1 - _blep(t, dt)


def pulse(f, n, width=.5):
    t, dt = _phase(f, n)
    t2 = (t + width) % 1.0
    return .5 * ((2 * t - 1 - _blep(t, dt)) - (2 * t2 - 1 - _blep(t2, dt)))


def sine(f, n):
    t, _ = _phase(f, n)
    return np.sin(2 * np.pi * t)


def detuned(f, n, voices, cents, spread=1.0):
    """Stack of detuned saws spread across the stereo field."""
    out = np.zeros((n, 2))
    for c in np.linspace(-cents, cents, voices) if voices > 1 else [0.0]:
        pos = .5 + .5 * spread * (c / cents if cents else 0)
        out += saw(f * 2 ** (c / 1200), n)[:, None] * [math.sqrt(1 - pos), math.sqrt(pos)]
    return out / math.sqrt(voices)


# ---------------------------------------------------------------- instruments
# signature: (gate_seconds, midi, velocity, params) -> mono (n,) or stereo (n, 2)

def kick(g, m, v, p):
    """Layered modern kick: 2 ms noise click + pitch-dropping punch + sub tail, saturated.
    p: base (tail Hz), sweep (Hz above base at the hit), pitch_t, decay (sub tail), click, drive, length."""
    t = ts(int(p.get("length", .45) * SR))
    f = p.get("base", 50) + p.get("sweep", 130) * np.exp(-t / p.get("pitch_t", .012))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * (.7 * np.exp(-t / .045) + .8 * np.exp(-t / p.get("decay", .14)))
    click = hp(noise(len(t)), 2000, 4) * np.exp(-t / .0015) * p.get("click", .5)
    return np.tanh(p.get("drive", 2.2) * (body + click)) * np.minimum(1, t / .0005)


def snare(g, m, v, p):
    """Layered big-room/dubstep snare: pitched click + hard-clipped ~200 Hz body + bright noise tail.
    p/note: tone (body Hz), decay (tail s), pitch (semitones - raise it through a roll), length."""
    t = ts(int(p.get("length", .5) * SR))
    k = 2 ** (p.get("pitch", 0) / 12)
    tone = p.get("tone", 200) * k
    click = np.sin(2 * np.pi * np.cumsum(tone + 400 * k * np.exp(-t / .003)) / SR) * np.exp(-t / .006)
    body = np.tanh(3 * np.sin(2 * np.pi * np.cumsum(tone * (1 + .3 * np.exp(-t / .01))) / SR)) * np.exp(-t / .045)
    nz = noise(len(t), 2, corr=.5)
    tail = (hp(nz, 3000) + .6 * bp(nz, 1200, 5000)) * np.exp(-t / p.get("decay", .12))[:, None]
    return np.tanh(1.6 * ((.5 * click + .8 * body)[:, None] + tail)) * np.minimum(1, t / .001)[:, None]


def clap(g, m, v, p):
    t = ts(int(.5 * SR))
    e = sum(np.exp(-(t - o) * 300) * (t >= o) for o in (0, .008, .017, .026))
    e = e + .9 * np.exp(-(t - .03) / p.get("decay", .1)) * (t >= .03)
    nz = noise(len(t), 2, corr=.5)
    return np.tanh(1.3 * (bp(nz, 1000, 7000) + .4 * hp(nz, 6000)) * e[:, None])


def hat(g, m, v, p):
    """Filtered-noise hat (no 808 metal): 24 dB HP at 7 kHz + 9-11.5 kHz emphasis. p/note: open, decay."""
    opn = p.get("open", False)
    d = p.get("decay", .09 if opn else .025)
    t = ts(int(min(.05 + 6 * d, .8) * SR))
    nz = noise(len(t), 2, corr=.5)
    y = hp(nz, 7000, 4) + .8 * bp(nz, 9000, 11500)
    return y * (np.minimum(1, t / .0005) * np.exp(-t / d))[:, None]


def ride(g, m, v, p):
    t = ts(int(1.0 * SR))
    nz = noise(len(t), 2, corr=.5)
    ping = sum(np.sin(2 * np.pi * fr * t + rng.random() * 6) for fr in (3150, 4870, 6230, 8110)) * np.exp(-t / .06) * .15
    y = (.8 * hp(nz, 5000, 4) + bp(nz, 7000, 12000)) * np.exp(-t / p.get("decay", .3))[:, None] + ping[:, None]
    return y * [1, .9]


def cowbell(g, m, v, p):
    """808-style cowbell, pitched: two squares a fifth-ish apart (phonk melodies)."""
    t = ts(int(.4 * SR))
    f = hz(m)
    y = pulse(f, len(t)) + pulse(f * 1.48, len(t))
    y = bp(y, f * .9, min(f * 6, 12000)) * (.7 * np.exp(-t / .015) + .3 * np.exp(-t / .12))
    return y


def crash(g, m, v, p):
    t = ts(int(p.get("length", 3.0) * SR))
    nz = noise(len(t), 2, corr=.5)
    return (hp(nz, 3500, 4) + .5 * bp(nz, 5000, 11000)) * (np.exp(-t / p.get("decay", .9)) * np.minimum(1, t / .002))[:, None]


def impact(g, m, v, p):
    """Sub boom + noise burst for the first beat of a drop."""
    t = ts(int(1.5 * SR))
    boom = np.sin(2 * np.pi * np.cumsum(30 + 50 * np.exp(-t / .1)) / SR) * np.exp(-t / .5)
    nz = lp(noise(len(t)), 3000) * np.exp(-t / .08) * .5
    return np.tanh(2 * (boom + nz))


def riser(g, m, v, p):
    """Noise sweeping dark->bright plus a saw climbing 2 octaves from `m`; length = note duration."""
    n = int(g * SR)
    k = np.linspace(0, 1, n)
    nz = noise(n, 2)
    sweep = vfilter(nz, "bp", 300 * (40 ** k), q=1.5)
    tone = vfilter(saw(hz(m) * 4 ** k, n), "lp", 500 + 6000 * k)
    return (sweep + .35 * tone[:, None]) * (k ** 2)[:, None]


def downlifter(g, m, v, p):
    n = int(g * SR)
    k = np.linspace(1, 0, n)
    return vfilter(noise(n, 2), "bp", 300 * (30 ** k), q=1.2) * (k ** 1.5)[:, None]


def vinyl(g, m, v, p):
    """Hiss + crackle bed, lasts the note duration."""
    n = int(g * SR)
    y = lp(noise(n, 2), 5000) * .05
    pops = (rng.random((n, 2)) < 10 / SR) * rng.standard_normal((n, 2)) * 2
    return y + hp(pops, 1200)


def air(g, m, v, p):
    """deadmau5 'lift': hi-passed noise bed; put it on a ducked track so it breathes with the pump."""
    n = int(g * SR)
    return hp(noise(n, 2), p.get("cut", 6000), 4) * np.minimum(1, ts(n) / .05)[:, None]


def bass_roll(g, m, v, p):
    """Trance rolling bass: short saw pluck (24 dB filter) + sub. Params: cut, env_amt, decay."""
    n = int((g + .015) * SR)
    t = ts(n)
    f = glide(m, n, p)
    x = np.tanh(1.3 * (saw(f, n) + .5 * saw(f * 1.004, n)))
    x = vfilter(x, "lp", p.get("cut", 180) + p.get("env_amt", 2500) * np.exp(-t / .04), q=1.1, poles=4)
    x = x + .9 * sine(f, n)
    return np.tanh(1.5 * x) * env(n, g, .001, p.get("decay", .05), p.get("sustain", .15), .01)


def bass_saw(g, m, v, p):
    """Harder offbeat saw bass (hard trance), 24 dB filter. Params: cut, env_amt, decay."""
    n = int((g + .02) * SR)
    t = ts(n)
    f = glide(m, n, p)
    x = np.tanh(1.5 * detuned(f, n, 3, 12, .3).mean(1))
    x = vfilter(x, "lp", p.get("cut", 300) + p.get("env_amt", 3500) * np.exp(-t / .06), q=1.2, poles=4)
    x = x + .8 * sine(f, n)
    return np.tanh(2.2 * x) * env(n, g, .001, p.get("decay", .12), p.get("sustain", .4), .015)


def sub808(g, m, v, p):
    """Distorted 808 sub with pitch slides. Note kwargs: slide_from (midi), glide (s)."""
    n = int((g + p.get("release", .08)) * SR)
    y = sine(glide(m, n, p), n)
    y = np.tanh(p.get("drive", 2.0) * y)
    return y * env(n, g, .003, p.get("decay", .8), p.get("sustain", .5), p.get("release", .08))


def clean_sub(g, m, v, p):
    """Clean mono sub: sine fundamental, quiet second harmonic, soft attack, no distortion."""
    release = p.get("release", .1)
    n = int((g + release) * SR)
    t = ts(n)
    phase = 2 * np.pi * hz(m) * t
    y = np.sin(phase) + p.get("harmonic", .08) * np.sin(2 * phase)
    return y * env(n, g, p.get("attack", .012), .08, .9, release)


def reese(g, m, v, p):
    """Layered reese: mono sine sub + drifting detuned mid saws (24 dB LP) + distorted top, mids widened.
    Params: cut (mid LP Hz), detune (cents), sub (level)."""
    n = int((g + .15) * SR)
    t = ts(n)
    f = glide(m, n, p)
    sub = sine(f * (.5 if hz(m) > 130 else 1), n) * p.get("sub", .9)
    drift = 1 + .002 * np.sin(2 * np.pi * .3 * t)
    dt = 2 ** (p.get("detune", 15) / 1200)
    mid = np.tanh(1.5 * (saw(f * dt * drift, n) + saw(f / dt / drift, n) + .5 * saw(2 * f, n)))
    mid = vfilter(mid, "lp", p.get("cut", 2500), q=.9, poles=4)
    body = hp(mid, 150) + .35 * hp(np.tanh(4 * mid), 3500)
    y = widen(np.tanh(1.3 * body), 11, .5) + np.tanh(1.3 * sub)[:, None]  # saturate before widening: sub stays mono
    return y * env(n, g, .008, .5, .9, .15)[:, None]


def electro_bass(g, m, v, p):
    n = int((g + .02) * SR)
    t = ts(n)
    f = glide(m, n, p)
    x = pulse(f, n, .5) + saw(f, n)
    x = vfilter(x, "lp", p.get("cut", 1200) + 1500 * np.exp(-t / .03), q=2)
    return np.tanh(2 * x) * env(n, g, .001, p.get("decay", .1), .5, .02)


def pluck(g, m, v, p):
    """deadmau5/trance pluck: 2 saws +-4 ct + a little noise, driven into a key-tracked 24 dB LP with a fast
    envelope, zero sustain, widened. Params: cut (Hz at middle C), env_oct (or legacy env_amt Hz), fdecay,
    decay, drive, keytrack."""
    n = int(min(g + .45, 1.8) * SR)
    t = ts(n)
    f = hz(m)
    x = saw(f * 2 ** (4 / 1200), n) + saw(f * 2 ** (-4 / 1200), n) + .25 * noise(n)
    x = np.tanh(p.get("drive", 1.5) * x / 2)
    base = p.get("cut", 400) * (f / 261.6) ** p.get("keytrack", 1.0)
    eo = p.get("env_oct", math.log2(1 + p["env_amt"] / p.get("cut", 400)) if "env_amt" in p else 3.0)
    x = vfilter(x, "lp", np.minimum(base * 2 ** (eo * np.exp(-t / p.get("fdecay", .12))), 18000), q=.75, poles=4)
    return widen(x * env(n, g + .45, .001, p.get("decay", .22), 0, .08), 9, .35)


def stab(g, m, v, p):
    """Rave/hoover stab: detuned saws + PWM, pitch dives from +bend semitones, driven 24 dB LP.
    Params: bend, cut, decay, detune, sustain."""
    n = int((g + .2) * SR)
    t = ts(n)
    f = hz(m) * 2 ** (p.get("bend", 12) * np.exp(-t / .045) / 12)
    x = detuned(f, n, 3, p.get("detune", 30), .8)
    x = x + .6 * pulse(f, n, .5 + .35 * np.sin(2 * np.pi * 1.3 * t))[:, None]
    x = vfilter(np.tanh(1.8 * x), "lp", np.minimum(p.get("cut", 4000) * 2 ** (1.5 * np.exp(-t / .08)), 18000), q=.9, poles=4)
    return x * env(n, g, .002, p.get("decay", .3), p.get("sustain", .15), .1)[:, None]


def supersaw(g, m, v, p):
    """7-voice supersaw, driven into a dark 24 dB LP. Params: cut, voices, detune."""
    n = int((g + .2) * SR)
    x = detuned(hz(m), n, p.get("voices", 7), p.get("detune", 25))
    x = vfilter(np.tanh(1.4 * x), "lp", p.get("cut", 5000), q=.8, poles=4)
    return x * env(n, g, .005, .3, .8, .2)[:, None]


def saw_lead(g, m, v, p):
    """deadmau5 'Ghosts n Stuff' lead/chord voice: 2 saws (0/-6 ct) + a quieter saw `interval` st up, driven
    into a 24 dB LP with a +env_oct filter envelope, widened. Stack 3-4 note chords for the big sound.
    Params: cut, env_oct, q, interval."""
    n = int((g + .2) * SR)
    t = ts(n)
    f = glide(m, n, p)
    x = saw(f, n) + saw(f * 2 ** (-6 / 1200), n) + db(-6) * saw(f * 2 ** (p.get("interval", 12) / 12), n)
    x = np.tanh(1.8 * x / 2)
    cut = np.minimum(p.get("cut", 2000) * 2 ** (p.get("env_oct", 1.5) * np.exp(-t / .2)), 18000)
    x = vfilter(x, "lp", cut, q=p.get("q", .9), poles=4)
    return widen(x * env(n, g, .005, .3, .8, .15), 12, .45)


GROWL_VOWELS = {"a": (800, 1200), "o": (500, 900), "u": (320, 800), "e": (400, 2000), "i": (300, 2300)}


def growl(g, m, v, p):
    """Skrillex-style FM growl / 'talking' bass (FM8-style): sine carrier, modulators at ratio 1 (index rising
    2->index) and 3, Chebyshev grit, hard-clipped, then swept through a vowel formant pair, HP 120 Hz, plus a mono
    sine sub. Play it at midi ~36-52. Note kwargs: vowels ('aou' = sweep a->o->u over the note), vowel_beats
    (cycle the path every N beats instead), index, index_t, ratio, drive, sub."""
    n = int((g + .03) * SR)
    t = ts(n)
    f = glide(m, n, p)

    def ph(fr):
        return 2 * np.pi * np.cumsum(fr) / SR

    idx = 2 + (p.get("index", 8) - 2) * (1 - np.exp(-t / p.get("index_t", .1)))
    y = np.sin(ph(f) + idx * np.sin(ph(f * p.get("ratio", 1))) + 1.5 * np.sin(ph(f * 3)))
    y = y + .3 * (2 * y * y - 1)
    path = p.get("vowels", "aou")
    beats = p.get("vowel_beats")
    pos = (t / (beats * p["spb"])) % 1.0 if beats else np.linspace(0, 1, n)
    k, xp = pos * (len(path) - 1), np.arange(len(path))
    y = np.clip(p.get("drive", 3) * y, -1, 1)  # distort BEFORE the formants: clipping after would erase the vowel
    form = sum(vfilter(y, "bp", np.interp(k, xp, [GROWL_VOWELS[c][j] for c in path]), q=10) for j in (0, 1))
    form *= np.sqrt((y ** 2).mean() / ((form ** 2).mean() + 1e-12))
    y = hp(np.tanh(1.5 * (.35 * y + form)), 120)
    e = env(n, g, .008, .5, .95, .03)
    sub = sine(f * (.5 if hz(m) > 90 else 1), n) * p.get("sub", .8)
    return stereo(.9 * y * e) + (sub * e)[:, None]


def wobble(g, m, v, p):
    """Wobble / screech bass: saws + a +19 st square, driven into a resonant 24 dB LP swept by a tempo-synced
    triangle LFO, optional comb (metallic screech), foldback distortion, mono sine sub.
    Note kwargs: lfo_beats (1/3 = 1/8T, .5 = 1/8, 1 = 1/4), cut, depth_oct, q, comb (ms), sub."""
    n = int((g + .02) * SR)
    t = ts(n)
    f = glide(m, n, p)
    x = np.tanh(3 * (saw(f, n) + saw(f * 1.004, n) + db(-12) * pulse(f * 2 ** (19 / 12), n)))
    lfo = 1 - np.abs(2 * ((t / (p.get("lfo_beats", 1 / 3) * p["spb"])) % 1.0) - 1)
    x = vfilter(x, "lp", np.minimum(p.get("cut", 120) * 2 ** (p.get("depth_oct", 5.5) * lfo), 16000), q=p.get("q", 3), poles=4)
    if p.get("comb"):
        d, fb = max(1, int(p["comb"] / 1000 * SR)), .6
        x = lfilter([1 - fb], np.r_[1, np.zeros(d - 1), -fb], x)
    x = 1 - np.abs(((1.3 * x / (np.abs(x).max() + 1e-12) + 1) % 4) - 2)  # foldback distortion
    x = hp(x, 110)
    e = env(n, g, .005, .5, .95, .02)
    sub = sine(f * (.5 if hz(m) > 90 else 1), n) * p.get("sub", .8)
    return stereo(x * e) + (sub * e)[:, None]


def acid(g, m, v, p):
    """303-style line, resonant 24 dB LP. Note kwargs: accent (bool), slide_from (midi). Params: cut, q."""
    n = int((g + .02) * SR)
    t = ts(n)
    acc = p.get("accent", False)
    x = saw(glide(m, n, {**p, "glide": p.get("glide", .04)}), n)
    cut = p.get("cut", 300) + (3500 if acc else 1600) * np.exp(-t / (.12 if acc else .08))
    x = vfilter(x, "lp", cut, q=p.get("q", 7) * .35, poles=4)
    return np.tanh((3 if acc else 2) * x) * env(n, g, .002, .2, .6, .02)


def atari(g, m, v, p):
    """Crystal-Castles style chip lead: pulse + tri sub, drifting pitch, bitcrushed. Params: width, bits, factor."""
    n = int((g + .08) * SR)
    t = ts(n)
    f = glide(m, n, p) * (1 + .003 * np.sin(2 * np.pi * .3 * t + rng.random() * 6))
    x = pulse(f, n, p.get("width", .25))
    tri = 2 * np.abs(saw(f / 2, n)) - 1
    x = crush(x + .5 * tri, p.get("bits", 6), p.get("factor", 4))
    return x * env(n, g, .002, .4, .7, .06)


VOWELS = {"a": (700, 1220, 2600), "o": (450, 800, 2830), "u": (300, 870, 2250),
          "e": (400, 2000, 2550), "i": (270, 2300, 3000)}


def vox(g, m, v, p):
    """Formant-filtered synth 'voice' (pitched vocal chops). Note kwargs: vowel ('a','o','u','e','i')."""
    n = int((g + p.get("release", .12)) * SR)
    t = ts(n)
    vib = .35 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - .12) / .2, 0, 1)  # delayed vibrato, semitones
    f = glide(m, n, p) * 2 ** (vib / 12)
    src = saw(f, n) + .5 * saw(f * 1.003, n) + .08 * noise(n)
    y = sum(w * sosfilt(biquad("bp", fr, 9), src) for fr, w in zip(VOWELS[p.get("vowel", "a")], (1, .6, .35)))
    y = np.tanh(2.5 * y)
    return y * env(n, g, p.get("attack", .02), .5, .85, p.get("release", .12))


def pad(g, m, v, p):
    """deadmau5-style pad: 4 detuned saws + a square an octave down into a 24 dB LP (automate the track's lp
    for long 16-32 bar sweeps). Params: cut, attack, release, detune, q."""
    rel = p.get("release", 1.0)
    n = int((g + rel) * SR)
    f = hz(m)
    x = detuned(f, n, 4, p.get("detune", 8)) + db(-6) * stereo(pulse(f / 2, n))
    x = vfilter(x, "lp", p.get("cut", 1800), q=p.get("q", .8), poles=4)
    return x * env(n, g, p.get("attack", .4), 1, 1, rel)[:, None]


GROUPS = {"drums": ("kick", "snare", "clap", "hat", "ride", "cowbell"),
          "bass": ("sub808", "clean_sub", "bass_roll", "bass_saw", "reese", "growl", "wobble", "electro_bass"),
          "lead": ("saw_lead", "pluck", "stab", "supersaw", "acid", "atari", "vox", "audio"),
          "atmos": ("pad", "air", "vinyl"),
          "fx": ("crash", "impact", "riser", "downlifter", None)}
GROUP_OF = {inst: g for g, insts in GROUPS.items() for inst in insts}
LOW_END = {"kick", "impact"} | set(GROUPS["bass"])  # everything else is high-passed at 35 Hz (rumble clean-up)


INSTRUMENTS = {k: f for k, f in globals().items()
               if callable(f) and getattr(f, "__code__", None) and f.__code__.co_varnames[:4] == ("g", "m", "v", "p")}


# ---------------------------------------------------------------- song / mixer

class Song:
    def __init__(self, bpm, bars, seed=0, duck_release=.12, master_drive=1.8, master_lp=None,
                 low_shelf=-3.0, high_shelf=2.0, side_boost=5.0, master_ott=.15, target_lufs=-10.0,
                 glue_db=1.5, key=None, title=None):
        """Master chain: low shelf (dB @120 Hz) / high shelf (dB @4 kHz) tilt; everything below 120 Hz mono;
        side_boost (dB) widens above 250 Hz; light OTT (master_ott) for density; soft-clip drive; loudness
        set to target_lufs (K-weighted); look-ahead limiter at -0.3 dBFS. duck_release = time constant (s)
        of the sidechain pump's recovery (~.12 at 128 BPM, ~.09 at 140+). glue_db = master glue compression
        on loud passages (0 = off). key/title go into the production log (title: never a reference artist or
        song name)."""
        global rng
        rng = np.random.default_rng(seed)  # fixed seed = identical render every run
        self.bpm, self.bars, self.spb = bpm, bars, 60 / bpm
        self.duck_release, self.master_drive, self.master_lp = duck_release, master_drive, master_lp
        self.low_shelf, self.high_shelf, self.side_boost = low_shelf, high_shelf, side_boost
        self.master_ott, self.target_lufs, self.glue_db = master_ott, target_lufs, glue_db
        self.seed, self.key, self.title = seed, key, title
        self.tracks = {}
        self.gaps = []

    def s(self, beat):
        return int(round(beat * self.spb * SR))

    def track(self, name, inst, gain=0.0, pan=0.0, width=1.0, duck=0.0, plate=0.0, hall=0.0, delay=0.0,
              drive=0.0, bitcrush=None, lp=None, hp=None, sidechain_source=False, ott=0.0, widen=0.0,
              duck_lo=0.0, group=None, eq=None, **params):
        """gain dB after peak-normalising the stem; duck 0..1 pump depth (1 = silent on the kick); plate/hall/
        delay are send levels; bitcrush=(bits, factor); ott 0..1 multiband squash; widen 0..1 (Haas side for
        mono-ish stems); duck_lo 0..1 spectral sidechain - the kick ducks only this track's lows (<150 Hz), so
        bass clears the kick without audible pumping; eq = [(Hz, dB, q), ...] static bells (e.g. (3500, -3, 1)
        to take the harsh edge off a snare); group = stem group (default from the instrument:
        drums/bass/lead/atmos/fx); lp/hp = [(bar, Hz), ...] 24 dB filter automation; params -> instrument."""
        assert inst in (None, "audio") or inst in INSTRUMENTS, f"unknown instrument {inst}: {sorted(INSTRUMENTS)}"
        self.tracks[name] = dict(inst=inst, gain=gain, pan=pan, width=width, duck=duck, plate=plate, hall=hall,
                                 delay=delay, drive=drive, bitcrush=bitcrush, lp=lp, hp=hp,
                                 sidechain_source=sidechain_source, ott=ott, widen=widen, duck_lo=duck_lo,
                                 group=group or GROUP_OF.get(inst, "lead"), eq=eq, params=params, notes=[])

    def note(self, track, beat, dur, midi=60, vel=100, **extra):
        assert 0 <= beat < self.bars * 4, f"{track}: beat {beat} outside song"
        assert 1 <= vel <= 127, f"{track}: velocity {vel}"
        self.tracks[track]["notes"].append((beat, dur, midi, vel, extra))

    def fx(self, kind, bar, bars=0.0, midi=48, vel=110, **extra):
        """One-shot FX on an auto-created 'FX' track: crash, impact, riser, downlifter, vinyl."""
        if "FX" not in self.tracks:
            self.track("FX", None, gain=-13, hall=.25, duck=.3)
        self.note("FX", bar * 4, bars * 4, midi, vel, inst=kind, **extra)

    def gap(self, beat, beats=.5):
        """True silence (whole mix incl. reverb/delay tails, 4 ms fades) from `beat` for `beats` - the
        pre-drop hole. Put the drop's first hit at beat + beats."""
        self.gaps.append((beat, beats))

    # -- rendering

    def _curve(self, points, n):
        xs = [self.s(b * 4) for b, _ in points]
        return np.exp(np.interp(np.arange(n), xs, np.log([f for _, f in points])))

    def _sidechain(self, n):
        imp = np.zeros(n)
        for tr in self.tracks.values():
            if tr["sidechain_source"]:
                for beat, *_ in tr["notes"]:
                    imp[max(0, self.s(beat) - int(.001 * SR))] = 1  # 1 ms early, like deadmau5's nudged kick
        tau = self.duck_release
        shape = np.exp(-ts(int(6 * tau * SR)) / tau)  # LFO-Tool style: silent on the hit, exponential recovery
        k = int(.001 * SR)
        shape[:k] *= np.linspace(0, 1, k)
        return np.clip(oaconvolve(imp, shape)[:n], 0, 1)[:, None]

    def _stem(self, tr, n):
        buf = np.zeros((n, 2), np.float32)
        if tr["inst"] == "audio":  # pre-rendered audio (recorded vocals), aligned to beat 0
            y, sr = sf.read(tr["params"]["path"], dtype="float32", always_2d=True)
            assert sr == SR, f"{tr['params']['path']}: {sr} Hz, need {SR}"
            y = stereo(y[:, 0]) if y.shape[1] == 1 else y[:, :2]
            buf[:min(n, len(y))] = y[:n]
            return buf
        for beat, dur, m, v, extra in tr["notes"]:
            p = {**tr["params"], **extra, "spb": self.spb}
            y = stereo(INSTRUMENTS[extra.get("inst", tr["inst"])](dur * self.spb, m, v, p)) * (v / 127) ** 1.2
            a = self.s(beat)
            b = min(n, a + len(y))
            buf[a:b] += y[:b - a]
        return buf

    @staticmethod
    def _reverb(x, secs, pre, lo, hi):
        t = ts(int(secs * SR))
        ir = lp(hp(noise(len(t), 2) * np.exp(-t * 6.9 / secs)[:, None], lo), hi)
        ir = np.concatenate([np.zeros((int(pre * SR), 2)), ir])
        ir /= np.sqrt((ir ** 2).sum(0))
        return np.stack([oaconvolve(x[:, c], ir[:, c])[:len(x)] for c in (0, 1)], 1)

    def _delay(self, x, beats=.75, fb=.45, taps=6):
        d = self.s(beats)
        src = lp(hp(x.mean(1), 300), 4500)
        y = np.zeros_like(x)
        for k in range(1, taps + 1):
            if k * d < len(x):
                y[k * d:, k % 2] += src[:len(x) - k * d] * fb ** k
        return y

    def render(self, name, sections=None, tail=4.0, edits=None):
        """Stage-separated render: per-group mix (+ that group's returns) -> linear mix-bus chain -> stereo master.
        Writes out/<name>.wav (listen), out/<name>_loop.wav/.ogg (seamless loop) and out/<name>_log.json
        (production log). edits = [(label, start_bar, seconds), ...] -> out/<name>_<label>.wav (e.g. 30 s / 60 s
        cuts for trailers/shorts). `--stems` also writes out/<name>_stems/<group>.wav: pre-master group stems
        that sum to the pre-master mix. Returns section loudness (LUFS, ungated)."""
        instrumental = "--instrumental" in sys.argv  # backing track to sing over: no vocals of any kind
        voc = VOCALS / f"{name}_tuned.wav"
        if instrumental:
            for tr in self.tracks.values():
                if tr["inst"] == "vox":
                    tr["notes"] = []
        elif voc.exists() and "Vocals" not in self.tracks:
            print(f"mixing in recorded vocals: {voc}")
            self.track("Vocals", "audio", gain=-1, duck=.15, plate=.12, hall=.22, delay=.18, path=voc)
            self.tracks["Vocals"]["notes"] = [(0, 0, 0, 127, {})]  # marks the track active
        song_n = self.s(self.bars * 4)
        n = song_n + int(tail * SR)
        sc = self._sidechain(n)
        want_stems = "--stems" in sys.argv
        mask = np.ones(n)
        f = int(.004 * SR)
        for beat, beats in self.gaps:
            a, b = self.s(beat), self.s(beat + beats)
            mask[a:b] = 0
            mask[a:a + f] = np.linspace(1, 0, f)
            mask[b - f:b] = np.linspace(0, 1, f)

        # stage 1: mix each stem group (tracks + that group's own reverb/delay returns); groups sum to the mix
        mix = np.zeros((n, 2))
        stems = {}
        for grp in GROUPS:
            members = [tr for tr in self.tracks.values() if tr["group"] == grp and tr["notes"]]
            if members:
                g = self._mix_group(members, n, sc) * mask[:, None]
                mix += g
                if want_stems:
                    stems[grp] = self._post(g).astype(np.float32)
        mix = self._post(mix)  # stage 2: linear mix-bus EQ / imaging (identical on stems, so they still sum)
        mix_premaster = mix[:song_n]

        loop = mix[:song_n].copy()
        for start in range(song_n, n, song_n):
            chunk = mix[start:start + song_n]
            loop[:len(chunk)] += chunk
        full = mix.copy()
        fade = min(len(full), int(1.5 * SR))
        full[-fade:] *= np.linspace(1, 0, fade)[:, None]
        loop, full = self._master(loop), self._master(full)  # stage 3: master (stereo only)
        if self.gaps:  # again after the master: its filters/limiter ring into the hole otherwise (~-40 dBFS of bass)
            full *= mask[:, None]
            loop *= mask[:song_n, None]
        assert np.isfinite(loop).all() and np.isfinite(full).all()
        return self._deliver(name, sections, instrumental, full, loop, stems, mix_premaster, edits)

    def _mix_group(self, members, n, sc):
        mix = np.zeros((n, 2))
        sends = {k: np.zeros((n, 2)) for k in ("plate", "hall", "delay")}
        for tr in members:
            x = self._stem(tr, n).astype(float)
            if tr["drive"]:
                x = np.tanh(tr["drive"] * x / (np.abs(x).max() + 1e-12))
            if tr["bitcrush"]:
                x = crush(x, *tr["bitcrush"])
            if tr["ott"]:
                x = ott(x, tr["ott"])
            if tr["lp"]:
                x = vfilter(x, "lp", self._curve(tr["lp"], n), poles=4)
            if tr["hp"]:
                x = vfilter(x, "hp", self._curve(tr["hp"], n), poles=4)
            if tr["eq"]:
                x = sosfilt(np.vstack([peak(*b) for b in tr["eq"]]), x, axis=0)
            if tr["inst"] not in LOW_END and tr["group"] != "fx":
                x = hp(x, 35)  # clean-up: nothing but kick and bass lives below 35 Hz
            x *= db(tr["gain"]) / (np.abs(x).max() + 1e-12)
            if tr["width"] != 1:
                mid, side = x.mean(1, keepdims=True), (x[:, :1] - x[:, 1:]) / 2
                x = np.hstack([mid + tr["width"] * side, mid - tr["width"] * side])
            if tr["widen"]:
                x = x + widen(x.mean(1), 13, tr["widen"]) - x.mean(1, keepdims=True)
            if tr["pan"]:
                ang = (tr["pan"] + 1) * np.pi / 4
                x *= [math.cos(ang) * math.sqrt(2), math.sin(ang) * math.sqrt(2)]
            if tr["duck"]:
                x *= 1 - tr["duck"] * sc
            if tr["duck_lo"]:  # spectral ducking: only the lows move out of the kick's way
                # zero-phase split: a causal LP lags ~50 deg at 40 Hz, so x - lp(x) would barely remove the lows
                lows = sosfiltfilt(butter(2, 150, "low", fs=SR, output="sos"), x, axis=0)
                x = x - lows * (tr["duck_lo"] * sc)
            mix += x
            for k in sends:
                if tr[k]:
                    sends[k] += x * tr[k]
        ret = np.zeros((n, 2))
        if any(tr["plate"] for tr in members):
            ret += self._reverb(sends["plate"], 1.2, .01, 300, 9000) * db(-4)
        if any(tr["hall"] for tr in members):
            ret += self._reverb(sends["hall"], 4.0, .03, 200, 5000) * db(-4)
        if any(tr["delay"] for tr in members):
            ret += self._delay(sends["delay"])
        return mix + ret * (1 - .5 * sc)

    def _post(self, mix):
        """Linear mix-bus chain: 30 Hz HP, tilt shelves, mono below 120 Hz, side lift above 250 Hz, optional LP."""
        mix = hp(mix, 30)
        mix = sosfilt(np.vstack([shelf("ls", 120, self.low_shelf), shelf("hs", 4000, self.high_shelf)]), mix, axis=0)
        mid, side = mix.mean(1, keepdims=True), (mix[:, :1] - mix[:, 1:]) / 2
        side = hp(side, 120, 4)  # mono low end
        side = side + (db(self.side_boost) - 1) * hp(side, 250)
        mix = np.hstack([mid + side, mid - side])
        return lp(mix, self.master_lp) if self.master_lp else mix

    def _deliver(self, name, sections, instrumental, full, loop, stems, mix_premaster, edits):
        song_n = len(loop)
        OUT.mkdir(exist_ok=True)
        if instrumental:
            sf.write(OUT / f"{name}_instrumental.wav", full, SR, subtype="PCM_16")
            print(f"{name}: instrumental -> {OUT / f'{name}_instrumental.wav'}")
            raise SystemExit(0)  # the song's loudness asserts don't apply to the backing track
        sf.write(OUT / f"{name}.wav", full, SR, subtype="PCM_16")
        sf.write(OUT / f"{name}_loop.wav", loop, SR, subtype="PCM_16")
        with sf.SoundFile(OUT / f"{name}_loop.ogg", "w", SR, 2, format="OGG", subtype="VORBIS") as fh:
            for i in range(0, len(loop), SR):  # libsndfile's vorbis encoder crashes on one huge write
                fh.write(loop[i:i + SR])

        # ITU-R BS.1770 K-weighting (ungated): loudness as heard, so a kick-dominated section
        # doesn't read as loud as a drop that adds mids and highs.
        for label, bar, secs in edits or []:
            a = self.s(bar * 4)
            clip = full[a:a + int(secs * SR)].copy()
            fi, fo = int(.02 * SR), int(min(2.0, secs / 6) * SR)
            clip[:fi] *= np.linspace(0, 1, fi)[:, None]
            clip[-fo:] *= np.linspace(1, 0, fo)[:, None]
            sf.write(OUT / f"{name}_{label}.wav", clip, SR, subtype="PCM_24")
        if stems:
            d = OUT / f"{name}_stems"
            d.mkdir(exist_ok=True)
            k = db(-1) / (np.abs(mix_premaster).max() + 1e-12)  # one common scale: stems still sum to the mix
            for grp, x in stems.items():
                sf.write(d / f"{grp}.wav", (x[:len(mix_premaster)] * k).astype(np.float32), SR, subtype="FLOAT")

        loud = {sec_name: lufs(loop[self.s(a * 4):self.s(b * 4)]) for sec_name, (a, b) in (sections or {}).items()}
        print(f"{name}: {self.bpm} BPM, {self.bars} bars, {song_n / SR:.1f}s, {lufs(loop):.1f} LUFS, "
              f"peak {20 * np.log10(np.abs(loop).max()):.1f} dBFS -> {OUT}")
        for k, val in loud.items():
            print(f"  {k:<14}{val:6.1f} LUFS")
        log = {"title": self.title or name, "file": name, "bpm": self.bpm, "key": self.key, "bars": self.bars,
               "seed": self.seed, "seconds": round(song_n / SR, 3), "lufs": round(float(lufs(loop)), 2),
               "peak_dbfs": round(float(20 * np.log10(np.abs(loop).max())), 2),
               "sections_lufs": {k: round(float(v), 2) for k, v in loud.items()},
               "gaps": self.gaps, "edits": edits or [], "stems": sorted(stems),
               "engine_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest()[:16],
               "rendered": time.strftime("%Y-%m-%d %H:%M:%S"), "args": sys.argv[1:],
               "master": {"target_lufs": self.target_lufs, "drive": self.master_drive, "ott": self.master_ott,
                          "glue_db": self.glue_db, "low_shelf": self.low_shelf, "high_shelf": self.high_shelf,
                          "side_boost": self.side_boost, "master_lp": self.master_lp},
               "tracks": {t: {"inst": tr["inst"], "group": tr["group"], "gain": tr["gain"], "notes": len(tr["notes"])}
                          for t, tr in self.tracks.items() if tr["notes"]}}
        (OUT / f"{name}_log.json").write_text(json.dumps(log, indent=1, default=str))
        return loud

    def _master(self, x):
        if not np.isfinite(x).all():
            raise ValueError("Master input contains non-finite audio")
        amplitude = np.abs(x).max()
        if amplitude == 0:
            return x.astype(np.float32)
        x = x / amplitude
        if self.master_ott:
            x = ott(x, self.master_ott)
        if self.glue_db:
            x = glue(x, gr_db=self.glue_db)
        k = self.master_drive
        x = np.tanh(k * x) / np.tanh(k)
        x = x * db(self.target_lufs - lufs(x))
        return limit(x).astype(np.float32)
