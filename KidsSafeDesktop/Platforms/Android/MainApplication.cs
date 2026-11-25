using Android.App;
using Android.Runtime;

namespace KidsSafeDesktop;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		/*
		 * 安卓上「抛到 Java 层」的未捕获异常，.NET 的 AppDomain.UnhandledException 有时收不到
		 * （JNI 边界会把异常转成 Java 异常交出去），闪退就一点记录都不剩。
		 * 这里补一个入口，保证任何一个没接住的异常都先在 runtime.log 里留一行。
		 */
		AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
		{
			Helpers.AppLog.E("崩溃", "Android 未捕获异常", e.Exception);
		};

		Helpers.AppLog.I("启动", "MainApplication 构造完成（安卓）");
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
