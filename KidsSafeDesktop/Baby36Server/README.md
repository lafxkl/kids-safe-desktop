# 宝贝三十六计 · 家长端服务

《宝贝三十六计》的联机陪练模式需要一个能跑在家里电脑上的小服务：
家长在手机上出题，孩子在平板上判断「这个人能不能跟他走」。

这个目录就是那个服务。**纯 Node 内置模块，不装依赖，不建数据库。**

```sh
node server.js            # 默认 8099 端口
PORT=9000 node server.js  # 想换端口就带环境变量
```

启动后终端会打印两个地址：

```
家长在本机打开：  http://127.0.0.1:8099
孩子在平板上填：  http://192.168.0.100:8099
```

---

## 一、怎么玩

### 家长这边

1. 电脑上 `node server.js`，浏览器打开终端打印的**本机地址**
2. 点「开通房间」，拿到一个 4 位房号
3. 把页面上的**地址 + 房号**念给孩子（有「复制」按钮，可以直接发到微信）
4. 孩子连上后点「开始陪练」
5. 从「出题」里挑一句台词（36 计全在里面），或者自己编一句发出去
6. 孩子的判断会实时出现在「孩子的反应」里，最后点「他识破了 / 他被带走了」收尾

### 孩子这边

平板上打开《宝贝三十六计》→「家长陪我玩」→ 填地址和房号 →「连接」。

连上之后**家长点开始，孩子的屏幕会自动进入场景**，不用孩子再操作什么。
之后每发一句，孩子屏幕上就出现一个陌生人说这句话，孩子点 ✓ 或 ✕ 判断。

---

## 二、为什么不用原工程那套

原工程 `Baby-s_36_Skills-master/Server/` 用的是 `nodejs-websocket` + MySQL，端口 56。

| | 原工程 | 这里 |
|---|---|---|
| 通信 | WebSocket（`nodejs-websocket`） | SSE 下行 + fetch POST 上行 |
| 存储 | MySQL（用户表、房间表） | 全在内存，进程退出即清 |
| 依赖 | 要 `npm install`，要建库建表 | 零依赖，`node server.js` 就跑 |
| 账号 | 注册 / 登录 / 房间列表 / 角色选择 | 一个 4 位房号，没有账号 |

理由很直接：这是家长陪孩子玩五分钟的场景，不该为了开个房间去装数据库。
去掉账号体系之后，原来的 `signIn / signUp / getRooms / createRoom / joinRoom / selectRole`
六个事件全部消失，只剩「建房 / 说话 / 应答」三件事。

原工程那套三角色轮转（`dialog.js` 里按 `room.turn % 3` 轮换 child / referee / scoundrel）
也去掉了 —— 实际陪玩时家长一个人就能既当坏人又当裁判，多一个 referee 只会让孩子分心。

---

## 三、接口

都是 JSON，都带 `Access-Control-Allow-Origin: *`（孩子端是 `file://` 打开的，origin 为 null）。

### `POST /api/rooms`

建房。返回：

```json
{ "ok": true, "code": "4821", "ips": ["192.168.0.100"], "port": 8099 }
```

### `GET /api/info`

只查地址，不建房。家长端用它把「孩子该填什么」显示出来：

```json
{ "ok": true, "ips": ["192.168.0.100", "10.124.3.207"], "port": 8099 }
```

虚拟网卡（VMware / WSL / Hyper-V 的 172.16-31.x、169.254.x）会排到后面。

### `GET /api/rooms/:code/events?role=kid|parent`

SSE 长连接。`role` 决定收哪些消息：

| 消息 | 发给谁 | 什么时候 |
|---|---|---|
| `sync` | 家长 | 刚连上，带 `log` / `score` / `presence`，刷新页面靠它复原 |
| `presence` | 双方 | 房间里有人进出 |
| `joined` | 对面 | 有人连上来了 |
| `left` | 对面 | 有人断开了 |
| `hello` | 孩子 | 家长点了「开始」 |
| `say` | 孩子 | 家长发了一句台词（带 `id` / `who` / `text`） |
| `said` | 家长 | 自己发的台词回显 |
| `answer` | 家长 | 孩子做了判断（`ok: true` = 识破，`false` = 上当） |
| `end` | 双方 | 一局结束（`win` / `text`） |

### `POST /api/rooms/:code/act`

```json
{ "role": "parent", "type": "say", "who": "陌生人", "text": "小朋友，我有一个礼物给你…" }
```

`type` 取 `hello` / `say` / `answer` / `end` / `reset` 之一。

### `GET /api/rooms` · `GET /api/rooms/:code` · `DELETE /api/rooms/:code`

查房间列表、查单个房间、销毁房间。调试用。

---

## 四、几个容易踩的坑

**「孩子连不上」**
九成是地址填错了。家长在电脑上打开的是 `127.0.0.1:8099`，这个地址孩子平板上填了没用，
必须是局域网地址（`192.168.x.x`）。家长端页面右上角那串蓝字才是要念给孩子的。
还连不上就检查两台设备是不是同一个 Wi-Fi（有些路由器开了「AP 隔离」，设备之间不通）。

**房号过期**
房间闲置 3 小时后会被回收，同时没有任何人连着时立刻可回收。重开一个就行。

**改了台词两边对不上**
36 计的台词以**孩子端** `index.html` 里的 `JI` 表为准，`public/data.js` 是导出的副本。
改完台词要重跑：

```sh
node Baby36Server/tools/gen-data.js
```

**明文 HTTP**
鸿蒙那边要在 `module.json5` 里配 `ohos.networkSecurityConfig` 放行 cleartext，
工程里已经在 `resources/base/profile/network_config.json` 配好了。

---

## 五、自测

```sh
# 协议测试：脚本自己扮演孩子端和家长端，走完整一局（37 项断言）
node Baby36Server/tools/test-e2e.js

# 浏览器测试：把孩子端 index.html 真丢进无头 Chrome，验证它能不能接上（16 项断言）
node Baby36Server/tools/test-browser.js

# 截图：用 CDP 打开家长端，点完「开通房间 → 开始」再截（避开 SSE 长连接干扰）
node Baby36Server/tools/shot.js
```

前两个要求 `server.js` 已经在跑。

`test-browser.js` 会在下面的位置临时生成一个 `_e2e.html`，跑完自己删掉，
不会污染工程。它断言的是**浏览器里真实渲染出来的东西**：
台词是不是一个字不差地打完了、点叉之后家长端是不是真的收到了 `answer`。

---

## 六、目录

```
Baby36Server/
├── server.js             服务本体（约 400 行）
├── public/
│   ├── index.html        家长端网页
│   └── data.js           36 计台词库（自动导出，别手改）
└── tools/
    ├── gen-data.js       从孩子端 index.html 导出 data.js
    ├── test-e2e.js       协议测试
    ├── test-browser.js   浏览器端到端测试
    └── shot.js           CDP 截图
```
