#include "vnengine.h"

#include <QDebug>
#include <QDir>
#include <QDirIterator>
#include <QFileInfo>
#include <QProcess>
#include <QProcessEnvironment>
#include <QStandardPaths>
#include <QTimer>
#include <QVariantMap>

#include <algorithm>
#include <utility>

namespace {

/// 体积给人看的写法：1.2 GB / 340 MB。小文件不显示（一个 3KB 的 .xp3 没意义）。
QString sizeText(qint64 bytes)
{
    if (bytes < 1024 * 1024) {
        return QString();
    }
    if (bytes < qint64(1024) * 1024 * 1024) {
        return QStringLiteral("%1 MB").arg(bytes / (1024.0 * 1024.0), 0, 'f', 0);
    }
    return QStringLiteral("%1 GB").arg(bytes / (1024.0 * 1024.0 * 1024.0), 0, 'f', 1);
}

/// 游戏目录名当标题：下划线换成空格，够用就行，不做「翻译」这种越界的事。
QString prettyTitle(const QString &dirName)
{
    QString title = dirName;
    title.replace(QLatin1Char('_'), QLatin1Char(' '));
    return title.trimmed();
}

} // namespace

VnEngine::VnEngine(QObject *parent)
    : QObject(parent)
{
    detectBackend();
    scanGames();
}

VnEngine::~VnEngine()
{
    // 程序退出时别把子进程留成孤儿：它会继续占着屏幕，而 kiosk 会话里
    // 没人能把它关掉。
    if (m_process != nullptr) {
        m_process->disconnect(this);
        m_process->terminate();
        if (!m_process->waitForFinished(2000)) {
            m_process->kill();
            m_process->waitForFinished(1000);
        }
    }
}

QString VnEngine::backend() const
{
    switch (m_backend) {
    case Backend::Custom:
        return QStringLiteral("自定义命令");
    case Backend::KrkrSdl:
        return QFileInfo(m_program).fileName();
    case Backend::Wine:
        return QStringLiteral("Wine");
    case Backend::None:
        break;
    }
    return QString();
}

QString VnEngine::gamesDir() const
{
    // 与 ROM 同一套约定：KIDSSAFE_GAMES_DIR 可以指到别处（调试/测试用），
    // 默认落在 ~/.local/share/kidssafe/games。
    const QString override = qEnvironmentVariable("KIDSSAFE_GAMES_DIR").trimmed();
    QString dir;
    if (!override.isEmpty()) {
        dir = override;
    } else {
        dir = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation)
              + QStringLiteral("/kidssafe/games");
    }
    QDir().mkpath(dir);
    return dir;
}

void VnEngine::refresh()
{
    detectBackend();
    scanGames();
    emit stateChanged();
}

void VnEngine::detectBackend()
{
    m_backend = Backend::None;
    m_program.clear();
    m_args.clear();
    m_detail.clear();

    // ---- 1. 家长显式指定 ----
    // 写成 `KIDSSAFE_VN_ENGINE='krkrsdl2 {game}'` 这样，{game} 会被换成游戏目录。
    // 留这个口子是因为各发行版/各移植版的命令行差得远，写死顺序只能覆盖常见的。
    const QString custom = qEnvironmentVariable("KIDSSAFE_VN_ENGINE").trimmed();
    if (!custom.isEmpty()) {
        const QStringList parts = QProcess::splitCommand(custom);
        if (!parts.isEmpty()) {
            m_backend = Backend::Custom;
            m_program = parts.first();
            m_args = parts.mid(1);
            m_detail = QStringLiteral("使用 KIDSSAFE_VN_ENGINE 指定的命令：%1").arg(custom);
            return;
        }
    }

    // ---- 2. 原生 SDL 移植版 ----
    // 这几个名字覆盖了目前社区里能跑在 Linux 上的吉里吉里实现。
    // 放两个地方找：PATH，以及 ~/.local/share/kidssafe/engine（源码编译装不打扰系统）。
    const QString engineDir = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation)
                              + QStringLiteral("/kidssafe/engine");
    const QStringList names{QStringLiteral("krkrsdl2"), QStringLiteral("krkrsdl"),
                            QStringLiteral("kirikiri2")};
    for (const QString &name : names) {
        const QString inPath = QStandardPaths::findExecutable(name);
        if (!inPath.isEmpty()) {
            m_backend = Backend::KrkrSdl;
            m_program = inPath;
            m_detail = QStringLiteral("使用原生引擎 %1（%2）").arg(name, inPath);
            return;
        }
        const QString local = engineDir + QLatin1Char('/') + name;
        if (QFileInfo(local).isExecutable()) {
            m_backend = Backend::KrkrSdl;
            m_program = local;
            m_detail = QStringLiteral("使用原生引擎 %1（%2）").arg(name, local);
            return;
        }
    }

    // ---- 3. Wine ----
    // 这条最不挑游戏（网上分享的整合包基本都是 Windows 版），但启动慢、
    // 还会在 ~/.wine 里建一套环境，所以排在原生引擎后面。
    const QString wine = QStandardPaths::findExecutable(QStringLiteral("wine"));
    if (!wine.isEmpty()) {
        m_backend = Backend::Wine;
        m_program = wine;
        m_detail = QStringLiteral("使用 Wine 运行游戏自带的 Windows 程序（%1）").arg(wine);
        return;
    }

    m_detail = QStringLiteral(
        "没找到可用的吉里吉里运行时。三种办法任选一种："
        "① 装原生引擎 krkrsdl2；"
        "② 装 wine，然后把带 .exe 的游戏放进游戏目录；"
        "③ 用 KIDSSAFE_VN_ENGINE 指定你自己的命令。");
}

void VnEngine::scanGames()
{
    m_games.clear();
    m_list.clear();

    const QDir root(gamesDir());
    const QFileInfoList dirs = root.entryInfoList(QDir::Dirs | QDir::NoDotAndDotDot, QDir::Name);

    for (const QFileInfo &info : dirs) {
        const QString dir = info.absoluteFilePath();

        // 数据包：*.xp3。子目录也算（有些整合包把 data.xp3 埋在 data/ 下）。
        int xp3Count = 0;
        qint64 bytes = 0;
        QDirIterator it(dir, {QStringLiteral("*.xp3")}, QDir::Files,
                        QDirIterator::Subdirectories);
        while (it.hasNext()) {
            it.next();
            ++xp3Count;
            bytes += it.fileInfo().size();
        }

        // Windows 程序：wine 后端要用。优先 krkr.exe，其次任意 exe，
        // 跳过常见的安装器/卸载器（那些点开只会把目录搞乱）。
        QString exe;
        // 注意用 QDir(dir)：QFileInfo::dir() 给的是**父目录**，写成
        // QFileInfo(dir).dir() 会去游戏目录外面找 exe，一个都匹配不到。
        const QFileInfoList exes =
            QDir(dir).entryInfoList({QStringLiteral("*.exe")}, QDir::Files, QDir::Name);
        for (const QFileInfo &candidate : exes) {
            const QString base = candidate.completeBaseName().toLower();
            if (base.contains(QLatin1String("unins")) || base.contains(QLatin1String("setup"))
                || base.contains(QLatin1String("install"))) {
                continue;
            }
            if (base == QLatin1String("krkr") || base == QLatin1String("krkrz")) {
                exe = candidate.absoluteFilePath();
                break;
            }
            if (exe.isEmpty()) {
                exe = candidate.absoluteFilePath();
            }
        }

        if (xp3Count == 0 && exe.isEmpty()) {
            continue;
        }

        Game game;
        game.dir = dir;
        game.title = prettyTitle(info.fileName());
        game.entry = exe;

        QStringList bits;
        if (xp3Count > 0) {
            bits << QStringLiteral("%1 个数据包").arg(xp3Count);
        } else {
            bits << QStringLiteral("Windows 程序");
        }
        const QString size = sizeText(bytes);
        if (!size.isEmpty()) {
            bits << size;
        }
        game.note = bits.join(QStringLiteral(" · "));

        m_games.append(game);
    }

    // 中文/日文标题按本地排序，别用 ASCII 序（那会把「あ」排到「阿」前面）。
    std::sort(m_games.begin(), m_games.end(), [](const Game &a, const Game &b) {
        return QString::localeAwareCompare(a.title, b.title) < 0;
    });

    for (const Game &game : std::as_const(m_games)) {
        QVariantMap item;
        item.insert(QStringLiteral("title"), game.title);
        item.insert(QStringLiteral("dir"), game.dir);
        item.insert(QStringLiteral("note"), game.note);
        m_list.append(item);
    }

    emit gamesChanged();
}

bool VnEngine::buildCommand(const Game &game, QString *program, QStringList *args,
                            QString *error) const
{
    *program = m_program;
    args->clear();

    switch (m_backend) {
    case Backend::Custom: {
        // {game} 换成游戏目录；一个占位符都没写时，把游戏目录作为最后一个参数
        // 补上 —— 社区里的移植版绝大多数接受「目录」这一个参数。
        bool replaced = false;
        for (const QString &arg : m_args) {
            if (arg.contains(QLatin1String("{game}"))) {
                QString expanded = arg;
                expanded.replace(QLatin1String("{game}"), game.dir);
                args->append(expanded);
                replaced = true;
            } else {
                args->append(arg);
            }
        }
        if (!replaced) {
            args->append(game.dir);
        }
        return true;
    }
    case Backend::KrkrSdl:
        // SDL 移植版默认从当前目录找 data.xp3 / startup.tjs，
        // 所以不传参数，靠 setWorkingDirectory 进游戏目录。
        return true;
    case Backend::Wine: {
        if (game.entry.isEmpty()) {
            *error = QStringLiteral("这个游戏目录里没有 .exe，Wine 起不来");
            return false;
        }
        args->append(game.entry);
        return true;
    }
    case Backend::None:
        break;
    }

    *error = QStringLiteral("没有可用的引擎");
    return false;
}

bool VnEngine::start(int index)
{
    if (m_process != nullptr) {
        setMessage(QStringLiteral("上一个游戏还没退出"));
        return false;
    }
    if (index < 0 || index >= m_games.size()) {
        return false;
    }

    const Game game = m_games.at(index);

    QString program;
    QStringList args;
    QString error;
    if (!buildCommand(game, &program, &args, &error)) {
        setMessage(error);
        return false;
    }

    auto *process = new QProcess(this);
    process->setWorkingDirectory(game.dir);
    // 引擎的日志（TJS 报错、缺插件）直接进我们的终端：孩子那边只会看到
    // 「黑屏/闪退」，家长要能在日志里找到原因。
    process->setProcessChannelMode(QProcess::ForwardedChannels);
    if (m_backend == Backend::Wine) {
        // WINEDEBUG=-all：否则 wine 会把 fixme 刷满终端，帧率也跟着掉。
        QProcessEnvironment env = QProcessEnvironment::systemEnvironment();
        env.insert(QStringLiteral("WINEDEBUG"), QStringLiteral("-all"));
        process->setProcessEnvironment(env);
    }

    connect(process, QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished), this,
            [this, process](int code, QProcess::ExitStatus status) {
                if (m_process != process) {
                    return;
                }
                if (status == QProcess::CrashExit) {
                    cleanup(QStringLiteral("游戏异常退出，可以让家长看日志"));
                } else if (code != 0) {
                    cleanup(QStringLiteral("游戏退出（代码 %1）").arg(code));
                } else {
                    cleanup(QStringLiteral("故事结束了"));
                }
            });
    connect(process, &QProcess::errorOccurred, this,
            [this, process](QProcess::ProcessError err) {
                if (m_process != process || err != QProcess::FailedToStart) {
                    return;
                }
                // FailedToStart 时 finished 不会来，必须在这里收摊，
                // 否则界面会永远停在「正在播放」。
                cleanup(QStringLiteral("启动失败：找不到或跑不起来 %1").arg(m_program));
            });

    m_process = process;
    m_runningTitle = game.title;

    process->start(program, args);
    // 起进程本身是毫秒级的；等 1.5 秒就够把「文件不存在 / 没执行权限」
    // 这类立刻失败的情况当场抓住，返回 false 让界面别切到「播放中」。
    if (!process->waitForStarted(1500)) {
        const QString why = process->errorString();
        cleanup(QStringLiteral("启动失败：%1").arg(why));
        return false;
    }

    // 起来之后再通知界面，免得启动失败时窗口先让开、再跳回来闪一下。
    emit runningChanged();

    qInfo() << "[vn] 启动" << game.title << "→" << program << args;
    return true;
}

void VnEngine::stop()
{
    if (m_process == nullptr) {
        return;
    }

    QProcess *process = m_process;
    process->terminate();
    // 有的引擎不理 SIGTERM（KAG 的全屏独占模式尤其），3 秒后补一刀。
    QTimer::singleShot(3000, this, [this, process] {
        if (m_process == process && process->state() != QProcess::NotRunning) {
            process->kill();
        }
    });
}

void VnEngine::cleanup(const QString &note)
{
    if (m_process == nullptr) {
        return;
    }

    QProcess *process = m_process;
    m_process = nullptr;
    m_runningTitle.clear();

    process->disconnect(this);
    process->deleteLater();

    emit runningChanged();
    setMessage(note);
}

void VnEngine::setMessage(const QString &text)
{
    m_message = text;
    emit stateChanged();

    // 3.5 秒后自动清掉，但只清这一条：期间来了新提示（序号变了）就不动它。
    const int seq = ++m_messageSeq;
    QTimer::singleShot(3500, this, [this, seq] {
        if (m_messageSeq != seq) {
            return;
        }
        m_message.clear();
        emit stateChanged();
    });
}
