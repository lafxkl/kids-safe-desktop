#pragma once

#include <QByteArray>
#include <QColor>
#include <QList>
#include <QObject>
#include <QString>
#include <QTimer>

class QSocketNotifier;

/**
 * 终端窗口。
 *
 * 用的是真的 PTY（forkpty），不是「把命令丢给 QProcess 再收输出」：
 * 后者做不出 Tab 补全、cd、管道、`python3` 交互 —— 而那些恰好是孩子
 * 最想玩的部分（他敲 python3 能算 1+1，这件事本身就是奖励）。
 *
 * 屏幕上显示的是 PTY 吐回来的字节流，我们只负责把 ANSI 转义序列翻译成
 * 「哪几个字是什么颜色」。做成富文本而不是自己维护光标坐标，是因为
 * 完整的终端仿真（vim / top 那种全屏重绘）工作量是另一个量级，而孩子
 * 用不到。代价写在文档里：全屏 TUI 程序会花屏。
 *
 * 输入分两路：
 *   type()  —— 用户敲的可见字符，同时记进「当前这一行」的镜像
 *   key()   —— 回车 / 退格 / Tab / Ctrl+C 这类控制键
 * 镜像存在的唯一目的是在**回车那一刻**检查命令。检查必须发生在按键
 * 转成字节之前 —— 一旦写进 PTY，bash 就执行了，再拦没有意义。
 *
 * 安全策略分两级，写在 looksDangerous() 里：
 *   硬拦：rm -rf /、mkfs、dd of=/dev/sda、fork 炸弹、关机重启
 *   拦 sudo：孩子本来也没有管理员密码，直接告诉他「这个要先问家长」
 * 这不是沙箱。真把终端交到孩子手里，就得承认它能做的和成人终端一样多；
 * 这里只挡住「手滑一次就报废整台机器」的那几个动作。
 */
class Terminal : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool running READ running NOTIFY runningChanged)
    /// 整个屏幕的内容（富文本）。界面直接丢给 Text，不用自己拼颜色。
    Q_PROPERTY(QString html READ html NOTIFY outputChanged)
    /// 已经滚出屏幕的行数，界面用来决定「要不要自动跟到底部」
    Q_PROPERTY(int lineCount READ lineCount NOTIFY outputChanged)
    /// shell 退出了（孩子敲了 exit，或者它自己崩了）
    Q_PROPERTY(bool exited READ exited NOTIFY runningChanged)
    /// 被拦下来的命令（一句话），界面弹提示用
    Q_PROPERTY(QString lastBlocked READ lastBlocked NOTIFY blocked)

public:
    explicit Terminal(QObject *parent = nullptr);
    ~Terminal() override;

    bool running() const { return m_master >= 0; }
    bool exited() const { return m_exited; }
    QString html() const { return m_html; }
    int lineCount() const { return int(m_lines.size()); }
    QString lastBlocked() const { return m_lastBlocked; }

    Q_INVOKABLE void start();
    Q_INVOKABLE void stop();
    Q_INVOKABLE void clear();

    /// 敲进来的一段可见文字（可能一次多个字符，输入法提交中文时就是这样）。
    Q_INVOKABLE void type(const QString &text);

    /// 丢掉已经敲进 shell 的这一行（Ctrl+U）。快捷命令按钮用。
    Q_INVOKABLE void killLine();

    /// 控制键：8 退格、9 Tab、13 回车、27 Esc、3 Ctrl+C、4 Ctrl+D。
    Q_INVOKABLE void key(int code);

    /// 直接发一串转义序列（方向键、Ctrl 组合），不经过镜像。
    Q_INVOKABLE void raw(const QString &sequence);

    /// 回车：先过一遍安全检查，再真正发出去。
    Q_INVOKABLE void enter();

    /// 告诉 PTY 窗口多大。界面尺寸变了调一次，某些程序（如 less）靠它排版。
    Q_INVOKABLE void resize(int columns, int rows);

signals:
    void runningChanged();
    void outputChanged();
    void blocked(const QString &reason);

private:
    struct Span {
        QString text;
        QColor color;
        bool bold = false;
    };
    struct Line {
        QList<Span> spans;
    };

    void onReadable();
    void feed(const QByteArray &bytes);
    void flushPending();
    /// 吃进一个 >=0x80 的字节，凑满一个 UTF-8 字符再交给 appendText
    void pushUtf8(unsigned char uc);
    void handleAscii(char c);
    void handleCsi(char final);
    void applySgr(const QString &params);

    void appendText(const QString &text);
    void newLine();
    void backspace();
    void appenTab();
    void clearScreen();

    void writeBytes(const QByteArray &data);
    void markOutput();
    QString buildHtml() const;
    /// 屏幕末尾那个方块光标（是否可见由 caretOn() 决定）
    QString caretHtml() const;
    bool caretOn() const;
    bool looksDangerous(const QString &line, QString *why) const;

    int m_master = -1;
    int m_child = -1;
    QSocketNotifier *m_notifier = nullptr;

    QList<Line> m_lines;
    QList<Span> m_current;
    QColor m_fg;
    bool m_bold = false;

    // 解析状态机
    enum State { Normal, Escape, Csi, Osc };
    State m_state = Normal;
    QString m_seq;          ///< CSI / OSC 累积的参数
    QByteArray m_pending;   ///< 没凑齐的 UTF-8 多字节序列
    int m_need = 0;         ///< 当前这个字符共占几字节（由首字节决定）
    /// 上一个字符是 \r（回到行首）。真正「抹掉这一行」要等到后面跟着可见
    /// 字符时才做 —— PTY 把 \n 翻成 \r\n，一见 \r 就清行会把整屏文字抹光。
    bool m_crPending = false;

    // 输入镜像（用来在回车前检查命令）
    QString m_typing;
    bool m_typingTrusted = true;   ///< 用过方向键/历史的行不再可信

    int m_maxLines = 1200;         ///< 超过就丢最老的，防止 ls -R 把内存吃光
    QString m_html;
    QString m_lastBlocked;
    bool m_exited = false;

    QTimer m_coalesce;             ///< 把一串输出合并成一次界面刷新
    QTimer m_caret;                ///< 光标闪烁
    bool m_caretOn = true;
};
