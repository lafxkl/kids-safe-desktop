# README 图片与徽章的托管方式

> 适用：仓库根目录 `README.md` / `README.en.md` 的顶部横幅、平台徽章、界面截图。
> 结论：**全部用官网域名的绝对地址**，不用仓库相对路径。

## 为什么不用相对路径

Gitee 手机端对 README 的图片路径有重写逻辑，但对大图和一些扩展名不可靠：

| 写法 | 结果 |
|---|---|
| 相对路径 `.png` 大图 | 时好时坏，弱网下常年显示成裂图 |
| 相对路径 `.webp` | 基本不被重写，直接 404，只剩 alt 文字 |
| `width="100%"` / `width="31%"` | Gitee 会把 `<img>` 的 `src` 弄丢，同样变裂图 |
| **绝对地址（本方案）** | 浏览器直接向官网取图，与 Gitee 的规则无关 |

所以 README 里所有 `<img>` 都不带 `width`，宽度交给 CSS 的 `max-width:100%`。

## 文件放在哪

| 用途 | 仓库内源文件 | 线上地址 |
|---|---|---|
| 顶部横幅 | `KidsSafeDesktop/Website/assets/header.png`（1280×720 原图） | `https://kids.siguangblog.cc/assets/header.png`（官网分享卡片 `og:image` 用） |
| 顶部横幅（README 用） | 由上面那张生成，不入库 | `https://kids.siguangblog.cc/assets/header-readme.png` |
| 五张界面截图 | `KidsSafeDesktop/Website/assets/screenshot_*.png` | `https://kids.siguangblog.cc/assets/screenshot_*.png` |
| 五枚徽章 | `KidsSafeDesktop/Website/assets/badges/*.svg` | `https://kids.siguangblog.cc/assets/badges/*.svg` |

## 换图流程

```bash
# 1. 改仓库里的源文件（Website/assets/ 下同名替换）
# 2. 传到官网服务器
scp -i ~/.ssh/id_rsa_49 KidsSafeDesktop/Website/assets/screenshot_home.png \
    root@43.153.192.19:/www/wwwroot/kids.siguangblog.cc/assets/

# 3. 改属主与权限，和同目录其他文件保持一致
ssh -i ~/.ssh/id_rsa_49 root@43.153.192.19 \
    "chown www:www /www/wwwroot/kids.siguangblog.cc/assets/screenshot_home.png && \
     chmod 744 /www/wwwroot/kids.siguangblog.cc/assets/screenshot_home.png"

# 4. 确认公网可访问
curl -sI https://kids.siguangblog.cc/assets/screenshot_home.png | head -3
```

上传完不用改 README，地址不变。只有新增文件才需要动 README。

## 顶部横幅：header-readme.png 怎么来的

原图 534 KB，手机首屏太重，生成一版 1000 px 宽、256 色的：

```python
from PIL import Image

im = Image.open('header.png').convert('RGB')
im = im.resize((1000, round(im.height * 1000 / im.width)), Image.LANCZOS)
im.quantize(colors=256, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG) \
  .save('header-readme.png', optimize=True)
# 1280×720 / 534 KB  →  1000×562 / 173 KB
```

量化后肉眼无色带（截图类 UI 大面积纯色，损失很小）。**不要存成 `.webp` 再走相对路径**，Gitee 不认。

## 徽章：为什么自绘

`shields.io` 在国内经常超时或直接连不上，README 顶部就变成一排空白。改成自绘 SVG 放在官网 `assets/badges/`，不依赖任何第三方。

五枚分别是 `dotnet-10` / `maui` / `platform` / `platform-alt` / `license-mit`，源码在 `Website/assets/badges/`。

生成要点（改了文案就按这个重算，别手改宽度）：

```python
from PIL import ImageFont

f = ImageFont.truetype('C:/Windows/Fonts/verdana.ttf', 11)
w = lambda t: f.getlength(t)

# 盒宽 = 文字宽 + 左右各 5px；标签段灰底 #555，数值段配色
# .NET / MAUI 用 #512BD4，platform 用 #007ec6，license 用 #4c1
```

结构照 shields 的 flat 风格：`height=20`、`rx=3`、顶部叠 10% 白渐变做立体感、文字 `y=15` 画一层 `#010101` 30% 的投影再 `y=14` 画白字。

两个坑：

1. **`fill` 必须带 `#`**。写成 `fill="512BD4"` 不报错，整块渲染成黑色，只有肉眼看预览才发现。
2. 数值文字加 `textLength` + `lengthAdjust="spacingAndGlyphs"`，浏览器替换字体时也不会溢出盒外。

## 带宽

一次 README 浏览大约 1.1 MB，几乎全是五张截图（236–292 KB 各一张）。要省流量可以把截图也压成 2 倍图（如 540→400 px 宽），能再降六成左右。
