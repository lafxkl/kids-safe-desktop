import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from clean_img import clean

SRC = r"d:\WorkBuddy\儿童安全桌面\generated-images"
DST = r"D:\WorkBuddy\KidsSafeDesktop\Resources\Images"

mapping = {
    "Cute_cartoon_panda_character_s_2026-10-31T07-54-30.png": "maze_panda.png",
    "Cute_cartoon_butterfly_with_pu_2026-10-31T07-54-45.png": "ll_butterfly.png",
    "Cute_cartoon_bee_with_yellow_a_2026-10-31T07-54-55.png": "ll_bee.png",
    "Cute_cartoon_ladybug_with_red__2026-10-31T07-56-15.png": "ll_ladybug.png",
    "Cute_cartoon_snail_with_a_spir_2026-10-31T07-56-26.png": "ll_snail.png",
    "Cute_cartoon_green_caterpillar_2026-10-31T07-56-37.png": "ll_caterpillar.png",
    "Cute_cartoon_purple_spider_wit_2026-10-31T07-56-47.png": "ll_spider.png",
    "Cute_cartoon_dragonfly_with_tr_2026-10-31T07-56-59.png": "ll_dragonfly.png",
    "Cute_cartoon_beetle_with_a_rou_2026-10-31T07-57-11.png": "ll_beetle.png",
    "Cute_cartoon_green_start_flag__2026-10-31T07-57-22.png": "maze_start.png",
    "Cute_cartoon_golden_trophy_wit_2026-10-31T07-57-34.png": "maze_end.png",
}

os.makedirs(DST, exist_ok=True)
for src_name, dst_name in mapping.items():
    sp = os.path.join(SRC, src_name)
    dp = os.path.join(DST, dst_name)
    if not os.path.exists(sp):
        print("MISSING", sp); continue
    clean(sp, dp)
print("DONE")
