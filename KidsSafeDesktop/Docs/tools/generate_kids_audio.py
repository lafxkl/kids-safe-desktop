#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
儿童安全桌面 —— 「听一听」离线音频生成器

用途：为媒体中心合成可直接播放的 WAV 音频，随应用打包，无需联网、无版权风险。

产出：
  Resources/Raw/Audio/*.wav

两类内容：
  1) 儿歌：仅使用公有领域（Public Domain）的传统曲调，以音乐盒音色合成。
     之所以自己合成而不是外挂第三方网站：
       · 第三方站内嵌有登录墙 / VIP 弹窗 / 成人向推荐，不适合儿童模式；
       · 网站改版会导致链接失效；
       · 离线可在无网环境（飞行模式、儿童平板）正常使用。
  2) 环境音（雨声 / 海浪 / 白噪音）：滤波噪声合成，用于安睡助眠，循环播放。

调参：改 SR（采样率）可显著影响体积：16000Hz/16bit/单声道 ≈ 32KB/秒。
"""

import os
import sys
import numpy as np

# ---------------------------------------------------------------- 基本参数
SR = 16000          # 采样率：语音乐音足够，且体积可控
NOTE_BASE = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}


def out_dir():
    """定位输出目录：始终写到 KidsSafeDesktop/Resources/Raw/Audio。"""
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.abspath(os.path.join(here, '..', '..'))
    d = os.path.join(root, 'Resources', 'Raw', 'Audio')
    os.makedirs(d, exist_ok=True)
    return d


def note_freq(name):
    """音名 -> 频率。C4 为中央 C（MIDI 60）。"""
    if name == '-':
        return None
    step = NOTE_BASE[name[0].upper()]
    octave = int(name[1:])
    midi = (octave + 1) * 12 + step
    return 440.0 * (2 ** ((midi - 69) / 12.0))


# ---------------------------------------------------------------- 音色合成
def bell_tone(freq, dur, sr=SR, volume=0.5):
    """音乐盒 / 钟琴音色：基频 + 若干谐波，快速起振 + 指数衰减。"""
    n = int(sr * dur)
    if n <= 0:
        return np.zeros(0)
    t = np.arange(n) / sr

    sig = (np.sin(2 * np.pi * freq * t)
           + 0.45 * np.sin(2 * np.pi * 2 * freq * t)
           + 0.20 * np.sin(2 * np.pi * 3 * freq * t)
           + 0.09 * np.sin(2 * np.pi * 5.1 * freq * t))

    # 衰减时间常数与音长相关，保证短音快速收敛、长音有余韵
    tau = max(dur * 0.55, 0.18)
    env = np.exp(-t / tau)

    # 起振（消除爆音）+ 收尾淡出（消除截断噪声）
    attack = np.clip(t / 0.006, 0.0, 1.0)
    release = np.clip((dur - t) / 0.035, 0.0, 1.0)
    env = env * attack * release

    return sig * env * volume


def soft_pad(freq, dur, sr=SR, volume=0.18):
    """柔和衬底音色：纯正弦 + 缓慢起振收尾，给旋律垫底增加厚度。"""
    n = int(sr * dur)
    if n <= 0:
        return np.zeros(0)
    t = np.arange(n) / sr
    sig = np.sin(2 * np.pi * freq * t)
    env = np.sin(np.pi * np.clip(t / dur, 0.0, 1.0)) ** 0.6  # 平滑纺锤形包络
    return sig * env * volume


def render_melody(notes, bpm, sr=SR, pad=True, gap_ratio=0.02):
    """
    渲染一段旋律。
    notes: [(音名或 '-', 拍数), ...]  拍数以四分音符为 1
    pad:   是否叠加低八度衬底
    """
    spb = 60.0 / bpm
    parts = []
    for name, beats in notes:
        dur = beats * spb
        if dur <= 0:
            continue
        # 音符之间的极小间隙，让颗粒感更清晰
        audible = dur * (1 - gap_ratio)
        if name == '-':
            parts.append(np.zeros(int(sr * dur)))
            continue
        f = note_freq(name)
        parts.append(bell_tone(f, audible, sr))
        # 补上间隙
        parts.append(np.zeros(int(sr * dur) - int(sr * audible)))
    melody = np.concatenate(parts) if parts else np.zeros(0)

    if pad:
        pad_parts = []
        for name, beats in notes:
            dur = beats * spb
            if name == '-':
                pad_parts.append(np.zeros(int(sr * dur)))
                continue
            f = note_freq(name) / 2.0  # 低八度
            pad_parts.append(soft_pad(f, dur, sr))
        accompaniment = np.concatenate(pad_parts) if pad_parts else np.zeros(0)
        m = min(len(melody), len(accompaniment))
        buf = np.zeros(m)
        buf[:m] = melody[:m] + accompaniment[:m]
        # 其余部分按主旋律
        if len(melody) > m:
            buf = np.concatenate([buf, melody[m:]])
        return buf

    return melody


def write_wav(path, data, sr=SR):
    """写入 16bit 单声道 WAV（先归一化再输出，避免削波）。"""
    peak = float(np.max(np.abs(data))) if len(data) else 0.0
    if peak > 0:
        data = data * (0.89 / peak)
    pcm = np.clip(data, -1.0, 1.0)
    pcm = (pcm * 32767.0).astype('<i2')
    with open(path, 'wb') as f:
        data_len = pcm.nbytes
        f.write(b'RIFF')
        f.write((36 + data_len).to_bytes(4, 'little'))
        f.write(b'WAVEfmt ')
        f.write((16).to_bytes(4, 'little'))
        f.write((1).to_bytes(2, 'little'))            # PCM
        f.write((1).to_bytes(2, 'little'))            # mono
        f.write(sr.to_bytes(4, 'little'))
        f.write((sr * 2).to_bytes(4, 'little'))       # byte rate
        f.write((2).to_bytes(2, 'little'))            # block align
        f.write((16).to_bytes(2, 'little'))           # bits
        f.write(b'data')
        f.write(data_len.to_bytes(4, 'little'))
        f.write(pcm.tobytes())


def fade_edges(x, sr=SR, fade=0.35):
    n = min(int(sr * fade), len(x) // 2)
    if n <= 0:
        return x
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def repeat(x, times, sr=SR, tail=0.25):
    """整段重复若干遍，每遍之间留一点呼吸间隙。"""
    if times <= 1:
        return x
    gap = np.zeros(int(sr * tail))
    return np.concatenate([x] + [np.concatenate([gap, x]) for _ in range(times - 1)])


# ---------------------------------------------------------------- 曲库
# 说明：以下全部为公有领域传统曲调 / 民间旋律。
SONGS = [
    ('twinkle', [
        ('C4',1),('C4',1),('G4',1),('G4',1),('A4',1),('A4',1),('G4',2),
        ('F4',1),('F4',1),('E4',1),('E4',1),('D4',1),('D4',1),('C4',2),
        ('G4',1),('G4',1),('F4',1),('F4',1),('E4',1),('E4',1),('D4',2),
        ('G4',1),('G4',1),('F4',1),('F4',1),('E4',1),('E4',1),('D4',2),
        ('C4',1),('C4',1),('G4',1),('G4',1),('A4',1),('A4',1),('G4',2),
        ('F4',1),('F4',1),('E4',1),('E4',1),('D4',1),('D4',1),('C4',2),
    ], 104, 1),

    ('two_tigers', [
        ('C4',1),('D4',1),('E4',1),('C4',1),
        ('C4',1),('D4',1),('E4',1),('C4',1),
        ('E4',1),('F4',1),('G4',2),
        ('E4',1),('F4',1),('G4',2),
        ('G4',0.75),('A4',0.75),('G4',0.75),('F4',0.75),('E4',1),('C4',1),
        ('G4',0.75),('A4',0.75),('G4',0.75),('F4',0.75),('E4',1),('C4',1),
        ('C4',1),('G3',1),('C4',2),
        ('C4',1),('G3',1),('C4',2),
    ], 112, 2),

    ('happy_birthday', [
        ('G4',0.75),('G4',0.25),('A4',1),('G4',1),('C5',1),('B4',2),
        ('G4',0.75),('G4',0.25),('A4',1),('G4',1),('D5',1),('C5',2),
        ('G4',0.75),('G4',0.25),('G5',1),('E5',1),('C5',1),('B4',1),('A4',2),
        ('F5',0.75),('F5',0.25),('E5',1),('C5',1),('D5',1),('C5',2),
    ], 104, 2),

    ('jingle_bells', [
        ('E4',1),('E4',1),('E4',2),
        ('E4',1),('E4',1),('E4',2),
        ('E4',1),('G4',1),('C4',1.5),('D4',0.5),('E4',4),
        ('F4',1),('F4',1),('F4',1),('F4',1),('F4',1),('E4',1),('E4',1),('E4',0.5),('E4',0.5),
        ('E4',1),('D4',1),('D4',1),('E4',1),('D4',2),('G4',2),
    ], 118, 2),

    ('new_year', [
        ('C4',1),('C4',1),('C4',1),('G3',1),
        ('E4',1),('E4',1),('E4',1),('C4',1),
        ('C4',1),('E4',1),('G4',1),('G4',1),
        ('F4',1),('E4',1),('D4',2),
        ('D4',1),('E4',1),('F4',1),('F4',1),
        ('E4',1),('D4',1),('E4',1),('C4',1),
        ('C4',1),('G3',1),('E4',1),('C4',2),
    ], 110, 2),

    ('mary_lamb', [
        ('E4',1),('D4',1),('C4',1),('D4',1),('E4',1),('E4',1),('E4',2),
        ('D4',1),('D4',1),('D4',2),
        ('E4',1),('E4',1),('E4',2),
        ('E4',1),('D4',1),('C4',1),('D4',1),('E4',1),('E4',1),('E4',1),('E4',1),
        ('D4',1),('D4',1),('E4',1),('D4',1),('C4',3),
    ], 116, 2),

    ('london_bridge', [
        ('G4',1),('A4',1),('G4',1),('F4',1),('E4',1),('F4',1),('G4',2),
        ('D4',1),('E4',1),('F4',2),
        ('E4',1),('F4',1),('G4',2),
        ('G4',1),('A4',1),('G4',1),('F4',1),('E4',1),('F4',1),('G4',2),
        ('D4',1),('G4',1),('E4',1),('C4',2),
    ], 110, 2),

    ('little_bee', [
        ('G4',1),('E4',1),('E4',2),
        ('F4',1),('D4',1),('D4',2),
        ('C4',1),('D4',1),('E4',1),('F4',1),('G4',1),('G4',1),('G4',2),
        ('G4',1),('E4',1),('E4',1),('E4',1),('F4',1),('D4',1),('D4',2),
        ('C4',1),('E4',1),('G4',1),('G4',1),('E4',2),
    ], 116, 2),
]


# ---------------------------------------------------------------- 环境音
def pink_noise(n, rng):
    """Voss-McCartney 近似的粉红噪声，比白噪音柔和。"""
    rows = 16
    state = rng.normal(0, 1, rows)
    out = np.empty(n)
    for i in range(n):
        k = int(rng.random() * rows)
        # 每次随机更新少量行，形成 1/f 特性
        if rng.random() < 0.25:
            state[k] = rng.normal(0, 1)
        out[i] = state.sum() / rows
    return out


def gen_rain(dur=22.0, sr=SR):
    rng = np.random.default_rng(11)
    n = int(sr * dur)
    x = rng.normal(0, 1, n)
    # 低通：雨声以中低频沙沙为主
    b = 0.06
    lp = np.zeros(n)
    acc = 0.0
    for i in range(n):
        acc += b * (x[i] - acc)
        lp[i] = acc
    x = lp
    # 叠一层极缓慢的强度起伏，避免死板
    t = np.arange(n) / sr
    x *= 0.82 + 0.18 * np.sin(2 * np.pi * t / 9.0)
    return fade_edges(x * 0.55, sr, 1.2)


def gen_waves(dur=22.0, sr=SR):
    rng = np.random.default_rng(23)
    n = int(sr * dur)
    x = pink_noise(n, rng)
    # 低通得到 Brown 质感的低沉水声
    b = 0.035
    lp = np.zeros(n)
    acc = 0.0
    for i in range(n):
        acc += b * (x[i] - acc)
        lp[i] = acc
    lp *= 6.0
    t = np.arange(n) / sr
    # 6.5 秒周期的浪涌包络
    swell = 0.45 + 0.55 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 6.5 - np.pi / 2)) ** 1.4
    return fade_edges(lp * swell * 0.5, sr, 1.2)


def gen_whitenoise(dur=22.0, sr=SR):
    rng = np.random.default_rng(37)
    n = int(sr * dur)
    x = pink_noise(n, rng)
    x *= 0.42
    return fade_edges(x, sr, 1.2)


AMBIENTS = [
    ('rain', gen_rain),
    ('waves', gen_waves),
    ('whitenoise', gen_whitenoise),
]


# ---------------------------------------------------------------- 主流程
def main():
    d = out_dir()
    print('output ->', d)
    total = 0

    for name, notes, bpm, times in SONGS:
        buf = render_melody(notes, bpm)
        buf = repeat(buf, times)
        buf = fade_edges(buf, SR, 0.5)
        p = os.path.join(d, 'song_%s.wav' % name)
        write_wav(p, buf)
        size = os.path.getsize(p)
        total += size
        print('  %-18s %6.1fs  %7.2f MB' % ('song_%s.wav' % name, len(buf) / SR, size / 1048576.0))

    for name, fn in AMBIENTS:
        buf = fn()
        p = os.path.join(d, 'amb_%s.wav' % name)
        write_wav(p, buf)
        size = os.path.getsize(p)
        total += size
        print('  %-18s %6.1fs  %7.2f MB' % ('amb_%s.wav' % name, len(buf) / SR, size / 1048576.0))

    print('total: %.2f MB' % (total / 1048576.0))
    return 0


if __name__ == '__main__':
    sys.exit(main())
