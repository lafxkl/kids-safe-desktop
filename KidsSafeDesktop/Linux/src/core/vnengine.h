#pragma once

#include <QObject>
#include <QString>
#include <QStringList>
#include <QVariantList>
#include <QVector>

class QProcess;

/**
 * 吉里吉里（KiriKiri 2 / KAG3）游戏桥。
 *
 * 为什么是「桥」不是「引擎」：krkr2 的源码（199 个文件 include <windows.h>，
 * 渲染走 GDI、声音走 DirectSound、窗口走 HWND 消息循环）编不进 Qt 程序，
 * 仓库里那份还是 Borland C++ Builder 时代的只读存档，官方 readme 自己写着
 * 「残念ながら それほど移植性が高い物とは言えません」。所以这里不做内核移植，
 * 只做**启动器**：找到本机的吉里吉里运行时，把游戏目录交给它，游戏退出后
 * 交还窗口。
 *
 * 还一个更硬的理由：krkr2 是 GPLv2（双重许可里的 GPL 分支），链进本程序会
 * 让整个程序被迫 GPL。用独立进程调用就没有这个问题 —— 进程边界就是许可边界。
 *
 * 后端选择顺序：
 *   1. KIDSSAFE_VN_ENGINE 环境变量（家长显式指定，支持 `{game}` 占位）；
 *   2. krkrsdl2 / krkrsdl（SDL2 移植版，Linux 原生，首选）；
 *   3. wine（跑游戏自带的 .exe，兼容性最好但重）。
 *
 * 游戏目录：~/.local/share/kidssafe/games 下**一个子目录一个游戏**，
 * 目录里有 *.xp3（吉里吉里的数据包）或 *.exe 就算数。
 * 游戏本体不随包分发（版权），也和 ROM 一样由家长自己放。
 */
class VnEngine : public QObject
{
    Q_OBJECT

    Q_PROPERTY(QVariantList games READ games NOTIFY gamesChanged)
    /// 有没有可用的运行时。false 时界面只显示「怎么装」，不显示列表。
    Q_PROPERTY(bool available READ available NOTIFY stateChanged)
    /// 后端的人话名字（「原生引擎 krkrsdl2」/「Wine」/「自定义命令」）。
    Q_PROPERTY(QString backend READ backend NOTIFY stateChanged)
    /// 给人看的说明：后端从哪来、或者缺什么。
    Q_PROPERTY(QString detail READ detail NOTIFY stateChanged)
    /// 游戏目录，家长要往这里放东西。
    Q_PROPERTY(QString gamesDir READ gamesDir CONSTANT)
    Q_PROPERTY(bool running READ running NOTIFY runningChanged)
    /// 正在跑的游戏名，用于「故事正在播放」那层提示。
    Q_PROPERTY(QString runningTitle READ runningTitle NOTIFY runningChanged)
    /// 一句话状态 / 错误原因，几秒后自己消失。
    Q_PROPERTY(QString message READ message NOTIFY stateChanged)

public:
    explicit VnEngine(QObject *parent = nullptr);
    ~VnEngine() override;

    QVariantList games() const { return m_list; }
    bool available() const { return m_backend != Backend::None; }
    QString backend() const;
    QString detail() const { return m_detail; }
    QString gamesDir() const;
    bool running() const { return m_process != nullptr; }
    QString runningTitle() const { return m_runningTitle; }
    QString message() const { return m_message; }

    /// 重新探测运行时 + 重扫游戏目录。
    Q_INVOKABLE void refresh();
    /// 启动第 index 个游戏。返回是否成功起了进程。
    Q_INVOKABLE bool start(int index);
    /// 结束当前游戏（先 terminate，3 秒不退再 kill）。
    Q_INVOKABLE void stop();

signals:
    void gamesChanged();
    void stateChanged();
    void runningChanged();

private:
    enum class Backend {
        None,
        Custom,   ///< KIDSSAFE_VN_ENGINE 指定的命令
        KrkrSdl,  ///< krkrsdl2 / krkrsdl
        Wine      ///< wine + 游戏自带的 exe
    };

    struct Game {
        QString dir;
        QString title;
        QString entry;  ///< wine 要跑的 exe；SDL 后端为空
        QString note;   ///< 卡片副标题：数据包数量 / 体积
    };

    void detectBackend();
    void scanGames();
    void setMessage(const QString &text);
    /// 组装命令行：可执行文件 + 参数。失败时填 error。
    bool buildCommand(const Game &game, QString *program, QStringList *args, QString *error) const;
    void cleanup(const QString &note);

    Backend m_backend = Backend::None;
    QString m_program;
    QStringList m_args;
    QString m_detail;

    QVector<Game> m_games;
    QVariantList m_list;

    QProcess *m_process = nullptr;
    QString m_runningTitle;
    QString m_message;
    /// 「已结束」这类提示要几秒后自己消失，用序号避免误清掉新提示。
    int m_messageSeq = 0;
};
