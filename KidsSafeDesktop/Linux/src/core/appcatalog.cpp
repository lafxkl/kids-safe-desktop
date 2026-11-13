#include "appcatalog.h"

#include <algorithm>

#include <KApplicationTrader>
#include <KService>

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
    return {
        {QStringLiteral("id"), entry.id},
        {QStringLiteral("name"), entry.name},
        {QStringLiteral("exec"), entry.exec},
        {QStringLiteral("icon"), entry.icon},
        {QStringLiteral("accent"), QStringLiteral("#4C8BF5")},
    };
}

void AppCatalog::refresh()
{
    beginResetModel();
    m_entries.clear();

    const KService::List services = KApplicationTrader::query(
        [](const KService::Ptr &service) {
            if (service->noDisplay()) {
                return false;
            }
            if (service->exec().trimmed().isEmpty()) {
                return false;
            }
            return true;
        });

    m_entries.reserve(services.size());
    for (const KService::Ptr &service : services) {
        Entry entry;
        // storageId 形如 "org.kde.kate.desktop"，去掉后缀做稳定标识：
        // 家长后台的白名单是按这个 id 存的，绝不能因为显示名变化而失效。
        entry.id = service->storageId();
        if (entry.id.endsWith(QLatin1String(".desktop"))) {
            entry.id.chop(8);
        }
        entry.name = service->name();
        entry.exec = service->exec();
        entry.icon = service->icon();
        entry.comment = service->comment();
        m_entries.append(entry);
    }

    std::sort(m_entries.begin(), m_entries.end(), [](const Entry &a, const Entry &b) {
        return QString::localeAwareCompare(a.name, b.name) < 0;
    });

    endResetModel();
}
