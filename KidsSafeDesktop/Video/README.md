# 宣传片

两个版本，同一套时间轴、同一轨音乐：

| 文件 | 用途 | 第 7 个镜头 |
|---|---|---|
| `儿童安全桌面-宣传片-1080p.mp4` | 官网 / 自有渠道 | 「立即下载」：官网域名 + 二维码 + 三步安装 |
| `儿童安全桌面-宣传片-1080p-平台版.mp4` | B 站等第三方平台投稿 | 「还有更多」：六个功能块 + 实验页截图 |

平台版删掉了全部站外导流信息——官网域名、二维码、Gitee 字样、下载步骤都不出现，首尾只剩
「永久免费 · 无广告 · 不收集儿童信息」。这类内容属于平台判定的「诱导站外注册 / 关注 / 交易」，
投稿时必须用平台版；要放链接请走平台官方组件（悬赏带货、经营助手、效果广告、课堂/商品上架）。

规格：1920×1080 / 30fps / 46.4s / 纯音乐 + 画面字幕。

## 怎么做出来的

不用剪辑软件，用网页当「动效舞台」逐帧录制——好处是中文排版和真机截图都保持矢量清晰，改文案只改 HTML。

```
promo.html?mode=site|platform ──无头 Chrome 逐帧截图──▶  frames/f00000.jpg …
                                                           │
music.py ──▶ music.wav ────────────────────────────────────┴─▶ encode.py ──▶ MP4
```

- `promo.html`：8 个场景全部用绝对时间轴渲染，`window.render(t)` 是纯函数——同一个 `t` 永远出同一帧，所以可以任意跳帧、补帧、重录。`?mode=` 决定第 7 屏用哪一版（`s7` / `s7b`）。
- `capture.py`：CDP 起无头 Chrome（1920×1080，deviceScaleFactor=1），循环 `render(t)` + `Page.captureScreenshot`，30fps 约 4 分钟跑完 1392 帧。
- `music.py`：numpy 合成的 I–V–vi–IV 进行（C–G/B–Am–F），柔和 pad + 木琴拨奏 + 轻打击，无版权问题。
- `encode.py`：ffmpeg 合成 H.264（CRF 18，BT.709，`yuv420p` 电视范围）并抽封面。

## 改片

1. 改文案/时间：`promo.html` 里 `SC` 数组是场景起止时间，各场景的 `fade/lines/enterPhone` 用绝对秒数；总时长改 `DUR`。
2. 重录（参数顺序：总时长、时间点或 `-`、模式、帧目录）：

```bat
:: 平台版
python capture.py 46.4 - platform frames_platform
python encode.py frames_platform 儿童安全桌面-宣传片-1080p-平台版

:: 官网版
python capture.py 46.4 - site frames
python encode.py frames 儿童安全桌面-宣传片-1080p
```

单帧预览（不录整片，用来对位置）：

```bat
python capture.py 46.4 "6.4,14.0,27.8" platform frames_test
```

3. 换截图：把新图放 `Website/assets/`，再跑一遍裁剪（去掉底部 v1.19 版本号水印），生成 `Video/assets/*.png`。

## 素材说明

- 手机截图取自 `../Website/assets/screenshot_*.png`，裁掉底部 50px 去掉截图里残留的旧版本号。
- 配色沿用官网：`#FDF6EC` 底 / `#E2603D` 主橙 / `#2FB8CE` 点缀 / `#3A2B22` 文字。
- 二维码 `qr-site.png` 指向 `https://kids.siguangblog.cc`，随域名变更需重新生成；**只在官网版用**。

## 依赖

Chrome、`websocket-client`、`numpy`、`imageio-ffmpeg`、`Pillow`。
`frames*/`、`_chromeprofile/` 是中间产物，不必提交。
