# 官网搜索引擎优化方案（kids.siguangblog.cc）

> 版本：2026-11-05　适用站点：https://kids.siguangblog.cc　执行人：开发者本人
> 本文分三块：**已经做了什么 → 还要做什么 → 怎么验证有没有用**。
> 结论先说：这个站点的技术底子不差（HTTPS、HSTS、gzip、移动端适配都在），
> 真正缺的是**可被抓取的信封（robots / sitemap / canonical / 结构化数据）**和**内容页数量（4 页）**。
> 前者今天补齐了，后者是接下来三个月的活。

---

## 一、体检结果（2026-11-05 实测）

| 项目 | 现状 | 判断 |
|---|---|---|
| HTTPS / 证书 | 正常，`http://` → `https://` 301 | ✅ |
| HSTS | `max-age=31536000` | ✅ |
| 压缩 | gzip 已开（`Content-Encoding: gzip`） | ✅ 可用 brotli 再加一层 |
| HTTP 协议 | 响应头为 `HTTP/1.1` | ⚠️ 没开 HTTP/2 |
| `Content-Type` | `text/html`（无 charset） | ⚠️ 建议 nginx 加 `charset utf-8` |
| 缓存控制 | 只有 `ETag` / `Last-Modified`，无 `Cache-Control` | ⚠️ 图片每次都要协商 |
| robots.txt | **404（不存在）** | ❌ 今天已补 |
| sitemap.xml | **404（不存在）** | ❌ 今天已补 |
| canonical | 无 | ❌ 今天已补 |
| 结构化数据 | 无 | ❌ 今天已补 |
| 分享卡片（og/twitter） | 无 | ❌ 今天已补 |
| favicon | `/favicon.ico` 返回 404 | ❌ 今天已补 |
| 404 页面 | 状态码正确，但返回 nginx 默认页 | ⚠️ 今天已补自定义页，需服务器挂上 |
| 重复内容 | `/` 与 `/index.html` 都返回 200 | ⚠️ canonical 已挡，建议再 301 |
| 可索引页面数 | 4 个（首页 / 更新日志 / 权限说明 / 隐私政策） | ❌ 太少，是最大短板 |
| 标题 / 描述 | 都有，但详情页标题没带品牌词、隐私页原先无描述 | ⚠️ 今天已改 |
| 图片 alt | 「首页」「小游戏乐园」这类 2–4 字 | ⚠️ 今天已改成描述性长句 |
| 联系方式 | 页脚原来是占位邮箱 | ✅ 已换成真实邮箱 `2279125469@qq.com` |

一句话：**不是「优化得不够好」，是「搜索引擎几乎没东西可读」**——没有站点地图、没有结构化数据、只有 4 页内容。

---

## 二、今天已经落地的改动（可直接部署）

### 新增文件

| 文件 | 作用 |
|---|---|
| `Website/robots.txt` | 全站允许抓取；`.apk` / `.zip` / `README.txt` 不抓，省爬取预算；声明 sitemap |
| `Website/sitemap.xml` | 4 个 URL + `lastmod` + `changefreq` + `priority` |
| `Website/404.html` | 自定义 404，品牌化、`noindex,follow`、4 个回站入口 |
| `Website/favicon.ico` | 由 `assets/icon.png` 生成（16/32/48 三尺寸） |

### 四个页面统一补的 `<head>`

- `canonical`（与 sitemap 完全一致的绝对地址）
- `robots: index,follow,max-image-preview:large`
- favicon（ico + png）、`apple-touch-icon`、`theme-color`
- Open Graph / Twitter Card（分享到微信、飞书、知乎时是横版大图，不再是一行干巴巴的标题）
- JSON-LD 结构化数据：
  - 首页：`Organization` + `WebSite` + `SoftwareApplication`（含版本号、系统要求、免费价格、9 条 featureList、适龄 3–12）
  - 内页：`WebPage`，用 `isPartOf` / `about` 指回首页的 `#site` / `#app`
- 隐私政策页补上原本缺失的 `meta description`
- 三张详情页标题改为「品牌 + 页面名 + 具体值」，如
  `儿童安全桌面 应用权限说明 · 7 项权限逐条解释`

### 图片与性能

- 截图 alt 从「首页」改为可被图片搜索理解的描述句
- 首屏截图 `fetchpriority="high"`，其余截图 `loading="lazy" decoding="async"`
- 补 `width/height`（按 720×1280 原图）+ CSS `height:auto`，消除滚动时的布局抖动（CLS）

### 发版脚本同步

`Scripts/release.ps1` 新增第 11 步：发版时自动回填
`index.html` 里 JSON-LD 的 `softwareVersion`、`sitemap.xml` 里首页 / changelog / permissions 的 `lastmod`。
不这么做，页面写着 v1.18、结构化数据还写着 v1.17，搜索引擎会认为数据不可信。

> **部署注意**：`robots.txt`、`sitemap.xml`、`404.html`、`favicon.ico` 必须放在**站点根目录**，
> 不是 `Website/` 子目录；nginx 还要配 `error_page 404 /404.html`，否则访问不存在的地址仍是 nginx 默认页。

---

## 三、服务器侧（nginx）还差 5 行配置

```nginx
server {
    listen 443 ssl http2;                 # ① 开 HTTP/2（nginx ≥ 1.25.1 改用独立的 http2 on;）
    server_name kids.siguangblog.cc;
    root /www/wwwroot/kids.siguangblog.cc;
    charset utf-8;                        # ② Content-Type 带上 charset

    location = /index.html { return 301 https://$host/; }   # ③ 去掉重复入口
    error_page 404 /404.html;                               # ④ 自定义 404
    location = /404.html { internal; }
    location = /favicon.ico { try_files $uri /assets/icon.png; }

    location ~* \.html$ { add_header Cache-Control "no-cache"; }         # 发版立即生效
    location ~* \.(png|jpe?g|webp|svg|ico)$ {
        expires 30d;
        add_header Cache-Control "public, max-age=2592000, immutable";   # ⑤ 图片长缓存
    }
    gzip on;
    gzip_types text/css application/javascript application/json image/svg+xml;
}
```

优先级：①③④ 一天内做完；②⑤ 顺手。

---

## 四、关键词地图（先定词，再写页）

站点面向中国大陆家长，主力搜索引擎是**百度**（约 6 成）+ 必应 + 微信搜一搜；Google 只有少量技术型用户。
所以词要按「百度能搜到什么」来选，不是按英文 SEO 教材选。

### 第一层：品牌词（守）

| 词 | 现状 | 目标 |
|---|---|---|
| 儿童安全桌面 | 大概率只有自家页面 | 结果页第 1 位 + 右侧知识卡（结构化数据已在做） |
| 儿童安全桌面 app / 下载 | 无 | 第 1 页前 3 位 |
| KidsSafeDesktop | 无 | 第 1 位（英文品牌词，好拿） |

### 第二层：品类词（抢，竞争中等）

`儿童桌面`、`儿童手机桌面`、`儿童模式 app`、`防沉迷 儿童`、`儿童学习软件 免费`、`无广告 儿童 app`、`儿童护眼 软件`

这些词首页要抢，靠的是**标题 + H1 + 正文密度 + 内链**。目前首页标题已含品牌与场景，**H1 还没有品类词**（见第五节待办 1）。

### 第三层：长尾词（捡，最容易出量）

长尾词单个搜索量小（几十到几百/月），但竞争几乎为零，10 篇内容页就能覆盖，且转化率高——搜这些话的人已经在找解决方案了。

| 长尾词簇 | 承接页面（新建） |
|---|---|
| 儿童桌面 怎么退出 / 忘记家长密码 | `/faq.html` |
| 小孩玩手机 怎么控制时间 / 每天多久合适 | `/guide/screen-time.html` |
| 儿童桌面 app 哪个好 / 怎么选 | `/guide/how-to-choose.html` |
| 儿童 手机 安装不了 apk / 未知来源 | `/download.html` |
| 3–6 岁 识字 古诗 免费软件 | `/learn/poem.html` |
| 在家能做的 儿童科学小实验 | `/learn/science.html` |
| 口算练习 自动批改 / 每天几道 | `/learn/math.html` |
| 视障 / 听障 儿童 训练游戏 | `/care/special-needs.html` |
| 留守儿童 陪伴 / 心理 小课堂 | `/care/left-behind.html` |

---

## 五、首页还要动的一处（需要你点头）

1. **H1 补品类词**（现在是「给孩子一个安全、好玩的成长空间」，没有「儿童桌面」四个字）
   最小改动：在 H1 上方加一行小字 kicker：

   ```html
   <p class="hero-kicker">儿童安全桌面 · 3–12 岁儿童桌面 App</p>
   ```

   视觉上是一行浅灰小字，不动主标题排版，但把品类词放进了正文第一屏。

2. ~~新建 `/about.html`~~ → **已完成**（见第十一节）。页面里带了「三条不做」「6 天 18 个版本」的时间线、
   数字滚动动画和滚动出现效果，`prefers-reduced-motion` 打开时全部静态降级。
   四张已有页面（首页页脚、更新日志、权限页、隐私页）都已加内链指向它。

3. ~~页脚邮箱是占位符~~ → **已完成**：统一换成 `2279125469@qq.com`，
   覆盖首页页脚、权限页正文、about 联系按钮、privacy 第 13 节、App 内隐私政策文本、
   `Docs/隐私政策.md`、`StoreMaterials/listing_copy.md`，共 7 个文件。
   QQ 邮箱对家长沟通够用；若以后有域名邮箱，只需全局替换这一个串。

---

## 六、内容排期（三个月，10 个页面）

原则：**一页只打一个词簇**，每页 ≥800 字中文、3 张以上配图（带 alt）、正文内 2–3 条内链回到首页/下载页，
页尾放「相关页面」三条互链，形成网状结构（搜索引擎靠内链判断页面重要性）。

### 第 1 个月：把「转化路径」修好（3 页）

| 优先级 | 页面 | 目标词 | 内容要点 | 字数 |
|---|---|---|---|---|
| P0 | `/download.html` | 儿童桌面 下载 / 安装 | 安卓/Windows 两个安装包、版本号、SHA-256、安装步骤（含「未知来源」图示）、安装失败 6 种排查、iPad 说明 | 1200 |
| P0 | `/faq.html` | 儿童桌面 怎么退出 / 忘记密码 | 从 App 内 29 条帮助内容里挑 12 条最常问的改写；带 `FAQPage` 结构化数据 | 1500 |
| P1 | `/guide/screen-time.html` | 小孩玩手机 控制时间 | 各年龄段建议时长（引权威来源，如国家卫健委/WHO 建议）、怎么设、做不到怎么办 | 1000 |

### 第 2 个月：吃长尾流量（4 页）

| 页面 | 目标词 | 内容要点 |
|---|---|---|
| `/learn/science.html` | 儿童科学小实验 在家 | 12 个实验，每个：材料 / 步骤 / 原理一句话 / 适龄 / 安全提示。**这是最容易被家长收藏和外链的一页** |
| `/learn/poem.html` | 儿童古诗 26 首 / 古诗朗读 | 26 首诗全文 + 一句话讲解 + 适合几岁 + 朗读功能说明 |
| `/care/special-needs.html` | 视障儿童 训练游戏 / 听障 | 三条通道各写清「练什么、怎么带、多久一次」，不承诺疗效 |
| `/guide/how-to-choose.html` | 儿童桌面 app 哪个好 | 客观对比维度（广告、外链、权限、离线、家长锁），顺带说明本产品的取舍 |

### 第 3 个月：信任与品牌（3 页）

| 页面 | 作用 |
|---|---|
| `/about.html` | 开发者介绍、联系方式、更新承诺（E-E-A-T） |
| `/guide/parent-lock.html` | 家长锁使用教程（含截图），承接「忘记密码」类长尾 |
| `/changelog.html` 持续更新 | 已有 18 个版本条目，是现成的「活跃度」证据，保持每版都写 |

> 每新增一页，**必须同步** `sitemap.xml`（加 `<url>`）并在已有页面加一条内链。
> 孤立页面（没有任何内链指向）在百度那边基本等于不存在。

---

## 七、站外：这个域名最缺的是「别人提到你」

`.cc` 域名 + 新站 + 零外链，是迟迟不收录的主要原因。技术分已经拿满了，剩下的靠人。

**服务器在境外，不需要 ICP 备案**——这省掉了最大的一个前置门槛，也意味着主战场是 **Bing（+ Google）**：
必应对新站友好、收录以天计，索引还同时供给 ChatGPT 一类产品。百度可做可不做（境外站抓取本来就不稳）。

按性价比排序：

1. **软件著作权证书编号放到 `/about.html`**（免费、可核验；现在还没有，属于待补）。
2. **应用市场**（最大流量来源，且市场页能给官网导权重）：华为、小米、OPPO、vivo、应用宝、酷安。每家都填官网链接。
3. **酷安**：发一篇「开发者自荐」，酷安用户对独立开发者友好，是最容易拿到真实评价和外链的地方。
4. **知乎**：用「儿童桌面/防沉迷」相关问题写干货回答（不硬推产品），签名档带官网。
5. **小红书 / 视频号**：竖版短视频演示「家长锁 + 到点休息」，这类内容在宝妈群体传播最快。
6. **B 站**：3 分钟功能演示，标题带「儿童桌面」。视频页可在官网用 `VideoObject` 结构化数据引用。
7. **GitHub**：仓库 README 放官网链接与截图（技术型外链，权重不低，Bing 尤其吃）。
8. **QQ 群**：官网已挂「儿童安全桌面开发者交流群」（1134839505）。群本身不给权重，
   但它是**唯一真实的用户反馈入口**——有人愿意进群，说明站点已经开始被真人看到，比统计数字可信。

> 不要做：买外链、批量采集别家文案、关键词堆砌页、隐藏文字、把 APK 直链当落地页发到论坛。

---

## 八、提交与监测（Bing 优先）

### 上线当天（30 分钟）

1. **必应站长** `bing.com/webmasters` → 添加站点 `kids.siguangblog.cc` → 验证
   （推荐 DNS TXT；不想动 DNS 就把验证码填进 `about.html` 里预留的那行 `msvalidate.01` 注释）→ 提交 `sitemap.xml`。
2. **IndexNow 已经自动化**：`Website/` 根目录的 `da9d8752….txt` 是密钥文件，
   `deploy.ps1` 传完文件会自动 POST 给 `api.indexnow.org`，Bing / Yandex / Seznam 一起收到，不用等爬虫。
3. **Google Search Console** → 验证 + 提交 sitemap（海外用户与开发者流量，顺带看 Core Web Vitals）。
4. **微信搜一搜**：公众号「时光笔记」后台「搜索优化」里绑定站点 `kids.siguangblog.cc`（国内分享链路的兜底；公众号简介里也写上站点域名，两处互指更容易被关联）。
5. Bing 后台「URL 检查」对 `/about.html` 手动请求编入索引；用 **Rich Results Test** 检查 JSON-LD 有没有报错。

### 每周（10 分钟）

- 必应站长后台：看「已编入索引」页数、抓取错误、「搜索关键词」报表——真实进来的词直接决定下一批写什么
- `site:kids.siguangblog.cc`（Bing）看收录概况
- 服务器日志 `grep -i bingbot /var/log/nginx/access.log`：抓了哪些、返回什么状态码
- 百度若也提交了，`site:` 查一下就好，别把它的收录速度当 KPI

### 预期节奏（保守估计，别信「一个月上首页」）

| 时间 | 合理预期 |
|---|---|
| 第 1 周 | Bing 收录首页 + 2 个内页（IndexNow 生效后通常 24–72 小时）；Google 收录首页 |
| 2–4 周 | Bing 收录全部 5 页；搜「儿童安全桌面」第 1 位 |
| 2–3 个月 | 长尾词进 Bing 前 3 页；自然访问 50–200/月 |
| 3–6 个月 | 10 个内容页收录 ≥8；自然访问 500–1500/月；下载转化 3–8% |

### 核心 KPI（只看这三个）

1. **收录页数** / 已提交页数
2. **自然搜索访问量**（不加它，其他都是猜）
3. **下载次数 ÷ 自然访问量** = 转化率

> 加统计脚本时注意：任何第三方统计（GA4、Clarity、Umami）都算「第三方 SDK」，
> 必须同步更新 `privacy.html` 的 SDK 清单，否则和「无广告、无第三方追踪」的宣传自相矛盾——
> 儿童类产品这一点被举报，代价远大于流量收益。建议用自建 Umami（同一台服务器），不涉及第三方。

---

## 九、红线（儿童类产品特有）

- 文案不得出现「提升智商」「治疗注意力不集中」「矫正」等功效承诺
- 不得用「最」「第一」「国家级」等绝对化用语（广告法）
- 涉及儿童个人信息的功能，页面上要说清「本机存储、不上传」
- 隐私政策、权限说明、实际代码三者必须一致（这是应用市场审核和监管检查同时会看的）
- 官网外链到第三方（QQ 群、应用市场、视频平台）的地方，注明「第三方服务」，
  并提醒用户不要在公开渠道留孩子的真实信息——这两句已经写在交流群区块里

---

## 十、部署（`deploy.bat`，不备份、直接覆盖）

改完 `Website/` 之后一条命令上线：

```
deploy.bat                                  → 读 Scripts\deploy.config.json
deploy.bat -Server root@1.2.3.4             → 临时指定服务器
deploy.bat -Server root@1.2.3.4 -WithDownloads   → 连 APK/ZIP 一起传（发版时）
deploy.bat -DryRun                          → 只打印计划，不连服务器
```

先用一次性配置免得每次敲参数，`Scripts/deploy.config.json`：

```json
{ "server": "root@1.2.3.4", "port": 22, "remotePath": "/www/wwwroot/kids.siguangblog.cc", "key": "C:\\Users\\Administrator\\.ssh\\id_ed25519" }
```

脚本做的事（`Scripts/deploy.ps1`）：

| 步 | 动作 |
|---|---|
| 1 | 校验 9 个核心文件齐全（少一个直接中止，不会传半套） |
| 2 | 列出清单与体积；`downloads/` 默认跳过，加 `-WithDownloads` 才传 |
| 3 | `ssh mkdir -p` 确认远端目录存在 |
| 4 | `scp` 覆盖上传（核心文件 + `assets/`，可选 `downloads/`） |
| 5 | 逐个请求线上地址（带 `?v=<ticks>` 绕缓存），状态码 + 关键文字双校验 |
| 6 | 自动向 `api.indexnow.org` 推送 sitemap 里的全部 URL |

**不备份**：按你的要求直接覆盖。风险在于误删远端文件——脚本只上传、不删除，
所以线上多余文件不会被清掉；真要回滚只能重新上传上一版（`_release/` 里只有安装包，没有网站快照）。


---

## 十一、下一步（等你决定）

- [x] 主战场确认为 Bing（服务器境外，无需备案）
- [x] 建 `/about.html`（含滚动动效、数字滚动、进度条；`prefers-reduced-motion` 自动降级）
- [x] 官网挂 QQ 群二维码（首页「加入交流群」+ about 联系区）
- [x] 部署脚本 `deploy.bat` / `Scripts/deploy.ps1`（不备份，直接覆盖）
- [x] 换掉占位邮箱 → `2279125469@qq.com`（7 个文件一起换，含 App 内隐私政策）
- [ ] Bing 后台：确认站点验证通过 → 提交 `sitemap.xml`（**必须先把新文件部署上线，否则 sitemap 是 404**）
- [ ] nginx 那 5 行配置（HTTP/2、charset、`/index.html` 301、`error_page 404`、图片缓存）
- [ ] H1 kicker 是否要加（视觉改动）
- [ ] 先做 `/download.html` 与 `/faq.html`（素材从 `Docs/` 和 App 内 29 条帮助里取）
