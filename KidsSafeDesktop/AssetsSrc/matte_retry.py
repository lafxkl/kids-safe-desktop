import subprocess, json, os, shutil
from PIL import Image

SKILL = r"D:/Program Files/WorkBuddy/resources/app.asar.unpacked/resources/plugins/workbuddy-builtin/skills/buddy-image-processing/scripts/buddy-image-processing.py"
TOKEN = "ck_t_g2v0d3cvi39c.kMLEiNgL6YduezRb0osvdg"
HERE = os.path.dirname(os.path.abspath(__file__))

tiles = [
 ('r2c0','手','hand',0.42,0.97,0.12,0.72),
 ('r2c2','耳','ear',0.42,0.97,0.12,0.72),
 ('r2c3','目','eye',0.38,0.82,0.33,0.82),
 ('r3c2','门','door',0.42,0.88,0.12,0.72),
 ('r4c4','车','car',0.10,0.97,0.10,0.45),
 ('r5c0','鞋','shoe',0.14,0.95,0.16,0.58),
 ('r5c1','帽','hat',0.40,0.97,0.10,0.55),
 ('r4c1','杯','cup',0.40,0.97,0.12,0.72),
]
W,H=132,148
ok=fail=0
for cell,word,fn,bx0,bx1,by0,by1 in tiles:
    im=Image.open(f'cells5/{cell}.jpg').convert('RGB')
    crop=im.crop((int(bx0*W),int(by0*H),int(bx1*W),int(by1*H)))
    inp=os.path.join(HERE,'inputs',fn+'.png'); crop.save(inp)
    out=os.path.join(HERE,'outputs',fn+'.png')
    cmd=["python3",SKILL,"image-edit","--operation","matting","--image-file",inp,"--token",TOKEN]
    try:
        res=subprocess.run(cmd,capture_output=True,text=True,timeout=600)
        t=res.stdout; s=t.find("{"); e=t.rfind("}")
        if s==-1 or e==-1:
            print("NOJSON",fn,res.stdout[-150:]); fail+=1; continue
        data=json.loads(t[s:e+1])
        if data.get("status")!="completed" or not data.get("result_files"):
            print("BAD",fn,data.get("status"),str(data)[:150]); fail+=1; continue
        shutil.copy(data["result_files"][0]["path"],out)
        print("OK",fn,word,os.path.getsize(out)); ok+=1
    except Exception as ex:
        print("ERR",fn,repr(ex)); fail+=1
print(f"DONE ok={ok} fail={fail}")
