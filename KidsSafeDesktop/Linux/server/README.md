# 故事服务端（Linux）

给 Linux 版儿童安全桌面提供在线故事内容的最小服务。

- **只用 Python 3 标准库**，不装第三方包、不建数据库、不需要 root；
- 一个进程、两个 JSON 文件，`python3 kidssafe_story_server.py` 就能跑；
- 附带一个只读的家长后台网页：确认服务在跑、搜故事、读正文，以及看孩子端
  上报上来的**学习情况**（关卡进度 + 编程作业成绩）。

它和 Windows 那套 `KidsSafeServer`（.NET，带账号、同步、设置下发）**不是同一个东西**：
这个服务只回答「有哪些故事、正文是什么」，外加收一份学习情况，不做账号、不做配置同步。

Linux 客户端走网络只有两处，都已经接上：故事屋拉内容、小小程序员报成绩。

---

## 一、跑起来

### 直接跑（先试试）

```sh
cd Linux/server
python3 kidssafe_story_server.py
```

终端会打印：

```
儿童安全桌面 · 故事服务端 v1.1.0
内容：19 篇 / 7 类
学习情况：已收到 0 个孩子的上报
家长后台：http://127.0.0.1:8080/
孩子端可填：http://192.168.1.23:8080
```

浏览器打开家长后台那个地址，能看到分类和故事列表就说明服务正常。

### 装成用户级服务（推荐）

```sh
cd Linux/server
./install-server.sh

# 换端口
PORT=9000 ./install-server.sh

# 卸载（会删掉程序与内容目录）
./install-server.sh --uninstall
```

脚本做三件事：把程序装到 `~/.local/share/kidssafe-story`、写一个
`~/.config/systemd/user/kidssafe-story.service`、`enable` 并启动。

用**用户级**服务而不是系统级：服务只读本地文件、只监听局域网，不需要 root；
孩子桌面也是同一用户登录运行的，用户级服务随登录启动就够了。系统里不会多一个
以 root 身份跑的网络进程。

> 想在**没人登录时**也提供服务，执行一次 `sudo loginctl enable-linger $USER`。
> 只是家里自己用的话不需要。

---

## 一·补、部署到一台有公网域名的机器（项目自己就是这么跑的）

家里局域网自建用上面那套就够了。如果想让两台不在同一网络的机器也能联机下棋，
服务端就得有公网地址。项目自己用的是 **CentOS 7 + 宝塔 + nginx + Python 3.6**，
没有 root 之外的额外依赖，步骤只有三步：

```sh
# 1. 传代码（不需要装任何 Python 包）
rsync -a kidssafe_story_server.py data/ web/ root@<服务器>:/www/wwwroot/kidssafe-story/

# 2. 装成系统服务，只监听 127.0.0.1
#    （用户级服务也行，但 CentOS 7 上系统级更省事；记得建一个 nologin 用户）
cat > /etc/systemd/system/kidssafe-story.service <<'EOF'
[Unit]
Description=KidsSafe Story Netplay Server
After=network.target

[Service]
Type=simple
User=kidssafe
WorkingDirectory=/www/wwwroot/kidssafe-story
ExecStart=/usr/bin/python3 /www/wwwroot/kidssafe-story/kidssafe_story_server.py --host 127.0.0.1 --port 8000
Restart=always

[Install]
WantedBy=multi-user.target
EOF
systemctl enable --now kidssafe-story

# 3. nginx 反代，单独放一个文件，出问题删掉 reload 就回滚
cat > /etc/nginx/conf.d/story.conf <<'EOF'
location = /story { return 301 /story/; }
location ^~ /story/ {
    proxy_pass http://127.0.0.1:8000/;   # 末尾斜杠会吃掉 /story/ 前缀
    proxy_set_header Host $host;
    add_header Cache-Control "no-store" always;   # 联机轮询不能被缓存
}
EOF
nginx -t && nginx -s reload
```

客户端里填的地址是 `https://<域名>/story`（不带 `/api`），客户端自己拼后缀。

**Python 3.6 要注意**：`http.server.ThreadingHTTPServer` 是 3.7 才有的，
3.6 上会 `ImportError`。程序开头有一段兼容垫片，用 `socketserver.ThreadingMixIn`
临时拼一个同名类；3.7+ 上这段不生效，删掉也不影响。

**家长后台网页用的是相对路径**（`api/health` 而不是 `/api/health`），
所以同一份 `web/index.html` 挂在根目录（`/`）和子路径（`/story/`）下都能用 ——
改回绝对路径的话，挂在子路径下会全部 404。

项目自己在跑的那台：`https://kids.siguangblog.cc/story/`。

---

## 二、让客户端连上

1. 家长在儿童桌面上点右上角「家长」→ 输密码；
2. 找到「在线故事与内容」，把服务端地址填进去，例如 `http://192.168.1.23:8080`；
3. 打开同一张卡片右上角的开关；
4. 回桌面点「故事屋」。

**最常见的坑**：填了 `http://127.0.0.1:8080`。
`127.0.0.1` 指的是「运行客户端的那台机器」，如果服务和客户端在同一台电脑上，
这样填确实能用；但如果你是在另一台设备上看，必须填局域网地址（`192.168.x.x`）。
服务端启动时打印的「孩子端可填」那一行才是对的。

地址填错、服务没开、防火墙挡住，客户端都只会显示「连不上故事服务器」，
不会崩、不会卡——内容服务始终是可选增强。

学习情况上报走的是同一个地址**和同一个开关**：上面第 3 步的开关关着时，
客户端一个请求都不发，成绩只留在本机。这条是有意为之——上报意味着孩子的数据
离开这台电脑，得让家长明确打开，而不是装完自己就开始传。

---

## 三、加内容

有两种方式，改完在家长后台点「重新载入内容」即可生效，**不用重启服务**。

### 1. 改 `data/stories.json`

```json
{
  "categories": [
    { "id": 1, "name": "睡前故事" }
  ],
  "stories": [
    {
      "id": 1001,
      "typeId": 1,
      "title": "月亮邮差",
      "shortDesc": "一句话介绍，列表里显示",
      "content": "正文。段落之间用 \\n\\n 分开。"
    }
  ]
}
```

- `id` 必须唯一，重复会被自动改号；
- `typeId` 要对应 `categories` 里的 `id`；只写 `type`（分类名）也能识别，写错会自动补分类；
- `length`（字数）和 `readTime`（约几分钟）**不用手填**，服务端按正文自动算；
- `shortDesc` 留空会截取正文前 48 字。

### 2. 往 `data/stories/` 丢 txt

```
data/stories/
├── 家长自编/
│   └── 示例-可以删掉.txt
└── 睡前故事/
    └── 我自己写的故事.txt
```

文件名就是标题，文件夹名就是分类。分类不存在会自动创建。
这个方式适合家长把现成的故事文本直接放进来，不用碰 JSON。

`data/stories/家长自编/示例-可以删掉.txt` 是随包带的演示文件，删掉即可。

---

## 四、接口

客户端 `src/core/storyservice.cpp` 用到的全部接口：

| 方法与路径 | 说明 |
|---|---|
| `GET /api/story/types` | `{"categories":[{"id":1,"name":"睡前故事"}]}` |
| `GET /api/story/list?typeId=1&page=1` | `{"items":[…],"page":1,"hasMore":false,"total":3}`；`typeId=0` 表示全部 |
| `GET /api/story/search?keyword=月亮&page=1` | 结构同 list，匹配标题 / 分类 / 简介 / 正文 |
| `GET /api/story/detail?storyId=1001` | `{id,title,type,length,readTime,content}`；找不到返回 404 |
| `GET /api/health` | `{"ok":true,"version","stories","categories":[{"id","name","count"}],"errors":[]}` |
| `POST /api/reload` | 重新读盘，返回最新统计 |

每页 20 条。所有响应都是 UTF-8 JSON，带 `Access-Control-Allow-Origin: *`（家长后台是
同源访问，带这个头只是为了以后能挂在别的页面上）。

学习情况上报（客户端 `src/core/learnreport.cpp`）：

| 方法与路径 | 说明 |
|---|---|
| `POST /api/learn/report` | 孩子端上报进度 + 作业成绩，返回 `{"ok":true,"child","homework","progress","reports"}` |
| `GET /api/learn/summary` | 家长后台网页读的汇总，`children[].child / progress / homework / updatedAt` |
| `POST /api/learn/reset` | 清空记录；请求体 `{"child":"默认"}` 只清一个，不传就全清 |

联机对战（客户端 `src/core/netplay.cpp`）：

| 方法与路径 | 说明 |
|---|---|
| `POST /api/game/create` | 建房，`{"game":"chess","level":2}`，返回 `room`（含 4 位 `code` 和自己的 `token`） |
| `POST /api/game/join` | 进房，`{"code":"4821"}`；带上自己的 `token` 表示重连回原座位 |
| `GET /api/game/state?code=&token=` | 轮询整个房间，客户端 1.5 秒一次 |
| `POST /api/game/move` | 走一步，`{"code","token","from","to","promo"}`；不是你的回合返回 409 |
| `POST /api/game/finish` | 象棋 `{"result":"a"｜"b"｜"draw"}`；华容道 `{"steps":27}` |
| `POST /api/game/leave` | 退房；房主退房整个房间删除，客人退房把房间标成结束 |

房间只存内存（服务重启就没了，本来也没人在等），30 分钟没有任何请求自动回收。
身份用建/进房时返回的随机 token，不用座位号 —— 座位号是自报家门的协议，
知道对方是 a 还是 b 就能替对面落子。

`room` 的字段：

| 字段 | 说明 |
|---|---|
| `code` / `game` / `level` | 房间号 / `chess` 或 `klotski` / 华容道用哪一档布局 |
| `status` | `waiting` 等人 / `playing` 对局中 / `done` 结束 |
| `seat` / `opponent` | 自己的座位（`a` 房主 / `b` 来客）、对手到了没有 |
| `turn` | 轮到谁走（象棋） |
| `moves` | `[{"f":52,"t":44,"p":""}]`，从开局到现在每一步 |
| `startAt` / `now` | 华容道什么时候开始（服务器时间轴），客户端用它换算本地倒计时 |
| `results` | 华容道成绩 `[{"seat","ms","steps"}]`，`ms` 由服务端算，不信客户端报的 |
| `winner` / `reason` | 谁赢了 / `finish` 正常下完、`leave` 对手退出 |
| `version` | 每次改动加一，客户端用它丢弃迟到的旧响应 |

**服务端不校验走子是否合规**，只校验「轮到你了吗」。与其在 Python 里维护第二份
走子规则（两份迟早不一致），不如接受「孩子能改客户端走假棋」这点代价 ——
赢一盘棋的收益远小于「必须先注册账号」把他挡在门外的代价。

### 上报格式

```json
{
  "child": "默认",
  "source": "linux",
  "device": "ubuntu-vm",
  "full": true,
  "progress": {
    "done": 3, "total": 26,
    "passed": ["q-order-1", "q-order-2", "q-order-3"],
    "chapters": [{ "name": "第一章 · 顺序", "done": 3, "total": 4 }]
  },
  "homework": {
    "hw-order": { "title": "第 1 课 · 顺序与重复",
                  "score": 4, "total": 5, "best": 4,
                  "at": "2026-11-26T13:40:30" }
  }
}
```

- `progress` 是**快照**，每次上报整体替换：客户端本来就持有完整的过关列表，
  以最后一次为准最简单；孩子点「重置进度」后也不会被旧记录赖住。
- `homework` 按套合并，`best` 取历史最高 —— 孩子重做一遍只考了 2/5，
  家长看到的仍然是「最高 4/5」，不会被最新一次盖掉。
- `full: true` 表示「这是我本机的全量」。家长在端上点了「清空成绩」，
  下一次上报就不带那些作业了，服务端要跟着删（否则网页上永远清不掉）。
- 不传 `full` 的老客户端按纯合并处理，只增不减。
- `total <= 0` 的作业直接丢弃：`0/0` 会把统计弄成畸形数据。

数据落在 `data/learn.json`，和 `stories.json` 分开：那个是家长手改的，
这个由程序写，混在一起迟早被一次「另存为」覆盖掉。写入用
「临时文件 + `os.replace`」，断电最多丢最后一次，不会留下半个 JSON。

> **安全边界**：上报接口和内容接口一样**没有鉴权**，服务默认监听 `0.0.0.0`。
> 这是给「家里的局域网」设计的：同网段的人能读到学习情况，也能伪造一条上报。
> 别把 8080 直接映射到公网；确实需要远程看，套一层带密码的反向代理
> （Caddy / Nginx basic auth）再转发到本机。


---

## 五、自测与排障

改完内容或接口，跑一遍自测（起临时实例，走完整调用链，31 项断言，
包含上报 / 汇总 / 清空三个接口的合并与边界行为；会在临时目录里复制一份 `data/`，
不会往仓库里写 `learn.json`）：

```sh
python3 server/selftest.py
```

服务相关的排障命令：

```sh
systemctl --user status kidssafe-story        # 服务状态
journalctl --user -u kidssafe-story -n 50     # 最近日志
ss -ltnp | grep 8080                          # 端口有没有在听
curl -s http://127.0.0.1:8080/api/health      # 接口通不通
```

| 现象 | 原因 |
|---|---|
| `端口 8080 起不来` | 端口被占用。换 `--port 8081` 或 `PORT=8081 ./install-server.sh` |
| 客户端「连不上故事服务器」 | 地址填成 `127.0.0.1` 但服务在另一台机器；或防火墙拦了 8080 |
| 家长后台能开，客户端打不开 | 客户端填的是 `https://`，而本服务只提供 HTTP |
| 故事列表为空 | `stories.json` 的 `typeId` 全是 0，或 txt 放在了错误的目录层级 |
| `stories.json 解析失败` | JSON 语法错误（多余逗号最常见）。修好后点「重新载入内容」 |
| 家长后台「学习情况」一直是空的 | 孩子端没填服务器地址，或从没做过一关 / 交过作业；在孩子端家长后台点「立即上报」可手动触发 |
| 孩子端显示「上报失败：Connection refused」 | 服务没起，或地址 / 端口不对。成绩仍在本机，下次改动会重试 |

改完后确认内容真的进来了：

```sh
curl -s http://127.0.0.1:8080/api/health
```

`stories` 数量应该和你预期一致。
