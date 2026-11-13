#pragma once

#include <QObject>
#include <QPointer>

#include <KProcess>

class Settings;

/**
 * 对应 C# 的 IAppLauncherService。
 *
 * Linux 侧没有 ShellExecute 那种「给个目标就能开」的统一入口，分三种：
 *   1. .desktop 的 Exec 行     → 拆参数后 KProcess 直接起
 *   2. http(s) 链接            → QDesktopServices::openUrl（交给默认浏览器）
 *   3. 可执行文件绝对路径       → 同上，当作单参数命令处理
 *
 * 这里刻意用 KProcess 自己持有子进程，而不是 startDetached：因为儿童桌面
 * 要能在「应用退出后自动回到桌面」，必须拿到 finished 信号。用 detached
 * 就只能靠轮询进程名，既不可靠又费电。
 */
class AppLauncher : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool running READ running NOTIFY runningChanged)
    Q_PROPERTY(QString runningName READ runningName NOTIFY runningChanged)

public:
    explicit AppLauncher(Settings *settings, QObject *parent = nullptr);

    bool running() const { return m_process != nullptr; }
    QString runningName() const { return m_runningName; }

    /// 启动一个应用。exec 为 .desktop 的原始 Exec 行或 URL。
    Q_INVOKABLE bool launch(const QString &exec, const QString &name);

    /// 家长强制关闭正在运行的子应用（超时或误开时用）。
    Q_INVOKABLE void killRunning();

signals:
    void runningChanged();
    void launchFailed(const QString &reason);

private:
    void cleanup();

    Settings *m_settings = nullptr;
    QPointer<KProcess> m_process;
    QString m_runningName;
};
