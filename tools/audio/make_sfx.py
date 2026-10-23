"""Synthesizes every Signal Lost sound effect as a 16-bit mono WAV.

Everything is generated from oscillators and filtered noise (no samples), so the sounds are original and free to use.
Run: python3 make_sfx.py OUTPUT_DIR
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(1018)


# ---------------------------------------------------------------- helpers

def T(dur):
    return np.arange(int(SR * dur)) / SR


def noise(dur):
    return rng.standard_normal(int(SR * dur))


def pink(dur):
    n = int(SR * dur)
    white = rng.standard_normal(n)
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, white) * 6


def brown(dur):
    x = np.cumsum(rng.standard_normal(int(SR * dur)))
    x = signal.sosfilt(signal.butter(1, 20, 'hp', fs=SR, output='sos'), x)
    return x / (np.max(np.abs(x)) + 1e-9)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)


def lp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, 'lowpass', fs=SR, output='sos'), x)


def hp(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, 'highpass', fs=SR, output='sos'), x)


def sweep_lp(x, f_start, f_end):
    """One-pole lowpass whose cutoff glides exponentially from f_start to f_end."""
    n = len(x)
    f = f_start * (f_end / f_start) ** (np.arange(n) / max(1, n - 1))
    a = 1 - np.exp(-2 * np.pi * f / SR)
    y = np.empty(n)
    acc = 0.0
    for i in range(n):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def sweep_bp(x, centers, q=4.0, block=256):
    """Time-varying band-pass: centre frequency per sample (array), processed in blocks."""
    y = np.zeros_like(x)
    zi = None
    for s in range(0, len(x), block):
        c = float(np.clip(centers[min(s + block // 2, len(centers) - 1)], 40, SR * 0.45))
        bw = c / q
        lo, hi = max(20, c - bw / 2), min(SR * 0.49, c + bw / 2)
        sos = signal.butter(2, [lo, hi], 'bandpass', fs=SR, output='sos')
        if zi is None:
            zi = signal.sosfilt_zi(sos) * 0
        y[s:s + block], zi = signal.sosfilt(sos, x[s:s + block], zi=zi)
    return y


def osc(freq, dur=None, shape='sine', phase=0.0):
    """freq can be a number or an array (one value per sample)."""
    if np.isscalar(freq):
        freq = np.full(int(SR * dur), float(freq))
    ph = 2 * np.pi * np.cumsum(freq) / SR + phase
    if shape == 'sine':
        return np.sin(ph)
    if shape == 'saw':
        return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
    if shape == 'square':
        return np.sign(np.sin(ph))
    if shape == 'tri':
        return 2 * np.abs(2 * ((ph / (2 * np.pi)) % 1.0) - 1) - 1
    raise ValueError(shape)


def glide(f0, f1, dur, curve='exp'):
    n = int(SR * dur)
    k = np.arange(n) / max(1, n - 1)
    if curve == 'exp':
        return f0 * (f1 / f0) ** k
    return f0 + (f1 - f0) * k


def env_ad(dur, attack, decay_tau, hold=0.0):
    t = T(dur)
    e = np.where(t < attack, t / max(attack, 1e-5), np.exp(-(t - attack - hold).clip(0) / decay_tau))
    return e


def env_bell(dur, peak=0.5, sharp=2.0):
    t = np.linspace(0, 1, int(SR * dur))
    return np.where(t < peak, (t / peak) ** sharp, ((1 - t) / (1 - peak)) ** sharp)


def fade(x, fin=0.003, fout=0.01):
    x = x.copy()
    a, b = int(SR * fin), int(SR * fout)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, start, total):
    out = np.zeros(int(SR * total))
    s = int(SR * start)
    e = min(len(out), s + len(x))
    out[s:e] += x[:e - s]
    return out


def partials(freqs, amps, taus, dur):
    t = T(dur)
    out = np.zeros_like(t)
    for f, a, tau in zip(freqs, amps, taus):
        out += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / tau)
    return out


def reverb(x, seconds=0.8, wet=0.25, bright=4000):
    ir = noise(seconds) * np.exp(-T(seconds) / (seconds / 5))
    ir = lp(ir, bright)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    w = signal.fftconvolve(x, ir)[:len(x) + int(SR * seconds * 0.6)]
    dry = np.concatenate([x, np.zeros(len(w) - len(x))])
    return dry * (1 - wet) + w * wet


def formant_voice(f0, dur, formants, breath=0.05, shape='saw'):
    """Very small formant synth: a glottal buzz through band-pass resonances (vowel)."""
    src = osc(f0, dur, shape)
    out = np.zeros(len(src))
    for (fc, bw, g) in formants:
        out += g * bp(src, max(40, fc - bw / 2), fc + bw / 2, order=2)
    out += breath * bp(noise(len(src) / SR), 500, 4000)
    return out


VOWELS = {
    'a': [(800, 160, 1.0), (1200, 180, 0.6), (2600, 300, 0.25)],
    'e': [(500, 120, 1.0), (1900, 200, 0.5), (2500, 300, 0.25)],
    'i': [(300, 100, 1.0), (2300, 220, 0.45), (3000, 300, 0.2)],
    'o': [(500, 120, 1.0), (850, 140, 0.7), (2400, 300, 0.15)],
    'u': [(330, 100, 1.0), (750, 140, 0.5), (2300, 300, 0.1)],
    'uh': [(620, 140, 1.0), (1150, 180, 0.55), (2500, 300, 0.2)],
}


def seamless(fn, dur, xf=1.0):
    """Builds a loop: renders dur+xf seconds and crossfades the tail into the head."""
    x = fn(dur + xf)
    n, k = int(SR * dur), int(SR * xf)
    if len(x) < n + k:
        x = np.concatenate([x, np.zeros(n + k - len(x))])
    head, tail = x[:k], x[n:n + k]
    ramp = np.linspace(0, 1, k)
    out = x[:n].copy()
    out[:k] = head * np.sqrt(ramp) + tail * np.sqrt(1 - ramp)
    return out


def norm(x, peak_db=-1.0):
    peak = np.max(np.abs(x)) + 1e-9
    return x / peak * (10 ** (peak_db / 20))


SOUNDS = {}


def sound(name, peak_db=-1.0, loop=False):
    def deco(fn):
        SOUNDS[name] = (fn, peak_db, loop)
        return fn
    return deco


# ---------------------------------------------------------------- mining laser

def whoosh(dur, f_lo, f_hi, peak=0.45, q=3.0, hum=0.0):
    n = int(SR * dur)
    k = np.linspace(0, 1, n)
    centre = f_lo + (f_hi - f_lo) * np.sin(np.pi * np.clip(k / (peak * 2), 0, 1)) ** 1.5
    x = sweep_bp(noise(dur), centre, q=q) * env_bell(dur, peak, 1.6)
    if hum:
        x += hum * (osc(110, dur, 'saw') * 0.4 + osc(220, dur)) * env_bell(dur, peak, 2.0) * 0.15
    return x


@sound('laser_swing_1')
def _():
    return fade(whoosh(0.26, 350, 2600, 0.4, hum=0.6))


@sound('laser_swing_2')
def _():
    return fade(whoosh(0.3, 280, 2100, 0.5, q=2.5, hum=0.6))


@sound('laser_slam')
def _():
    dur = 0.75
    sw = at(whoosh(0.32, 200, 1600, 0.6, q=2.2, hum=0.8), 0, dur)
    thud = at(osc(glide(90, 38, 0.35), None) * env_ad(0.35, 0.002, 0.09), 0.28, dur) * 1.6
    grit = at(lp(noise(0.3), 900) * env_ad(0.3, 0.001, 0.06), 0.28, dur) * 0.8
    clank = at(partials([520, 1310, 2270, 3650], [0.6, 0.4, 0.25, 0.12], [0.18, 0.12, 0.08, 0.05], 0.4), 0.29, dur)
    return fade(mix(sw, thud, grit, clank))


@sound('laser_charge')
def _():
    dur = 1.25
    f = glide(80, 430, dur)
    tone = (osc(f, None, 'saw') * 0.5 + osc(f * 2.01, None) * 0.3 + osc(f * 0.5, None) * 0.4)
    tone = sweep_lp(tone, 400, 4000)
    k = np.linspace(0, 1, len(tone))
    crackle = hp(noise(dur), 2500) * (rng.random(len(tone)) < 0.002 + 0.02 * k ** 2) * 3
    crackle = lp(crackle, 9000)
    return fade(tone * (0.25 + 0.75 * k) + crackle * 0.5, 0.05, 0.02)


@sound('laser_charge_hold', loop=True)
def _():
    def gen(d):
        t = T(d)
        trem = 0.75 + 0.25 * np.sin(2 * np.pi * 11 * t)
        tone = (osc(430, d, 'saw') * 0.4 + osc(861, d) * 0.3 + osc(215, d) * 0.4) * trem
        tone = lp(tone, 3500)
        crackle = lp(hp(noise(d), 2500) * (rng.random(len(t)) < 0.012) * 3, 9000)
        return tone + crackle * 0.6
    return seamless(gen, 2.0, 0.5)


@sound('laser_charge_full')
def _():
    dur = 0.9
    ding = partials([1320, 1980, 2640, 3960], [0.7, 0.5, 0.3, 0.15], [0.35, 0.25, 0.18, 0.1], dur)
    zap = at(osc(glide(3000, 600, 0.12), None, 'saw') * env_ad(0.12, 0.001, 0.04) * 0.5, 0, dur)
    return fade(reverb(ding + zap, 0.5, 0.2))


def zap(dur, f0, f1, sub=0.0):
    f = glide(f0, f1, dur)
    x = osc(f, None, 'saw') * 0.5 + osc(f * 1.5, None, 'square') * 0.2
    x = sweep_lp(x, 8000, 600) * env_ad(dur, 0.002, dur / 3)
    x += bp(noise(dur), 2000, 8000) * env_ad(dur, 0.001, dur / 8) * 0.4
    if sub:
        x += osc(glide(140, 45, dur), None) * env_ad(dur, 0.003, dur / 2.5) * sub
    return x


@sound('laser_shot_weak')
def _():
    return fade(zap(0.18, 1900, 320))


@sound('laser_shot_strong')
def _():
    return fade(zap(0.42, 2600, 110, sub=1.2))


@sound('laser_blast')
def _():
    dur = 1.6
    boom = lp(brown(dur), 400) * env_ad(dur, 0.005, 0.35) * 1.4
    sub = osc(glide(70, 30, dur), None) * env_ad(dur, 0.003, 0.3) * 1.2
    crack = hp(noise(dur), 1500) * env_ad(dur, 0.001, 0.06) * 0.6
    debris = lp(hp(noise(dur), 1500) * (rng.random(int(SR * dur)) < 0.004) * 4, 6000) * env_ad(dur, 0.05, 0.4)
    return fade(reverb(mix(boom, sub, crack, debris), 0.9, 0.25, 2500))


@sound('laser_fizzle')
def _():
    dur = 0.32
    n = int(SR * dur)
    gate = np.repeat(rng.random(n // 400 + 1) < 0.55, 400)[:n].astype(float)
    sput = lp(bp(noise(dur), 600, 4000) * gate, 3000) * env_ad(dur, 0.003, 0.12)
    blip = osc(glide(420, 140, dur), None, 'square') * env_ad(dur, 0.002, 0.06) * 0.25
    return fade(sput + lp(blip, 2000))


@sound('hit_marker', peak_db=-6)
def _():
    dur = 0.08
    return fade(partials([2100, 3200], [0.6, 0.4], [0.02, 0.015], dur))


@sound('bolt_impact')
def _():
    dur = 0.32
    pop = bp(noise(dur), 900, 5000) * env_ad(dur, 0.001, 0.04)
    thump = osc(glide(320, 90, 0.12), None) * env_ad(0.12, 0.001, 0.04)
    sparks = hp(noise(dur), 4000) * (rng.random(int(SR * dur)) < 0.01) * 3 * env_ad(dur, 0.01, 0.1)
    return fade(mix(pop, thump, sparks * 0.5))


# ---------------------------------------------------------------- player

def grunt(f0a, f0b, dur, vowel, rough=0.2):
    f0 = glide(f0a, f0b, dur) * (1 + 0.02 * rng.standard_normal(int(SR * dur)).cumsum() / 300)
    v = formant_voice(f0, None, VOWELS[vowel], breath=0.08)
    v *= 1 + rough * lp(noise(dur), 60)
    return v * env_ad(dur, 0.012, dur / 2.5)


for i, (a, b, d, vw) in enumerate([(150, 105, 0.24, 'uh'), (165, 115, 0.2, 'o'), (140, 95, 0.28, 'a')], start=1):
    def _make(a=a, b=b, d=d, vw=vw):
        dur = d + 0.12
        hit = lp(noise(0.12), 700) * env_ad(0.12, 0.001, 0.03) * 0.8
        return fade(mix(hit, at(grunt(a, b, d, vw), 0.02, dur)))
    sound(f'player_hurt_{i}')(_make)


@sound('player_jump', peak_db=-4)
def _():
    dur = 0.22
    cloth = whoosh(0.18, 300, 1400, 0.35, q=1.5) * 0.7
    hup = grunt(190, 170, 0.1, 'uh', 0.1) * 0.5
    return fade(mix(cloth, hup))


@sound('player_land', peak_db=-3)
def _():
    dur = 0.3
    thud = osc(glide(95, 45, dur), None) * env_ad(dur, 0.002, 0.06)
    grit = lp(noise(dur), 1800) * env_ad(dur, 0.001, 0.05) * 0.7
    return fade(thud + grit)


def step(kind):
    if kind == 'grass':
        dur = 0.2
        x = np.zeros(int(SR * dur))
        for _ in range(rng.integers(4, 8)):
            burst = hp(noise(0.03), 1800) * env_ad(0.03, 0.001, 0.008)
            x += at(burst * rng.uniform(0.3, 1.0), rng.uniform(0, 0.09), dur)
        x += lp(noise(dur), 300) * env_ad(dur, 0.002, 0.03) * 0.6
        return x
    if kind == 'sand':
        dur = 0.25
        x = bp(noise(dur), 350, 2500) * env_bell(dur, 0.25, 1.5)
        x += lp(noise(dur), 200) * env_ad(dur, 0.003, 0.04) * 0.5
        return x
    dur = 0.18
    x = osc(glide(110, 60, dur), None) * env_ad(dur, 0.002, 0.03) * 0.8
    x += bp(noise(dur), 800, 4000) * env_ad(dur, 0.001, 0.025) * 0.6
    for _ in range(3):
        x += at(hp(noise(0.01), 3000) * env_ad(0.01, 0.0005, 0.003) * 0.4, rng.uniform(0.01, 0.06), dur)
    return x


for kind in ('grass', 'sand', 'dirt'):
    for i in range(1, 5):
        sound(f'footstep_{kind}_{i}', peak_db=-2)(lambda kind=kind: fade(step(kind)))


@sound('death_sting')
def _():
    dur = 2.0
    f = glide(440, 90, 1.4)
    synth = osc(f, None, 'saw') * 0.5 + osc(f * 1.005, None, 'saw') * 0.5
    synth = sweep_lp(synth, 5000, 250) * env_ad(1.4, 0.01, 0.6)
    static = bp(noise(dur), 1500, 7000) * env_bell(dur, 0.15, 1.2) * 0.25
    whine = osc(glide(2400, 200, 1.1), None) * env_ad(1.1, 0.01, 0.4) * 0.15
    return fade(reverb(mix(synth, static, at(whine, 0.5, dur)), 1.0, 0.3, 3000), 0.01, 0.2)


@sound('blackout_whoosh')
def _():
    dur = 2.2
    t = T(dur)
    swell = lp(pink(dur), 700) * env_bell(dur, 0.55, 1.5)
    wob = osc(glide(260, 70, dur) * (1 + 0.05 * np.sin(2 * np.pi * 3 * t)), None) * env_bell(dur, 0.4, 1.2) * 0.4
    return fade(reverb(swell + wob, 1.2, 0.35, 2000), 0.05, 0.3)


@sound('wake_ringing', peak_db=-4)
def _():
    dur = 4.5
    t = T(dur)
    ring = osc(4200, dur) * np.exp(-t / 1.6) * 0.25
    drone = lp(brown(dur), 220) * (0.4 + 0.6 * np.exp(-t / 2.0)) * 0.8
    beats = np.zeros_like(t)
    for s in np.arange(0.2, dur - 0.4, 1.05):
        for off, g in ((0, 1.0), (0.22, 0.7)):
            beat = osc(glide(65, 40, 0.15), None) * env_ad(0.15, 0.004, 0.04)
            beats += at(beat * g, s + off, dur)
    return fade(mix(ring, drone, beats * 0.9), 0.3, 0.8)


# ---------------------------------------------------------------- flask

@sound('flask_uncork')
def _():
    dur = 0.2
    pop = osc(glide(950, 280, 0.05), None) * env_ad(0.05, 0.0005, 0.012)
    click = hp(noise(0.02), 2000) * env_ad(0.02, 0.0003, 0.003) * 0.6
    air = bp(noise(dur), 1500, 5000) * env_ad(dur, 0.005, 0.05) * 0.15
    return fade(mix(pop, click, air))


@sound('flask_glug')
def _():
    dur = 0.8
    x = np.zeros(int(SR * dur))
    for i, s in enumerate((0.0, 0.2, 0.42)):
        b = osc(glide(180 + i * 25, 520 + i * 40, 0.08), None) * env_bell(0.08, 0.3, 1.5)
        b += lp(noise(0.08), 400) * env_bell(0.08, 0.2, 1.5) * 0.4
        x += at(b, s, dur)
    return fade(lp(x, 2500))


@sound('flask_ahh')
def _():
    dur = 0.75
    f0 = glide(125, 100, dur)
    v = formant_voice(f0, None, VOWELS['a'], breath=0.25) * env_ad(dur, 0.05, 0.3, hold=0.1)
    return fade(v, 0.02, 0.15)


@sound('flask_hiccup')
def _():
    dur = 0.16
    f0 = glide(230, 320, dur)
    v = formant_voice(f0, None, VOWELS['i'], breath=0.1) * env_ad(dur, 0.002, 0.04)
    return fade(v)


@sound('flask_burp')
def _():
    dur = 0.65
    n = int(SR * dur)
    f0 = glide(95, 70, dur) * (1 + 0.15 * lp(rng.standard_normal(n), 30) * 10)
    v = formant_voice(np.clip(f0, 40, 200), None, VOWELS['o'], breath=0.1)
    v *= 1 + 0.8 * lp(noise(dur), 45)
    return fade(lp(v * env_ad(dur, 0.02, 0.25, hold=0.1), 2200), 0.01, 0.1)


# ---------------------------------------------------------------- gear, scrap, items

def servo(f0, f1, dur):
    whir = osc(glide(f0, f1, dur), None, 'saw') * 0.4 + osc(glide(f0, f1, dur) * 2, None) * 0.2
    whir = lp(whir, 2500) * env_bell(dur, 0.5, 0.7)
    click = at(hp(noise(0.02), 2500) * env_ad(0.02, 0.0005, 0.004), dur - 0.03, dur)
    return whir + click * 0.8


@sound('scanner_raise', peak_db=-3)
def _():
    return fade(servo(280, 900, 0.32))


@sound('scanner_lower', peak_db=-3)
def _():
    return fade(servo(900, 260, 0.28))


@sound('scanner_ping', peak_db=-9)
def _():
    dur = 0.16
    return fade(partials([1050, 2100, 3150], [0.8, 0.25, 0.08], [0.05, 0.03, 0.02], dur) * np.minimum(1, T(dur) / 0.002))


@sound('item_pickup', peak_db=-2)
def _():
    notes = [1046.5, 1318.5, 1568.0, 2093.0]
    dur = 0.75
    x = np.zeros(int(SR * dur))
    for i, f in enumerate(notes):
        tone = (osc(f, 0.35, 'tri') * 0.6 + osc(f * 2, 0.35) * 0.2) * env_ad(0.35, 0.003, 0.12)
        x += at(tone, i * 0.07, dur)
    return fade(reverb(x, 0.5, 0.2))


for i in range(1, 4):
    def _clink(i=i):
        base = rng.uniform(1800, 3200)
        dur = 0.3
        return fade(partials([base, base * 1.63, base * 2.38, base * 3.1], [0.7, 0.45, 0.3, 0.15],
                             [0.09, 0.06, 0.045, 0.03], dur))
    sound(f'scrap_clink_{i}', peak_db=-3)(_clink)


@sound('scrap_vacuum', peak_db=-3)
def _():
    dur = 0.35
    k = np.linspace(0, 1, int(SR * dur))
    s = sweep_bp(noise(dur), 400 * (3000 / 400) ** k, q=3) * env_bell(dur, 0.7, 1.2)
    pop = at(osc(glide(700, 1300, 0.04), None) * env_ad(0.04, 0.001, 0.01), dur - 0.05, dur)
    return fade(s + pop * 0.6)


# ---------------------------------------------------------------- creatures

@sound('crab_skitter', loop=True, peak_db=-3)
def _():
    def gen(d):
        t = T(d)
        x = np.zeros_like(t)
        time = 0.0
        while time < d:
            click = bp(noise(0.008), 2500, 6000) * env_ad(0.008, 0.0003, 0.0015)
            x += at(click * rng.uniform(0.4, 1.0), time, d)
            time += rng.uniform(0.045, 0.085)
        return x
    return seamless(gen, 1.2, 0.2)


@sound('crab_snip')
def _():
    dur = 0.3
    clack1 = (hp(noise(0.015), 3000) * env_ad(0.015, 0.0003, 0.004) +
              partials([2900, 4300], [0.5, 0.3], [0.03, 0.02], 0.015))
    clack2 = clack1 * 1.2
    squeak = osc(glide(1800, 2400, 0.06), None) * env_bell(0.06, 0.3, 1.5) * 0.15
    return fade(mix(at(clack1, 0, dur), at(clack2, 0.07, dur), at(squeak, 0.08, dur)))


for i in range(1, 3):
    def _clang(i=i):
        dur = 0.7
        s = 1 + (i - 1) * 0.12
        bucket = partials([380 * s, 1020 * s, 1750 * s, 2610 * s, 3400 * s], [0.8, 0.6, 0.4, 0.25, 0.1],
                          [0.25, 0.18, 0.12, 0.08, 0.05], dur)
        thump = lp(noise(0.08), 600) * env_ad(0.08, 0.001, 0.02) * 0.8
        return fade(mix(bucket, thump))
    sound(f'crab_hit_{i}')(_clang)


@sound('crab_block')
def _():
    dur = 0.6
    ring = partials([1450, 2320, 3700, 5100], [0.7, 0.5, 0.3, 0.2], [0.25, 0.18, 0.1, 0.06], dur)
    spark = hp(noise(dur), 5000) * env_ad(dur, 0.0005, 0.03) * 0.5
    return fade(ring + spark)


@sound('crab_death')
def _():
    dur = 0.6
    t = T(0.45)
    f = np.interp(t, [0, 0.1, 0.45], [1400, 2300, 800]) * (1 + 0.03 * np.sin(2 * np.pi * 28 * t))
    eek = osc(f, None) * env_ad(0.45, 0.01, 0.15, 0.1) * 0.7
    deflate = bp(noise(dur), 800, 3000) * env_ad(dur, 0.1, 0.15) * 0.25
    return fade(mix(eek, at(deflate, 0.2, dur)))


@sound('crab_whimper')
def _():
    dur = 1.1
    x = np.zeros(int(SR * dur))
    for i, s in enumerate((0.0, 0.35, 0.68)):
        t = T(0.28)
        f = np.interp(t, [0, 0.28], [1500 - i * 120, 1050 - i * 120]) * (1 + 0.04 * np.sin(2 * np.pi * 9 * t))
        x += at(osc(f, None) * env_bell(0.28, 0.2, 1.3) * 0.6, s, dur)
    return fade(x)


# ---------------------------------------------------------------- shop

@sound('shop_chaching')
def _():
    dur = 1.3
    kachunk = mix(at(lp(noise(0.05), 1500) * env_ad(0.05, 0.001, 0.012), 0, dur),
                  at(bp(noise(0.04), 1000, 4000) * env_ad(0.04, 0.001, 0.01), 0.09, dur))
    bell = at(partials([2093, 2637, 4186, 5274], [0.7, 0.6, 0.3, 0.15], [0.5, 0.45, 0.25, 0.15], 1.1), 0.12, dur)
    coins = np.zeros(int(SR * dur))
    for s in np.linspace(0.18, 0.5, 6):
        base = rng.uniform(3000, 5200)
        coins += at(partials([base, base * 1.5], [0.25, 0.15], [0.05, 0.03], 0.12), s + rng.uniform(0, 0.03), dur)
    return fade(reverb(mix(kachunk, bell, coins), 0.6, 0.2))


@sound('shop_bell')
def _():
    dur = 1.4
    d1 = partials([1568, 3136, 4700], [0.7, 0.3, 0.1], [0.4, 0.25, 0.12], 1.0)
    d2 = partials([1318.5, 2637, 3955], [0.7, 0.3, 0.1], [0.5, 0.3, 0.15], 1.2)
    return fade(reverb(mix(d1, at(d2, 0.22, dur)), 0.6, 0.2))


# ---------------------------------------------------------------- UI

@sound('ui_hover', peak_db=-10)
def _():
    return fade(osc(1700, 0.03) * env_ad(0.03, 0.001, 0.008))


@sound('ui_click', peak_db=-5)
def _():
    dur = 0.07
    return fade(osc(glide(850, 1250, dur), None, 'tri') * env_ad(dur, 0.001, 0.02) +
                hp(noise(dur), 3000) * env_ad(dur, 0.0005, 0.003) * 0.3)


@sound('ui_open', peak_db=-6)
def _():
    dur = 0.14
    return fade(osc(glide(320, 760, dur), None, 'tri') * env_ad(dur, 0.004, 0.05) +
                bp(noise(dur), 800, 3000) * env_bell(dur, 0.4, 1.5) * 0.2)


@sound('ui_close', peak_db=-7)
def _():
    dur = 0.12
    return fade(osc(glide(700, 330, dur), None, 'tri') * env_ad(dur, 0.003, 0.04))


@sound('quest_chime', peak_db=-3)
def _():
    dur = 1.5
    x = np.zeros(int(SR * dur))
    for i, f in enumerate((783.99, 1046.5, 1318.5)):
        x += at(partials([f, f * 2, f * 3.01], [0.7, 0.25, 0.08], [0.45, 0.25, 0.12], 0.9), i * 0.11, dur)
    return fade(reverb(x, 0.8, 0.3))


@sound('part_fanfare', peak_db=-2)
def _():
    dur = 2.6
    x = np.zeros(int(SR * dur))
    chords = [((523.25, 659.25, 783.99), 0.0, 0.18), ((523.25, 659.25, 783.99), 0.2, 0.18),
              ((698.46, 880.0, 1046.5), 0.42, 0.3), ((783.99, 987.77, 1174.66, 1567.98), 0.78, 1.6)]
    for notes, s, d in chords:
        for f in notes:
            tone = osc(f, d, 'saw') * 0.3 + osc(f * 1.003, d, 'saw') * 0.3
            tone = lp(tone, 3500) * env_ad(d, 0.02, d * 0.8, hold=d * 0.3)
            x += at(tone, s, dur)
    return fade(reverb(x, 1.0, 0.3), 0.005, 0.4)


# ---------------------------------------------------------------- ambience loops

def wind(d, lo=150, hi=900, gust_rate=0.11, depth=0.6):
    t = T(d)
    gust = 1 - depth + depth * (0.5 + 0.25 * np.sin(2 * np.pi * gust_rate * t + 1.3)
                                + 0.25 * np.sin(2 * np.pi * gust_rate * 2.37 * t + 0.4))
    centre = lo + (hi - lo) * (gust - (1 - depth)) / max(depth, 1e-3)
    return sweep_bp(pink(d), centre, q=1.4, block=512) * gust


def birds(d, count):
    x = np.zeros(int(SR * d))
    for _ in range(count):
        kind = rng.integers(3)
        s = rng.uniform(0, d - 1.0)
        if kind == 0:  # quick chirps
            for j in range(rng.integers(2, 5)):
                cd = rng.uniform(0.05, 0.09)
                f = glide(rng.uniform(2800, 3600), rng.uniform(4200, 5200), cd)
                x += at(osc(f, None) * env_bell(cd, 0.3, 1.5) * 0.5, s + j * rng.uniform(0.1, 0.16), d)
        elif kind == 1:  # trill
            td = rng.uniform(0.4, 0.7)
            tt = T(td)
            f = rng.uniform(3500, 4500) * (1 + 0.08 * np.sin(2 * np.pi * rng.uniform(18, 26) * tt))
            x += at(osc(f, None) * env_bell(td, 0.4, 1.0) * 0.3, s, d)
        else:  # two-note call
            for j, f0 in enumerate((rng.uniform(2200, 2600), rng.uniform(1800, 2100))):
                cd = 0.18
                x += at(osc(f0, cd) * env_bell(cd, 0.25, 1.4) * 0.4, s + j * 0.24, d)
    return x


@sound('amb_meadow', loop=True, peak_db=-6)
def _():
    def gen(d):
        insects = hp(noise(d), 6000) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.07 * T(d))) * 0.02
        return wind(d, 120, 700, 0.09, 0.5) * 0.9 + birds(d, 9) * 0.35 + insects
    return seamless(gen, 14.0, 2.0)


@sound('amb_desert', loop=True, peak_db=-5)
def _():
    def gen(d):
        howl = wind(d, 180, 650, 0.07, 0.8)
        hiss = hp(pink(d), 3500) * (0.4 + 0.6 * (0.5 + 0.5 * np.sin(2 * np.pi * 0.13 * T(d)))) * 0.15
        tone = osc(260 + 30 * np.sin(2 * np.pi * 0.05 * T(d)), None) * 0.02
        return howl + hiss + tone
    return seamless(gen, 14.0, 2.0)


@sound('amb_wind', loop=True, peak_db=-7)
def _():
    def gen(d):
        return wind(d, 90, 400, 0.06, 0.5) + lp(brown(d), 120) * 0.3
    return seamless(gen, 12.0, 2.0)


@sound('fire_crackle', loop=True, peak_db=-3)
def _():
    def gen(d):
        n = int(SR * d)
        roar = lp(brown(d), 350) * (0.8 + 0.2 * lp(noise(d), 3)) * 0.8
        hissy = bp(pink(d), 1500, 5000) * 0.05
        x = roar + hissy
        time = 0.0
        while time < d:
            size = rng.uniform(0.2, 1.0)
            cd = rng.uniform(0.004, 0.02)
            pop = bp(noise(cd), rng.uniform(800, 2000), rng.uniform(3000, 9000)) * env_ad(cd, 0.0003, cd / 4)
            x += at(pop * size * 1.2, time, d)
            time += rng.exponential(0.06)
        return x
    return seamless(gen, 8.0, 1.0)


@sound('electric_sizzle', loop=True, peak_db=-4)
def _():
    def gen(d):
        t = T(d)
        hum = (osc(120, d) * 0.5 + osc(240, d) * 0.3 + osc(360, d, 'saw') * 0.1) * 0.25
        sizzle = bp(noise(d), 2500, 9000) * (0.15 + 0.85 * (lp(noise(d), 4) > 0.3)) * 0.25
        x = hum + sizzle
        time = 0.0
        while time < d:
            bd = rng.uniform(0.03, 0.12)
            burst = hp(noise(bd), 3000) * (rng.random(int(SR * bd)) < 0.08) * 3 * env_ad(bd, 0.001, bd / 3)
            x += at(burst, time, d)
            time += rng.exponential(0.5)
        return x
    return seamless(gen, 6.0, 1.0)


@sound('water_lap', loop=True, peak_db=-5)
def _():
    def gen(d):
        t = T(d)
        swell = 0.5 + 0.5 * np.sin(2 * np.pi * 0.35 * t) * np.sin(2 * np.pi * 0.11 * t + 0.7)
        lap = lp(pink(d), 900) * (0.3 + 0.7 * swell ** 2)
        x = lap
        time = 0.0
        while time < d:
            bd = rng.uniform(0.03, 0.07)
            b = osc(glide(rng.uniform(300, 500), rng.uniform(700, 1100), bd), None) * env_bell(bd, 0.3, 1.5)
            x += at(b * rng.uniform(0.05, 0.15), time, d)
            time += rng.exponential(0.4)
        return x
    return seamless(gen, 10.0, 2.0)


# ---------------------------------------------------------------- low health (added 10-23; last, so earlier sounds keep their random draws)

@sound('low_health_heartbeat', loop=True)
def _():
    """A slow, muffled lub-dub (about 70 bpm) that loops while the player is badly hurt."""
    beat = 0.86
    def thump(dur, f0, f1, amp):
        body = osc(glide(f0, f1, dur), None) * env_ad(dur, 0.004, dur * 0.28)
        return lp(body + lp(noise(dur), 160) * env_ad(dur, 0.002, 0.03) * 0.35, 220) * amp
    x = at(thump(0.22, 72, 44, 1.0), 0.0, beat) + at(thump(0.2, 64, 40, 0.7), 0.27, beat)
    return x


# ---------------------------------------------------------------- write

def write_wav(path, x):
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype('<i2').tobytes()
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, (fn, peak_db, loop) in SOUNDS.items():
        x = np.asarray(fn(), dtype=float)
        if not np.all(np.isfinite(x)):
            raise ValueError(name + ' has NaN/inf')
        x = norm(x, peak_db)
        write_wav(os.path.join(out, name + '.wav'), x)
        print(f'{name:24s} {len(x) / SR:6.2f}s {"loop" if loop else ""}')
    print(len(SOUNDS), 'sounds')


if __name__ == '__main__':
    main()
