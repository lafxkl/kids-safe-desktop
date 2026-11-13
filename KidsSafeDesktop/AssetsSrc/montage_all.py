import glob, os
from PIL import Image
import numpy as np

files = sorted(glob.glob("D:/WorkBuddy/KidsSafeDesktop/Resources/Images/pw_*.png"))
cols = 6
rows = (len(files) + cols - 1) // cols
cell = 200
pad = 6
bg = Image.new('RGBA', (cols*cell, rows*cell), (210, 210, 210, 255))
for i, f in enumerate(files):
    im = Image.open(f).convert('RGBA')
    w, h = im.size
    s = min((cell-2*pad)/w, (cell-2*pad)/h, 2.0)
    im = im.resize((int(w*s), int(h*s)), Image.LANCZOS)
    r = i // cols; c = i % cols
    bg.paste(im, (c*cell + (cell-im.width)//2, r*cell + (cell-im.height)//2), im)
bg.convert('RGB').save("D:/WorkBuddy/KidsSafeDesktop/AssetsSrc/montage_all.png")
print("montage", len(files))
