# -*- coding: utf-8 -*-
"""
成语大挑战素材管线：D:/Downloads/egret_cydtz-master → playland/chengyu/

原工程是 Egret（白鹭）引擎的 TS 项目，成品在 bin-release/web/<时间戳>/ 里，
整包 17MB（egret 运行时 700KB + 621 张题目图 12.4MB + 音效）。

不整包搬的三条理由：
  1. 题库是繁体，「一刀兩斷」「畫蛇添足」孩子认不全；
  2. Egret 靠 XHR 读 res.json 清单，离线包（resource://rawfile）下这条路走不通；
  3. 720×1136 固定竖版、只认点击，放进现有桌面和另外几款游戏不是一个手感。

所以只取「题库 + 题目插画」，玩法用 H5 重写。这个脚本负责：
  - 简繁转换（Windows 自带的 LCMapStringEx，比手写映射表可靠）
  - 干扰字去重（21 道题的干扰字里混着答案字，会让答案不唯一）
  - 420 张插画 → webp（12.4MB → 约 4MB）

用法：python Harmony/tools/build-chengyu.py [--dump]   # --dump 打印转换对照表
"""
import ctypes
import io
import json
import os
import re
import sys
from ctypes import wintypes

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))          # Harmony/
SRC = r'D:/Downloads/egret_cydtz-master/bin-release/web/180822143417'
OUT = os.path.join(ROOT, 'entry/src/main/resources/rawfile/playland/chengyu')

# ── 简繁转换 ──
# 表在同目录的 t2s.py。这里不放 fallback：转不动的字会被 build_data 抓出来报错，
# 悄悄放过就等于把繁体塞给孩子，比报错更糟。
import t2s                                          # noqa: E402  (同目录)
T2S = t2s.T2S

# GB2312 不含的字里，这些是简体本来就会用到的标点和常用字，不该报错
OK_EXTRA = set('——“”‘’…·《》〈〉「」『』、。，；：？！（）【】')


def to_simplified(s):
    if not s:
        return ''
    return ''.join(T2S.get(ch, ch) for ch in s)


# 逐条人工确认过的多音字：光靠单字映射会把「著作」写成「着作」。
FIX = {
    '着作': '著作', '显着': '显著', '着名': '著名', '着述': '著述',
    # 乾 是多音字：干燥义写「干」，乾坤/乾隆 仍作「乾」。
    '乾燥': '干燥', '乾净': '干净', '乾脆': '干脆',
    '外强中乾': '外强中干', '乳臭未乾': '乳臭未干',
    # 题库里这三处本身就是错字（蘆/義/栗），照着抄会把错的教给孩子
    '三顾茅芦': '三顾茅庐',
    '集思广义': '集思广益',
    '沧海一栗': '沧海一粟',
}


def fix_words(s):
    for wrong, right in FIX.items():
        s = s.replace(wrong, right)
    return s


def leftover(s):
    """返回 s 里既不在 GB2312、也不在白名单里的字 —— 说明对照表漏了。"""
    bad = []
    for ch in s:
        if ch in OK_EXTRA:
            continue
        try:
            ch.encode('gb2312')
        except Exception:
            bad.append(ch)
    return ''.join(sorted(set(bad)))


def load_questions():
    path = os.path.join(SRC, 'resource/assets/questions.json')
    with io.open(path, encoding='utf-8') as f:
        return json.load(f)


def build_data(dump=False):
    qs = load_questions()
    rows, changes = [], []
    missing = set()
    # 所有备选字池：干扰字不够时从这里补，保证不会补出答案里的字
    all_chars = ''.join(it['answer'] + it['word'] for it in qs)

    for idx, it in enumerate(qs):
        ans_t = it['answer']
        word_t = it['word']
        ans = fix_words(to_simplified(ans_t))
        word = fix_words(to_simplified(word_t))
        tip = fix_words(to_simplified(it.get('tip', '')))

        if ans != ans_t:
            changes.append((ans_t, ans, tip if tip != it.get('tip') else ''))
        if len(ans) != 4:
            raise SystemExit('第 %d 题答案不是 4 字：%s → %s' % (idx + 1, ans_t, ans))

        # 对照表漏字时必须当场停：漏一个「斷」，孩子看到的就是「一刀两斷」，
        # 而脚本会一声不响地把它写进 data.js。
        # 但先攒齐再报 —— 一次只报一个字，改一轮跑一轮太慢。
        miss = leftover(ans + word + tip)
        if miss:
            missing.update(miss)
            continue

        # 干扰字去重：同一个字出现两次，玩家能点两块同样的字凑答案，
        # 「答案唯一」这条就没了。补字从全题库里挑，且不能撞答案。
        seen = list(ans)
        picked = []
        for ch in word:
            if ch in seen:
                continue
            seen.append(ch)
            picked.append(ch)
        guard = 0
        while len(picked) < 6 and guard < 400:
            guard += 1
            ch = all_chars[(idx * 37 + guard * 11) % len(all_chars)]
            ch = fix_words(to_simplified(ch))
            if len(ch) != 1 or ch in seen:
                continue
            seen.append(ch)
            picked.append(ch)
        if len(picked) < 6:
            raise SystemExit('第 %d 题凑不齐 6 个干扰字' % (idx + 1))

        img = os.path.splitext(os.path.basename(it['img']))[0]
        rows.append({'a': ans, 'w': ''.join(picked), 't': tip, 'i': img})

    if missing:
        raise SystemExit('对照表缺 %d 个字：%s\n  请补进 Harmony/tools/t2s.py'
                         % (len(missing), ''.join(sorted(missing))))

    if dump:
        print('=== answer 有变化的 %d 条 ===' % len(changes))
        for old, new, tip in changes:
            print('  %s → %s   %s' % (old, new, tip[:24] if tip else ''))
    return rows


def build_images(rows):
    src_dir = os.path.join(SRC, 'resource/assets/Images')
    dst_dir = os.path.join(OUT, 'img')
    os.makedirs(dst_dir, exist_ok=True)
    total = 0
    need = {r['i'] for r in rows}
    for name in sorted(need):
        src = os.path.join(src_dir, name + '.jpg')
        if not os.path.exists(src):
            raise SystemExit('缺图：' + src)
        im = Image.open(src).convert('RGB')
        # 原图 390×260。题目区按卡片宽度铺满，手机上 390 逻辑像素足够；
        # q80 是实测下来画质和体积的平衡点（q72 开始出现明显的块状）
        dst = os.path.join(dst_dir, name + '.webp')
        im.save(dst, 'WEBP', quality=80, method=6)
        total += os.path.getsize(dst)
    return len(need), total


def main():
    dump = '--dump' in sys.argv
    os.makedirs(OUT, exist_ok=True)
    rows = build_data(dump=dump)

    # data.js 用短字段：420 条全部内联，字段名省下的字节是实打实的
    head = ("'use strict';\n"
            "/*\n"
            " * 420 道成语题库 —— 由 Harmony/tools/build-chengyu.py 从 egret_cydtz 的 questions.json 生成。\n"
            " * 不要手改；改题请改源工程再重跑脚本。\n"
            " * a=答案 w=6 个干扰字 t=释义 i=插图编号\n"
            " */\n")
    js = head + 'window.CY_DATA = [\n'
    for k, r in enumerate(rows):
        js += '  ' + json.dumps(r, ensure_ascii=False, separators=(',', ':'))
        js += ',\n' if k < len(rows) - 1 else '\n'
    js += '];\n'
    with io.open(os.path.join(OUT, 'data.js'), 'w', encoding='utf-8') as f:
        f.write(js)

    n, img_bytes = build_images(rows)
    data_bytes = os.path.getsize(os.path.join(OUT, 'data.js'))
    print('题库 %d 条（%.0f KB），插图 %d 张（%.2f MB）' % (
        len(rows), data_bytes / 1024, n, img_bytes / 1048576))
    print('答案样例：' + '  '.join(r['a'] for r in rows[:6]))


if __name__ == '__main__':
    main()
