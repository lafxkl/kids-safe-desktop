using Foundation;
using UIKit;

namespace KidsSafeDesktop;

/// <summary>
/// iOS 应用入口。
///
/// 平台差异记录（写在这里，省得以后又去翻当年的提交）：
///   · iOS 没有 Android 的 PatchBootstrapper —— App Store 不允许运行期下发可执行代码，
///     更新只能发新版本，因此这里不做任何补丁预加载；
///   · 没有 Kiosk / 应用白名单 / 使用统计 —— 系统不开放，相关服务在 MauiProgram 里注册为桩；
///   · 护眼遮罩在窗口层完成（Platforms/iOS/NativeEyeCare.cs），不需要在这里开权限。
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
	{
		var ok = base.FinishedLaunching(application, launchOptions);

		try
		{
			Helpers.AppLog.I("启动", $"iOS {UIDevice.CurrentDevice.SystemName} {UIDevice.CurrentDevice.SystemVersion}，应用已启动");

			// 听一听是核心场景：锁屏后音频要继续（配合 Info.plist 的 UIBackgroundModes=audio）。
			// 这里只声明类别，真正激活在播放器首次装载时做——没播放就不占音频通道。
			AVFoundation.AVAudioSession.SharedInstance().SetCategory(AVFoundation.AVAudioSessionCategory.Playback);
		}
		catch (Exception ex)
		{
			// 日志与音频会话都不能影响启动
			Helpers.AppLog.W("启动", "iOS 启动后置初始化失败：" + ex.Message);
		}

		return ok;
	}
}
