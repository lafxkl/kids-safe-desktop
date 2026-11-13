"""Remove the small 'AI生成 / WORKBUDDY' corner watermark from ImageGen outputs.

These are decorative wallpapers shown via AspectFill, so the cleanest fix is to
crop off the bottom-right corner region that holds the watermark (no smearing).
"""
from PIL import Image
import os

SRC = r"D:\WorkBuddy\儿童安全桌面\generated-images"
DST = r"D:\WorkBuddy\KidsSafeDesktop\Resources\Images"

JOBS = [
    # (source, dest, crop_right, crop_bottom)
    ("Children_s_wallpaper_backgroun_2026-10-31T08-18-47.png", "theme_sunshine.png", 250, 110),
    ("Children_s_wallpaper_backgroun_2026-10-31T08-19-06.png", "theme_sky.png",      250, 110),
    ("Children_s_wallpaper_backgroun_2026-10-31T08-19-18.png", "theme_rainbow.png",  250, 110),
    ("Cute_cartoon_child_wearing_big_2026-10-31T08-19-30.png", "xmly_header.png",    200, 90),
]

for src_name, dst_name, cr, cb in JOBS:
    path = os.path.join(SRC, src_name)
    im = Image.open(path).convert("RGB")
    w, h = im.size
    im = im.crop((0, 0, w - cr, h - cb))
    out_path = os.path.join(DST, dst_name)
    im.save(out_path)
    print("saved", out_path, im.size)
