# -*- coding: utf-8 -*-
"""
病毒斗士素材管线：D:/Downloads/Adapted-game-virus-fighter-master → playland/virusfighter/

原工程是 pygame，600×600 固定窗口、素材是 300×300 / 600×600 的 PNG。
直接搬进来有三处不合适：
  1. 体积：6 张 600×600 PNG 各 1.4MB，光背景就 8.5MB。
  2. 音乐：bgm.wav 80MB、home_bgm.wav 5.5MB，一小时的 PCM 音频。
  3. 数字素材：0-9 的 270×270 PNG 是给 pygame 逐位贴图用的，canvas 直接画字更清楚。

所以这里只做「重新编码」，不改画面内容：
  - 背景/过场（600×600 的整屏图）→ webp q80，控制在 60KB 以内
  - 角色（300×300 的病毒/医护）→ webp 160×160，进游戏后固定缩放到格子大小
  - 球/驰援（150/300 方形）→ webp 96×96
  - 音乐 → ogg q4（ffmpeg），两个文件从 86MB 压到 1MB 以内

用法：python Harmony/tools/build-virusfighter.py
"""
import os
import subprocess
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))          # Harmony/
SRC = r'D:/Downloads/Adapted-game-virus-fighter-master'
IMG = os.path.join(SRC, 'img')
OUT = os.path.join(ROOT, 'entry/src/main/resources/rawfile/playland/virusfighter')
FFMPEG = r'D:/Netease/UltimateKtv1.1.3/ffmpeg/ffmpeg.exe'


def flatten_green(im, target=(27, 198, 111), tol=26):
    """
    原工程的几张整屏图各用各的绿：win.png 边角是 #1BC66F，thanks.png 是 #22C874。
    直接当背景铺上去，页面底色和图片边缘差一点点，屏幕上就是一道看得见的方块边。
    这里把「接近目标绿」的像素统一刷成同一个值，图片和 CSS 底色就能完全咬合。
    """
    im = im.convert('RGB')
    px = im.load()
    w, h = im.size
    tr, tg, tb = target
    for y in range(h):
        for x in range(w):
            r, g, b = px[x, y]
            if abs(r - tr) <= tol and abs(g - tg) <= tol and abs(b - tb) <= tol:
                px[x, y] = target
    return im


def save_webp(src, dst, size, quality=82):
    im = Image.open(src)
    has_alpha = im.mode in ('RGBA', 'LA') or (im.mode == 'P' and 'transparency' in im.info)
    im = im.convert('RGBA' if has_alpha else 'RGB')
    if size:
        im = im.resize(size, Image.LANCZOS) if im.size != size else im
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im.save(dst, 'WEBP', quality=quality, method=6)
    return os.path.getsize(dst)


def main():
    os.makedirs(os.path.join(OUT, 'img'), exist_ok=True)
    os.makedirs(os.path.join(OUT, 'snd'), exist_ok=True)
    total = 0

    # 角色：10 种病毒 + 12 位医护。格子是 30px，但 2x 屏要 60px，
    # 取 160 让队伍变长后逐节缩小也不糊。
    for i in range(1, 11):
        total += save_webp(os.path.join(IMG, 'v%d.png' % i),
                           os.path.join(OUT, 'img/v%d.webp' % i), (160, 160), 85)
    for i in range(1, 13):
        total += save_webp(os.path.join(IMG, 'm%d.png' % i),
                           os.path.join(OUT, 'img/m%d.webp' % i), (160, 160), 85)

    # 药片球 5 帧 + 驰援标志
    for i in range(1, 6):
        total += save_webp(os.path.join(IMG, 'drag%d.png' % i),
                           os.path.join(OUT, 'img/drag%d.webp' % i), (96, 96), 85)
    total += save_webp(os.path.join(IMG, 'help.png'), os.path.join(OUT, 'img/help.webp'), (96, 96), 85)

    # 场地背景（bg.png：「纯绿 + 半透明白病毒」）没有随包：
    # 原版 600×600 时它是很淡的一层水印，铺满整块棋盘后会盖住棋子，
    # 游戏里改成纯色 + 细网格绘制，这张图留着只会白占体积。

    # 首页那 4 张 600×600 整屏图里带着「病毒斗士 / VIRUS FIGHTER」标题，
    # 缩成 220px 的小图后标题糊成一团，还会和页面自己的大标题打架。
    # 四帧的差异只出现在 y=167..465（病毒本体的表情动画），标题是静止的，
    # 所以按 x=150..450 / y=150..490 裁出纯 logo 就够。
    for i in range(1, 5):
        im = Image.open(os.path.join(IMG, 'home%d.png' % i))
        crop = flatten_green(im.crop((150, 150, 450, 490)))
        dst = os.path.join(OUT, 'img/logo%d.webp' % i)
        crop.save(dst, 'WEBP', quality=86, method=6)
        total += os.path.getsize(dst)

    # 通关页：原图是「恭喜过关」「分数：」两块木牌挂在绿底上。
    # 整张当背景铺会被拉成一条，字也跟着变形；而且「分数：」是印在牌子上的，
    # 分数数字没地方放。所以只取上面那块木牌当标题装饰，分数自己排版。
    # 木牌本体实测只占 x=150..450 / y=108..262，两侧留白处有藤蔓花纹，
    # 整宽裁会把花纹一起带进来，浮在绿底上像块脏点。左右各留 10px 余量裁出来。
    win = flatten_green(Image.open(os.path.join(IMG, 'win.png')))
    wood = win.crop((140, 100, 460, 268))
    dst = os.path.join(OUT, 'img/wood.webp')
    wood.save(dst, 'WEBP', quality=84, method=6)
    total += os.path.getsize(dst)

    thanks = flatten_green(Image.open(os.path.join(IMG, 'thanks.png')))
    dst = os.path.join(OUT, 'img/thanks.webp')
    thanks.resize((520, 520), Image.LANCZOS).save(dst, 'WEBP', quality=82, method=6)
    total += os.path.getsize(dst)

    # go.png / back.png 是 pygame 时代的按钮贴图（绿底白框、白底绿字），
    # 在网页里按钮直接由 CSS 画，缩放到 360dpi 也不会糊，这两张不再打包。

    # 音乐：wav → ogg。两个 wav 加起来 86MB，是 HAP 里最不该出现的东西。
    # 混成单声道：这两个文件左右声道基本一样，立体声是纯浪费。
    # bgm 有 7 分 37 秒，正好够打完一局（5 关），不裁，免得循环点落在乐句中间。
    if os.path.exists(FFMPEG):
        for src_name, dst_name in (('bgm.wav', 'bgm.ogg'), ('home_bgm.wav', 'home.ogg')):
            dst = os.path.join(OUT, 'snd', dst_name)
            subprocess.run([FFMPEG, '-y', '-loglevel', 'error', '-i',
                            os.path.join(SRC, 'music', src_name),
                            '-ac', '1', '-c:a', 'libvorbis', '-q:a', '1', dst], check=True)
            total += os.path.getsize(dst)
    else:
        print('!! 找不到 ffmpeg，跳过音频。联机不需要它，但游戏会没有声音。')

    files = sum(len(f) for _, _, f in os.walk(OUT))
    print('产出 %d 个文件，共 %.2f MB → %s' % (files, total / 1048576, os.path.relpath(OUT, ROOT)))


if __name__ == '__main__':
    sys.exit(main())
