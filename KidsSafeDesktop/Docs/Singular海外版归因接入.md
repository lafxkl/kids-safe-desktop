# Singular 海外版归因接入（Kids SDK）

> 适用版本：客户端 1.20 及以后（安卓）；iOS 端本次**未**接入。
> 目标：海外渠道能看见安装来源与会话量，**中国大陆设备不初始化、不发送任何请求**。
> 从 1.20 起国内版与海外版**分开打包**：国内包里连 SDK 的代码都没有，不是「带了但不调用」。
> 合规口径见 `Docs/第三方SDK清单.md` 第 1-B 节，代码只有一个入口：`Platforms/Android/SingularAnalytics.cs`。

---

## 一、两个包的区别

| | 国内版 `-p:AppChannel=cn`（默认） | 海外版 `-p:AppChannel=global` |
|---|---|---|
| applicationId | `com.companyname.kidssafedesktop` | `cc.siguangblog.kidsafe` |
| Singular AAR | **不引用** | 引用（kids + installreferrer） |
| `SingularAnalytics.cs` | 不参与编译（无 `SINGULAR` 宏） | 参与编译 |
| 家长面板开关 | 不显示（钩子为 null） | 海外地区才显示 |
| `INSTALL_REFERRER` 权限 | 没有 | 有 |
| 中间产物 | `obj\`、`bin\Release\` | `obj\global\`、`bin\global\Release\` |

国内版沿用老包名是**故意的**：v1.x 已经用它发了 20 多个版本，换包名等于变成另一个应用，
老用户升级会丢数据。海外版是全新上架，用域名反写的正式包名。

两个包名不同，同一台手机上不能互相覆盖安装（也不需要）；升级关系各自独立。

---

## 二、涉及的文件

| 文件 | 作用 |
|---|---|
| `Directory.Build.props` | `AppChannel` 默认值 + 海外版独立的 obj/bin 目录 |
| `KidsSafeDesktop.csproj` | 按渠道条件引用 AAR、定义 `SINGULAR` 宏、切包名、清残留 |
| `Platforms/Android/libs/singular_kids_sdk-12.17.0.aar` | 儿童版 SDK（不入库，见 `.gitignore`） |
| `Platforms/Android/libs/installreferrer-2.2.aar` | Google Play 安装来源 |
| `Platforms/Android/Transforms/Metadata.xml` | 修绑定冲突（`Utils` 类与 `utils` 包同名），只改 C# 类型名；只在海外版生效 |
| `Platforms/Android/SingularAnalytics.cs` | 地区判定 + 初始化 + 开关 + 上报接口 |
| `Singular.props`（本机，不入库） | SDK Key / Secret；模板见 `Singular.props.sample` |
| `Scripts/build-android-channels.ps1` | 一次打两个包，带自检，产物进 `dist\` |

AAR 与荣耀推送一样**不提交**（`libs/*.aar` 被忽略），换机器时按下面的地址重下：

```bash
# 儿童版（注意是 singular_kids_sdk，不是 singular_sdk）
curl -LO https://maven.singular.net/com/singular/sdk/singular_kids_sdk/12.17.0/singular_kids_sdk-12.17.0.aar
# Install Referrer（Google Maven）
curl -LO https://dl.google.com/dl/android/maven2/com/android/installreferrer/installreferrer/2.2/installreferrer-2.2.aar
```

缺这两个 AAR 时，国内版照常能编（根本不引用），**海外版会编不过**。

---

## 三、构建

```powershell
# 推荐：一次打两个包，自动复制到 dist\ 并打印 SHA256
.\Scripts\build-android-channels.ps1              # 两个都打
.\Scripts\build-android-channels.ps1 -Channel cn  # 只打国内版
```

手工打单个包：

```powershell
# 国内版（不传 AppChannel 也是这个）
dotnet build KidsSafeDesktop.csproj -f net10.0-android -c Release

# 海外版
dotnet build KidsSafeDesktop.csproj -f net10.0-android -c Release -p:AppChannel=global
```

**密钥**（只有海外版用得上）：

```powershell
# 方式一：本机文件（推荐，长期）
Copy-Item KidsSafeDesktop\Singular.props.sample KidsSafeDesktop\Singular.props
# 填好 SingularApiKey / SingularSecret 后再打包

# 方式二：命令行传参（CI）
dotnet build KidsSafeDesktop.csproj -f net10.0-android -c Release -p:AppChannel=global `
  -p:SingularApiKey=xxx -p:SingularSecret=yyy
```

`SingularChannel`（**注意与 `AppChannel` 不是一回事**）：`AppChannel` 决定「包里有没有这个 SDK」，
`SingularChannel` 决定「包里这个 SDK 要不要按地区判断」。

| `SingularChannel` | 行为 | 用在什么时候 |
|---|---|---|
| `auto`（海外版默认） | 运行时按地区判断，中国大陆不初始化 | 默认，推荐 |
| `global` | 强制按海外，忽略地区 | 明确只面向境外、想拿到更全数据时 |
| `cn`（国内版默认） | 强制按国内，永不初始化 | 国内版（代码本来就不编译，这是兜底） |

没配密钥不会报错：`TryStart()` 读到空串直接返回，日志里留一行「未配置 Singular 密钥」。

---

## 四、运行时判定顺序

按顺序命中即返回，命中「中国大陆」的特征一律不初始化：

1. `SingularChannel`（构建期）
2. 系统地区 `RegionInfo.CurrentRegion`
3. 移动网络国家码 `getNetworkCountryIso()` —— 不需要任何权限
4. 时区（Asia/Shanghai、Chongqing、Harbin、Urumqi、Beijing）
5. 语言（`zh-CN`、`zh-Hans-*`）

**没读** SIM 国家码、IMEI、OAID、Android ID、定位 —— 判断地区不需要这些。
结果在进程内只算一次（`Lazy`），依据会写进 `runtime.log`，关键字 `[统计]`。

这一层是海外包内部的第二道闸：国内用户如果自己装了海外包（例如从第三方站点下载），
也不会被上报。

---

## 五、怎么验证接对了

**先看包里有没有类**（最直接，不依赖运行）：

```bash
python - <<'PY'
import zipfile
for tag, p in [('cn','dist/KidsSafeDesktop-1.20-cn.apk'),
               ('global','dist/KidsSafeDesktop-1.20-global.apk')]:
    z = zipfile.ZipFile(p)
    blob = b''.join(z.read(n) for n in z.namelist() if n.endswith('.dex'))
    print(tag, 'Singular 类:', b'Lcom/singular/sdk/' in blob)
PY
```

预期：`cn False` / `global True`。打包脚本已经内置这条自检，不满足会直接报错。

**再看运行时**：

1. `adb logcat | grep -E "统计|Singular"`；
   - 海外包在国内跑：`按中国大陆处理（系统地区 CN），不加载统计 SDK`
   - 海外包在境外跑：`Singular Kids SDK 已初始化（海外版）`
2. Singular 后台 → Analytics → 安装/会话应在 5 分钟内出现；
3. 清单核对：`grep -c AD_ID obj/.../android/AndroidManifest.xml` 必须是 0；
4. 家长面板 → 隐私与合规：只有海外包 + 海外地区才出现「海外版使用统计」开关。

---

## 六、常见问题

**为什么不用标准版 SDK？**
标准版读 GAID / IDFA。App Store 审核指南 1.3 与 Google Play 家庭政策都禁止儿童应用这么做，
过了审核也会在后续抽查里被下架。

**为什么没有 SKAdNetwork / iOS？**
本次只接安卓。iOS 端要单独接 `Singular-Kids-SDK`（SwiftPM），且**不能链接 AdSupport.framework**，
项目目前没有 iOS 发布计划。

**为什么国内包干脆不打进去？**
国内商店没有 Google Play，Install Referrer 拿不到数据；同时把设备信息发到境外服务器，
在《个人信息保护法》第 38 条下需要单独同意 + 告知境外接收方，收益为零、成本很高。
不打进去还省掉一个举证成本：应用商店问「你这个包里有没有统计 SDK」，可以直接拆包给对方看。

**两个包要注意什么？**
- 版本号是两套独立的升级序列（都从 21 开始），商店各看各的；
- 两个包名不同，用户从国内版换到海外版不能覆盖安装，需要卸载重装（数据会丢）；
- 提交 Google Play 时用 `dist\KidsSafeDesktop-1.20-global.apk`，别传错了。

**构建时切渠道报 CS0101 / 「程序包不存在」？**
八成是 obj 里有上一轮的解包残留。分渠道目录（`Directory.Build.props`）已经避免了新生问题，
老目录由 `KsDropStaleSingularBindings` 目标兜底清理；仍不行就删掉 `obj\` 与 `bin\` 重来。

**能不能再加事件？**
可以，`SingularAnalytics.Track("事件名")` 或 `Track("事件名", 字典)`。
注意：事件属性里不要放昵称、头像、作业内容 —— 儿童应用的统计只到「用了哪个功能」这一层。

**升级 SDK 要注意什么？**
新版可能改包结构（例如 `com.singular.sdk.internal.Utils` 是否还在），
如果 `dotnet build` 报 CS0101 命名冲突，改 `Transforms/Metadata.xml` 里的 `managedName` 即可。
