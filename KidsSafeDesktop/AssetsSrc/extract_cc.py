from PIL import Image
import numpy as np, glob, os
from collections import deque

HERE = os.path.dirname(os.path.abspath(__file__))
os.makedirs('outputs_cc', exist_ok=True)

def cc_labels(fg):
    H,W = fg.shape
    lab = np.zeros((H,W), np.int32)
    cur=0
    for y in range(H):
        for x in range(W):
            if fg[y,x] and lab[y,x]==0:
                cur+=1
                q=deque([(x,y)]); lab[y,x]=cur
                while q:
                    cx,cy=q.popleft()
                    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                        nx,ny=cx+dx,cy+dy
                        if 0<=nx<W and 0<=ny<H and fg[ny,nx] and lab[ny,nx]==0:
                            lab[ny,nx]=cur; q.append((nx,ny))
    return lab,cur

def extract(path):
    im = Image.open(path).convert('RGB')
    a = np.asarray(im).astype(np.int32)
    R,G,B = a[:,:,0],a[:,:,1],a[:,:,2]
    maxc = np.maximum.reduce([R,G,B]); minc = np.minimum.reduce([R,G,B])
    sat = maxc-minc
    lum = (R+G+B)//3
    fg = (sat>30) | (lum<110)
    H,W = fg.shape
    lab,n = cc_labels(fg)
    keep = np.zeros((H,W), np.bool_)
    if n>0:
        sizes = np.bincount(lab.ravel(), minlength=n+1)[1:]
        thresh = H*W*0.03
        for i,s in enumerate(sizes, start=1):
            if s>=thresh:
                keep |= (lab==i)
    # mild dilation to close anti-alias gaps
    dilated = np.zeros((H,W), np.bool_)
    for dy in (-2,-1,0,1,2):
        for dx in (-2,-1,0,1,2):
            if abs(dx)+abs(dy)<=2:
                dilated |= np.roll(np.roll(keep,dx,axis=1),dy,axis=0)
    ao = np.dstack([R.astype(np.uint8),G.astype(np.uint8),B.astype(np.uint8),
                    (dilated*255).astype(np.uint8)])
    return Image.fromarray(ao,'RGBA')

for f in sorted(glob.glob('inputs/*.png')):
    name=os.path.splitext(os.path.basename(f))[0]
    out=extract(f)
    out.save(f'outputs_cc/{name}.png')
    tp=(np.asarray(out)[:,:,3]<20).mean()*100
    print(f'{name:10s} transparent%={tp:5.1f}')
print('done')
