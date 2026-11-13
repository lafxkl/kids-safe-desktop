import subprocess, json, os, shutil, glob
from PIL import Image

SKILL = r"D:/Program Files/WorkBuddy/resources/app.asar.unpacked/resources/plugins/workbuddy-builtin/skills/buddy-image-processing/scripts/buddy-image-processing.py"
TOKEN = "ck_t_g2v1xwqqtu68.mNi3jti0Fp_gB7PVQQ4_lQ"
HERE = os.path.dirname(os.path.abspath(__file__))
W,H=132,148
# uniform safe box: exclude char(left), pinyin(top), example strip(bottom), side bars
X0,X1,Y0,Y1 = 0.30,0.92,0.18,0.62
os.makedirs('inputs_u',exist_ok=True); os.makedirs('outputs_u',exist_ok=True)
# use cells5 tiles
srcs = {
 'sun':'r0c0','moon':'r0c1','fire':'r0c3','mountain':'r0c4',
 'hand':'r2c0','ear':'r2c2','eye':'r2c3','door':'r3c2',
 'car':'r4c4','shoe':'r5c0','hat':'r5c1','cup':'r4c1'}
ok=fail=0
for name,cell in srcs.items():
    im=Image.open(f'cells5/{cell}.jpg').convert('RGB')
    crop=im.crop((int(X0*W),int(Y0*H),int(X1*W),int(Y1*H)))
    inp=os.path.join(HERE,'inputs_u',name+'.png'); crop.save(inp)
    out=os.path.join(HERE,'outputs_u',name+'.png')
    cmd=["python3",SKILL,"image-edit","--operation","matting","--image-file",inp,"--token",TOKEN]
    res=subprocess.run(cmd,capture_output=True,text=True,timeout=600)
    t=res.stdout; s=t.find("{"); e=t.rfind("}")
    if s==-1 or e==-1: print("NOJSON",name); fail+=1; continue
    data=json.loads(t[s:e+1])
    if data.get("status")!="completed" or not data.get("result_files"):
        print("BAD",name,data.get("status")); fail+=1; continue
    shutil.copy(data["result_files"][0]["path"],out)
    print("OK",name); ok+=1
print(f"DONE ok={ok} fail={fail}")
