from PIL import Image
import numpy as np, glob, os
from collections import deque

def cc_labels(fg):
    H,W=fg.shape; lab=np.zeros((H,W),np.int32); cur=0
    for y in range(H):
        for x in range(W):
            if fg[y,x] and lab[y,x]==0:
                cur+=1; q=deque([(x,y)]); lab[y,x]=cur
                while q:
                    cx,cy=q.popleft()
                    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                        nx,ny=cx+dx,cy+dy
                        if 0<=nx<W and 0<=ny<H and fg[ny,nx] and lab[ny,nx]==0:
                            lab[ny,nx]=cur; q.append((nx,ny))
    return lab,cur

def extract(src):
    im=src.convert('RGBA'); a=np.asarray(im).astype(np.int32)
    R,G,B=a[:,:,0],a[:,:,1],a[:,:,2]
    maxc=np.maximum.reduce([R,G,B]); minc=np.minimum.reduce([R,G,B]); sat=maxc-minc
    lum=(R+G+B)//3
    fg=(sat>30)|(lum<110)
    H,W=fg.shape; lab,n=cc_labels(fg)
    keep=np.zeros((H,W),np.bool_)
    if n>0:
        sizes=np.bincount(lab.ravel(),minlength=n+1)[1:]
        # border-touching mask per component
        thresh=H*W*0.03
        order_idx=np.argsort(sizes)[::-1]+1
        maxs=sizes.max() if n>0 else 0
        for i in order_idx:
            s=sizes[i-1]
            if s>=maxs*0.45 and s>=thresh:
                keep|=(lab==i)
        if not keep.any() and n>0:
            keep|=(lab==order_idx[0])
    dilated=np.zeros((H,W),np.bool_)
    for dy in range(-2,3):
        for dx in range(-2,3):
            if abs(dx)+abs(dy)<=2: dilated|=np.roll(np.roll(keep,dx,1),dy,0)
    ao=np.dstack([R.astype(np.uint8),G.astype(np.uint8),B.astype(np.uint8),(dilated*255).astype(np.uint8)])
    return Image.fromarray(ao,'RGBA')

os.makedirs('outputs_final3',exist_ok=True)
for f in sorted(glob.glob('outputs_u/*.png')):
    name=os.path.splitext(os.path.basename(f))[0]
    out=extract(Image.open(f))
    out.save(f'outputs_final3/{name}.png')
    tp=(np.asarray(out)[:,:,3]<20).mean()*100
    print(f'{name:10s} transp%={tp:5.1f}')
print('done')
