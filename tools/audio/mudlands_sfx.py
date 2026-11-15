"""Generates the Mudlands' placeholder sound effects (synthesised, no recordings) into Resources/SignalLostAudio.
Run from the repo root: python3 tools/audio/mudlands_sfx.py"""
import numpy as np, wave, os, sys
SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "signal-lost", "Assets", "Audio", "Resources", "SignalLostAudio")
rng = np.random.default_rng(1108)

def t(sec): return np.arange(int(SR * sec)) / SR
def env(n, a=0.005, r=0.2, sec=None):
    x = np.ones(n)
    na, nr = int(SR * a), int(SR * r)
    if na: x[:na] = np.linspace(0, 1, na)
    if nr: x[-nr:] *= np.linspace(1, 0, nr) ** 2
    return x
def lowpass(x, cut):
    a = np.exp(-2 * np.pi * cut / SR); y = np.zeros_like(x); s = 0.0
    for i, v in enumerate(x): s = (1 - a) * v + a * s; y[i] = s
    return y
def noise(sec): return rng.uniform(-1, 1, int(SR * sec))
def saw(f, tt): return 2 * ((f * tt) % 1) - 1
def save(name, x, loop=False):
    x = np.asarray(x, dtype=float)
    peak = np.max(np.abs(x)) or 1
    x = x / peak * 0.9
    if loop:  # crossfade the ends so the loop is seamless
        n = int(SR * 0.05); fade = np.linspace(0, 1, n)
        x[:n] = x[:n] * fade + x[-n:] * (1 - fade); x = x[:-n]
    data = (x * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())

def engine(base, sec=1.2, grit=0.25):
    tt = t(sec); wob = 1 + 0.03 * np.sin(2 * np.pi * 7 * tt)
    ph = np.cumsum(base * wob) / SR
    x = sum((1 / k) * np.sign(np.sin(2 * np.pi * k * ph)) for k in (1, 2, 3)) * 0.4
    x += grit * lowpass(noise(sec), 900)
    x *= 0.75 + 0.25 * np.abs(np.sin(2 * np.pi * base * 0.5 * tt))  # cylinder chug
    return lowpass(x, 1800)

def boom(sec, low=60):
    tt = t(sec); n = noise(sec)
    x = lowpass(n, 700) * np.exp(-tt * 3) + np.sin(2 * np.pi * low * tt * np.exp(-tt * 1.5)) * np.exp(-tt * 4) * 1.2
    x += lowpass(n, 3000) * np.exp(-tt * 18) * 0.6
    return x

def pluck(f, sec, damp=0.996):
    n = int(SR * sec); p = int(SR / f); buf = rng.uniform(-1, 1, p); out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % p]; buf[i % p] = damp * 0.5 * (buf[i % p] + buf[(i + 1) % p])
    return out

os.makedirs(OUT, exist_ok=True)
save("car_engine", engine(48), loop=True)
save("bike_engine", engine(78, grit=0.35), loop=True)
tt = t(0.6); save("car_horn", (np.sign(np.sin(2*np.pi*415*tt)) + np.sign(np.sin(2*np.pi*349*tt))) * env(len(tt), 0.01, 0.08) * 0.5)
tt = t(0.9); save("car_skid", lowpass(noise(0.9), 2500) * (0.6 + 0.4*np.sin(2*np.pi*31*tt)) * env(len(tt), 0.05, 0.3))
save("car_crash", np.concatenate([boom(0.5, 45) + lowpass(noise(0.5), 4000) * np.exp(-t(0.5) * 9)]))
save("car_explode", boom(2.0, 38))
tt = t(0.7); thud = np.sin(2*np.pi*70*tt) * np.exp(-tt*9); boing = np.sin(2*np.pi*(180 + 60*np.sin(2*np.pi*9*tt))*tt) * np.exp(-tt*4) * 0.5
save("car_drop", thud + boing)
tt = t(0.8); glass = sum(np.sin(2*np.pi*f*tt) * np.exp(-tt*(6+i)) for i, f in enumerate((2300, 3100, 4170, 5230)))
save("bottle_smash", glass * 0.4 + lowpass(noise(0.8), 6000) * np.exp(-tt * 14))
tt = t(0.32); honk = np.sign(np.sin(2*np.pi*(330 - 80*tt)*tt)) * env(len(tt), 0.01, 0.08)
save("kart_horn", np.concatenate([honk, np.zeros(int(SR*0.06)), honk]) * 0.6)
tt = t(1.6); growl = lowpass(noise(1.6), 500) * (1 + 0.6*np.sin(2*np.pi*23*tt)) + saw(95 - 25*tt, tt) * 0.5
save("kong_roar", lowpass(growl, 1200) * env(len(tt), 0.08, 0.6))
save("banjo_twang", pluck(196, 0.9, 0.994) + pluck(294, 0.9, 0.994) * 0.6)
tune = np.concatenate([pluck(f, 0.22, 0.993) for f in (392, 330, 294, 330, 392, 392, 392, 0.0001)[:-1]])
save("banjo_tune", tune)
tt = t(2.5); save("gong", sum(np.sin(2*np.pi*f*tt) * np.exp(-tt*d) for f, d in ((110, 0.9), (173, 1.2), (267, 1.6), (409, 2.2), (512, 3))) * env(len(tt), 0.002, 0.4))
tt = t(0.5); save("turtle_whistle", np.sin(2*np.pi*(2400 + 120*np.sign(np.sin(2*np.pi*30*tt)))*tt) * env(len(tt), 0.01, 0.1) + lowpass(noise(0.5), 3000)*0.15)
tt = t(0.18); save("pup_squeak", np.sin(2*np.pi*(900 + 900*tt/0.18)*tt) * env(len(tt), 0.005, 0.06))
tt = t(0.7); save("wood_smash", lowpass(noise(0.7), 2200) * np.exp(-tt*7) + sum(np.sin(2*np.pi*f*tt)*np.exp(-tt*14) for f in (180, 260, 410)) * 0.5)
tt = t(0.45); save("mud_splat", lowpass(noise(0.45), 600) * np.exp(-tt*9) + np.sin(2*np.pi*(120-80*tt)*tt)*np.exp(-tt*10))
tt = t(1.4); save("magic_charge", sum(np.sin(2*np.pi*f*(1+tt*0.6)*tt) for f in (440, 554, 659)) * np.linspace(0.2, 1, len(tt)) * env(len(tt), 0.05, 0.1) * 0.4)
tt = t(0.5); save("hex_zap", np.sin(2*np.pi*(1200*np.exp(-tt*5))*tt) * np.exp(-tt*6))
tt = t(1.0); save("cackle", np.concatenate([np.sin(2*np.pi*(620 + 140*np.sin(2*np.pi*3*s))*s) * np.exp(-s*14) for s in [t(0.12)]*7] + [np.zeros(int(SR*0.16))]) )
tt = t(0.9); save("stone_rise", lowpass(noise(0.9), 400) * env(len(tt), 0.15, 0.3) + np.sin(2*np.pi*50*tt)*0.6*env(len(tt), 0.15, 0.3))
tt = t(0.25); save("chain_swish", lowpass(noise(0.25), 5000) * np.sin(np.pi*tt/0.25) + sum(np.sin(2*np.pi*f*tt)*np.exp(-tt*25) for f in (2600, 3900))*0.3)
tt = t(1.2); save("rev", engine(60, 1.2) * np.linspace(0.4, 1, len(tt)) * env(len(tt), 0.05, 0.25))
print("wrote", len([f for f in os.listdir(OUT) if f.endswith('.wav')]), "wavs in", os.path.abspath(OUT))
