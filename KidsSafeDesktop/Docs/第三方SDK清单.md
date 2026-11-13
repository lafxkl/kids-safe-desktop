# 儿童安全桌面 第三方 SDK 清单

> 应用：儿童安全桌面（`com.companyname.kidssafedesktop`）
> 清单版本：1.0　最后更新：2026-11-04
> 与 App 内「第三方 SDK 清单」页内容一致。提交商店时可直接粘贴。

---

## 一、已接入的第三方 SDK（共 1 项）

### 1. 荣耀推送 Push Kit

| 项目 | 内容 |
|---|---|
| SDK 名称 | 荣耀推送服务（Honor Push Kit） |
| 提供方 | 荣耀终端有限公司 |
| SDK 版本 | 10.0.39.302（`push-10.0.39.302.aar`、`pushcommon-10.0.39.302.aar`） |
| 引入方式 | 本地 AAR（`Platforms/Android/libs/`），仅安卓端打包 |
| 使用目的 | 家长在电脑端修改配置后，唤醒孩子设备立即同步设置 |
| 收集的个人信息 | 推送 token（由荣耀服务端生成）、应用包名、应用签名证书 SHA-256 指纹、设备型号与系统版本、网络连接状态 |
| 不收集的信息 | 不读取通讯录、短信、位置、相册、麦克风、摄像头，不采集 IMEI / OAID / Android ID |
| 是否必需 | 否。未开通推送时应用功能完全不受影响 |
| 关闭方式 | 家长面板 → 推送 → 关闭 token 申请；或卸载应用 |
| 官方隐私政策 | https://developer.hihonor.com/cn/doc/guides/100226 |

**补充说明**：该 SDK 需要在荣耀开发者平台为该应用登记包名与签名指纹后才能取得推送 token。本应用当前**未开通**推送权限（个人开发者账号），SDK 会返回错误码 `10202`，此时不会向荣耀服务器上传任何数据。即使后续开通，家长也可随时在家长面板关闭。

---

## 二、系统组件（非第三方 SDK，但会处理数据）

### 2. 系统语音合成 TTS

| 项目 | 内容 |
|---|---|
| 组件 | Android TextToSpeech / Windows SAPI |
| 提供方 | 设备操作系统 |
| 使用目的 | 朗读题目、英语单词、故事与提示语 |
| 处理的信息 | 待朗读的文本内容。若设备使用厂商的在线语音引擎，文本可能由该引擎上传至厂商服务器处理，本应用无法控制该行为 |
| 关闭方式 | 系统设置 → 语音合成 → 切换为离线引擎，或关闭系统语音服务 |

### 3. .NET MAUI 运行时

| 项目 | 内容 |
|---|---|
| 组件 | .NET MAUI / Microsoft.Extensions.Logging |
| 提供方 | Microsoft Corporation |
| 使用目的 | 应用界面与运行框架 |
| 处理的信息 | 无。运行时不联网、不上报任何数据 |
| 官方隐私政策 | https://privacy.microsoft.com/zh-cn/privacystatement |

---

## 三、非第三方的自建服务

### 4. 家长端服务器（由家长自行部署）

| 项目 | 内容 |
|---|---|
| 服务 | KidsSafeServer（随产品提供的本地服务端，家长在自己电脑上运行） |
| 提供方 | 家长本人（不是第三方，也不是开发者运营） |
| 使用目的 | 家长在电脑上远程下发配置到孩子设备 |
| 传输的信息 | 孩子昵称、头像、每日时长、应用与网站黑白名单、更新时间戳 |
| 数据位置 | 家长自己电脑的 `data/profile.json` |
| 关闭方式 | 家长面板 → 关闭「家长端服务器」开关 |

---

## 四、明确未接入的 SDK 类别

- 广告类 SDK（穿山甲、优量汇、AdMob、Mintegral 等）
- 统计分析类 SDK（友盟、TalkingData、神策、Google Analytics 等）
- 崩溃上报类 SDK（Bugly、Sentry、Firebase Crashlytics 等）
- 社交登录与分享类 SDK（微信、QQ、微博）
- 支付类 SDK
- 地图与定位类 SDK
- 人脸识别、语音识别类 SDK

**结论**：应用内没有开屏广告、插屏广告；不会采集设备标识做用户画像；崩溃时不会把堆栈上传到第三方服务器。

---

## 五、一致性声明

本清单与以下工程事实严格对应，审核时可逐项核对：

| 来源 | 内容 |
|---|---|
| `KidsSafeDesktop.csproj` → `PackageReference` | 仅 `Microsoft.Maui.Controls`、`Microsoft.Extensions.Logging.Debug`（Debug 配置） |
| `KidsSafeDesktop.csproj` → `AndroidLibrary` | 仅 `Platforms\Android\libs\*.aar`（荣耀推送两个 AAR） |
| `Platforms/Android/Assets/` | 仅 `mcs-services.json`（荣耀推送配置） |
| `Platforms/Android/AndroidManifest.xml` | 无广告 / 统计 / 推送以外任何 SDK 的 meta-data 或 service |
