#include "applauncher.h"
#include "settings.h"

#include <QDesktopServices>
#include <QRegularExpression>
#include <QTimer>
#include <QUrl>

#include <KProcess>
#include <KShell>

namespace {
/// .desktop 的 Exec 字段代码：%f %F %u %U %d %D %n %N %i %c %k %v %m
/// 儿童桌面不处理文件/URL 参数，直接全部剥掉，避免把 "%U" 当成参数传给应用。
QString stripFieldCodes(QString exec)
{
    static const QRegularExpression pattern(QStringLiteral("%[fFuUdDnNickvm]"));
    exec.remove(pattern);
    return exec.simplified();
}
}

AppLauncher::AppLauncher(Settings *settings, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
{
}

bool AppLauncher::launch(const QString &exec, const QString &name)
{
    if (running()) {
        return false;
    }

    const QString target = exec.trimmed();
    if (target.isEmpty()) {
        Q_EMIT launchFailed(QStringLiteral("启动目标为空"));
        return false;
    }

    // 1) 网页类目标：交给默认浏览器。kiosk 会话下通常是内置的 WebView，
    //    所以这里额外记一条日志，方便家长排查「点了没反应」的问题。
    if (target.startsWith(QLatin1String("http://")) || target.startsWith(QLatin1String("https://"))) {
        const bool ok = QDesktopServices::openUrl(QUrl(target));
        if (!ok) {
            Q_EMIT launchFailed(QStringLiteral("无法打开链接：%1").arg(target));
        }
        return ok;
    }

    // 2) 命令行目标：拆参数后自己持有子进程，以便拿到退出信号
    const QStringList args = KShell::splitArgs(stripFieldCodes(target));
    if (args.isEmpty()) {
        Q_EMIT launchFailed(QStringLiteral("无法解析启动命令：%1").arg(target));
        return false;
    }

    auto *process = new KProcess(this);
    process->setProgram(args.first());
    process->setArguments(args.mid(1));
    process->setProcessChannelMode(QProcess::ForwardedChannels);

    connect(process, &KProcess::finished, this, [this](int exitCode, QProcess::ExitStatus status) {
        Q_UNUSED(exitCode)
        Q_UNUSED(status)
        cleanup();
    });

    connect(process, &KProcess::errorOccurred, this, [this, process](QProcess::ProcessError error) {
        if (error == QProcess::FailedToStart) {
            Q_EMIT launchFailed(QStringLiteral("启动失败：%1").arg(process->program()));
            cleanup();
        }
    });

    process->start();

    m_process = process;
    m_runningName = name;
    Q_EMIT runningChanged();
    return true;
}

void AppLauncher::killRunning()
{
    if (!m_process) {
        return;
    }
    m_process->terminate();
    // 给 3 秒体面退出的机会，之后强杀。游戏类应用常忽略 SIGTERM。
    QTimer::singleShot(3000, this, [this] {
        if (m_process) {
            m_process->kill();
        }
    });
}

void AppLauncher::cleanup()
{
    if (m_process) {
        m_process->deleteLater();
        m_process = nullptr;
    }
    m_runningName.clear();
    Q_EMIT runningChanged();
}
