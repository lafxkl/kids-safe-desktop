#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""生成宣传片背景音乐：温暖的 I-V-vi-IV 进行，柔和 pad + 木琴拨奏 + 轻打击。"""
import numpy as np, wave, struct

SR = 44100
DUR = 46.4
BPM = 92.0
BEAT = 60.0 / BPM
BAR = BEAT * 4

rng = np.random.default_rng(7)
N = int(SR * DUR) + SR
L = np.zeros(N)
R = np.zeros(N)


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def add(sig, start, gain=1.0, pan=0.0):
    i = int(start * SR)
    if i >= N:
        return
    sig = sig[: N - i]
    gl = gain * (1 - max(0.0, pan) * 0.55)
    gr = gain * (1 + min(0.0, pan) * 0.55)
    L[i:i + len(sig)] += sig * gl
    R[i:i + len(sig)] += sig * gr


def env_adsr(n, a=0.02, d=0.1, s=0.7, r=0.3):
    e = np.ones(n) * s
    ai = min(n, int(a * SR)); di = min(n - ai, int(d * SR)); ri = min(n - ai - di, int(r * SR))
    if ai: e[:ai] = np.linspace(0, 1, ai) ** 1.5
    if di: e[ai:ai + di] = np.linspace(1, s, di)
    if ri: e[n - ri:] = np.linspace(s, 0, ri)
    return e


def pad(freqs, dur, gain=0.1):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.zeros(n)
    for f in freqs:
        for det in (-0.6, 0.0, 0.7):
            ff = f * (1 + det / 1200.0)
            sig += np.sin(2 * np.pi * ff * t + rng.uniform(0, 6.28))
            sig += 0.22 * np.sin(2 * np.pi * ff * 2 * t + rng.uniform(0, 6.28))
    sig /= (len(freqs) * 3 * 1.22)
    # 一阶低通，柔化高频
    a = 0.22
    out = np.zeros(n)
    acc = 0.0
    for i in range(n):
        acc += a * (sig[i] - acc)
        out[i] = acc
    return out * env_adsr(n, a=1.1, d=0.4, s=0.85, r=1.4) * gain


def pluck(freq, dur=1.0, gain=0.16):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = (np.sin(2 * np.pi * freq * t) * np.exp(-t * 5.2)
           + 0.34 * np.sin(2 * np.pi * freq * 2.01 * t) * np.exp(-t * 8.5)
           + 0.12 * np.sin(2 * np.pi * freq * 3.02 * t) * np.exp(-t * 12))
    return sig * gain * np.minimum(1, t / 0.004)


def bell(freq, dur=2.2, gain=0.09):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = (np.sin(2 * np.pi * freq * t) * np.exp(-t * 1.6)
           + 0.45 * np.sin(2 * np.pi * freq * 2.0 * t) * np.exp(-t * 2.4)
           + 0.2 * np.sin(2 * np.pi * freq * 3.01 * t) * np.exp(-t * 3.4))
    return sig * gain * np.minimum(1, t / 0.006)


def bass(freq, dur, gain=0.2):
    n = int(dur * SR)
    t = np.arange(n) / SR
    sig = np.sin(2 * np.pi * freq * t) + 0.25 * np.sin(2 * np.pi * freq * 2 * t)
    return sig * env_adsr(n, a=0.02, d=0.2, s=0.75, r=0.35) * gain


def shaker(gain=0.05):
    n = int(0.09 * SR)
    noise = rng.normal(0, 1, n)
    noise = np.diff(np.concatenate([[0], noise]))  # 高通化
    e = np.exp(-np.arange(n) / SR * 55)
    return noise * e * gain * 0.4


def kick(gain=0.16):
    n = int(0.42 * SR)
    t = np.arange(n) / SR
    f = 96 * np.exp(-t * 16) + 46
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.exp(-t * 7.5) * gain


# ---- 和弦：C - G/B - Am - F（加七度，温暖） ----
CH = [
    [48, 55, 60, 64, 67],   # C
    [47, 54, 59, 62, 67],   # G/B
    [45, 52, 57, 60, 64],   # Am
    [41, 48, 53, 57, 60],   # F
]
TOP = [72, 71, 69, 65]      # 每小节的旋律最高音

n_bars = int(np.ceil(DUR / BAR)) + 1
for b in range(n_bars):
    t0 = b * BAR
    if t0 > DUR:
        break
    ci = b % 4
    ch = CH[ci]
    # pad：每小节铺底
    add(pad([midi(x) for x in ch[1:]], BAR * 1.05, gain=0.30),
        t0, gain=1.0, pan=0.0)
    # bass：根音，1、3 拍
    root = midi(ch[0] - 12)
    add(bass(root, BEAT * 1.7, 0.22), t0, pan=-0.05)
    add(bass(root, BEAT * 1.4, 0.14), t0 + BEAT * 2, pan=-0.05)
    # 木琴拨奏：八分音符琶音
    if t0 >= 3.4:
        pat = [1, 2, 3, 4, 3, 2, 4, 3]
        for k, idx in enumerate(pat):
            tk = t0 + k * BEAT / 2
            if tk > DUR - 1.2:
                continue
            note = ch[min(idx, len(ch) - 1)] + 12
            pan = 0.35 if k % 2 == 0 else -0.35
            add(pluck(midi(note), 0.9, 0.115), tk, pan=pan)
    # 打击
    if t0 >= 3.4:
        add(kick(0.13), t0, pan=0)
        add(kick(0.08), t0 + BEAT * 2.5, pan=0)
        for k in range(8):
            add(shaker(0.055 if k % 2 else 0.03), t0 + k * BEAT / 2, pan=0.2)
    # 高音铃：每两小节一次
    if b % 2 == 1 and t0 >= 3.4:
        add(bell(midi(TOP[ci] + 12), 2.4, 0.055), t0 + BEAT * 3, pan=0.25)

# ---- 简单空间感：多抽头延迟 ----
mix = np.stack([L, R])
for tap, g, side in ((0.083, 0.22, 0), (0.149, 0.15, 1), (0.223, 0.09, 0)):
    d = int(tap * SR)
    sh = np.zeros_like(mix)
    sh[:, d:] = mix[:, :-d] * g
    sh = np.roll(sh, side, axis=0)
    mix = mix + sh

# ---- 结尾渐弱 / 开头渐入 ----
t = np.arange(mix.shape[1]) / SR
fade_in = np.clip(t / 2.0, 0, 1) ** 1.4
fade_out = np.clip((DUR - t) / 3.2, 0, 1) ** 1.2
mix *= np.minimum(fade_in, fade_out)

mix = mix[:, : int(DUR * SR)]
peak = np.max(np.abs(mix))
mix = mix / peak * 0.72
# 轻微软限幅
mix = np.tanh(mix * 1.15) / np.tanh(1.15) * 0.72

pcm = (mix.T * 32767).astype(np.int16)
with wave.open("music.wav", "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(pcm.tobytes())
print("music.wav", pcm.shape, f"{DUR}s")
