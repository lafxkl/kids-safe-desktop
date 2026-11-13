import glob
from PIL import Image, ImageDraw, ImageFont
import numpy as np

files = sorted(glob.glob("D:/WorkBuddy/KidsSafeDesktop/Resources/Images/pw_*.png"))
cols = 6
rows = (len(files) + cols - 1) // cols
cell = 210
bg = Image.new('RGBA', (cols*cell, rows*cell), (235, 235, 235, 255))
try:
    fnt = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 20)
except Exception:
    fnt = ImageFont.load_default()
d = ImageDraw.Draw(bg)
for i, f in enumerate(files):
    im = Image.open(f).convert('RGBA')
    w, h = im.size
    s = min((cell-24)/w, (cell-24)/h, 2.2)
    im = im.resize((int(w*s), int(h*s)), Image.LANCZOS)
    r = i // cols; c = i % cols
    bg.paste(im, (c*cell + (cell-im.width)//2, r*cell + 26 + (cell-26-im.height)//2), im)
    label = f.split("\\")[-1].replace("pw_", "").replace(".png", "")
    d.text((c*cell+6, r*cell+4), label, fill=(20, 20, 20, 255), font=fnt)
bg.convert('RGB').save("D:/WorkBuddy/KidsSafeDesktop/AssetsSrc/montage_lab.png")
print("montage", len(files))
