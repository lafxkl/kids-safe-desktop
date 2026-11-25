#include "speech.h"

#include <QCoreApplication>
#include <QElapsedTimer>
#include <QEventLoop>
#include <QProcess>
#include <QStandardPaths>
#include <QStringList>
#include <QTimer>

namespace {

/// spd-say -O 的输出里挑出真正的模块名。
/// 正常输出是「标题行（以冒号结尾）+ 每行一个模块名」，但也见过只打模块名、
/// 或者夹着 `Speech Dispatcher: ...` 告警的版本，所以这里逐行过滤：
/// 去掉空行、含冒号的行（标题/告警）、以及 "dummy"。
QStringList parseModules(const QString &text)
{
    QStringList modules;
    const QStringList lines = text.split(QLatin1Char('\n'), Qt::SkipEmptyParts);
    for (const QString &raw : lines) {
        const QString line = raw.trimmed();
        if (line.isEmpty() || line.contains(QLatin1Char(':'))) {
            continue;
        }
        if (line.compare(QLatin1String("dummy"), Qt::CaseInsensitive) == 0) {
            continue;
        }
        modules << line;
    }
    modules.removeDuplicates();
    return modules;
}

} // namespace

Speech::Speech(QObject *parent)
    : QObject(parent)
{
    probeBinary();
}

Speech::~Speech()
{
    if (m_probe == nullptr) {
        return;
    }
    // 先断开信号：析构过程中再跑一遍 finish() 没有意义，而且它要去改
    // 已经在销毁的成员。断开后对象由 QObject 的父子关系负责回收。
    m_probe->disconnect(this);
    m_probe->kill();
    m_probe->waitForFinished(300);
}

void Speech::probeBinary()
{
    m_probing = true;
    m_program = QStandardPaths::findExecutable(QStringLiteral("spd-say"));

    if (m_program.isEmpty()) {
        // 没装就是没装，直接给安装命令，家长照着敲就行。
        finish(false, QStringLiteral("未安装 speech-dispatcher（sudo apt install speech-dispatcher）"));
        return;
    }

    probeModules();
}

void Speech::probeModules()
{
    if (m_probe != nullptr) {
        m_probe->kill();
        m_probe->deleteLater();
        m_probe = nullptr;
    }

    m_probe = new QProcess(this);
    QProcess *const proc = m_probe;
    proc->setProgram(m_program);
    proc->setArguments({QStringLiteral("-O")});
    proc->setProcessChannelMode(QProcess::MergedChannels);

    // 所有回调都先判 `m_probe != proc`：超时分支会主动 kill 并结束检测，
    // 之后 kill 引发的 finished 还会再来一次，不判就会对着已置空的指针取数据。
    connect(proc, &QProcess::errorOccurred, this, [this, proc](QProcess::ProcessError error) {
        if (m_probe != proc || error != QProcess::FailedToStart) {
            return;
        }
        // 文件在但执行不了：缺动态库、架构不对、权限不对，都归到这里。
        finish(false, QStringLiteral("spd-say 无法启动（权限或依赖缺失）"));
    });

    connect(proc, QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished), this,
            [this, proc](int exitCode, QProcess::ExitStatus status) {
        if (m_probe != proc) {
            return;
        }
        const QString out = QString::fromLocal8Bit(proc->readAll()).trimmed();

        if (status == QProcess::CrashExit) {
            finish(false, QStringLiteral("语音服务异常退出"));
            return;
        }

        // 老版本 spd-say 没有 -O，会打用法并返回非 0。这种情况二进制是好的，
        // 只是没法验证输出模块 —— 判可用，避免误伤能正常朗读的机器。
        const bool looksLikeUsage = out.contains(QLatin1String("-O"))
            || out.contains(QLatin1String("usage"), Qt::CaseInsensitive)
            || out.contains(QStringLiteral("用法"));

        const QStringList modules = parseModules(out);

        if (exitCode != 0) {
            if (looksLikeUsage) {
                finish(true, QStringLiteral("可用（旧版 spd-say，未能列出输出模块）"));
            } else {
                finish(false, QStringLiteral("spd-say 报错（退出码 %1）").arg(exitCode));
            }
            return;
        }

        if (modules.isEmpty()) {
            // 典型的「装了没配」：服务没起来，或者只有一个 dummy 输出模块。
            finish(false, QStringLiteral("语音服务没有可用的音频输出（检查 speech-dispatcher 是否在运行）"));
            return;
        }

        finish(true, QStringLiteral("可用（输出模块：%1）").arg(modules.join(QStringLiteral("、"))));
    });

    proc->start();

    // 超时保护：speech-dispatcher 没起来时 spd-say 可能卡在 D-Bus 上很久。
    // 4 秒还没回来就判不可用，免得家长后台的「可用」探测器一直挂着。
    QTimer::singleShot(4000, this, [this, proc] {
        if (m_probe != proc) {
            return;
        }
        proc->kill();
        finish(false, QStringLiteral("语音服务无响应（4 秒超时）"));
    });
}

void Speech::finish(bool available, const QString &detail)
{
    m_probing = false;
    m_available = available;
    m_detail = detail;

    if (m_probe != nullptr) {
        m_probe->deleteLater();
        m_probe = nullptr;
    }

    emit stateChanged();
}

void Speech::waitForReady(int timeoutMs)
{
    if (!m_probing) {
        return;
    }

    // 自检链路里还没有事件循环，所以不能只靠 waitForFinished：
    //   进程可能已经退出（state() 变 NotRunning），但 finished 信号还没被派发，
    //   那样 detail 会一直停在「正在检测…」。
    // 两种状态都照顾到：还在跑就阻塞等，已经退了就手动把事件派发一次。
    QElapsedTimer timer;
    timer.start();
    while (m_probing && timer.elapsed() < timeoutMs) {
        if (m_probe != nullptr && m_probe->state() != QProcess::NotRunning) {
            m_probe->waitForFinished(200);
        } else {
            QCoreApplication::processEvents(QEventLoop::AllEvents, 50);
        }
    }
}

void Speech::recheck()
{
    if (m_probing) {
        return;
    }
    m_detail = QStringLiteral("正在检测…");
    emit stateChanged();
    probeBinary();
}

void Speech::say(const QString &text)
{
    // 不可用时静默返回：调用方（眼保健操、故事朗读）不需要各自判断，
    // 界面上该置灰的按钮由 Speech.available 绑定决定。
    if (m_program.isEmpty() || text.trimmed().isEmpty()) {
        return;
    }

    // -w 等待朗读结束（由子进程自己等，不影响 UI 线程）
    // -l zh 指定中文；某些发行版没有装中文语音包时会自动回退到默认语音，
    // 而不是报错，所以这里不加 --strict。
    QProcess::startDetached(m_program, {
        QStringLiteral("-l"), QStringLiteral("zh"),
        QStringLiteral("-r"), QStringLiteral("-10"),
        QStringLiteral("-w"),
        text,
    });
}

void Speech::stop()
{
    if (m_program.isEmpty()) {
        return;
    }
    QProcess::startDetached(m_program, {QStringLiteral("--cancel")});
}
