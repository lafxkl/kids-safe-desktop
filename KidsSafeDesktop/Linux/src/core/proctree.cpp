#include "core/proctree.h"

#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QTextStream>

#include <algorithm>
#include <cerrno>
#include <cstring>

#include <signal.h>
#include <unistd.h>

namespace {

/// 读一个文件的全部内容。读不到（进程在这一瞬间退出了）返回空。
QString slurp(const QString &path)
{
    QFile file(path);
    if (!file.open(QIODevice::ReadOnly)) {
        return QString();
    }
    return QString::fromLocal8Bit(file.readAll());
}

/// /proc/uptime 第二列是空闲时间，第一列是开机秒数。
qint64 uptimeSeconds()
{
    const QString text = slurp(QStringLiteral("/proc/uptime"));
    if (text.isEmpty()) {
        return 0;
    }
    return qint64(text.section(QLatin1Char(' '), 0, 0).toDouble());
}

/*
 * 进程状态字母转成孩子看得懂的话。
 *
 * 不显示字母是有原因的：家长问「这个 R 是什么」还说得过去，孩子看到一排
 * R / S / D 只会以为软件坏了。这里只保留四种他真会遇到的。
 */
QString stateText(QChar state)
{
    switch (state.toLatin1()) {
    case 'R': return QStringLiteral("运行中");
    case 'S': return QStringLiteral("等待中");
    case 'D': return QStringLiteral("等磁盘");
    case 'T': return QStringLiteral("已暂停");
    case 'Z': return QStringLiteral("已结束");
    case 'I': return QStringLiteral("空闲");
    default:  return QStringLiteral("未知");
    }
}

}  // namespace

ProcModel::ProcModel(QObject *parent)
    : QAbstractListModel(parent)
{
    m_cpuCount = std::max(1, int(sysconf(_SC_NPROCESSORS_ONLN)));

    // 2 秒一次：比 top 的 3 秒密一点，孩子点「结束」之后能立刻看到那一行消失；
    // 又不至于密到让低配机器一直在读 /proc。
    m_timer.setInterval(2000);
    m_timer.setSingleShot(false);
    connect(&m_timer, &QTimer::timeout, this, &ProcModel::refresh);
}

int ProcModel::rowCount(const QModelIndex &parent) const
{
    return parent.isValid() ? 0 : int(m_rows.size());
}

QVariant ProcModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= m_rows.size()) {
        return QVariant();
    }
    const Row &row = m_rows.at(index.row());
    switch (role) {
    case PidRole:     return row.pid;
    case NameRole:    return row.name;
    case CmdRole:     return row.cmd;
    case UserRole:    return row.user;
    case CpuRole:     return row.cpu;
    case MemRole:     return row.mem;
    case MemKbRole:   return row.memKb;
    case StateRole:   return stateText(row.state);
    case GuardedRole: return row.guarded;
    case SelfRole:    return row.self;
    default:          return QVariant();
    }
}

QHash<int, QByteArray> ProcModel::roleNames() const
{
    return {
        { PidRole,     "pid" },
        { NameRole,    "name" },
        { CmdRole,     "cmd" },
        { UserRole,    "user" },
        { CpuRole,     "cpu" },
        { MemRole,     "mem" },
        { MemKbRole,   "memKb" },
        { StateRole,   "stateText" },
        { GuardedRole, "guarded" },
        { SelfRole,    "isSelf" },
    };
}

void ProcModel::setFilter(const QString &value)
{
    if (m_filter == value) {
        return;
    }
    m_filter = value;
    Q_EMIT filterChanged();
    // 只重新过滤已有数据，不再读一遍 /proc —— 打字的时候每敲一个字母读一次太浪费。
    refresh();
}

void ProcModel::setRunning(bool value)
{
    if (value == m_timer.isActive()) {
        return;
    }
    if (value) {
        m_timer.start();
        refresh();      // 立刻给一帧，别让孩子盯着空表等 2 秒
    } else {
        m_timer.stop();
    }
    Q_EMIT runningChanged();
}

QString ProcModel::userName(uint uid)
{
    // 不用 getpwuid()：它返回静态缓冲区，线程里不安全，而这里每 2 秒要查几百次。
    // 直接读 /etc/passwd 意义也不大，进程的 uid 翻来覆去就那几个，
    // 用一个静态表缓存住就够了。
    static QHash<uint, QString> cache;
    const auto hit = cache.constFind(uid);
    if (hit != cache.constEnd()) {
        return hit.value();
    }
    QString name = QStringLiteral("用户%1").arg(uid);
    if (uid == 0) {
        name = QStringLiteral("系统");
    }
    cache.insert(uid, name);
    return name;
}

bool ProcModel::isGuardedName(const QString &name)
{
    /*
     * 这些进程一停，桌面就没了或者会黑屏。名字是硬编码的，因为判断依据只能是
     * 名字 —— 它们没有统一的父进程，也没有 cgroup 标记能可靠区分。
     */
    static const QStringList blocked = {
        QStringLiteral("systemd"),
        QStringLiteral("init"),
        QStringLiteral("Xorg"),
        QStringLiteral("Xwayland"),
        QStringLiteral("kwin_x11"),
        QStringLiteral("kwin_wayland"),
        QStringLiteral("plasmashell"),
        QStringLiteral("dbus-daemon"),
        QStringLiteral("dbus-broker"),
        QStringLiteral("systemd-logind"),
        QStringLiteral("systemd-journal"),
        QStringLiteral("sddm"),
        QStringLiteral("lightdm"),
        QStringLiteral("gdm"),
        QStringLiteral("gdm3"),
        QStringLiteral("kidssafe"),
        QStringLiteral("kidssafe-session"),
        QStringLiteral("kidssafe-wayland-session"),
    };
    return blocked.contains(name);
}

void ProcModel::readSystem()
{
    /*
     * /proc/stat 的第一行是所有核加起来的累计滴答：
     *   cpu  user nice system idle iowait irq softirq steal guest guest_nice
     * 减去上一次的值就是这段时间里 CPU 到底忙了多少。
     */
    const QString stat = slurp(QStringLiteral("/proc/stat"));
    if (!stat.isEmpty()) {
        const QString first = stat.section(QLatin1Char('\n'), 0, 0);
        const QStringList parts = first.split(QLatin1Char(' '), Qt::SkipEmptyParts);
        quint64 total = 0;
        for (int i = 1; i < parts.size() && i <= 8; ++i) {
            total += parts.at(i).toULongLong();
        }
        // iowait（第 5 个数）算空闲更贴近体感：等磁盘时 CPU 确实没在算，
        // 但机器又把活干得很慢。gnome-system-monitor 也是这么处理的。
        quint64 idle = parts.size() > 4 ? parts.at(4).toULongLong() : 0;
        if (parts.size() > 5) {
            idle += parts.at(5).toULongLong();
        }

        if (m_lastTotalTicks > 0 && total > m_lastTotalTicks) {
            const quint64 dTotal = total - m_lastTotalTicks;
            const quint64 dIdle = idle >= m_lastIdleTicks ? idle - m_lastIdleTicks : 0;
            m_totalTicksDelta = dTotal;   // 留给 scan() 当每个进程的分母
            m_cpuPercent = 100.0 * double(dTotal - std::min(dIdle, dTotal)) / double(dTotal);
        }
        m_lastTotalTicks = total;
        m_lastIdleTicks = idle;
    }

    /*
     * 内存用 MemAvailable 而不是 MemFree。
     *
     * Linux 会把空闲内存拿去做页缓存（buff/cache），MemFree 常年只有几百 MB，
     * 看着像「内存快满了」，其实随时可以回收。MemAvailable 才是「还能给新程序
     * 用多少」，3.14 起有了；老内核没有这一行，退回 MemFree + Buffers + Cached。
     */
    const QString meminfo = slurp(QStringLiteral("/proc/meminfo"));
    if (!meminfo.isEmpty()) {
        qint64 total = 0;
        qint64 available = -1;
        qint64 freeKb = 0;
        qint64 buffers = 0;
        qint64 cached = 0;

        const QStringList lines = meminfo.split(QLatin1Char('\n'), Qt::SkipEmptyParts);
        for (const QString &line : lines) {
            const int colon = line.indexOf(QLatin1Char(':'));
            if (colon < 0) {
                continue;
            }
            const QString key = line.left(colon);
            const qint64 value = line.mid(colon + 1).trimmed().section(QLatin1Char(' '), 0, 0).toLongLong();
            if (key == QLatin1String("MemTotal")) {
                total = value;
            } else if (key == QLatin1String("MemAvailable")) {
                available = value;
            } else if (key == QLatin1String("MemFree")) {
                freeKb = value;
            } else if (key == QLatin1String("Buffers")) {
                buffers = value;
            } else if (key == QLatin1String("Cached")) {
                cached = value;
            }
        }
        if (available < 0) {
            available = freeKb + buffers + cached;
        }
        m_memTotalKb = total;
        m_memUsedKb = std::max<qint64>(0, total - available);
        m_memPercent = total > 0 ? 100.0 * double(m_memUsedKb) / double(total) : 0.0;
    }

    const qint64 up = uptimeSeconds();
    if (up > 0) {
        const qint64 hours = up / 3600;
        const qint64 minutes = (up % 3600) / 60;
        m_uptimeText = hours > 0
                           ? QStringLiteral("%1 小时 %2 分钟").arg(hours).arg(minutes)
                           : QStringLiteral("%1 分钟").arg(minutes);
    }

    const QString loadavg = slurp(QStringLiteral("/proc/loadavg"));
    if (!loadavg.isEmpty()) {
        const QStringList parts = loadavg.split(QLatin1Char(' '), Qt::SkipEmptyParts);
        if (parts.size() >= 3) {
            m_loadText = QStringLiteral("%1 / %2 / %3")
                             .arg(parts.at(0), parts.at(1), parts.at(2));
        }
    }
}

void ProcModel::scan()
{
    QDir proc(QStringLiteral("/proc"));
    const QStringList entries = proc.entryList(QDir::Dirs | QDir::NoDotAndDotDot, QDir::Name);

    const int selfPid = int(QCoreApplication::applicationPid());
    const int parentPid = int(getppid());

    QHash<int, Row> next;
    next.reserve(entries.size());

    int total = 0;

    for (const QString &entry : entries) {
        bool ok = false;
        const int pid = entry.toInt(&ok);
        if (!ok || pid <= 0) {
            continue;
        }

        const QString base = QStringLiteral("/proc/") + entry;

        // ---- stat：状态、累积 CPU 滴答、常驻内存页数 ----
        const QString stat = slurp(base + QStringLiteral("/stat"));
        if (stat.isEmpty()) {
            continue;   // 这一瞬间进程没了
        }

        /*
         * comm 字段是 (bash) 这样包起来的，而且里允许出现空格和右括号
         * （比如 (Web Content)），所以必须按**最后一个** ')' 切，不能 split(' ')。
         */
        const int open = stat.indexOf(QLatin1Char('('));
        const int close = stat.lastIndexOf(QLatin1Char(')'));
        if (open < 0 || close <= open) {
            continue;
        }
        Row row;
        row.pid = pid;
        row.name = stat.mid(open + 1, close - open - 1);

        const QStringList rest = stat.mid(close + 1).split(QLatin1Char(' '), Qt::SkipEmptyParts);
        // rest[0] 是 state，rest[11] 是 utime，rest[12] 是 stime，rest[21] 是 rss
        if (rest.size() < 22) {
            continue;
        }
        row.state = rest.at(0).at(0);
        const quint64 ticks = rest.at(11).toULongLong() + rest.at(12).toULongLong();
        const qint64 rssPages = rest.at(21).toLongLong();
        row.memKb = rssPages > 0 ? rssPages * (sysconf(_SC_PAGESIZE) / 1024) : 0;

        // ---- 用户名：直接看 /proc/<pid> 目录的属主，省掉再读一次 status ----
        row.user = userName(uint(QFileInfo(base).ownerId()));

        // ---- 命令行：读不到（内核线程）就用进程名顶上 ----
        const QString cmdline = slurp(base + QStringLiteral("/cmdline"));
        if (cmdline.isEmpty()) {
            row.cmd = row.name;
        } else {
            row.cmd = cmdline;
            row.cmd.replace(QChar(0), QLatin1Char(' '));
            row.cmd = row.cmd.trimmed();
            if (row.cmd.size() > 180) {
                row.cmd = row.cmd.left(177) + QStringLiteral("…");
            }
        }

        // ---- 和上一次的采样做差，得到这段时间的 CPU 占用 ----
        const auto prev = m_cache.constFind(pid);
        if (prev != m_cache.constEnd() && ticks >= prev->ticks) {
            const quint64 delta = ticks - prev->ticks;
            const quint64 dTotal = m_totalTicksDelta > 0 ? m_totalTicksDelta : 0;
            if (dTotal > 0 && delta > 0) {
                // 乘核数是按 top 的口径：一个线程吃满一个核显示 100%。
                row.cpu = 100.0 * double(delta) / double(dTotal) * m_cpuCount;
            }
        }
        row.ticks = ticks;

        row.guarded = (pid == 1) || (pid == selfPid) || (pid == parentPid)
                      || isGuardedName(row.name) || cmdline.isEmpty();
        row.self = (pid == selfPid);

        ++total;
        next.insert(pid, row);
    }

    // 下一次算差值要用这一轮的总滴答增量（由 refresh() 里的 readSystem 算好）
    m_cache = next;

    // ---- 过滤 ----
    QList<Row> visible;
    visible.reserve(m_cache.size());
    const QString needle = m_filter.trimmed();
    for (auto it = m_cache.constBegin(); it != m_cache.constEnd(); ++it) {
        const Row &row = it.value();
        if (!needle.isEmpty()
            && !row.name.contains(needle, Qt::CaseInsensitive)
            && !row.cmd.contains(needle, Qt::CaseInsensitive)) {
            continue;
        }
        visible.append(row);
    }

    // ---- 排序：占 CPU 多的排前面，其次看内存，最后按 pid 保证顺序稳定 ----
    std::sort(visible.begin(), visible.end(), [](const Row &a, const Row &b) {
        if (qAbs(a.cpu - b.cpu) > 0.05) {
            return a.cpu > b.cpu;
        }
        if (a.memKb != b.memKb) {
            return a.memKb > b.memKb;
        }
        return a.pid < b.pid;
    });

    beginResetModel();
    m_rows = visible;
    m_procTotal = total;
    endResetModel();
}

void ProcModel::refresh()
{
    /*
     * 顺序不能反：先读 /proc/stat 拿到「这一轮总共走了多少滴答」，
     * 再逐个进程算差值 —— 每个进程都要拿它当分母。
     * 两个函数各读一次 /proc/stat 就重复了，所以这里只读一遍。
     */
    readSystem();
    scan();

    Q_EMIT countChanged();
    Q_EMIT statsChanged();
    Q_EMIT updated();
}

QVariantMap ProcModel::at(int pid) const
{
    const auto it = m_cache.constFind(pid);
    if (it == m_cache.constEnd()) {
        return {};
    }
    const Row &row = it.value();
    return {
        { QStringLiteral("pid"), row.pid },
        { QStringLiteral("name"), row.name },
        { QStringLiteral("cmd"), row.cmd },
        { QStringLiteral("user"), row.user },
        { QStringLiteral("cpu"), row.cpu },
        { QStringLiteral("mem"), row.mem },
        { QStringLiteral("memKb"), row.memKb },
        { QStringLiteral("state"), stateText(row.state) },
        { QStringLiteral("guarded"), row.guarded },
    };
}

bool ProcModel::canTerminate(int pid) const
{
    const auto it = m_cache.constFind(pid);
    if (it == m_cache.constEnd()) {
        return false;
    }
    return !it.value().guarded;
}

QString ProcModel::terminate(int pid, bool force)
{
    const auto it = m_cache.constFind(pid);
    if (it == m_cache.constEnd()) {
        return QStringLiteral("这个程序已经不在了");
    }
    const Row row = it.value();

    if (row.guarded) {
        if (row.pid == int(QCoreApplication::applicationPid())) {
            return QStringLiteral("这是儿童桌面自己，关掉就没法用了");
        }
        if (row.pid == 1) {
            return QStringLiteral("这是系统总管，停掉整台电脑都会关");
        }
        return QStringLiteral("这个是系统要用的，不能关");
    }

    const int sig = force ? SIGKILL : SIGTERM;
    if (::kill(pid, sig) != 0) {
        const int code = errno;
        if (code == EPERM || code == EACCES) {
            return QStringLiteral("这个程序是系统管理的，需要有管理员权限才能结束");
        }
        if (code == ESRCH) {
            return QStringLiteral("这个程序刚刚已经自己结束了");
        }
        return QStringLiteral("结束失败：%1").arg(QString::fromLocal8Bit(std::strerror(code)));
    }

    /*
     * 不在这里删行、也不在这里报「已结束」。
     *
     * SIGTERM 只是「请求退出」，程序可能还要几秒钟存盘。真正退没退，2 秒后的
     * 下一轮刷新会告诉我们。提前说「已结束」而它还在，孩子会以为按钮坏了。
     */
    return QString();
}
