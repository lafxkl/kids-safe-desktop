# KidsSafeServer · 家长管理服务器

一个几十 MB 的 ASP.NET Core（Minimal API）服务：家长在浏览器里管配置，孩子设备长轮询取变更。**它是可选的**——不装服务端，客户端就是一个完全离线的儿童桌面；装了，才有多设备远程管理。

```
浏览器（家长后台 / 登录页） ──┐
                             ├── HTTP :5180 ── data/ 下 JSON 文件
孩子端 / 家长端 App ─────────┘
```

## 运行

```bash
cp appsettings.sample.json appsettings.json   # 至少改掉 AdminPin，填自己的 Mxnzp 密钥（可留空）
dotnet run
```

默认监听 `http://0.0.0.0:5180`（写在 `Program.cs` 末尾，用 `KS_URLS` 环境变量可覆盖，便于同机开第二个实例做验证）。

首次使用：

1. 浏览器打开 `http://localhost:5180` → 没有账号时自动跳 `login.html` 的注册页
2. 注册家长账号。**第一个账号是管理员**，注册时会把老的 `data/profile.json` 复制成 `data/profiles/{id}.json` 并接管它
3. 后台「👤 账号与设备」→ 生成 6 位配对码 → 在孩子端的服务器设置里填地址 + 这 6 位数字

## 三种身份

| 谁 | 凭证 | 权限 |
|---|---|---|
| 家长 | 用户名 + 密码（PBKDF2-SHA256，10 万次迭代、16 字节盐） | 读写配置、生成配对码、移除设备、改密码 |
| 儿童设备 | 6 位配对码（10 分钟有效、一次性） | **只读**；`PUT/POST /api/settings` 返回 403 |
| 老客户端 | 老的固定令牌 / `AdminPin` | 过渡期可用；有账号后老密码通道自动关闭 |

令牌格式 `v1.payload.sig`，HMAC-SHA256 自签，密钥落 `data/auth.key`（首次启动自动生成，重启不掉线）。

三个设计点，改动时别破坏：

1. **`Account.PasswordVersion`**：改密码时 +1，令牌里的 `ver` 对不上就 401 —— 改密码即踢掉所有家长会话。设备令牌 `ver=0` 不参与校验，所以改密码不会把孩子平板踢下线。
2. **`BootstrapAccountId`**：第一个注册的账号。老的固定令牌映射到它，于是已经装在孩子设备上的旧版本**升级后不用重新配对**。
3. **`WatchSignal` 按账号分桶**：每个账号独立的版本号，否则 A 家保存会唤醒 B 家的设备。

## 接口

令牌可放查询串 `?token=`（老客户端）或 `Authorization: Bearer` 头。

| 分组 | 端点 |
|---|---|
| 账号 | `GET /api/auth/status`、`POST /api/auth/register`、`POST /api/auth/login`、`GET /api/auth/me`、`POST /api/auth/logout`、`POST /api/auth/password`、`PUT /api/auth/profile` |
| 设备 | `GET /api/auth/paircode`、`GET /api/auth/devices`、`DELETE /api/auth/devices/{id}` |
| 配置 | `GET/PUT/POST /api/settings`、`GET /api/watch?since=`（长轮询，挂起 25 秒） |
| 发布 | `GET /api/app/release`、`/release/latest`、`/release/{version}`、`/changelog`、`/update-check?versionCode=&platform=` |
| 内容 | `GET /api/content/audio`（另有 `video`、`articles`）、`GET /api/books`、`/api/books/{id}` |
| 故事代理 | `GET /api/story/types`、`…/list`、`…/detail`、`…/search`（第三方密钥只在服务端） |
| 用户反馈 | `POST /api/feedback/submit`、`GET /api/feedback/stats`、`GET /api/feedback/quotes`；管理端 `GET /api/feedback/list`、`/export.csv`、`DELETE /api/feedback/{id}`（需 `Feedback:AdminKey` 或 owner 令牌） |
| 运维 | `GET /api/health`、`GET /api/download/list` |

`/api/health` 返回 `serverVersion`、`uptimeSeconds`、`accountCount`（0 表示还没注册账号）、`watchVersion`，排障先看这个。

## 数据目录

```
data/
├── accounts.json          账号列表（含 PBKDF2 哈希）
├── auth.key               令牌签名密钥（自动生成）
├── profile.json           老的全局配置，未注册时给 default 账号用
├── profiles/{id}.json     每个账号一份配置
└── feedback/YYYY-MM.jsonl 用户反馈 / 满意度 / 问卷，一行一条（UTF-8 无 BOM）
```

**整个 `data/` 目录拷走就是完整备份**。它被 `.gitignore` 排除，不要提交。

## 配置项

| 键 | 默认 | 说明 |
|---|---|---|
| `AdminPin` | `1234` | 老的登录密码。只做兼容，有账号后自动失效 |
| `Auth:AllowRegister` | `true` | 关掉后登录页只剩登录（已经注册过账号时建议关） |
| `Auth:AllowPinLogin` | `null` | 是否还接受老密码 / 配对码登录。`null` = 服务器还没有账号时接受，注册后自动关闭 |
| `Auth:Secret` | 空 | 令牌签名密钥；留空则自动生成到 `data/auth.key` |
| `Site:DownloadUrl` | 空 | 官网/应用市场下载地址，空表示暂不提供公网下载 |
| `Site:ReleasePage` | `changelog.html` | 更新日志页路径 |
| `Mxnzp:AppId` / `AppSecret` | 空 | 第三方故事大全 API。留空则 `/api/story/*` 返回错误，客户端降级到内置故事 |
| `Feedback:AdminKey` | 空 | 反馈管理密钥。官网 `feedback-admin.html` 用它看明细、导 CSV；留空则只有 owner 账号令牌能访问 |

## 安全边界

**这个服务端是为家庭局域网设计的，不要直接暴露到公网。**

- 没有 HTTPS，令牌与密码明文过网。需要公网访问请在前面挂 Nginx / Caddy 做 TLS 与访问控制。
- CORS 全开（`AllowAnyOrigin`），因为家长可能用手机浏览器从任意来源访问后台。
- 只有登录接口与反馈提交接口有频率限制（登录：同 IP 连续 5 次失败锁 60 秒；反馈：同 IP 10 分钟 12 条、24 小时 60 条），其余接口没有限流。
- 反馈数据不存 IP、账号、设备标识；联系方式只有管理端可见，公开统计接口永远不带。
- 静态目录直接暴露 `wwwroot`。别把私密文件放进去。
- 令牌是自签格式，不是标准 JWT，没有过期时间——退出登录只清客户端，不吊销令牌；改密码才会让家长令牌失效。

## 部署

```bash
dotnet publish KidsSafeServer.csproj -c Release -o out
# 把 out/ 整个拷到服务器，dotnet out/KidsSafeServer.dll
```

Windows 上也可以直接 `dotnet run -c Release`。想开机自启，用 systemd（Linux）或「任务计划程序」（Windows），把工作目录设为 `out/`。

## 电子课本

`wwwroot/books/` 下的教材 PDF 与页图**不随仓库分发**（版权归出版社），仓库只保留 `books.json` 书目元数据。没有页图时 `/api/books` 会过滤掉空版本，客户端书架为空，不影响其他功能。要自己用，把 PDF 按 `Scripts/BookRender` 的方式渲成页图放进 `books/pages/`。
