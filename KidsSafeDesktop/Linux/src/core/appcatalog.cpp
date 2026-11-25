#include "appcatalog.h"

#include <algorithm>

#include <QDirIterator>
#include <QFileInfo>
#include <QLocale>
#include <QSet>
#include <QSettings>
#include <QStandardPaths>

namespace {

/// 取 .desktop 里的本地化字段：优先 Name[zh_CN]，其次 Name[zh]，最后 Name。
QString localizedValue(const QSettings &desktop, const QString &key)
{
    const QString full = QLocale::system().name();                 // 例如 zh_CN
    const QString brief = full.section(QLatin1Char('_'), 0, 0);    // 例如 zh

    const QStringList candidates{
        QStringLiteral("%1[%2]").arg(key, full),
        QStringLiteral("%1[%2]").arg(key, brief),
        key,
    };

    for (const QString &candidate : candidates) {
        const QString value =
            desktop.value(QStringLiteral("Desktop Entry/") + candidate).toString().trimmed();
        if (!value.isEmpty()) {
            return value;
        }
    }
    return {};
}

} // namespace

AppCatalog::AppCatalog(QObject *parent)
    : QAbstractListModel(parent)
{
}

int AppCatalog::rowCount(const QModelIndex &parent) const
{
    if (parent.isValid()) {
        return 0;
    }
    return static_cast<int>(m_entries.size());
}

QVariant AppCatalog::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= m_entries.size()) {
        return QVariant();
    }

    const Entry &entry = m_entries.at(index.row());
    switch (role) {
    case IdRole:
        return entry.id;
    case NameRole:
        return entry.name;
    case ExecRole:
        return entry.exec;
    case IconRole:
        return entry.icon;
    case CommentRole:
        return entry.comment;
    default:
        return QVariant();
    }
}

QHash<int, QByteArray> AppCatalog::roleNames() const
{
    return {
        {IdRole, "appId"},
        {NameRole, "name"},
        {ExecRole, "exec"},
        {IconRole, "icon"},
        {CommentRole, "comment"},
    };
}

QVariantMap AppCatalog::get(int row) const
{
    if (row < 0 || row >= m_entries.size()) {
        return {};
    }

    const Entry &entry = m_entries.at(row);

    // 磁贴底色按 id 稳定分配。不用 qHash()：Qt 6 的哈希带随机种子，
    // 同一个应用每次启动会换一种颜色，孩子会以为「我的游戏不见了」。
    // 这里自己算一个与运行环境无关的简单校验和。
    static const QStringList palette{
        QStringLiteral("#7E57C2"), QStringLiteral("#26A69A"),
        QStringLiteral("#EF6C00"), QStringLiteral("#3949AB"),
        QStringLiteral("#66BB6A"), QStringLiteral("#EC407A"),
        QStringLiteral("#42A5F5"), QStringLiteral("#8D6E63"),
    };
    int seed = 0;
    for (const QChar &ch : entry.id) {
        seed = (seed * 31 + ch.unicode()) % 100003;
    }

    return {
        {QStringLiteral("id"), entry.id},
        {QStringLiteral("name"), entry.name},
        {QStringLiteral("exec"), entry.exec},
        {QStringLiteral("icon"), entry.icon},
        {QStringLiteral("accent"), palette.at(seed % palette.size())},
    };
}

void AppCatalog::scanDesktopFiles()
{
    const QStringList dirs =
        QStandardPaths::standardLocations(QStandardPaths::ApplicationsLocation);

    QSet<QString> seen;

    for (const QString &dir : dirs) {
        if (!QFileInfo::exists(dir)) {
            continue;
        }

        QDirIterator it(dir,
                        {QStringLiteral("*.desktop")},
                        QDir::Files | QDir::Readable,
                        QDirIterator::Subdirectories);
        while (it.hasNext()) {
            const QString path = it.next();

            QSettings desktop(path, QSettings::IniFormat);
            if (desktop.status() != QSettings::NoError) {
                continue;
            }

            // 只认应用条目：链接（Type=Link）与目录项都不进白名单候选。
            // Type 缺省时按 Application 处理，个别老条目不写这一行。
            const QString type = desktop.value(QStringLiteral("Desktop Entry/Type")).toString();
            if (!type.isEmpty() && type != QLatin1String("Application")) {
                continue;
            }
            if (desktop.value(QStringLiteral("Desktop Entry/NoDisplay"), false).toBool()
                || desktop.value(QStringLiteral("Desktop Entry/Hidden"), false).toBool()) {
                continue;
            }

            const QString exec =
                desktop.value(QStringLiteral("Desktop Entry/Exec")).toString().trimmed();
            if (exec.isEmpty()) {
                continue;
            }

            // 同名 .desktop 由前面的目录胜出（用户目录排在系统目录之前），
            // 与 KService 的覆盖规则一致。
            const QString fileName = QFileInfo(path).completeBaseName();
            if (fileName.isEmpty() || seen.contains(fileName)) {
                continue;
            }

            const QString name = localizedValue(desktop, QStringLiteral("Name"));
            if (name.isEmpty()) {
                continue;
            }
            seen.insert(fileName);

            Entry entry;
            entry.id = fileName;
            entry.name = name;
            entry.exec = exec;
            entry.icon = desktop.value(QStringLiteral("Desktop Entry/Icon")).toString();
            entry.comment = localizedValue(desktop, QStringLiteral("Comment"));
            m_entries.append(entry);
        }
    }
}

void AppCatalog::refresh()
{
    beginResetModel();
    m_entries.clear();

    scanDesktopFiles();

    std::sort(m_entries.begin(), m_entries.end(), [](const Entry &a, const Entry &b) {
        return QString::localeAwareCompare(a.name, b.name) < 0;
    });

    endResetModel();
}
