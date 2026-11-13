# KidsSafeShared · 共享库

孩子端（`KidsSafeDesktop`）与家长端（`KidsSafeParent`）共用的**全部界面与业务逻辑**。两个 App 只是不同的外壳：一个进儿童桌面，一个直接进家长中心，中间的页面一行代码都不重复。

| 目录 | 内容 |
|---|---|
| `Pages/` | 87 个页面（每个含 XAML 与 code-behind），从桌面、游戏、学习到家长中心 |
| `ViewModels/` | 38 个视图模型，`BaseViewModel` 提供 `SetProperty` / `IsBusy` |
| `Services/` | 44 个服务接口与实现：设置、时长、排行、天气、服务器同步、实时下发、内容、故事、课本、账号、护眼 |
| `Models/` | 49 个数据模型：内容分类、题库、游戏数据、奖励、主题、远程配置 |
| `Helpers/` | TTS、语音输入、导航动画、护眼遮罩、星星奖励、版本号、A11y、日志 |
| `Controls/` | `LauncherGridView`：Windows 用 FlexLayout 换行，手机用 CarouselView 分页 |
| `Converters/` | XAML 值转换器（`BoolToColor`、`InverseBool`、`HasItem`…） |
| `Platforms/` | 库内的平台实现（`NativeEyeCare`、`WindowsKioskService`），全部带 `#if` 守卫 |
| `ServiceProvider.cs` | DI 静态入口，页面里 `ServiceProvider.Get<IXxx>()` |

## 两条必须遵守的约定

### 1. 命名空间是 `KidsSafeDesktop.*`，不是 `KidsSafeShared.*`

拆分工程时，340 多个文件从孩子端搬到这里，为了让它们一行 `using` 都不用改，刻意保留了原命名空间。所以你会看到共享库里的文件写着 `namespace KidsSafeDesktop.Pages`。

**新增代码请沿用 `KidsSafeDesktop.*`。** 改成 `KidsSafeShared.*` 会让两个 App 的所有引用全部失效，收益为零。

### 2. 库里的 XAML 不是 WinUI 页面

`KidsSafeShared.csproj` 有两行不能删：

```xml
<EnableDefaultPageItems>false</EnableDefaultPageItems>
<EnableDefaultApplicationDefinition>false</EnableDefaultApplicationDefinition>
```

库工程默认会把 `**/*.xaml` 当作 WinUI `Page` 交给 WindowsAppSDK 的 XamlCompiler，而这里的 XAML 全是 MAUI 页面（`x:Class` + MauiXaml）。后果是很难查的报错：**`XamlCompiler.exe` 退出码 1，没有任何具体信息**。关掉默认项后它们才只按 `MauiXaml` 处理。App 工程不需要这段，因为它有 `SingleProject` 自动处理。

## 平台代码的写法

库工程的多目标是「先声明 Android，Windows 再追加」：

```xml
<TargetFrameworks>net10.0-android</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
```

而且**库里的 `Platforms/` 不会按 TFM 自动过滤**（App 工程会）。所以每个平台文件都要自己守卫，并且 **`#if` 必须包住 `using`**：

```csharp
#if WINDOWS
using Microsoft.UI.Windowing;   // ← 必须在 #if 里面
#endif

namespace KidsSafeDesktop.Services;

public class WindowsKioskService
{
#if WINDOWS
    // ...
#else
    // 空实现
#endif
}
```

写成「`using` 在外面、`#if` 只包住类体」会在 Android 目标下直接编译失败（找不到 `Microsoft.UI`）。

## 不能直接调用 App 工程的类型

共享库不能 `using` 孩子端的 `MainActivity`。跨层调用统一走静态委托：

```csharp
// KidsSafeShared/Platforms/Android/AndroidHooks.cs
public static class AndroidHooks
{
    public static Action? ApplyLockTaskNow { get; set; }
    // ...
}

// 孩子端 MainActivity.OnCreate 里注册
AndroidHooks.ApplyLockTaskNow = ApplyLockTaskNow;
```

家长端不注册，委托为 `null`，调用点判空即可——同一个页面在两端都能跑。

## DI 与资源

- **服务注册在各自的 App 里**，不在库里。孩子端注册真实平台实现，家长端注册 `Stub*` 空实现（不枚举已装应用、不启动外部应用、不锁屏、不放音效）。
- **转换器要在两个 App 的 `App.xaml` 各注册一份**。共享页面用 `{StaticResource InverseBool}` 是 App 级查找，某个 App 漏注册就是运行期崩，编译期发现不了。
- 库没有 `App.xaml`，所有 `StaticResource` 都依赖宿主 App 提供。

新增服务的顺序：接口 + 实现放 `Services/` → 在两个 `MauiProgram.cs` 各注册一次（家长端不需要就给个桩）→ 页面通过 `ServiceProvider.Get<T>()` 取。

## 编译

```bash
dotnet build KidsSafeShared.csproj -f net10.0-windows10.0.19041.0
dotnet build KidsSafeShared.csproj -f net10.0-android
```

改完务必两个 App 都编一遍：

```bash
dotnet build ../KidsSafeDesktop/KidsSafeDesktop.csproj -f net10.0-windows10.0.19041.0
dotnet build ../KidsSafeParent/KidsSafeParent.csproj   -f net10.0-windows10.0.19041.0
```

## 已知坑位

| 现象 | 原因 |
|---|---|
| `XamlCompiler.exe` 退出码 1，无细节 | 删了 `EnableDefaultPageItems=false` |
| Android 目标找不到 `Microsoft.UI` | `#if` 没包住 `using` |
| `CS9135: 需要常量值` | `DeviceInfo.Platform is DevicePlatform.Android` —— `DevicePlatform` 是静态字段不是常量，改 `==` |
| 找不到 `FlexWrap` / `FlexDirection` | 在 `Microsoft.Maui.Layouts` 命名空间，不是 `Microsoft.Maui.Controls` |
| `CollectionView` 没有 `SnapPointsType` | MAUI 的 `CollectionView` 不支持，分页要用 `CarouselView` |
| 页面切换卡顿 | fire-and-forget 的 `view.Pop()` 与 `new Page()` 抢 UI 线程；用 `UiFx.PopAsync` / `NavFx.PushAfterPressAsync` |
