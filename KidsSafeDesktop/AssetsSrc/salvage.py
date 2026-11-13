import subprocess, json, os, shutil, glob
from PIL import Image, ImageDraw
import numpy as np
from collections import deque

SKILL = r"D:/Program Files/WorkBuddy/resources/app.asar.unpacked/resources/plugins/workbuddy-builtin/skills/buddy-image-processing/scripts/buddy-image-processing.py"
TOKEN = "ck_t_g2v0d3cvi39c.kMLEiNgL6YduezRb0osvdg"
HERE = os.path.dirname(os.path.abspath(__file__))

# include a sliver of the colored card border so matting has a background to cut
tiles = [
 ('r3c2','door',0.28,1.0,0.05,0.95),
 ('r5c1','hat',0.28,1.0,0.03,0.62),
 ('r5c0','shoe',0.02,1.0,0.10,0.62),
]
W,H=132,148
def flood_strip(path):
    src=Image.open(path).convert('RGBA'); w,h=src.size
    arr=np.array(src); a=arr[:,:,3]
    R,G,B=arr[:,:,0],arr[:,:,1],arr[:,:,2]
    maxc=np.maximum.reduce([R,G,B]); minc=np.minimum.reduce([R,G,B]); sat=maxc-minc
    bg=(a>200)&(maxc>180)&(sat<60)
    mask=bg.astype(np.uint8); q=deque(); visit=set()
    for sx,sy in [(2,2),(w-3,2),(2,h-3),(w-3,h-3)]:
        if 0<=sx<w and 0<=sy<h and mask[sy,sx]: q.append((sx,sy)); visit.add((sx,sy))
    while q:
        x,y=q.popleft()
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nx,ny=x+dx,y+dy
            if 0<=nx<w and 0<=ny<h and (nx,ny) not in visit and mask[ny,nx]:
                visit.add((nx,ny)); q.append((nx,ny))
    ao=arr.copy()
    for (x,y) in visit: ao[y,x,3]=0
    return Image.fromarray(ao,'RGBA')

for cell,fn,bx0,bx1,by0,by1 in tiles:
    im=Image.open(f'cells5/{cell}.jpg').convert('RGB')
    crop=im.crop((int(bx0*W),int(by0*H),int(bx1*W),int(by1*H)))
    inp=os.path.join(HERE,'inputs',fn+'_b.png'); crop.save(inp)
    cmd=["python3",SKILL,"image-edit","--operation","matting","--image-file",inp,"--token",TOKEN]
    res=subprocess.run(cmd,capture_output=True,text=True,timeout=600)
    t=res.stdout; s=t.find("{"); e=t.rfind("}")
    data=json.loads(t[s:e+1])
    raw=data["result_files"][0]["path"]
    out=flood_strip(raw)
    out.save(f'outputs_final/{fn}.png')
    tp=(np.array(out)[:,:,3]<20).mean()*100
    print(f'{fn:8s} transparent%={tp:5.1f}')
print('done')
