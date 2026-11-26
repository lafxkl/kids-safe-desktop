#pragma once

#include <QAbstractListModel>
#include <QHash>
#include <QString>
#include <QTimer>
#include <QVariantMap>

/**
 * 任务管理器用的进程列表。
 *
 * 数据源是 /proc —— Linux 上没有任何「标准接口」能列出进程，ps / top 自己也是
 * 读这个目录。这里直接读，不 fork 子进程：任务管理器每 2 秒刷一次，如果每次都
 * 起一个 ps，孩子开着一整天就是几万次进程创建，还会在低配机器上把界面拖卡。
 *
 * 三件事值得单独说明：
 *
 * 1. **CPU 占比要两次采样**。/proc/<pid>/stat 给的是「开机以来累计了多少个
 *    时钟滴答」，单看这个数只能算出平均占用。真实做法是隔一段时间再读一次，
 *    用两次的差值除以这段时间的总滴答数。所以第一次刷新所有进程都显示 0%，
 *    第二次（2 秒后）才有数 —— 界面上第一次打开会看到一行 0，这是正常的。
 *
 * 2. **结束进程分两步**。先 SIGTERM（相当于「请你自己退出」），进程有机会存盘；
 *    孩子再点一次才发 SIGKILL（强杀）。直接强杀会把正在写的文件留成半截，
 *    这在儿童机上尤其糟糕 —— 他可能正在写作业。
 *
 * 3. **有一份保护名单**。pid 1、桌面自己、会话脚本、Xorg / Xwayland 一律不动，
 *    界面上那一行连按钮都不给。孩子想「看看会发生什么」是本能，一个点错就黑屏
 *    的成本太高；真需要停掉桌面的场景，家长有「退出儿童桌面」的正规入口。
 *
 * 权限：普通用户只能给自己名下的进程发信号。杀 root 的进程会拿到 EPERM，
 * 这里把 errno 翻译成人话（「这个进程是系统管理的，需要有管理员权限」），
 * 而不是把 "Operation not permitted" 甩给孩子看。
 */
class ProcModel : public QAbstractListModel
{
    Q_OBJECT

    /// 列表里显示的行数（已经过关键字过滤）。界面用它决定是否显示「没有匹配」。
    Q_PROPERTY(int count READ count NOTIFY countChanged)

    /// 关键字过滤：匹配进程名或命令行，不区分大小写。
    Q_PROPERTY(QString filter READ filter WRITE setFilter NOTIFY filterChanged)

    /// 页面可见时才刷新。隐藏后停掉定时器，别在后台空转。
    Q_PROPERTY(bool running READ running WRITE setRunning NOTIFY runningChanged)

    // ---- 顶部概览用的几个数 ----
    Q_PROPERTY(double cpuPercent READ cpuPercent NOTIFY statsChanged)
    Q_PROPERTY(double memPercent READ memPercent NOTIFY statsChanged)
    Q_PROPERTY(qint64 memUsedKb READ memUsedKb NOTIFY statsChanged)
    Q_PROPERTY(qint64 memTotalKb READ memTotalKb NOTIFY statsChanged)
    Q_PROPERTY(int procTotal READ procTotal NOTIFY statsChanged)
    Q_PROPERTY(QString uptimeText READ uptimeText NOTIFY statsChanged)
    Q_PROPERTY(QString loadText READ loadText NOTIFY statsChanged)

public:
    enum Roles {
        PidRole = Qt::UserRole + 1,
        NameRole,
        CmdRole,
        UserRole,
        CpuRole,
        MemRole,
        MemKbRole,
        StateRole,
        GuardedRole,   ///< 受保护，界面上不给「结束」
        SelfRole,      ///< 是不是这个桌面自己
    };

    explicit ProcModel(QObject *parent = nullptr);

    int rowCount(const QModelIndex &parent = QModelIndex()) const override;
    QVariant data(const QModelIndex &index, int role) const override;
    QHash<int, QByteArray> roleNames() const override;

    int count() const { return int(m_rows.size()); }
    QString filter() const { return m_filter; }
    void setFilter(const QString &value);
    bool running() const { return m_timer.isActive(); }
    void setRunning(bool value);

    double cpuPercent() const { return m_cpuPercent; }
    double memPercent() const { return m_memPercent; }
    qint64 memUsedKb() const { return m_memUsedKb; }
    qint64 memTotalKb() const { return m_memTotalKb; }
    int procTotal() const { return m_procTotal; }
    QString uptimeText() const { return m_uptimeText; }
    QString loadText() const { return m_loadText; }

    /// 重新读一遍 /proc 并发出刷新信号。界面打开时调一次，之后由定时器驱动。
    Q_INVOKABLE void refresh();

    /**
     * 结束某个进程。返回空串表示信号发出去了，否则是给孩子看的一句话。
     *
     * force = false 发 SIGTERM（先礼后兵），true 发 SIGKILL。
     */
    Q_INVOKABLE QString terminate(int pid, bool force = false);

    /// 按 pid 取一行，用于「点开详情」。找不到返回空 map。
    Q_INVOKABLE QVariantMap at(int pid) const;

    /// 这一行能不能结束（给 QML 做按钮灰显，不用自己拼名单）。
    Q_INVOKABLE bool canTerminate(int pid) const;

signals:
    void countChanged();
    void filterChanged();
    void runningChanged();
    void statsChanged();
    /// 刷新完成（界面可以收起「正在刷新」的转圈）。
    void updated();

private:
    struct Row {
        int pid = 0;
        QString name;
        QString cmd;
        QString user;
        double cpu = 0.0;
        double mem = 0.0;
        qint64 memKb = 0;
        QChar state;
        bool guarded = false;
        bool self = false;

        // 上次采样值，算差值用
        quint64 ticks = 0;
        qint64 lastSeen = 0;   ///< 上次出现的毫秒时间戳（判断进程是新起的）
    };

    void scan();
    void readSystem();
    static QString userName(uint uid);
    static bool isGuardedName(const QString &name);

    QList<Row> m_rows;          ///< 已过滤、已排序，行号直接对应界面
    QHash<int, Row> m_cache;    ///< pid → 上一次的采样，用来做差

    QString m_filter;
    QTimer m_timer;

    double m_cpuPercent = 0.0;
    double m_memPercent = 0.0;
    qint64 m_memUsedKb = 0;
    qint64 m_memTotalKb = 0;
    int m_procTotal = 0;
    QString m_uptimeText;
    QString m_loadText;

    quint64 m_lastTotalTicks = 0;
    quint64 m_lastIdleTicks = 0;
    quint64 m_totalTicksDelta = 0;  ///< 本轮与上轮之间全部核的总滴答差
    int m_cpuCount = 1;
};
