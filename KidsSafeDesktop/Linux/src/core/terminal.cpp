#include "core/terminal.h"

#include <QDateTime>
#include <QDir>
#include <QRegularExpression>
#include <QSocketNotifier>
#include <QStandardPaths>

#include <cerrno>

#ifdef Q_OS_LINUX
#include <fcntl.h>
#include <pty.h>
#include <signal.h>
#include <sys/ioctl.h>
#include <sys/wait.h>
#include <termios.h>
#include <unistd.h>
#endif

namespace {

/*
 * xterm 的 16 个基础色，取的是 One Dark 那一套。
 *
 * 不用系统主题色：终端的可读性靠对比度，深底配深色字在孩子那块偏暗的屏幕上
 * 几乎看不见。这套配色在深灰底上每种颜色的亮度都够，红绿也不至于刺眼。
 */
const QColor kAnsi[16] = {
    QColor("#3B4048"), QColor("#E06C75"), QColor("#98C379"), QColor("#E5C07B"),
    QColor("#61AFEF"), QColor("#C678DD"), QColor("#56B6C2"), QColor("#ABB2BF"),
    QColor("#5C6370"), QColor("#E06C75"), QColor("#98C379"), QColor("#E5C07B"),
    QColor("#61AFEF"), QColor("#C678DD"), QColor("#56B6C2"), QColor("#FFFFFF"),
};

const QColor kDefaultFg("#D7DAE0");

QColor ansi16(int n)
{
    if (n < 0 || n > 15) {
        return kDefaultFg;
    }
    return kAnsi[n];
}

/// 256 色：0-15 是基础色，16-231 是 6×6×6 色立方，232-255 是灰阶。
QColor xterm256(int n)
{
    if (n < 16) {
        return ansi16(n);
    }
    if (n < 232) {
        const int i = n - 16;
        const int r = i / 36;
        const int g = (i % 36) / 6;
        const int b = i % 6;
        auto level = [](int v) { return v == 0 ? 0 : 55 + v * 40; };
        return QColor(level(r), level(g), level(b));
    }
    const int gray = 8 + (n - 232) * 10;
    return QColor(gray, gray, gray);
}

}  // namespace

Terminal::Terminal(QObject *parent)
    : QObject(parent)
{
    m_fg = kDefaultFg;

    // 输出合并：apt / ls 会在一瞬间吐几百行，每行都重建一次富文本会把界面拖住。
    // 攒 60ms 一起刷，肉眼看起来仍然是「实时」的。
    m_coalesce.setSingleShot(true);
    m_coalesce.setInterval(60);
    connect(&m_coalesce, &QTimer::timeout, this, [this]() {
        m_html = buildHtml();
        Q_EMIT outputChanged();
    });

    // 光标闪烁。530ms 是 xterm 的默认节奏，孩子盯一会儿就能看出「它活着」。
    // 只改一个 bool 再重建一次富文本，比在 QML 里叠一个矩形去算坐标稳得多——
    // 折行以后光标该落在第几个像素，自己算是算不准的。
    m_caret.setInterval(530);
    connect(&m_caret, &QTimer::timeout, this, [this]() {
        m_caretOn = !m_caretOn;
        m_html = buildHtml();
        Q_EMIT outputChanged();
    });
}

Terminal::~Terminal()
{
    stop();
}

void Terminal::start()
{
#ifdef Q_OS_LINUX
    if (m_master >= 0) {
        return;
    }

    m_lines.clear();
    m_current.clear();
    m_pending.clear();
    m_typing.clear();
    m_typingTrusted = true;
    m_exited = false;
    m_state = Normal;
    m_fg = kDefaultFg;
    m_bold = false;
    m_html.clear();
    m_crPending = false;
    m_caretOn = true;

    struct winsize ws = {};
    ws.ws_row = 40;
    ws.ws_col = 120;

    int master = -1;
    const pid_t pid = forkpty(&master, nullptr, nullptr, &ws);
    if (pid < 0) {
        m_exited = true;
        appendText(QStringLiteral("终端起不来（forkpty 失败），可能是系统资源不够。"));
        newLine();
        Q_EMIT runningChanged();
        return;
    }

    if (pid == 0) {
        /*
         * 子进程：只能调 async-signal-safe 的函数，所以这里不碰 Qt、不分配大对象。
         *
         * HISTFILE 指到 /dev/null：不留历史文件。否则孩子敲过的命令会存进
         * ~/.bash_history，下次家长开终端按一下上箭头就全看到了 —— 那既不
         * 是孩子想要的，也不是家长想管的。
         */
        setenv("TERM", "xterm-256color", 1);
        setenv("COLORTERM", "truecolor", 1);
        setenv("PS1",
               "\\[\\e[38;5;114m\\]kidsafe\\[\\e[0m\\]:\\[\\e[38;5;110m\\]\\w\\[\\e[0m\\] $ ",
               1);
        setenv("PS2", "> ", 1);
        setenv("PROMPT_COMMAND", "", 1);
        setenv("HISTFILE", "/dev/null", 1);

        const QString home = QStandardPaths::writableLocation(QStandardPaths::HomeLocation);
        if (!home.isEmpty()) {
            if (chdir(home.toLocal8Bit().constData()) != 0) {
                // 家目录进不去就退到根目录；根目录也不行就留在继承来的目录里。
                // 初始工作目录不对而已，不该因此拦住 shell 启动。
                if (chdir("/") != 0) {
                    /* 两个都失败，忽略即可 */
                }
            }
        }

        execl("/bin/bash", "bash", "--noprofile", "--norc", "-i", nullptr);
        _exit(127);
    }

    // 父进程
    m_master = master;
    m_child = pid;

    const int flags = fcntl(m_master, F_GETFL, 0);
    fcntl(m_master, F_SETFL, flags | O_NONBLOCK);
    // 子进程退出时别在 write 时给我们一个 SIGPIPE，直接结束进程
    fcntl(m_master, F_SETFD, FD_CLOEXEC);

    m_notifier = new QSocketNotifier(m_master, QSocketNotifier::Read, this);
    connect(m_notifier, &QSocketNotifier::activated, this, &Terminal::onReadable);

    m_caret.start();
    // 清完屏立刻刷一次：屏幕上只剩一个光标，孩子一眼就知道「可以打字了」。
    // 不刷的话会一直显示上一次 shell 的旧内容，直到新输出到达。
    m_html = buildHtml();
    Q_EMIT outputChanged();
    Q_EMIT runningChanged();
#endif
}

void Terminal::stop()
{
    m_caret.stop();
#ifdef Q_OS_LINUX
    if (m_notifier) {
        m_notifier->setEnabled(false);
        m_notifier->deleteLater();
        m_notifier = nullptr;
    }
    if (m_child > 0) {
        // 杀整个进程组：shell 里可能还挂着一个正在跑的 python3
        ::kill(-m_child, SIGKILL);
        ::kill(m_child, SIGKILL);
        int status = 0;
        ::waitpid(m_child, &status, WNOHANG);
        m_child = -1;
    }
    if (m_master >= 0) {
        ::close(m_master);
        m_master = -1;
    }
#endif
    m_exited = true;
    Q_EMIT runningChanged();
}

void Terminal::onReadable()
{
#ifdef Q_OS_LINUX
    if (m_master < 0) {
        return;
    }
    char buffer[8192];
    for (;;) {
        const ssize_t got = ::read(m_master, buffer, sizeof(buffer));
        if (got > 0) {
            feed(QByteArray(buffer, int(got)));
            continue;
        }
        if (got == 0) {
            // shell 退出（敲了 exit，或者崩了）
            if (m_notifier) {
                m_notifier->setEnabled(false);
            }
            ::close(m_master);
            m_master = -1;
            m_exited = true;
            m_caret.stop();
            appendText(QStringLiteral("\n[终端已结束]"));
            newLine();
            markOutput();
            Q_EMIT runningChanged();
            return;
        }
        if (errno == EAGAIN || errno == EWOULDBLOCK) {
            break;
        }
        if (errno == EINTR) {
            continue;
        }
        // 其他错误（EIO）：PTY 那头没了
        if (m_notifier) {
            m_notifier->setEnabled(false);
        }
        ::close(m_master);
        m_master = -1;
        m_exited = true;
        m_caret.stop();
        Q_EMIT runningChanged();
        return;
    }
    markOutput();
#endif
}

void Terminal::flushPending()
{
    if (m_pending.isEmpty()) {
        return;
    }
    const QString text = QString::fromUtf8(m_pending);
    m_pending.clear();
    if (text.isEmpty()) {
        return;
    }
    // 解出替换字符说明这段不是合法 UTF-8，按本地编码再试一次
    if (text.contains(QChar(0xFFFD))) {
        appendText(QString::fromLocal8Bit(text.toUtf8()));
    } else {
        appendText(text);
    }
}

void Terminal::feed(const QByteArray &bytes)
{
    for (int i = 0; i < bytes.size(); ++i) {
        const unsigned char uc = static_cast<unsigned char>(bytes.at(i));
        if (uc < 0x80) {
            flushPending();
            handleAscii(char(uc));
        } else {
            m_pending.append(char(uc));
            // 4 字节是 UTF-8 的上限，攒够了就试着解一次
            if (m_pending.size() >= 4) {
                flushPending();
            }
        }
    }
}

void Terminal::handleAscii(char c)
{
    switch (m_state) {
    case Escape:
        if (c == '[') {
            m_state = Csi;
            m_seq.clear();
        } else if (c == ']') {
            m_state = Osc;
            m_seq.clear();
        } else {
            // ESC ( B 这种字符集切换，吞掉一个字节就够了
            m_state = Normal;
        }
        return;

    case Csi:
        if (c >= 0x30 && c <= 0x3f) {
            m_seq.append(QLatin1Char(c));
            if (m_seq.size() > 64) {
                m_seq = m_seq.right(64);
            }
            return;
        }
        if (c >= 0x40 && c <= 0x7e) {
            handleCsi(c);
            m_state = Normal;
        }
        // 中间字节（0x20-0x2f）忽略
        return;

    case Osc:
        if (c == '\a') {
            m_state = Normal;
        } else if (c == '\x1b') {
            // 有些程序用 ESC \ 结束 OSC，留给下一轮 Escape 处理
            m_state = Normal;
        } else if (m_seq.size() < 256) {
            m_seq.append(QLatin1Char(c));
        }
        return;

    case Normal:
        break;
    }

    switch (c) {
    case '\x1b':
        m_state = Escape;
        break;
    case '\n':
        flushPending();
        newLine();
        break;
    case '\r':
        /*
         * 回车 = 回到行首，**不是**「把这一行擦掉」。
         *
         * PTY 的输出会把每个 \n 翻成 \r\n（ONLCR），所以正常的一行输出是
         * 「文字 + \r + \n」。原来这里一看到 \r 就 m_current.clear()，于是
         * 每一行文字在换行前都被自己抹掉，屏幕上只剩一串空行 —— 孩子敲完
         * 命令按回车，那一行立刻变成空白，看起来就是「打字不显示」。
         *
         * 现在只记一个「下次写字要回到行首」的标记：
         *   · `\r\n` ：\n 直接把这一行收走，标记在 newLine 里清掉；
         *   · `\r` + 文字（readline 重绘一整行）：写字前才把旧内容丢掉。
         */
        m_crPending = true;
        break;
    case '\b':
        backspace();
        break;
    case '\t':
        appenTab();
        break;
    case '\a':
        break;   // 响铃：静音
    default:
        if (c >= 0x20 && c < 0x7f) {
            appendText(QString(QChar(c)));
        }
        break;
    }
}

void Terminal::handleCsi(char final)
{
    switch (final) {
    case 'm':
        applySgr(m_seq);
        break;
    case 'J': {
        // 2 / 3 是清整屏；0 是从光标清到末尾，这里近似成清当前行
        const int mode = m_seq.isEmpty() ? 0 : m_seq.toInt();
        if (mode == 2 || mode == 3) {
            clearScreen();
        } else {
            m_current.clear();
        }
        break;
    }
    case 'K':
        m_current.clear();
        break;
    default:
        // 光标移动（A-D/H/f）、隐藏光标（h/l）、滚动区域（r）统统忽略：
        // 我们不是完整终端，没有光标坐标可动。
        break;
    }
}

void Terminal::applySgr(const QString &params)
{
    const QStringList parts = params.split(QLatin1Char(';'));
    if (parts.isEmpty()) {
        m_fg = kDefaultFg;
        m_bold = false;
        return;
    }

    for (int i = 0; i < parts.size(); ++i) {
        const QString piece = parts.at(i).trimmed();
        const int v = piece.isEmpty() ? 0 : piece.toInt();

        if (v == 0) {
            m_fg = kDefaultFg;
            m_bold = false;
        } else if (v == 1) {
            m_bold = true;
        } else if (v == 22) {
            m_bold = false;
        } else if (v == 39) {
            m_fg = kDefaultFg;
        } else if (v >= 30 && v <= 37) {
            m_fg = ansi16(v - 30);
        } else if (v >= 90 && v <= 97) {
            m_fg = ansi16(v - 90 + 8);
        } else if (v == 38) {
            // 38;5;n 或 38;2;r;g;b
            if (i + 1 < parts.size()) {
                const int mode = parts.at(i + 1).toInt();
                if (mode == 5 && i + 2 < parts.size()) {
                    m_fg = xterm256(parts.at(i + 2).toInt());
                    i += 2;
                } else if (mode == 2 && i + 4 < parts.size()) {
                    m_fg = QColor(parts.at(i + 2).toInt(),
                                  parts.at(i + 3).toInt(),
                                  parts.at(i + 4).toInt());
                    i += 4;
                }
            }
        } else if (v == 48) {
            // 背景色：不支持，但必须把参数吃干净，否则 r;g;b 会被当前景色
            if (i + 1 < parts.size()) {
                const int mode = parts.at(i + 1).toInt();
                if (mode == 5) {
                    i += 2;
                } else if (mode == 2) {
                    i += 4;
                }
            }
        }
        // 4 下划线 / 7 反显 / 9 删除线：不做，忽略
    }
}

void Terminal::appendText(const QString &text)
{
    if (text.isEmpty()) {
        return;
    }
    if (m_crPending) {
        // 上一个字符是 \r：这次写字从行首开始，旧内容（readline 重绘前的
        // 那一版）整段丢掉
        m_current.clear();
        m_crPending = false;
    }
    if (!m_current.isEmpty()) {
        Span &last = m_current.last();
        if (last.color == m_fg && last.bold == m_bold) {
            last.text += text;
            return;
        }
    }
    Span span;
    span.text = text;
    span.color = m_fg;
    span.bold = m_bold;
    m_current.append(span);
}

void Terminal::newLine()
{
    m_crPending = false;
    Line line;
    line.spans = m_current;
    m_lines.append(line);
    m_current.clear();

    if (m_lines.size() > m_maxLines) {
        // 一次多丢一点，避免每行都触发一次 removeFirst（O(n)）
        m_lines.erase(m_lines.begin(), m_lines.begin() + (m_lines.size() - m_maxLines));
    }
}

void Terminal::backspace()
{
    if (m_current.isEmpty()) {
        return;
    }
    Span &last = m_current.last();
    if (!last.text.isEmpty()) {
        last.text.chop(1);
    }
    if (last.text.isEmpty()) {
        m_current.removeLast();
    }
}

void Terminal::appenTab()
{
    int column = 0;
    for (const Span &span : m_current) {
        column += span.text.size();
    }
    const int spaces = 8 - (column % 8);
    appendText(QString(spaces, QLatin1Char(' ')));
}

void Terminal::clearScreen()
{
    m_lines.clear();
    m_current.clear();
}

QString Terminal::buildHtml() const
{
    QString out;
    out.reserve(48 * 1024);

    auto emitSpans = [&out](const QList<Span> &spans) {
        if (spans.isEmpty()) {
            out += QStringLiteral("&nbsp;");
            return;
        }
        for (const Span &span : spans) {
            QString escaped = span.text.toHtmlEscaped();
            // 行首空格会被 HTML 折叠，换成 &nbsp; 才能保住缩进
            escaped.replace(QStringLiteral("  "), QStringLiteral("&nbsp;&nbsp;"));
            if (span.bold) {
                out += QStringLiteral("<b><span style=\"color:%1\">%2</span></b>")
                           .arg(span.color.name(), escaped);
            } else {
                out += QStringLiteral("<span style=\"color:%1\">%2</span>")
                           .arg(span.color.name(), escaped);
            }
        }
    };

    for (const Line &line : m_lines) {
        emitSpans(line.spans);
        out += QStringLiteral("<br/>");
    }

    /*
     * 最后一行还没换行，光标就落在这行的末尾。
     *
     * 光标用实心方块字符（U+2588）而不是「带背景色的 span」：QML 的富文本
     * 子集对 bgcolor 的支持随版本变，而这个字符在 Linux 上常见的等宽字体
     * （DejaVu / Noto / Liberation）里都有。它是一段文本，所以折行以后
     * 自动跟着走，不用自己算坐标。
     */
    if (m_current.isEmpty()) {
        if (caretOn()) {
            out += caretHtml();
        } else {
            // 空行也要撑出行高，否则光标那一行会被压扁
            out += QStringLiteral("&nbsp;");
        }
    } else {
        emitSpans(m_current);
        if (caretOn()) {
            out += caretHtml();
        }
    }
    return out;
}

QString Terminal::caretHtml() const
{
    // 用正文色，和真实终端一致：光标是「文字所在的位置」，不是第二种颜色
    return QStringLiteral("<span style=\"color:%1\">%2</span>")
        .arg(kDefaultFg.name(), QString(QChar(0x2588)));
}

bool Terminal::caretOn() const
{
    return m_caretOn && m_master >= 0;
}

void Terminal::markOutput()
{
    // 有新内容就先把光标点亮（真实终端也是这样：一有动静就不再处于「暗」的
    // 半周期）。顺便重置相位，别让光标刚好在换行的一瞬间闪掉。
    m_caretOn = true;
    if (m_master >= 0 && m_caret.isActive()) {
        m_caret.start();
    }
    if (!m_coalesce.isActive()) {
        m_coalesce.start();
    }
}

void Terminal::type(const QString &text)
{
    if (m_master < 0 || text.isEmpty()) {
        return;
    }
    writeBytes(text.toUtf8());
    if (m_typingTrusted) {
        m_typing += text;
    }
}

void Terminal::key(int code)
{
    if (m_master < 0) {
        return;
    }
    switch (code) {
    case 8:      // 退格：tty 的 erase 字符是 DEL(0x7f)，不是 BS
        writeBytes(QByteArray(1, '\x7f'));
        if (m_typingTrusted && !m_typing.isEmpty()) {
            m_typing.chop(1);
        }
        break;
    case 9:
        writeBytes(QByteArray(1, '\t'));
        break;
    case 13:
        enter();
        break;
    case 27:
        writeBytes(QByteArray(1, '\x1b'));
        break;
    case 3:      // Ctrl+C：取消当前输入
        writeBytes(QByteArray(1, '\x03'));
        m_typing.clear();
        m_typingTrusted = true;
        break;
    case 4:      // Ctrl+D
        writeBytes(QByteArray(1, '\x04'));
        m_typing.clear();
        break;
    default:
        break;
    }
}

void Terminal::raw(const QString &sequence)
{
    if (m_master < 0) {
        return;
    }
    writeBytes(sequence.toUtf8());
    /*
     * 方向键会把 PTY 里的行换成历史命令，而我们本地的镜像还是空的/旧的。
     * 从这一刻起这一行不再可信 —— 回车时跳过检查，别用一个错误的镜像
     * 去判断一条它根本没见过的命令。
     */
    m_typingTrusted = false;
}

void Terminal::enter()
{
    if (m_master < 0) {
        return;
    }

    if (m_typingTrusted) {
        QString why;
        if (looksDangerous(m_typing, &why)) {
            m_lastBlocked = why;
            Q_EMIT blocked(why);
            // 用 Ctrl+C 把 readline 里已经打好的那行丢掉，孩子眼前立刻回到提示符
            writeBytes(QByteArray(1, '\x03'));
            m_typing.clear();
            return;
        }
    }

    writeBytes(QByteArray(1, '\r'));
    m_typing.clear();
    m_typingTrusted = true;
}

void Terminal::resize(int columns, int rows)
{
#ifdef Q_OS_LINUX
    if (m_master < 0) {
        return;
    }
    struct winsize ws = {};
    ws.ws_col = quint16(qBound(20, columns, 400));
    ws.ws_row = quint16(qBound(5, rows, 200));
    ::ioctl(m_master, TIOCSWINSZ, &ws);
#endif
}

void Terminal::clear()
{
    clearScreen();
    m_html = buildHtml();
    Q_EMIT outputChanged();
    /*
     * 清屏不要往 PTY 里塞 "\x1b[2J\x1b[H"：那是**输出**序列，写进 PTY 等于
     * 把它当键盘输入交给 readline，bash 会把 ESC 当成 meta 前缀，命令行里
     * 留下一堆乱字符。Ctrl+L 才是「请清屏」的输入约定，readline 收到后会
     * 自己发清屏序列并重画提示符。
     */
    writeBytes(QByteArray(1, '\x0c'));
}

void Terminal::killLine()
{
    if (m_master < 0) {
        return;
    }
    // Ctrl+U：把已经敲进 readline 的这一行丢掉。快捷命令按钮靠它「先清空
    // 再输入」，否则孩子已经打了一半的命令会和按钮的命令拼在一起。
    writeBytes(QByteArray(1, '\x15'));
    m_typing.clear();
    m_typingTrusted = true;
}

void Terminal::writeBytes(const QByteArray &data)
{
#ifdef Q_OS_LINUX
    if (m_master < 0 || data.isEmpty()) {
        return;
    }
    qint64 written = 0;
    while (written < data.size()) {
        const ssize_t n = ::write(m_master, data.constData() + written,
                                  size_t(data.size() - written));
        if (n > 0) {
            written += n;
            continue;
        }
        if (n < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
            // 缓冲区满了：这一小段丢掉比阻塞界面强，键盘输入不会这么猛
            break;
        }
        if (n < 0 && errno == EINTR) {
            continue;
        }
        break;
    }
#endif
}

bool Terminal::looksDangerous(const QString &line, QString *why) const
{
    const QString s = line.toLower().simplified();
    if (s.isEmpty()) {
        return false;
    }

    auto hit = [&s](const char *needle) {
        return s.contains(QLatin1String(needle));
    };

    if (hit("rm -rf /") || hit("rm -fr /") || hit("rm -r -f /")) {
        *why = QStringLiteral("这条命令会把整台电脑的文件删光，不能执行");
        return true;
    }
    if (hit("mkfs")) {
        *why = QStringLiteral("这条命令会格式化硬盘，硬盘上的东西全没了");
        return true;
    }
    if (hit("dd ") && hit("of=/dev/")) {
        *why = QStringLiteral("这条命令会直接往硬盘上写，写坏了就打不开了");
        return true;
    }
    if (hit(":(){") || hit(":(){ :|:& };:")) {
        *why = QStringLiteral("这是一条会让电脑卡死的命令");
        return true;
    }
    if (hit("shutdown") || hit("reboot") || hit("poweroff") || hit("init 0")) {
        *why = QStringLiteral("关机请用家长入口，这里不能关");
        return true;
    }
    if (hit("chmod -r 777 /") || hit("chown -r")) {
        *why = QStringLiteral("这条命令会改掉整台电脑的权限，改坏了就乱了");
        return true;
    }

    // sudo 单独按词判断：`echo sudo` 不该被拦
    static const QRegularExpression sudoRe(QStringLiteral("(^|[;&|])\\s*sudo\\b"));
    if (sudoRe.match(s).hasMatch()) {
        *why = QStringLiteral("sudo 要先问家长，这里用不了");
        return true;
    }
    return false;
}
