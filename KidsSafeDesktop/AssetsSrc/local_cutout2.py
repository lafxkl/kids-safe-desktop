from PIL import Image, ImageDraw
import glob, numpy as np, os

HERE = os.path.dirname(os.path.abspath(__file__))
os.makedirs('outputs_final', exist_ok=True)
files = sorted(glob.glob('outputs/*.png'))
for f in files:
    name = os.path.splitext(os.path.basename(f))[0]
    src = Image.open(f).convert('RGBA')
    w,h = src.size
    arr = np.array(src)
    a = arr[:,:,3]
    # candidate background: opaque, light, low saturation
    R,G,B = arr[:,:,0],arr[:,:,1],arr[:,:,2]
    maxc = np.maximum.reduce([R,G,B]); minc = np.minimum.reduce([R,G,B])
    sat = maxc-minc
    bg = (a>200)&(maxc>190)&(sat<55)
    # flood fill bg from edges
    mask = bg.astype(np.uint8)
    # simple stack BFS
    seeds=[(2,2),(w-3,2),(2,h-3),(w-3,h-3)]
    visit=set()
    from collections import deque
    q=deque()
    for sx,sy in seeds:
        if 0<=sx<w and 0<=sy<h and mask[sy,sx]:
            q.append((sx,sy)); visit.add((sx,sy))
    while q:
        x,y=q.popleft()
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx,ny=x+dx,y+dy
            if 0<=nx<w and 0<=ny<h and (nx,ny) not in visit and mask[ny,nx]:
                visit.add((nx,ny)); q.append((nx,ny))
    fill=np.zeros((h,w),np.bool_)
    for (x,y) in visit: fill[y,x]=True
    ao=arr.copy()
    ao[fill,3]=0
    out=Image.fromarray(ao,'RGBA')
    out.save(f'outputs_final/{name}.png')
    tp=(ao[:,:,3]<20).mean()*100
    print(f'{name:10s} transparent%={tp:5.1f}')
print('done')
