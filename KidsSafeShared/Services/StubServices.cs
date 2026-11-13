using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 占位实现：用于未实现儿童模式锁定的平台（iOS/macOS），
/// 以及不需要这些设备能力的 App（家长端只改配置，不枚举应用、不锁屏、不播放音频）。
/// 原先是 #if IOS || MACCATALYST 条件编译，拆分家长端后改为全平台可用——
/// 孩子在用的设备能力不该被家长端带进来，注册这些桩实现即可。
/// </summary>
public class StubAppCatalogService : IAppCatalogService
{
	public Task<List<AppItem>> GetInstalledAppsAsync() => Task.FromResult(new List<AppItem>());

	public Task<Microsoft.Maui.Controls.ImageSource?> GetAppIconAsync(string packageName)
		=> Task.FromResult((Microsoft.Maui.Controls.ImageSource?)null);
}

public class StubAppLauncherService : IAppLauncherService
{
	public Task LaunchAsync(AppItem app) => Task.CompletedTask;
}

public class StubAudioPlayerService : IAudioPlayerService
{
	public bool IsPlaying => false;

	public double DurationSeconds => 0;

	public double PositionSeconds => 0;

	public Task SetSourceAsync(string filePath, bool loop) => Task.CompletedTask;

	public void Play() { }

	public void Pause() { }

	public void Stop() { }

	public void SeekTo(TimeSpan position) { }

	public event EventHandler? Ended;
}

public class StubXimalayaService : IXimalayaService
{
	public Task OpenAsync(string categoryKey) => Task.CompletedTask;
}

public class StubKioskService : IKioskService
{
	public Task EnterKioskAsync() => Task.CompletedTask;
	public Task BringToFrontAsync() => Task.CompletedTask;
	public Task ExitAppAsync() => Task.CompletedTask;
}
