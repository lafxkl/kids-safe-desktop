#include "codelab.h"

#include <QCoreApplication>
#include <QDateTime>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonParseError>
#include <QProcess>
#include <QSettings>
#include <QStandardPaths>
#include <QTemporaryDir>
#include <QTimer>

#ifdef Q_OS_LINUX
#include <sys/resource.h>
#endif

namespace {

constexpr int kMaxOutput = 8000;      // 输出上限，防止死循环刷爆界面
constexpr int kCompileTimeout = 10000;
constexpr int kRunTimeout = 3000;     // 孩子写的循环最多跑 3 秒
constexpr int kMaxValue = 999;

QString pickCompiler()
{
    const QStringList candidates{QStringLiteral("gcc"), QStringLiteral("cc"),
                                 QStringLiteral("clang")};
    for (const QString &name : candidates) {
        const QString path = QStandardPaths::findExecutable(name);
        if (!path.isEmpty()) {
            return path;
        }
    }
    return QString();
}

// 编译产物放哪儿。默认不放 /tmp：不少系统把 /tmp 挂成 noexec，gcc 能写出文件，
// execve 却一步就被内核挡回来，界面上的表现就是「程序启动失败」——
// 明明编译通过了，却像是编译器坏了。放用户缓存目录就没有这个坑，
// 缓存目录写不了再退回 /tmp。
QString workDirTemplate()
{
    const QString cache = QStandardPaths::writableLocation(QStandardPaths::CacheLocation);
    if (!cache.isEmpty()) {
        const QString base = cache + QStringLiteral("/codelab");
        if (QDir().mkpath(base)) {
            return base + QStringLiteral("/run-XXXXXX");
        }
    }
    return QDir::tempPath() + QStringLiteral("/kidssafe-code-XXXXXX");
}

// 判题时的提示要用中文说清「缺哪种积木」，直接把 kind 抛给孩子没人看得懂。
QString kindName(const QString &kind)
{
    if (kind == QLatin1String("print")) {
        return QStringLiteral("打印一句话");
    }
    if (kind == QLatin1String("printvar")) {
        return QStringLiteral("打印变量");
    }
    if (kind == QLatin1String("varset")) {
        return QStringLiteral("变量设值");
    }
    if (kind == QLatin1String("varadd")) {
        return QStringLiteral("变量加");
    }
    if (kind == QLatin1String("loop")) {
        return QStringLiteral("重复 N 次");
    }
    if (kind == QLatin1String("if")) {
        return QStringLiteral("如果…那么");
    }
    return kind;
}

// 起不来的时候顺手跑一次 ldd：缺动态加载器、缺 .so，都能一眼看出来，
// 比让家长自己去终端敲命令再回来问要快。只在失败路径调用，卡一下无所谓。
QString probeLdd(const QString &path)
{
    const QString ldd = QStandardPaths::findExecutable(QStringLiteral("ldd"));
    if (ldd.isEmpty() || path.isEmpty()) {
        return QString();
    }
    QProcess probe;
    probe.setProcessChannelMode(QProcess::MergedChannels);
    probe.start(ldd, QStringList{path});
    if (!probe.waitForFinished(2000)) {
        probe.kill();
        probe.waitForFinished(200);
        return QString();
    }
    QStringList lines = QString::fromUtf8(probe.readAll()).trimmed().split(u'\n');
    while (lines.size() > 8) {   // 依赖多的时候没必要全贴出来
        lines.removeLast();
    }
    return lines.join(u'\n').trimmed();
}

// 0..2 的槽位 → C 变量名 v1..v3。越界一律夹回来，绝不拼出奇怪的标识符。
int slotNumber(int slot)
{
    return qBound(0, slot, 2) + 1;
}

int clampValue(int value)
{
    return qBound(-kMaxValue, value, kMaxValue);
}

// 用户输入的普通文字 → C 字符串字面量内部。引号、反斜杠、换行必须转义，
// 否则一个引号就能把 printf 的语法拆坏（虽然编译器会拦住，但界面会难看）。
QString escapeForC(const QString &raw)
{
    QString out;
    out.reserve(raw.size() + 8);
    for (const QChar ch : raw) {
        switch (ch.unicode()) {
        case '\\':
            out += QLatin1String("\\\\");
            break;
        case '"':
            out += QLatin1String("\\\"");
            break;
        case '\n':
            out += QLatin1String("\\n");
            break;
        case '\r':
            break;
        case '\t':
            out += QLatin1String("\\t");
            break;
        default:
            out += ch;
            break;
        }
    }
    return out;
}

} // namespace

CodeLab::CodeLab(QObject *parent)
    : QObject(parent)
    , m_compiler(pickCompiler())
{
    m_timer = new QTimer(this);
    m_timer->setSingleShot(true);
    connect(m_timer, &QTimer::timeout, this, &CodeLab::onTimeout);

    m_lessons = loadLessons();
    readProgress();

    // 上次被强杀（kiosk 会话断电、看门狗重启）时 run-XXXXXX 会留下来，
    // 清掉一天以前的；只动本应用自己建的目录，名字不对的一概不碰。
    const QString base = QStandardPaths::writableLocation(QStandardPaths::CacheLocation)
                         + QStringLiteral("/codelab");
    const QDir baseDir(base);
    if (baseDir.exists()) {
        const QDateTime cutoff = QDateTime::currentDateTime().addDays(-1);
        const QFileInfoList stale =
            baseDir.entryInfoList(QStringList{QStringLiteral("run-*")}, QDir::Dirs | QDir::NoDotAndDotDot);
        for (const QFileInfo &entry : stale) {
            if (entry.lastModified() < cutoff) {
                QDir(entry.absoluteFilePath()).removeRecursively();
            }
        }
    }

    setStatus(m_compiler.isEmpty() ? QStringLiteral("缺少编译器")
                                   : QStringLiteral("准备好了"));
}

CodeLab::~CodeLab()
{
    cleanupProcess();
}

QString CodeLab::compilerName() const
{
    return m_compiler.isEmpty() ? QString() : QFileInfo(m_compiler).fileName();
}

QVariantList CodeLab::loadLessons()
{
    // 四个候选位置按优先级排：环境变量 → 编译期源码树 → 安装路径 → build 目录旁。
    // 最后一个是为了「在 build/ 里直接跑」时也能找到关卡，省一次 make install。
    QStringList dirs;
    const QString env = qEnvironmentVariable("KIDSSAFE_CODELAB_DIR");
    if (!env.isEmpty()) {
        dirs << env;
    }
#ifdef KIDSSAFE_CODELAB_SRC
    dirs << QStringLiteral(KIDSSAFE_CODELAB_SRC);
#endif
    const QString installed = QStandardPaths::locate(
        QStandardPaths::GenericDataLocation,
        QStringLiteral("kidssafe/codelab/lessons.json"));
    if (!installed.isEmpty()) {
        dirs << QFileInfo(installed).absolutePath();
    }
    dirs << QCoreApplication::applicationDirPath() + QStringLiteral("/../assets/codelab");

    for (const QString &dir : dirs) {
        QFile file(QDir(dir).filePath(QStringLiteral("lessons.json")));
        if (!file.open(QIODevice::ReadOnly)) {
            continue;
        }
        QJsonParseError error;
        const QJsonDocument doc = QJsonDocument::fromJson(file.readAll(), &error);
        if (error.error != QJsonParseError::NoError) {
            qWarning() << "[code] lessons.json 格式有误：" << file.fileName()
                       << error.errorString();
            continue;
        }
        const QVariantList list = doc.object().value(QStringLiteral("lessons"))
                                      .toArray().toVariantList();
        if (list.isEmpty()) {
            qWarning() << "[code] lessons.json 里没有关卡：" << file.fileName();
            continue;
        }
        qInfo() << "[code] 关卡表：" << file.fileName() << list.size() << "关";
        return list;
    }

    qWarning() << "[code] 没找到 lessons.json，关卡列表会是空的";
    return {};
}

void CodeLab::readProgress()
{
    QSettings store(QSettings::IniFormat, QSettings::UserScope,
                    QStringLiteral("kidssafe"), QStringLiteral("codelab"));
    m_passed = store.value(QStringLiteral("passed")).toStringList();
}

void CodeLab::saveProgress()
{
    QSettings store(QSettings::IniFormat, QSettings::UserScope,
                    QStringLiteral("kidssafe"), QStringLiteral("codelab"));
    store.setValue(QStringLiteral("passed"), m_passed);
    store.sync();
}

bool CodeLab::isPassed(const QString &id) const
{
    return m_passed.contains(id);
}

void CodeLab::resetProgress()
{
    if (m_passed.isEmpty()) {
        return;
    }
    m_passed.clear();
    saveProgress();
    setVerdict(QString(), QString());
    emit progressChanged();
}

void CodeLab::setTask(const QVariantMap &task)
{
    m_task = task;
    setVerdict(QString(), QString());
}

void CodeLab::setVerdict(const QString &verdict, const QString &text)
{
    if (m_verdict == verdict && m_verdictText == text) {
        return;
    }
    m_verdict = verdict;
    m_verdictText = text;
    emit verdictChanged();
}

bool CodeLab::judge(const QVariantMap &task, QString *why) const
{
    // 1) 先看有没有用对积木。只看输出的话，「直接打印答案」也能过，
    //    那就学不到循环 / 变量 / 判断了。
    const QVariantList require = task.value(QStringLiteral("require")).toList();
    for (const QVariant &item : require) {
        const QString kind = item.toString();
        if (kind.isEmpty() || m_usedKinds.contains(kind)) {
            continue;
        }
        *why = QStringLiteral("这一关要用到「%1」积木，你还没用上").arg(kindName(kind));
        return false;
    }

    // 2) 再看输出。按顺序逐行找，允许程序多打印别的内容（孩子爱加点缀），
    //    但不允许缺行、也不允许顺序反了 —— 队列/栈的意义就在顺序上。
    const QVariantList expect = task.value(QStringLiteral("expect")).toList();
    if (expect.isEmpty()) {
        return true;
    }

    QStringList lines;
    for (const QString &raw : m_output.split(QLatin1Char('\n'))) {
        const QString trimmed = raw.trimmed();
        if (!trimmed.isEmpty()) {
            lines << trimmed;
        }
    }

    int cursor = 0;
    for (const QVariant &item : expect) {
        const QString want = item.toString().trimmed();
        bool found = false;
        for (int i = cursor; i < lines.size(); ++i) {
            if (lines.at(i) == want) {
                cursor = i + 1;
                found = true;
                break;
            }
        }
        if (!found) {
            *why = QStringLiteral("还差一行「%1」").arg(want);
            return false;
        }
    }
    return true;
}

void CodeLab::evaluateTask()
{
    if (m_task.isEmpty()) {
        return;    // 自由创作 / 教学浏览，不判题
    }
    QString why;
    if (judge(m_task, &why)) {
        setVerdict(QStringLiteral("pass"), QStringLiteral("过关！做得很好"));
        const QString id = m_task.value(QStringLiteral("id")).toString();
        if (!id.isEmpty() && !m_passed.contains(id)) {
            m_passed << id;
            saveProgress();
            emit progressChanged();
        }
    } else {
        setVerdict(QStringLiteral("fail"), why);
    }
}

QString CodeLab::generate(const QVariantList &blocks) const
{
    QStringList lines;
    int depth = 1;    // main() 的花括号之内，缩进一层
    int open = 0;     // 还没收口的循环/判断数

    for (const QVariant &item : blocks) {
        const QVariantMap b = item.toMap();
        const QString kind = b.value(QStringLiteral("kind")).toString();

        if (kind == QLatin1String("end")) {
            if (open == 0) {
                continue;    // 多出来的「结束」忽略，不让它变成孤零零的 }
            }
            --open;
            depth = qMax(1, depth - 1);
            lines << QString(depth * 4, QLatin1Char(' ')) + QLatin1String("}");
            continue;
        }

        const QString pad(depth * 4, QLatin1Char(' '));

        if (kind == QLatin1String("print")) {
            QString text = b.value(QStringLiteral("text")).toString();
            if (text.isEmpty()) {
                text = QStringLiteral("你好");
            }
            if (text.size() > 60) {
                text = text.left(60);    // 太长的一行在代码框里没法看
            }
            lines << pad + QLatin1String("printf(\"%s\\n\", \"") + escapeForC(text)
                         + QLatin1String("\");");
        } else if (kind == QLatin1String("printvar")) {
            const int v = slotNumber(b.value(QStringLiteral("slot")).toInt());
            // 注意这里必须用 QStringLiteral：QLatin1String 会把 UTF-8 的
            // 「变量」六个字节按 Latin-1 解成一个乱码串。arg 替换两处 %1。
            lines << pad + QStringLiteral("printf(\"变量%1 = %d\\n\", v%1);").arg(v);
        } else if (kind == QLatin1String("varset")) {
            const int v = slotNumber(b.value(QStringLiteral("slot")).toInt());
            const int value = clampValue(b.value(QStringLiteral("value")).toInt());
            lines << pad + QLatin1String("v") + QString::number(v)
                         + QLatin1String(" = ") + QString::number(value)
                         + QLatin1String(";");
        } else if (kind == QLatin1String("varadd")) {
            const int v = slotNumber(b.value(QStringLiteral("slot")).toInt());
            const int value = clampValue(b.value(QStringLiteral("value")).toInt());
            lines << pad + QLatin1String("v") + QString::number(v)
                         + QLatin1String(" = v") + QString::number(v)
                         + QLatin1String(" + ") + QString::number(value)
                         + QLatin1String(";");
        } else if (kind == QLatin1String("loop")) {
            const int times = qBound(1, b.value(QStringLiteral("times")).toInt(), 20);
            lines << pad + QLatin1String("for (int i = 0; i < ") + QString::number(times)
                         + QLatin1String("; i++) {");
            ++depth;
            ++open;
        } else if (kind == QLatin1String("if")) {
            const int v = slotNumber(b.value(QStringLiteral("slot")).toInt());
            const int value = clampValue(b.value(QStringLiteral("value")).toInt());
            QString op = b.value(QStringLiteral("op")).toString();
            if (op != QLatin1String("<") && op != QLatin1String("==")) {
                op = QLatin1String(">");    // 白名单，别的符号一律当大于
            }
            lines << pad + QLatin1String("if (v") + QString::number(v)
                         + QLatin1String(" ") + op + QLatin1String(" ")
                         + QString::number(value) + QLatin1String(") {");
            ++depth;
            ++open;
        }
        // 未知 kind 静默跳过：界面升级了而后端还没跟上时，宁可少一行也不要崩
    }

    // 孩子忘了收口时替他补上，而不是甩一段语法错误 —— 但界面上会提示一声，
    // 否则「为什么多了一个我并没搭的括号」会成为新的困惑。
    while (open > 0) {
        --open;
        depth = qMax(1, depth - 1);
        lines << QString(depth * 4, QLatin1Char(' ')) + QLatin1String("}");
    }

    QString src;
    src += QStringLiteral("/* 小小程序员：这些代码是你的积木变出来的 */\n");
    src += QLatin1String("#include <stdio.h>\n\n");
    src += QLatin1String("int main(void)\n{\n");
    src += QStringLiteral("    int v1 = 0;   /* 变量1 */\n");
    src += QStringLiteral("    int v2 = 0;   /* 变量2 */\n");
    src += QStringLiteral("    int v3 = 0;   /* 变量3 */\n");
    if (!lines.isEmpty()) {
        src += QLatin1Char('\n');
        src += lines.join(QLatin1Char('\n'));
        src += QLatin1Char('\n');
    }
    src += QLatin1String("\n    return 0;\n}\n");
    return src;
}

void CodeLab::setProgram(const QVariantList &blocks)
{
    m_blockCount = blocks.size();

    QString hint;
    // 先数一遍没配对的开始块，用于提示；generate 内部会自行补齐。
    int open = 0;
    for (const QVariant &item : blocks) {
        const QString kind = item.toMap().value(QStringLiteral("kind")).toString();
        if (kind == QLatin1String("loop") || kind == QLatin1String("if")) {
            ++open;
        } else if (kind == QLatin1String("end") && open > 0) {
            --open;
        }
    }
    if (open > 0) {
        hint = QStringLiteral("帮你补了 %1 个「结束」，程序才合得上").arg(open);
    }

    m_code = generate(blocks);
    setHint(hint);

    // 记住用到了哪些积木，判题的 require 检查要用
    m_usedKinds.clear();
    for (const QVariant &item : blocks) {
        m_usedKinds.insert(item.toMap().value(QStringLiteral("kind")).toString());
    }

    // 积木一动，上一次的判题结论就作废：不然孩子改了程序还挂着「过关！」
    setVerdict(QString(), QString());

    // 状态只在空闲时更新：正在编译/运行时不要被「可以运行了」打断
    if (m_stage == Idle) {
        setStatus(m_blockCount > 0 ? QStringLiteral("可以运行了")
                                   : QStringLiteral("准备好了"));
    }

    emit codeChanged();
}

void CodeLab::run()
{
    if (m_stage != Idle) {
        return;    // 已经在编译/运行，重复点不叠加
    }
    if (!compilerAvailable()) {
        setOutput(QStringLiteral("没找到 C 语言编译器。\n\n"
                                 "请家长在终端里执行：\n"
                                 "    sudo apt install gcc\n\n"
                                 "装好之后重新打开这个页面就能运行了。\n"
                                 "（代码照样可以在右边看到，也可以自己复制走。）"));
        setStatus(QStringLiteral("缺少编译器"));
        return;
    }
    if (m_blockCount == 0) {
        setOutput(QStringLiteral("还没有积木呢。先在左边点一块，把它变成程序吧。"));
        setStatus(QStringLiteral("还没有积木"));
        return;
    }

    m_dir = std::make_unique<QTemporaryDir>(workDirTemplate());
    if (!m_dir->isValid()) {
        setOutput(QStringLiteral("创建临时目录失败，没法编译。"));
        setStatus(QStringLiteral("出错了"));
        return;
    }

    const QString sourcePath = m_dir->filePath(QStringLiteral("prog.c"));
    m_progPath = m_dir->filePath(QStringLiteral("prog"));
    QFile file(sourcePath);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        setOutput(QStringLiteral("写不了源码文件：%1").arg(file.errorString()));
        setStatus(QStringLiteral("出错了"));
        return;
    }
    file.write(m_code.toUtf8());
    file.close();

    m_output.clear();
    emit outputChanged();

    m_stage = Compiling;
    emit runningChanged();
    setStatus(QStringLiteral("正在编译…"));

    m_proc = new QProcess(this);
    m_proc->setWorkingDirectory(m_dir->path());
    m_proc->setProgram(m_compiler);
    // -w 关掉「变量没用过」这类警告：界面里那三个变量是固定声明的，
    // 孩子只用一个的时候，警告会淹没真正有用的错误。
    // -fexec-charset=UTF-8 保证中文在 C locale 的会话里也能正确输出。
    m_proc->setArguments({QStringLiteral("-std=c11"),
                          QStringLiteral("-O0"),
                          QStringLiteral("-w"),
                          QStringLiteral("-finput-charset=UTF-8"),
                          QStringLiteral("-fexec-charset=UTF-8"),
                          QStringLiteral("-o"), QStringLiteral("prog"),
                          QStringLiteral("prog.c")});
    m_proc->setProcessChannelMode(QProcess::SeparateChannels);

    connect(m_proc, QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished),
            this, &CodeLab::onCompileFinished);
    connect(m_proc, &QProcess::errorOccurred, this, [this](QProcess::ProcessError error) {
        if (m_stage != Compiling || error != QProcess::FailedToStart) {
            return;
        }
        onStartFailed(QStringLiteral("编译器启动失败"));
    });

    m_timer->start(kCompileTimeout);
    m_proc->start();
}

void CodeLab::onCompileFinished(int exitCode, int exitStatus)
{
    if (m_stage != Compiling) {
        return;
    }
    m_timer->stop();

    const QString stdErr = QString::fromUtf8(m_proc->readAllStandardError()).trimmed();
    const QString stdOut = QString::fromUtf8(m_proc->readAllStandardOutput()).trimmed();
    cleanupProcess();

    if (exitStatus != QProcess::NormalExit || exitCode != 0) {
        QString text = QStringLiteral("编译器说这段程序还有些地方要改：\n\n");
        text += stdErr.isEmpty() ? stdOut : stdErr;
        text += QStringLiteral("\n\n提示：每一块「重复」「如果」都要有配对的「结束」，"
                               "积木的顺序也会影响结果。");
        setOutput(text);
        setStatus(QStringLiteral("编译没通过"));
        setVerdict(QStringLiteral("fail"), QStringLiteral("程序还没编译通过"));
        m_stage = Idle;
        emit runningChanged();
        return;
    }

    // 编译通过 → 运行
    const QFileInfo progInfo(m_progPath);
    if (!progInfo.exists() || progInfo.size() <= 0) {
        // gcc 返回 0 却没好产物，只可能是磁盘满 / 目录不可写这类环境问题。
        setOutput(QStringLiteral("编译器说编译通过了，但没有生成可执行文件。\n\n"
                                 "文件位置：%1\n\n"
                                 "多半是磁盘空间不够，或者这个目录不能写。"
                                 "清一下磁盘再试，或让家长看看终端里 `df -h` 的结果。")
                      .arg(m_progPath));
        setStatus(QStringLiteral("出错了"));
        m_stage = Idle;
        emit runningChanged();
        return;
    }

    m_stage = Running;
    setStatus(QStringLiteral("正在运行…"));

    m_proc = new QProcess(this);
    m_proc->setWorkingDirectory(m_dir->path());
    m_proc->setProgram(m_progPath);
    m_proc->setProcessChannelMode(QProcess::MergedChannels);

    // fork 之后、exec 之前给子进程套上资源上限。这三个限制是「跑飞了也伤不到
    // 系统」的底线：CPU 2 秒、内存 256 MB、能写的文件 1 MB、不产生 core。
    // 注意只能在子进程侧设置 —— 父进程里 setrlimit 会改掉整个应用。
#ifdef Q_OS_LINUX
    m_proc->setChildProcessModifier([]() {
        auto limit = [](int what, rlim_t soft, rlim_t hard) {
            struct rlimit rl;
            rl.rlim_cur = soft;
            rl.rlim_max = hard;
            setrlimit(what, &rl);
        };
        limit(RLIMIT_CPU, 2, 3);                                    // 最多 2 秒 CPU
        limit(RLIMIT_AS, 256ull * 1024 * 1024, 256ull * 1024 * 1024); // 256 MB 内存
        limit(RLIMIT_FSIZE, 1024 * 1024, 1024 * 1024);              // 最多写 1 MB
        limit(RLIMIT_CORE, 0, 0);                                   // 不产生 core
    });
#endif

    connect(m_proc, &QProcess::readyReadStandardOutput, this, [this]() {
        if (m_stage != Running || !m_proc) {
            return;
        }
        appendOutput(QString::fromUtf8(m_proc->readAllStandardOutput()));
    });
    connect(m_proc, QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished),
            this, &CodeLab::onRunFinished);
    connect(m_proc, &QProcess::errorOccurred, this, [this](QProcess::ProcessError error) {
        if (m_stage != Running || error != QProcess::FailedToStart) {
            return;
        }
        onRunStartFailed();
    });

    m_timer->start(kRunTimeout);
    m_proc->start();
}

void CodeLab::onRunFinished(int exitCode, int exitStatus)
{
    if (m_stage != Running) {
        return;
    }
    m_timer->stop();

    if (m_proc) {
        appendOutput(QString::fromUtf8(m_proc->readAllStandardOutput()));
    }
    cleanupProcess();

    const bool crashed = (exitStatus == QProcess::CrashExit);

    if (crashed) {
        // 被 RLIMIT_CPU 杀掉时就是这条路：孩子的循环写成了死循环。
        appendOutput(QStringLiteral("\n\n（程序跑得太久，被安全保护停下来了 —— "
                                    "检查一下「重复」的次数，或者有没有一直不结束的循环。）"));
        setStatus(QStringLiteral("运行太久，已停止"));
        setVerdict(QStringLiteral("fail"),
                   QStringLiteral("程序停不下来，检查一下「重复」的次数"));
    } else if (exitCode != 0) {
        // printf 正常返回 0；非 0 多半是越界访问，说明积木本身没写错，
        // 只是触到了边界，如实说清楚比含糊地报「出错了」有用。
        appendOutput(QStringLiteral("\n\n（程序自己退出了，返回码 %1）").arg(exitCode));
        setStatus(QStringLiteral("运行结束"));
    } else {
        setStatus(QStringLiteral("运行结束"));
    }

    m_stage = Idle;
    emit runningChanged();

    // 判题必须在补「没有输出」那句提示之前：那句话会写进 m_output，
    // 混进逐行比对里，让「差一行」的提示变得莫名其妙。
    if (!crashed) {
        evaluateTask();
    }

    if (m_output.trimmed().isEmpty()) {
        setOutput(QStringLiteral("（程序运行完了，但没有输出。"
                                 "用「打印一句话」积木才会看到文字哦。）"));
    }
}

void CodeLab::onTimeout()
{
    if (m_stage == Compiling) {
        cleanupProcess();
        setOutput(QStringLiteral("编译超过 %1 秒还没结束，已经停下了。"
                                 "换个简单的程序再试试。")
                      .arg(kCompileTimeout / 1000));
        setStatus(QStringLiteral("编译超时"));
        m_stage = Idle;
        emit runningChanged();
        return;
    }
    if (m_stage == Running) {
        cleanupProcess();
        appendOutput(QStringLiteral("\n\n（程序运行超过 %1 秒，被停下来了 —— "
                                    "多半是循环停不下来。）")
                         .arg(kRunTimeout / 1000));
        setStatus(QStringLiteral("运行太久，已停止"));
        setVerdict(QStringLiteral("fail"),
                   QStringLiteral("程序跑太久，检查一下「重复」的次数"));
        m_stage = Idle;
        emit runningChanged();
    }
}

void CodeLab::stop()
{
    if (m_stage == Idle) {
        return;
    }
    m_timer->stop();
    cleanupProcess();
    appendOutput(QStringLiteral("\n\n（已停止）"));
    setStatus(QStringLiteral("已停止"));
    m_stage = Idle;
    emit runningChanged();
}

void CodeLab::clearOutput()
{
    m_output.clear();
    emit outputChanged();
    if (m_stage == Idle) {
        setStatus(QStringLiteral("准备好了"));
    }
}

void CodeLab::cleanupProcess()
{
    if (!m_proc) {
        return;
    }
    // 先断开，否则 kill() 引起的 finished 会再走进一次收尾逻辑
    m_proc->disconnect(this);
    if (m_proc->state() != QProcess::NotRunning) {
        m_proc->kill();
        m_proc->waitForFinished(300);
    }
    m_proc->deleteLater();
    m_proc = nullptr;
}

void CodeLab::onStartFailed(const QString &what)
{
    m_timer->stop();
    const QString why = m_proc ? m_proc->errorString() : QStringLiteral("未知原因");
    cleanupProcess();
    setOutput(QStringLiteral("%1：%2").arg(what, why));
    m_stage = Idle;
    emit runningChanged();
    setStatus(QStringLiteral("出错了"));
}

// 程序「编译出来了却启动不了」时，光甩一句 execve 的 errno 没人看得懂。
// 这里把文件本身的状态查一遍，分清三种情形再说话：
//   文件没了 / 文件在但没执行权限 / 文件在、权限也有，那就是挂载或运行时环境的问题。
void CodeLab::onRunStartFailed()
{
    m_timer->stop();
    const QString why = m_proc ? m_proc->errorString() : QStringLiteral("未知原因");
    cleanupProcess();

    const QFileInfo info(m_progPath);
    // 日志里留一份，事后翻 kidssafe.log 能直接看到当时到底是哪种情形。
    qWarning("[codelab] 程序启动失败 path=%s exists=%d exec=%d err=%s",
             qUtf8Printable(m_progPath), int(info.exists()),
             int(info.isExecutable()), qUtf8Printable(why));
    QString text;
    if (!info.exists()) {
        text = QStringLiteral("程序启动失败：编译出来的文件不见了。\n\n"
                              "文件位置：%1\n\n"
                              "再点一次「运行」会重新编译一次，通常就好了。")
                   .arg(m_progPath);
    } else if (!info.isExecutable()) {
        text = QStringLiteral("程序启动失败：编译出来的文件没有执行权限。\n\n"
                              "文件位置：%1\n"
                              "系统说：%2\n\n"
                              "让家长在终端里执行 `chmod +x` 加上权限，"
                              "或者检查一下这个目录是不是被挂载成了 noexec。")
                   .arg(m_progPath, why);
    } else {
        const QString ldd = probeLdd(m_progPath);
        text = QStringLiteral("程序启动失败：文件在、也有执行权限，但系统不让它启动。\n\n"
                              "文件位置：%1\n"
                              "文件大小：%2 字节\n"
                              "系统说：%3\n\n"
                              "这不是缺 GCC —— 编译器已经把程序做出来了，只是内核在执行这一步"
                              "退回来了。常见原因：\n"
                              "  1. 编译器产出的程序和本机不配套（跨架构，或系统里缺动态加载器）；\n"
                              "  2. 这个目录被挂载成 noexec，或者子进程看不到这个路径。\n\n")
                   .arg(m_progPath)
                   .arg(info.size())
                   .arg(why);
        if (!ldd.isEmpty()) {
            text += QStringLiteral("ldd 的自查结果：\n%1\n\n").arg(ldd);
        }
        text += QStringLiteral("家长可以在终端里依次试：\n"
                               "    %1\n"
                               "    mount | grep -E ' /tmp | /home '\n\n"
                               "第一条若报「没有那个文件或目录」，就是加载器/依赖的问题；"
                               "第二条若看到 noexec，就是那个目录不能执行程序。"
                               "把这两条的结果发我，我按情况再改。")
                    .arg(m_progPath);
    }

    setOutput(text);
    setStatus(QStringLiteral("出错了"));
    m_stage = Idle;
    emit runningChanged();
}

void CodeLab::setOutput(const QString &text)
{
    if (m_output == text) {
        return;
    }
    m_output = text;
    emit outputChanged();
}

void CodeLab::appendOutput(const QString &text)
{
    if (text.isEmpty()) {
        return;
    }
    m_output += text;
    if (m_output.size() > kMaxOutput) {
        m_output.truncate(kMaxOutput);
        m_output += QStringLiteral("\n…（输出太多，后面的省略了）");
        emit outputChanged();
        // 这里不能调 stop()：stop() 还要往 output 里追加一行，会再次越界，
        // 再次调 stop() —— 直接就地收尾，别绕回去。
        m_timer->stop();
        cleanupProcess();
        setStatus(QStringLiteral("输出太多，已停止"));
        m_stage = Idle;
        emit runningChanged();
        return;
    }
    emit outputChanged();
}

void CodeLab::setStatus(const QString &text)
{
    if (m_status == text) {
        return;
    }
    m_status = text;
    emit statusChanged();
}

void CodeLab::setHint(const QString &text)
{
    if (m_hint == text) {
        return;
    }
    m_hint = text;
    emit hintChanged();
}
