from PIL import Image, ImageDraw
import glob, os, numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
os.makedirs('outputs_local', exist_ok=True)

# per-file tolerance (higher = more aggressive white removal)
files = sorted(glob.glob('inputs/*.png'))
for f in files:
    name = os.path.splitext(os.path.basename(f))[0]
    src = Image.open(f).convert('RGB')
    w,h = src.size
    # flood fill from a corner (white card) with tolerance
    mask = src.copy()
    # seed at top-left; if it's not light, try other corners
    seeds = [(2,2),(w-3,2),(2,h-3),(w-3,h-3)]
    filled = None
    for sx,sy in seeds:
        r,g,b = src.getpixel((sx,sy))
        if r>180 and g>180 and b>180:
            m = mask.copy()
            ImageDraw.floodfill(m, (sx,sy), (2,2,2), thresh=45, border=None)
            filled = m
            break
    if filled is None:
        # fallback: just keep original
        out = src.convert('RGBA')
    else:
        arr = np.array(filled)
        m = (arr[:,:,0]==2)&(arr[:,:,1]==2)&(arr[:,:,2]==2)
        out = src.convert('RGBA')
        ao = np.array(out)
        ao[m,3] = 0
        out = Image.fromarray(ao,'RGBA')
    out.save(f'outputs_local/{name}.png')
    tp = (np.array(out)[:,:,3]<20).mean()*100
    print(f'{name:10s} transparent%={tp:5.1f}')
print('done')
