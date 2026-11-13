# 电子课本渲染脚本

把一批 PDF 电子课本整理成「服务端可直接翻页」的形态：每页一张 jpg + 原件 PDF + 清单片段。

两个入口：

- `render_subject.py` —— **整科批量**（一个科目目录下多个版本一次跑完，并自动合并 `books.json`）。日常补内容用这个。
- `render.py` —— 单版本流程，产出 `books.fragment.json` 片段，需要手工粘进清单。适合一次性试跑或临时补一个版本。

`winrt-render/` 是解析失败时的兜底渲染器。

## 为什么是页图而不是 PDF 原件

课本单本 12–29 MB、120–150 页，安卓 WebView 本身不渲染 PDF。做成「一页一张 jpg（约 115 KB）」后，客户端翻到哪页取哪页，还能缓存到本地离线看，翻页也快。

## 用法

```bash
# 先把兜底渲染器编译好（只需一次）
cd Scripts/BookRender/winrt-render && dotnet build -c Release

# 整科批量（推荐）：一个科目目录，其下一层是「人教版 / 北师大版 / …」
python Scripts/BookRender/render_subject.py \
  --subject math --name 数学 --icon 🔢 \
  --src "D:/小学数学" \
  --out "<服务端>/wwwroot/books"
```

版本文件夹与书号的对应关系写在 `render_subject.py` 顶部的 `EDITIONS` 表里：

```python
"人教版": ("rj", "人教版", "人教", 6),   # 文件夹 → 代号 / 显示名 / 芯片短名 / 最大年级
```

新增一个版本就加一行；五四制版本的最大年级填 5。

日志写到 `logs/render_<科目>.log`（**不要**放 `wwwroot`：会被静态资源打包任务锁住，`dotnet build` 直接失败）。

跑完会得到：

```
<out>/pages/math-rj-1a/page-0001.jpg …  # 页图，0001 同时当封面
<out>/pdf/math-rj-1a.pdf                # 原件（同盘硬链接，不额外占空间）
<out>/books.json                        # 已自动合并：subjects / editions / books
```

补渲染个别书（比如上次某本失败了）：

```bash
python Scripts/BookRender/render_subject.py --subject math --name 数学 --src ... --out ... \
  --only math-2b,math-3b
```

已经有页图的书写进日志后跳过，所以中断了直接重跑即可续上。合并逻辑是「按 id 覆盖 + 保留未参与本次渲染的」，补跑单本不会把本科目其它书从清单里冲掉。

## 参数

| 参数 | 说明 |
|---|---|
| `--subject` | 科目代号 `chinese` / `math` / `english`，会写进清单 |
| `--name` | 科目中文名，界面显示用 |
| `--icon` | 科目图标，默认 🔢 |
| `--src` | 科目根目录，其下一层是各版本文件夹 |
| `--out` | 服务端的 `wwwroot/books` |
| `--dpi` | 页图分辨率，默认 120（约 870px 宽、115 KB/页） |
| `--only` | 只处理指定书号，逗号分隔 |
| `--editions` | 只处理指定版本文件夹，逗号分隔 |
| `--force` | 已有页图也重新渲染 |

## 书号规则

`{科目}-{版本}-{年级}{a上册|b下册}`，如 `math-rj-1a` = 人教版一年级上册、`math-qd54-5b` = 青岛版五四制五年级下册；`-old` = 同册旧版存档。语文沿用早期书号 `rj-1a` / `rj54-5b`，没有重命名（避免动 3000 多张页图）。

脚本按文件名里的「X年级 / 上册|下册 / (2024秋版)」自动识别年级、册次和年份；同一册有多份时，带年份的算现行版，不带的标成旧版存档。

## 两条渲染路线

1. **workbuddy-pdf CLI**（首选）：120 DPI 约 13 秒/本，走 `sh <skill>/scripts/run.sh <pdf> <out>/ -f jpg --dpi 120`。
2. **Windows 自带渲染器**（兜底）：有的 PDF 会让 CLI 静默返回 127（本次是两本 2026 春版，连文本解析都失败）。这时自动退回 `winrt-render`，用 `Windows.Data.Pdf` 渲成 PNG，再由 Pillow 转 jpg。

两条都不通才会跳过该书，并在结束时列出来。

## 已知限制

- CLI 读不了非 ASCII 路径，脚本会先把 PDF 拷成 `rj-1a.pdf` 这样的临时名再渲染。
- 页数以磁盘上实际图片数为准，服务端不会相信清单里写的页数。
- `render_subject.py` 会自动更新 `wwwroot/books/books.json`；服务端按文件时间缓存清单，**不用重启**。
- 106 本 / 1.2 万页整科渲染约 77 分钟（单线程，每本 30–90 秒，看源 PDF 大小）。
