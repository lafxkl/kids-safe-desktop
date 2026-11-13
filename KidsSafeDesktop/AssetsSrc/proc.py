import json, sys
sys.path.insert(0, "D:/WorkBuddy/KidsSafeDesktop/AssetsSrc")
from clean_img import clean

data = json.load(open(sys.argv[1], encoding="utf-8"))
dest = "D:/WorkBuddy/KidsSafeDesktop/Resources/Images"
for word, fname, src in data:
    clean(src, f"{dest}/{fname}")
    print(f"done {word} -> {fname}")
