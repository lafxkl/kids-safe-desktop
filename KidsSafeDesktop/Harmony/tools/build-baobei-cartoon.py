#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
宝贝三十六计 · 卡通素材重绘

原工程的素材是「真人照片抠图 + 一张实景城市地图」：
  buddy.png / e1..e4.png 是 5 帧走路序列图（照片抠的），world.jpg 是俯瞰城市实景，
  zoo_p/hosp_p/school_p/shop_p 是场景照片，win.png / lose.png 是真人照片配红字。

把它们全部换成手绘 SVG：
  1. 体积：照片合计约 700KB，SVG 全部加起来不到 120KB（且不失真、放大到平板也清楚）；
  2. 风格：孩子看到的是「照片里皱着眉的大人」，卡通化之后才是绘本；
  3. 可控：人物配色、表情、动作都是参数，改一处不用重新做图。

为什么用脚本生成而不是手写 SVG：
  5 个角色 × 5 帧 = 25 张图，手写会立刻长歪（这个头大 2px、那个胳膊短 3px），
  而它们必须在同一条基线上、同一个头身比。所以身体只写一份，靠参数换配色和配件。

尺寸约定（改之前先看这里）：
  * 角色帧 92×128，5 帧横排 → viewBox 0 0 460 128。
    CSS 里 `.actor img { height:100%; width:auto }` + `translateX(-f*20%)`，
    要求每帧等宽，所以总量必须正好是 5 倍帧宽。
  * 地图 1088×768，与原来的 world.jpg 同比例；入口标记按百分比定位，坐标不能变。
  * 场景 1024×576，`background: center/cover`，人物站在画面底部 36% 之上。

用法：python Harmony/tools/build-baobei-cartoon.py
"""

import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   '..', 'entry', 'src', 'main', 'resources', 'rawfile', 'playland', 'baobei36', 'img')

FRAME_W, FRAME_H = 92, 128


# ─────────────────────────── 人物 ───────────────────────────
class Who:
    """一个角色的外观参数。只写「长什么样」，动作由 frame 决定。"""

    def __init__(self, skin, hair, shirt, pants, shoe, style='boy',
                 hair_color=None, extra='', hood=False, coat=False, cap=None):
        self.skin = skin
        self.hair = hair if hair_color is None else hair_color
        self.shirt = shirt
        self.pants = pants
        self.shoe = shoe
        self.style = style      # boy / cap / glasses / hood / doctor
        self.extra = extra      # 额外图元（挂在最上层）
        self.hood = hood
        self.coat = coat
        self.cap = cap if cap else shirt


def shade(hex_color, f):
    """把颜色压暗一点。手臂和身体同色时，整块糊成一只麻袋 —— 差 12% 明度就分得开。"""
    h = hex_color.lstrip('#')
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    r = max(0, min(255, int(r * f)))
    g = max(0, min(255, int(g * f)))
    b = max(0, min(255, int(b * f)))
    return '#%02X%02X%02X' % (r, g, b)


KID = Who('#F7CFA8', '#2B2119', '#C6E85A', '#6C8CD5', '#3E4A5B', style='boy')
# 四个「陌生人」。都不做凶脸 —— 吓人的坏人是反效果，孩子记住的是「可疑 ≠ 可怕」。
FOE1 = Who('#EFC49A', '#3A2A1E', '#8E9AA8', '#4B5563', '#2F3540', style='cap', cap='#46525F')
FOE2 = Who('#F1C9A5', '#241C17', '#F7F9FB', '#34506B', '#2B3440', style='doctor')
FOE3 = Who('#E8BC92', '#241C17', '#3E5566', '#3A4757', '#232A33', style='cap', cap='#2F4351',
            extra='<rect x="61" y="68" width="8" height="4.5" rx="2" fill="#FFD76E"/>')
FOE4 = Who('#F2C79E', '#1F1A16', '#8C5A3C', '#3F4B5B', '#2B3440', style='hood', hood=True)


def figure(who, frame):
    """
    一帧 = 92×128。动作只有「走路」：腿前后摆、手臂反向摆、身体轻微上下。

    版面分配（改坐标前先看这张表，五帧共用同一条基线）：
        头   cy=34, r=23      →  11..57
        身体 y=56..94, x=30..62
        手臂 肩在 x=27 / 65（画在身体外侧，否则被身体盖住看不见）
        腿   y=90..119，髋在 x=39 / 53
    """
    phase = [0, 15, 0, -15, 0][frame]
    bob = [0, -2, 0, -2, 0][frame]
    tilt = [0, 2, 0, -2, 0][frame]
    leg_a, leg_b = phase, -phase
    arm_a, arm_b = -phase * 0.8, phase * 0.8

    s = []
    s.append(f'<g transform="translate(0,{bob})">')
    # 影子不跟着上下动，否则看起来像在蹦
    s.append('<ellipse cx="46" cy="123" rx="18" ry="4" fill="#2B2119" opacity=".13"/>')

    # ── 腿 ──
    for ang, hip in ((leg_a, 39), (leg_b, 53)):
        s.append(f'<g transform="rotate({ang},{hip},92)">')
        s.append(f'<rect x="{hip - 6.5}" y="90" width="13" height="27" rx="6.5" fill="{who.pants}" '
                 'stroke="rgba(0,0,0,.05)" stroke-width="1"/>')
        s.append(f'<rect x="{hip - 8}" y="112" width="17" height="9" rx="4.5" fill="{who.shoe}"/>')
        s.append('</g>')

    # ── 手臂：肩点放在身体轮廓外面（27 / 65），否则整条胳膊被身体吞掉 ──
    sleeve = shade('#FFFFFF' if who.style == 'doctor' else who.shirt, 0.86)
    for ang, sh in ((arm_a, 27), (arm_b, 65)):
        s.append(f'<g transform="rotate({ang},{sh},62)">')
        s.append(f'<rect x="{sh - 4.5}" y="60" width="9" height="24" rx="4.5" fill="{sleeve}" '
                 'stroke="rgba(0,0,0,.06)" stroke-width="1"/>')
        s.append(f'<circle cx="{sh}" cy="86" r="5" fill="{who.skin}"/>')
        s.append('</g>')

    # ── 身体 ──
    body_fill = '#FFFFFF' if who.style == 'doctor' else who.shirt
    s.append(f'<rect x="30" y="56" width="32" height="38" rx="14" fill="{body_fill}" '
             'stroke="rgba(0,0,0,.06)" stroke-width="1"/>')

    if who.style == 'doctor':
        s.append('<rect x="45" y="56" width="2.4" height="38" fill="#DDE6EE"/>')
        s.append('<path d="M40 56 L46 68 L52 56" fill="none" stroke="#DDE6EE" stroke-width="2.4"/>')
        s.append('<rect x="54" y="74" width="3" height="11" rx="1.5" fill="#E05A4F"/>')
        s.append('<rect x="50" y="78" width="11" height="3" rx="1.5" fill="#E05A4F"/>')
    elif who.hood:
        s.append('<path d="M30 60 q16 -12 32 0 l0 7 q-16 -8 -32 0z" fill="rgba(0,0,0,.14)"/>')
        s.append('<path d="M40 68 l0 9 M52 68 l0 9" stroke="#F3E0CF" stroke-width="2" stroke-linecap="round"/>')
    else:
        s.append('<path d="M40 56 q6 7 12 0" fill="none" stroke="rgba(0,0,0,.12)" stroke-width="3"/>')
        s.append('<rect x="33" y="72" width="26" height="5" rx="2.5" fill="rgba(255,255,255,.55)"/>')

    if who is KID:
        s.append(f'<rect x="35" y="56" width="5" height="15" fill="{who.pants}"/>')
        s.append(f'<rect x="52" y="56" width="5" height="15" fill="{who.pants}"/>')
        s.append(f'<rect x="35" y="68" width="22" height="13" rx="4" fill="{who.pants}"/>')
        s.append('<rect x="40" y="71" width="12" height="7" rx="2" fill="rgba(255,255,255,.35)"/>')

    # ── 头 ──
    s.append(f'<g transform="rotate({tilt},46,56)">')
    s.append(f'<circle cx="46" cy="34" r="23" fill="{who.skin}"/>')
    s.append(f'<circle cx="23" cy="36" r="4.2" fill="{who.skin}"/>')
    s.append(f'<circle cx="69" cy="36" r="4.2" fill="{who.skin}"/>')

    if who.style == 'doctor':
        s.append(f'<path d="M24 32 a22 22 0 0 1 44 0" fill="none" stroke="{who.hair}" '
                 'stroke-width="7" stroke-linecap="round"/>')
    else:
        # 头发：一条沿头顶的粗弧线 + 两侧鬓角，比画整块头发稳（不会盖到眼睛）
        s.append(f'<path d="M24 33 a22 22 0 0 1 44 0" fill="none" stroke="{who.hair}" '
                 'stroke-width="11" stroke-linecap="round"/>')
        s.append(f'<path d="M24 33 q2 -11 9 -7 q-4 2 -3 7z" fill="{who.hair}"/>')
        s.append(f'<path d="M68 33 q-2 -11 -9 -7 q4 2 3 7z" fill="{who.hair}"/>')

    # 眼睛：留一点高光，否则两个黑点看起来像没睡醒
    s.append('<circle cx="38" cy="37" r="3" fill="#2B2119"/>')
    s.append('<circle cx="54" cy="37" r="3" fill="#2B2119"/>')
    s.append('<circle cx="39" cy="36" r="1" fill="#FFFFFF"/>')
    s.append('<circle cx="55" cy="36" r="1" fill="#FFFFFF"/>')

    if who is KID:
        s.append('<path d="M41 44 q5 5 10 0" fill="none" stroke="#2B2119" '
                 'stroke-width="2" stroke-linecap="round"/>')
        s.append('<ellipse cx="30" cy="43" rx="4" ry="2.6" fill="#F3A0A0" opacity=".5"/>')
        s.append('<ellipse cx="62" cy="43" rx="4" ry="2.6" fill="#F3A0A0" opacity=".5"/>')
    else:
        s.append('<path d="M42 46 l8 0" stroke="#2B2119" stroke-width="2" stroke-linecap="round"/>')

    # ── 配件 ──
    if who.style == 'cap':
        s.append(f'<path d="M24 30 a22 22 0 0 1 44 0z" fill="{who.cap}"/>')
        s.append(f'<path d="M22 30 h30 a5 5 0 0 1 0 5 h-30z" fill="{who.cap}"/>')
        s.append(f'<circle cx="46" cy="9" r="3" fill="{who.cap}"/>')
        s.append('<path d="M24 30 a22 22 0 0 1 44 0" fill="none" stroke="rgba(0,0,0,.16)" stroke-width="1.5"/>')
        if who is FOE1:
            # 墨镜做得窄一点、留一道反光：压到鼻梁上会把表情全糊掉
            s.append('<rect x="30" y="33" width="14" height="7" rx="3" fill="#22282F"/>')
            s.append('<rect x="48" y="33" width="14" height="7" rx="3" fill="#22282F"/>')
            s.append('<rect x="44" y="35" width="4" height="2" fill="#22282F"/>')
            s.append('<path d="M32 35 l5 -1" stroke="#6B767F" stroke-width="1.4" stroke-linecap="round"/>')
    if who.style == 'doctor':
        # 额镜：一条横带 + 圆盘。这是「假医生」最好的身份符号，比白大褂还快
        s.append('<path d="M24 26 a23 23 0 0 1 44 0" fill="none" stroke="#9FB6C7" stroke-width="3"/>')
        s.append('<circle cx="46" cy="20" r="5.5" fill="#CFE3F2" stroke="#7E99AD" stroke-width="2"/>')
    if who.style == 'hood':
        # 兜帽画成「环」，把脸框在里面。第一版画的是整块圆，直接把脸盖成黑团
        s.append(f'<path fill-rule="evenodd" fill="{who.shirt}" '
                 'd="M46 5 a29 29 0 1 0 0 58 a29 29 0 1 0 0 -58 z '
                 'M46 12 a22 22 0 1 0 0 44 a22 22 0 1 0 0 -44 z"/>')
        s.append(f'<path d="M46 5 a29 29 0 0 1 0 58" fill="none" stroke="rgba(0,0,0,.18)" stroke-width="2"/>')
        # 兜帽里露出的刘海：没有这撮头发，人物看起来像光头戴头盔
        s.append(f'<path d="M32 22 q7 -6 14 -1 q7 5 14 1" fill="none" stroke="{who.hair}" '
                 'stroke-width="5" stroke-linecap="round"/>')

    s.append(who.extra)
    s.append('</g>')
    s.append('</g>')
    return ''.join(s)


def sprite(who):
    """5 帧横排。每帧必须正好 92 宽，否则 translateX(-f*20%) 会错位。"""
    frames = []
    for f in range(5):
        frames.append(f'<g transform="translate({f * FRAME_W},0)">{figure(who, f)}</g>')
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{FRAME_W * 5}" height="{FRAME_H}" '
            f'viewBox="0 0 {FRAME_W * 5} {FRAME_H}">' + ''.join(frames) + '</svg>')


# ─────────────────────────── 地图 ───────────────────────────
def map_svg():
    """
    俯瞰绘本地图。四个入口标记的坐标由 index.html 的 SCENES.at 给出（百分比），
    所以建筑必须画在那四个点上，否则「动物园」的名字会飘在空地上。

      zoo    at [20,30]  →  218, 230
      hosp   at [76,26]  →  827, 200
      school at [18,70]  →  196, 538
      shop   at [78,66]  →  849, 507
    """
    W, H = 1088, 768
    s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">']

    # 草坪底色 + 两块深浅不同的草地（避免大面积纯色发闷）
    s.append(f'<rect width="{W}" height="{H}" fill="#9ED97C"/>')
    s.append('<ellipse cx="150" cy="700" rx="320" ry="180" fill="#93D071" opacity=".7"/>')
    s.append('<ellipse cx="980" cy="120" rx="260" ry="150" fill="#A7DE86" opacity=".8"/>')

    # 河：从左上绕到右下，给地图一条「水」
    s.append('<path d="M-40 470 C 160 430, 240 520, 420 500 S 760 420, 900 470 S 1060 560, 1140 520" '
             'fill="none" stroke="#8FD7EA" stroke-width="46" stroke-linecap="round"/>')
    s.append('<path d="M-40 470 C 160 430, 240 520, 420 500 S 760 420, 900 470 S 1060 560, 1140 520" '
             'fill="none" stroke="#B6E7F3" stroke-width="26" stroke-linecap="round" opacity=".75"/>')

    # 主路：一条环路，把四个建筑串起来
    s.append('<path d="M120 150 C 340 90, 520 170, 700 140 S 980 110, 1040 210 L 1020 560 '
             'C 900 640, 700 600, 520 640 S 200 690, 120 620 Z" '
             'fill="none" stroke="#EDE6D8" stroke-width="54" stroke-linejoin="round"/>')
    s.append('<path d="M120 150 C 340 90, 520 170, 700 140 S 980 110, 1040 210 L 1020 560 '
             'C 900 640, 700 600, 520 640 S 200 690, 120 620 Z" '
             'fill="none" stroke="#FFFFFF" stroke-width="3" stroke-dasharray="18 16" opacity=".85"/>')

    # ── 建筑 ──
    s.append(zoo(218, 230))
    s.append(hospital(827, 200))
    s.append(school(196, 538))
    s.append(shop(849, 507))

    # ── 点缀：树 / 花 / 长椅 / 池塘 ──
    trees = [(70, 120), (150, 300), (60, 420), (330, 120), (400, 300), (500, 120),
             (620, 300), (720, 520), (960, 650), (120, 660), (300, 700), (640, 690),
             (1010, 330), (380, 620), (760, 60), (250, 60), (560, 690), (880, 60),
             (60, 540), (980, 430), (430, 690), (760, 690)]
    for (x, y) in trees:
        s.append(tree(x, y))
    for (x, y, c) in [(280, 180, '#F6A5C0'), (470, 430, '#FFD76E'), (600, 620, '#F6A5C0'),
                      (890, 380, '#FFD76E'), (200, 400, '#FFFFFF'), (930, 120, '#F6A5C0'),
                      (340, 470, '#FFFFFF'), (700, 200, '#FFD76E'), (560, 100, '#F6A5C0'),
                      (150, 380, '#FFD76E'), (860, 620, '#FFFFFF')]:
        s.append(flower(x, y, c))
    for (x, y) in [(120, 220), (250, 380), (470, 180), (600, 400), (700, 620), (960, 260),
                   (840, 90), (300, 560), (520, 660), (1000, 600)]:
        s.append(bush(x, y))
    s.append(car(340, 110, '#F27E9D'))
    s.append(car(700, 570, '#7FD3E8'))
    s.append(bench(430, 560))

    # 池塘
    s.append('<ellipse cx="520" cy="300" rx="86" ry="52" fill="#8FD7EA"/>')
    s.append('<ellipse cx="520" cy="296" rx="70" ry="40" fill="#B6E7F3" opacity=".7"/>')
    s.append('<ellipse cx="500" cy="288" rx="16" ry="7" fill="#7ECB6A" opacity=".9"/>')
    s.append('<ellipse cx="548" cy="306" rx="13" ry="6" fill="#7ECB6A" opacity=".9"/>')

    s.append('</svg>')
    return ''.join(s)


def bundle(x, y, fill='#C98B5E', w=44, h=16):
    """树干 + 三层树冠，绘本里最常见的那种圆头树。"""
    return (f'<g transform="translate({x},{y})">'
            f'<rect x="-5" y="0" width="10" height="{h}" rx="3" fill="#A9744C"/>'
            f'<circle cx="0" cy="-8" r="24" fill="{fill}"/>'
            f'<circle cx="-15" cy="2" r="16" fill="{fill}"/>'
            f'<circle cx="15" cy="2" r="16" fill="{fill}"/>'
            '</g>')


def tree(x, y):
    # 两种深浅交替，整片草地才不像贴了同一张图章
    return bundle(x, y, '#5FB65A' if (x + y) % 3 else '#4FA84E')


def bush(x, y):
    return (f'<g transform="translate({x},{y})">'
            '<circle cx="-12" cy="2" r="12" fill="#5FA84D"/>'
            '<circle cx="12" cy="2" r="12" fill="#5FA84D"/>'
            '<circle cx="0" cy="-6" r="14" fill="#6FB65A"/>'
            '</g>')


def car(x, y, c):
    return (f'<g transform="translate({x},{y})">'
            f'<rect x="-28" y="-14" width="56" height="26" rx="9" fill="{c}"/>'
            '<rect x="-17" y="-24" width="34" height="16" rx="7" fill="#EAF6FF"/>'
            '<circle cx="-15" cy="13" r="7" fill="#3A3A3A"/>'
            '<circle cx="15" cy="13" r="7" fill="#3A3A3A"/>'
            '<circle cx="-15" cy="13" r="3" fill="#C9C9C9"/>'
            '<circle cx="15" cy="13" r="3" fill="#C9C9C9"/>'
            '</g>')


def bench(x, y):
    return (f'<g transform="translate({x},{y})">'
            '<rect x="-24" y="-6" width="48" height="7" rx="3" fill="#C98B5E"/>'
            '<rect x="-24" y="-18" width="48" height="7" rx="3" fill="#DDAA7F"/>'
            '<rect x="-20" y="0" width="5" height="10" fill="#A9744C"/>'
            '<rect x="15" y="0" width="5" height="10" fill="#A9744C"/>'
            '</g>')


def flower(x, y, c):
    return (f'<g transform="translate({x},{y})">'
            f'<circle cx="0" cy="0" r="6" fill="{c}"/>'
            f'<circle cx="7" cy="3" r="5" fill="{c}" opacity=".85"/>'
            f'<circle cx="-7" cy="3" r="5" fill="{c}" opacity=".85"/>'
            f'<circle cx="0" cy="2" r="3" fill="#FFD76E"/>'
            '</g>')


def shadow(x, y, rx, ry):
    return f'<ellipse cx="{x}" cy="{y}" rx="{rx}" ry="{ry}" fill="#2B2119" opacity=".12"/>'


def zoo(cx, cy):
    """动物园：绿色大门 + 拱形 + 熊猫头像。熊猫比文字更认得出。"""
    s = [f'<g transform="translate({cx},{cy}) scale(1.46)">', shadow(0, 62, 78, 12)]
    # 后墙
    s.append('<rect x="-70" y="-30" width="140" height="86" rx="10" fill="#8AC97A"/>')
    s.append('<rect x="-70" y="-30" width="140" height="20" rx="10" fill="#6FB562"/>')
    # 拱形入口
    s.append('<path d="M-34 56 v-30 a34 34 0 0 1 68 0 v30z" fill="#3E6B4A"/>')
    s.append('<path d="M-27 56 v-28 a27 27 0 0 1 54 0 v28z" fill="#D9F0D0"/>')
    # 两侧柱子
    s.append('<rect x="-72" y="-6" width="16" height="64" rx="6" fill="#F0F4ED"/>')
    s.append('<rect x="56" y="-6" width="16" height="64" rx="6" fill="#F0F4ED"/>')
    # 熊猫头（门楣上）
    s.append('<circle cx="0" cy="-44" r="20" fill="#FFFFFF"/>')
    s.append('<circle cx="-13" cy="-56" r="7" fill="#2B2119"/>')
    s.append('<circle cx="13" cy="-56" r="7" fill="#2B2119"/>')
    s.append('<ellipse cx="-7" cy="-46" rx="5" ry="6" fill="#2B2119"/>')
    s.append('<ellipse cx="7" cy="-46" rx="5" ry="6" fill="#2B2119"/>')
    s.append('<ellipse cx="0" cy="-36" rx="3" ry="2.4" fill="#2B2119"/>')
    s.append('</g>')
    return ''.join(s)


def hospital(cx, cy):
    """医院：白色楼 + 红十字 + 玻璃窗。"""
    s = [f'<g transform="translate({cx},{cy}) scale(1.46)">', shadow(0, 60, 74, 12)]
    s.append('<rect x="-66" y="-46" width="132" height="106" rx="10" fill="#F4FAFF"/>')
    s.append('<rect x="-66" y="-46" width="132" height="18" rx="9" fill="#DDEBF7"/>')
    # 窗
    for r in range(2):
        for c in range(3):
            s.append(f'<rect x="{-48 + c * 34}" y="{-18 + r * 26}" width="22" height="16" rx="4" fill="#BBDCF2"/>')
    # 门
    s.append('<rect x="-16" y="26" width="32" height="34" rx="6" fill="#9CC4DF"/>')
    s.append('<rect x="-10" y="32" width="20" height="22" rx="4" fill="#D6ECFA"/>')
    # 红十字招牌：坐在屋顶上的方牌。
    # 第一版画成浮在屋顶上的圆牌，远看像挂了个气球。
    s.append('<rect x="-17" y="-66" width="34" height="22" rx="5" fill="#FFFFFF" stroke="#E9D7D2" stroke-width="2"/>')
    s.append('<rect x="-3" y="-62" width="6" height="14" rx="1.5" fill="#E05A4F"/>')
    s.append('<rect x="-7" y="-58" width="14" height="6" rx="1.5" fill="#E05A4F"/>')
    s.append('</g>')
    return ''.join(s)


def school(cx, cy):
    """学校：橙黄楼 + 钟楼 + 旗杆 + 前面的操场。"""
    s = [f'<g transform="translate({cx},{cy}) scale(1.46)">', shadow(0, 58, 78, 12)]
    s.append('<rect x="-70" y="-24" width="140" height="84" rx="10" fill="#FFD79A"/>')
    s.append('<rect x="-70" y="-24" width="140" height="18" rx="9" fill="#F2B96A"/>')
    # 钟楼
    s.append('<rect x="-18" y="-70" width="36" height="48" rx="6" fill="#FFE7C0"/>')
    s.append('<path d="M-18 -70 L0 -84 L18 -70z" fill="#E05A4F"/>')
    s.append('<circle cx="0" cy="-50" r="11" fill="#FFFFFF" stroke="#D9A25F" stroke-width="2"/>')
    s.append('<path d="M0 -50 v-6 M0 -50 h5" stroke="#4A3B32" stroke-width="2" stroke-linecap="round"/>')
    # 窗 + 门
    for c in range(3):
        s.append(f'<rect x="{-52 + c * 34}" y="0" width="20" height="16" rx="4" fill="#BFE0F5"/>')
    s.append('<rect x="-14" y="24" width="28" height="36" rx="5" fill="#C98B5E"/>')
    # 旗
    s.append('<rect x="74" y="-58" width="3" height="60" fill="#B8A492"/>')
    s.append('<path d="M77 -58 l26 7 l-26 7z" fill="#E05A4F"/>')
    s.append('</g>')
    return ''.join(s)


def shop(cx, cy):
    """商店：粉色门头 + 条纹雨棚 + 橱窗里的玩具。"""
    s = [f'<g transform="translate({cx},{cy}) scale(1.46)">', shadow(0, 56, 72, 12)]
    s.append('<rect x="-64" y="-34" width="128" height="92" rx="10" fill="#FFC9D6"/>')
    s.append('<rect x="-64" y="-34" width="128" height="16" rx="8" fill="#F7A8BB"/>')
    # 条纹雨棚
    for i in range(6):
        c = '#FFFFFF' if i % 2 == 0 else '#F27E9D'
        s.append(f'<rect x="{-62 + i * 21}" y="-22" width="21" height="18" fill="{c}" opacity=".95"/>')
    s.append('<path d="M-64 -4 q16 12 32 0 q16 12 32 0 q16 12 32 0 q16 12 32 0" fill="none" '
             'stroke="#F27E9D" stroke-width="3"/>')
    # 橱窗
    s.append('<rect x="-52" y="6" width="42" height="34" rx="6" fill="#EAF6FF"/>')
    s.append('<rect x="10" y="6" width="42" height="34" rx="6" fill="#EAF6FF"/>')
    s.append('<circle cx="-31" cy="24" r="8" fill="#FFD76E"/>')
    s.append('<rect x="22" y="16" width="14" height="16" rx="3" fill="#7FD3E8"/>')
    s.append('<rect x="-16" y="6" width="20" height="46" rx="4" fill="#C98B5E"/>')
    # 店招
    s.append('<rect x="-34" y="-52" width="68" height="18" rx="9" fill="#FFFFFF"/>')
    s.append('<circle cx="-14" cy="-43" r="4" fill="#F27E9D"/>')
    s.append('<circle cx="0" cy="-43" r="4" fill="#FFD76E"/>')
    s.append('<circle cx="14" cy="-43" r="4" fill="#7FD3E8"/>')
    s.append('</g>')
    return ''.join(s)


# ─────────────────────────── 场景 ───────────────────────────
SCENE_W, SCENE_H = 1024, 576


def scene(parts):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{SCENE_W}" height="{SCENE_H}" '
            f'viewBox="0 0 {SCENE_W} {SCENE_H}">' + ''.join(parts) + '</svg>')


def scene_zoo():
    """
    动物园：栏杆 + 竹林 + 两只熊猫。

    构图要点：竖屏时 background:cover 只露出画面中间约 26%（x 380..645），
    所以主角必须摆在中轴附近 —— 第一版把熊猫放在 210 / 810，
    手机上打开是一大片空草地，孩子以为走错了地方。
    """
    s = ['<rect width="1024" height="576" fill="#CDEBFF"/>']
    s.append('<path d="M0 300 q120 -90 250 -30 q130 60 260 -10 q140 -70 280 0 q120 50 234 10 v60 H0z" fill="#8CCB86"/>')
    s.append('<rect y="330" width="1024" height="246" fill="#A8D98B"/>')
    s.append('<path d="M0 400 q260 -26 520 0 t504 0 v176 H0z" fill="#97CE79"/>')
    # 竹林：两侧当氛围，中间留一组压在熊猫后面
    for x in (60, 110, 500, 540, 900, 950):
        s.append(f'<rect x="{x}" y="120" width="10" height="230" rx="5" fill="#7FBF63"/>')
        for y in (170, 220, 270):
            s.append(f'<ellipse cx="{x - 12}" cy="{y}" rx="18" ry="6" fill="#5FA84D" transform="rotate(-18 {x - 12} {y})"/>')
            s.append(f'<ellipse cx="{x + 22}" cy="{y + 20}" rx="18" ry="6" fill="#6FB65A" transform="rotate(16 {x + 22} {y + 20})"/>')
    # 栏杆：横跨整幅，竖屏时中间那一段还在
    s.append('<rect y="352" width="1024" height="10" fill="#C98B5E"/>')
    for x in range(20, 1024, 46):
        s.append(f'<rect x="{x}" y="356" width="7" height="52" rx="3" fill="#B87A4E"/>')
    s.append('<rect y="404" width="1024" height="8" fill="#C98B5E"/>')
    # 熊猫放在中轴两侧
    s.append(panda(430, 470, 1.0))
    s.append(panda(600, 496, .82))
    for x in (330, 690, 760):
        s.append(f'<path d="M{x} 530 q10 -26 20 0 q-10 -12 -20 0z" fill="#7FBF63"/>')
    return scene(s)


def panda(x, y, k):
    return (f'<g transform="translate({x},{y}) scale({k})">'
            '<ellipse cx="0" cy="6" rx="46" ry="10" fill="#2B2119" opacity=".1"/>'
            '<ellipse cx="0" cy="-34" rx="42" ry="38" fill="#FFFFFF"/>'
            '<circle cx="-34" cy="-64" r="14" fill="#2B2119"/>'
            '<circle cx="34" cy="-64" r="14" fill="#2B2119"/>'
            '<ellipse cx="-16" cy="-40" rx="11" ry="13" fill="#2B2119"/>'
            '<ellipse cx="16" cy="-40" rx="11" ry="13" fill="#2B2119"/>'
            '<circle cx="-16" cy="-41" r="4" fill="#FFFFFF"/>'
            '<circle cx="16" cy="-41" r="4" fill="#FFFFFF"/>'
            '<ellipse cx="0" cy="-26" rx="6" ry="4.4" fill="#2B2119"/>'
            '<path d="M-8 -20 q8 8 16 0" fill="none" stroke="#2B2119" stroke-width="2.4" stroke-linecap="round"/>'
            '<ellipse cx="-36" cy="-16" rx="14" ry="9" fill="#2B2119"/>'
            '<ellipse cx="36" cy="-16" rx="14" ry="9" fill="#2B2119"/>'
            '</g>')


def scene_hosp():
    """
    医院：候诊走廊。导诊台放在正中 —— 竖屏只露中间一条，
    台子放边上等于白画（见 scene_zoo 的说明）。
    """
    s = ['<rect width="1024" height="576" fill="#EAF6FF"/>']
    s.append('<rect y="380" width="1024" height="196" fill="#D8E7F1"/>')
    s.append('<rect y="320" width="1024" height="60" fill="#CFE3F2"/>')
    s.append('<rect y="316" width="1024" height="6" fill="#A9CBE3"/>')
    # 窗：两侧各一组，中间留白给导诊台
    for x in (90, 760):
        s.append(f'<rect x="{x}" y="86" width="140" height="160" rx="12" fill="#BFE0F5"/>')
        s.append(f'<rect x="{x}" y="86" width="140" height="160" rx="12" fill="none" stroke="#FFFFFF" stroke-width="8"/>')
        s.append(f'<path d="M{x + 70} 86 v160 M{x} 166 h140" stroke="#FFFFFF" stroke-width="6"/>')
    # 墙上的科室牌（正中，竖屏一定看得到）
    s.append('<rect x="404" y="96" width="216" height="46" rx="12" fill="#FFFFFF" stroke="#CFE3F2" stroke-width="3"/>')
    s.append('<rect x="420" y="108" width="10" height="22" rx="3" fill="#E05A4F"/>')
    s.append('<rect x="414" y="114" width="22" height="10" rx="3" fill="#E05A4F"/>')
    s.append('<rect x="452" y="112" width="140" height="8" rx="4" fill="#BBDCF2"/>')
    s.append('<rect x="452" y="126" width="96" height="7" rx="3.5" fill="#DDEBF7"/>')
    # 导诊台：正中
    s.append('<rect x="352" y="352" width="320" height="30" rx="12" fill="#FFFFFF"/>')
    s.append('<rect x="368" y="382" width="288" height="122" rx="10" fill="#E6F2FB"/>')
    s.append('<rect x="368" y="382" width="288" height="14" rx="7" fill="#C9E0F0"/>')
    s.append('<rect x="392" y="410" width="86" height="8" rx="4" fill="#CFE3F2"/>')
    s.append('<rect x="392" y="426" width="60" height="7" rx="3.5" fill="#DDEBF7"/>')
    # 台上的呼叫器
    s.append('<rect x="550" y="330" width="58" height="26" rx="6" fill="#9CC4DF"/>')
    s.append('<rect x="558" y="336" width="42" height="14" rx="3" fill="#D6ECFA"/>')
    # 候诊椅：两侧
    for i in range(2):
        x = 140 + i * 96
        s.append(f'<rect x="{x}" y="400" width="82" height="14" rx="7" fill="#7FA8C9"/>')
        s.append(f'<rect x="{x}" y="354" width="82" height="50" rx="10" fill="#9CC4DF"/>')
        s.append(f'<rect x="{x + 8}" y="414" width="8" height="38" rx="3" fill="#6E93B2"/>')
        s.append(f'<rect x="{x + 66}" y="414" width="8" height="38" rx="3" fill="#6E93B2"/>')
        s.append(f'<rect x="{x + 250}" y="400" width="82" height="14" rx="7" fill="#7FA8C9"/>')
        s.append(f'<rect x="{x + 250}" y="354" width="82" height="50" rx="10" fill="#9CC4DF"/>')
        s.append(f'<rect x="{x + 258}" y="414" width="8" height="38" rx="3" fill="#6E93B2"/>')
        s.append(f'<rect x="{x + 316}" y="414" width="8" height="38" rx="3" fill="#6E93B2"/>')
    # 盆栽
    s.append('<path d="M932 470 h56 l-8 56 h-40z" fill="#E0B08A"/>')
    for dx, dy, r in ((0, -30, 26), (-24, -10, 20), (24, -10, 20), (0, 6, 22)):
        s.append(f'<circle cx="{960 + dx}" cy="{470 + dy}" r="{r}" fill="#6FB65A"/>')
    return scene(s)


def scene_school():
    """学校：操场 + 教学楼 + 滑梯 + 单杠。下半部留白给孩子站。"""
    s = ['<rect width="1024" height="576" fill="#D6EEFF"/>']
    s.append('<rect y="330" width="1024" height="246" fill="#A8D98B"/>')
    s.append('<path d="M0 400 q260 -22 520 0 t504 0 v176 H0z" fill="#9BD17C"/>')
    # 教学楼（远景，压扁一点制造纵深）
    s.append('<rect x="200" y="150" width="620" height="190" rx="12" fill="#FFD79A"/>')
    s.append('<rect x="200" y="150" width="620" height="26" rx="13" fill="#F2B96A"/>')
    for r in range(3):
        for c in range(8):
            s.append(f'<rect x="{230 + c * 74}" y="{196 + r * 48}" width="46" height="30" rx="5" fill="#BFE0F5"/>')
    s.append('<rect x="470" y="270" width="80" height="70" rx="6" fill="#C98B5E"/>')
    # 滑梯
    s.append('<path d="M120 500 v-90 h40 l-60 90z" fill="#F27E9D"/>')
    s.append('<path d="M160 410 q40 20 60 80 l-26 4 q-16 -50 -44 -66z" fill="#FFB3C6"/>')
    s.append('<rect x="112" y="408" width="8" height="96" rx="4" fill="#7A6555"/>')
    # 单杠
    s.append('<path d="M800 500 v-96 M880 500 v-96" stroke="#B8A492" stroke-width="8" stroke-linecap="round"/>')
    s.append('<rect x="796" y="400" width="88" height="8" rx="4" fill="#B8A492"/>')
    # 草地上的小点
    for x in (300, 380, 560, 660, 740):
        s.append(f'<circle cx="{x}" cy="{520 + (x % 3) * 10}" r="4" fill="#8AC97A"/>')
    return scene(s)


def scene_shop():
    """商店：货架 + 玩具 + 收银台。暖色，和「糖衣炮弹」的调子对上。"""
    s = ['<rect width="1024" height="576" fill="#FFF1E3"/>']
    s.append('<rect y="400" width="1024" height="176" fill="#E8D3BE"/>')
    s.append('<rect y="330" width="1024" height="70" fill="#F6DFC6"/>')
    # 货架
    for x0 in (60, 460):
        s.append(f'<rect x="{x0}" y="120" width="380" height="220" rx="10" fill="#FFFFFF"/>')
        for r in range(3):
            y = 150 + r * 66
            s.append(f'<rect x="{x0 + 14}" y="{y + 46}" width="352" height="6" rx="3" fill="#E3CDB6"/>')
            # 商品：糖果罐、玩偶、盒子
            for c in range(5):
                cx = x0 + 44 + c * 70
                col = ['#FFB3C6', '#7FD3E8', '#FFD76E', '#8AC97A', '#F0862F'][(c + r) % 5]
                if (c + r) % 3 == 0:
                    s.append(f'<rect x="{cx - 16}" y="{y}" width="32" height="46" rx="6" fill="{col}"/>')
                    s.append(f'<rect x="{cx - 16}" y="{y}" width="32" height="12" rx="6" fill="#FFFFFF" opacity=".55"/>')
                elif (c + r) % 3 == 1:
                    s.append(f'<circle cx="{cx}" cy="{y + 26}" r="17" fill="{col}"/>')
                    s.append(f'<circle cx="{cx - 6}" cy="{y + 22}" r="9" fill="#FFFFFF" opacity=".35"/>')
                else:
                    s.append(f'<rect x="{cx - 18}" y="{y + 8}" width="36" height="38" rx="4" fill="{col}"/>')
    # 收银台
    s.append('<rect x="40" y="380" width="300" height="30" rx="10" fill="#C98B5E"/>')
    s.append('<rect x="60" y="410" width="260" height="110" rx="8" fill="#DDAA7F"/>')
    s.append('<rect x="150" y="340" width="70" height="46" rx="6" fill="#7A6555"/>')
    s.append('<rect x="160" y="350" width="50" height="30" rx="3" fill="#BFE0F5"/>')
    # 地上的球
    s.append('<circle cx="880" cy="500" r="26" fill="#F27E9D"/>')
    s.append('<path d="M854 500 q26 -14 52 0" fill="none" stroke="#FFFFFF" stroke-width="4"/>')
    return scene(s)


# ─────────────────────────── 小图 ───────────────────────────
def enter_svg():
    """入口标记：一扇小门，门里透出光。"""
    return ('<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">'
            '<ellipse cx="32" cy="56" rx="18" ry="4" fill="#2B2119" opacity=".18"/>'
            '<path d="M18 56 v-30 a14 14 0 0 1 28 0 v30z" fill="#FFFFFF"/>'
            '<path d="M23 56 v-28 a9 9 0 0 1 18 0 v28z" fill="#FFD79A"/>'
            '<path d="M32 26 l0 18 M27 32 l5 -6 5 6" stroke="#F0862F" stroke-width="3" '
            'stroke-linecap="round" stroke-linejoin="round" fill="none"/>'
            '</svg>')


def win_svg():
    """
    成功：警察叔叔护住孩子 —— 盾牌 + 绿勾。
    注意：原工程的 win.png 画的是「坏人抓走小孩」（判定为失败的那张），
    lose.png 才是警察救人。两张图在 index.html 里是反着用的，这次一并纠正。
    """
    s = ['<svg xmlns="http://www.w3.org/2000/svg" width="720" height="400" viewBox="0 0 720 400">']
    s.append('<rect width="720" height="400" rx="24" fill="#E8F7F4"/>')
    s.append('<circle cx="360" cy="210" r="150" fill="#D3F0EA"/>')
    # 警察（背侧身，抱住孩子）
    s.append('<g transform="translate(300,300)">')
    s.append('<ellipse cx="0" cy="8" rx="88" ry="12" fill="#2B2119" opacity=".10"/>')
    s.append('<rect x="-52" y="-134" width="104" height="140" rx="34" fill="#3E5566"/>')
    s.append('<rect x="-52" y="-134" width="104" height="26" rx="13" fill="#2C3E50"/>')
    s.append('<circle cx="0" cy="-158" r="42" fill="#F1C9A5"/>')
    s.append('<path d="M-42 -160 a42 42 0 0 1 84 0z" fill="#2C3E50"/>')
    s.append('<path d="M-42 -160 h34 a8 8 0 0 1 0 8 h-34z" fill="#2C3E50"/>')
    s.append('<circle cx="-14" cy="-154" r="4" fill="#2B2119"/>')
    s.append('<circle cx="14" cy="-154" r="4" fill="#2B2119"/>')
    s.append('<path d="M-10 -140 q10 8 20 0" fill="none" stroke="#2B2119" stroke-width="3" stroke-linecap="round"/>')
    # 臂章
    s.append('<circle cx="42" cy="-96" r="12" fill="#FFD76E"/>')
    s.append('<path d="M36 -96 l5 5 9 -10" fill="none" stroke="#8A6A20" stroke-width="3" stroke-linecap="round"/>')
    s.append('</g>')
    # 孩子（被护在怀里）
    s.append('<g transform="translate(390,300)">')
    s.append('<rect x="-28" y="-86" width="56" height="86" rx="22" fill="#C6E85A"/>')
    s.append('<circle cx="0" cy="-114" r="30" fill="#F7CFA8"/>')
    s.append('<path d="M-30 -114 a30 30 0 0 1 60 0" fill="none" stroke="#2B2119" stroke-width="13" stroke-linecap="round"/>')
    s.append('<circle cx="-11" cy="-112" r="4" fill="#2B2119"/>')
    s.append('<circle cx="11" cy="-112" r="4" fill="#2B2119"/>')
    s.append('<path d="M-8 -100 q8 7 16 0" fill="none" stroke="#2B2119" stroke-width="3" stroke-linecap="round"/>')
    s.append('</g>')
    # 绿勾徽章
    s.append('<circle cx="560" cy="120" r="54" fill="#5FCF9B"/>')
    s.append('<path d="M534 120 l20 20 l34 -40" fill="none" stroke="#FFFFFF" stroke-width="12" '
             'stroke-linecap="round" stroke-linejoin="round"/>')
    s.append('</svg>')
    return ''.join(s)


def lose_svg():
    """失败：陌生人牵着孩子走远。用背影 + 低头，比画恐怖的脸更有效。"""
    s = ['<svg xmlns="http://www.w3.org/2000/svg" width="720" height="400" viewBox="0 0 720 400">']
    s.append('<rect width="720" height="400" rx="24" fill="#FFF1E6"/>')
    s.append('<circle cx="360" cy="220" r="150" fill="#FFE1CC"/>')
    # 地面线
    s.append('<path d="M120 350 H600" stroke="#E8C9AC" stroke-width="4" stroke-linecap="round"/>')
    # 陌生人（大、暗、背对）
    s.append('<g transform="translate(300,330)">')
    s.append('<ellipse cx="0" cy="4" rx="54" ry="9" fill="#2B2119" opacity=".10"/>')
    s.append('<rect x="-40" y="-140" width="80" height="146" rx="26" fill="#6B7280"/>')
    s.append('<circle cx="0" cy="-168" r="38" fill="#E0B48C"/>')
    s.append('<path d="M-38 -170 a38 38 0 0 1 76 0z" fill="#3A2A1E"/>')
    s.append('<path d="M-38 -170 h30 a7 7 0 0 1 0 7 h-30z" fill="#3A2A1E"/>')
    s.append('</g>')
    # 孩子（小、被牵着、回头看）
    s.append('<g transform="translate(410,346)">')
    s.append('<rect x="-24" y="-74" width="48" height="74" rx="20" fill="#C6E85A"/>')
    s.append('<circle cx="0" cy="-100" r="26" fill="#F7CFA8"/>')
    s.append('<path d="M-26 -100 a26 26 0 0 1 52 0" fill="none" stroke="#2B2119" stroke-width="12" stroke-linecap="round"/>')
    s.append('<circle cx="-9" cy="-98" r="3.6" fill="#2B2119"/>')
    s.append('<circle cx="9" cy="-98" r="3.6" fill="#2B2119"/>')
    s.append('<path d="M-7 -86 q7 -6 14 0" fill="none" stroke="#2B2119" stroke-width="3" stroke-linecap="round"/>')
    s.append('</g>')
    # 牵着的手
    s.append('<path d="M330 240 q50 40 78 76" fill="none" stroke="#E0B48C" stroke-width="9" stroke-linecap="round"/>')
    # 警告徽章
    s.append('<g transform="translate(556,120)">')
    s.append('<circle r="54" fill="#F0862F"/>')
    s.append('<rect x="-5" y="-30" width="10" height="38" rx="5" fill="#FFFFFF"/>')
    s.append('<circle cx="0" cy="26" r="7" fill="#FFFFFF"/>')
    s.append('</g>')
    s.append('</svg>')
    return ''.join(s)


# ─────────────────────────── 输出 ───────────────────────────
def write(name, text):
    p = os.path.join(OUT, name)
    with open(p, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    return p, len(text.encode('utf-8'))


def main():
    os.makedirs(OUT, exist_ok=True)
    files = [
        ('kid.svg', sprite(KID)),
        ('foe1.svg', sprite(FOE1)),
        ('foe2.svg', sprite(FOE2)),
        ('foe3.svg', sprite(FOE3)),
        ('foe4.svg', sprite(FOE4)),
        ('map.svg', map_svg()),
        ('scene-zoo.svg', scene_zoo()),
        ('scene-hosp.svg', scene_hosp()),
        ('scene-school.svg', scene_school()),
        ('scene-shop.svg', scene_shop()),
        ('enter.svg', enter_svg()),
        ('win.svg', win_svg()),
        ('lose.svg', lose_svg()),
    ]
    total = 0
    for name, text in files:
        p, n = write(name, text)
        total += n
        print(f'  {name:<18} {n / 1024:6.1f} KB')
    print(f'  共 {len(files)} 个文件，{total / 1024:.1f} KB')


if __name__ == '__main__':
    main()
