#pragma once

#include <QAbstractListModel>
#include <QList>

/**
 * 对应 C# 的 IAppCatalogService（Windows 侧用 COM 枚举快捷方式，
 * Android 侧用 PackageManager）。
 *
 * Linux 侧的等价物是 XDG 桌面条目：/usr/share/applications 与
 * ~/.local/share/applications 下的 *.desktop。这里直接遍历目录读文件，
 * 自己处理 NoDisplay、Hidden、Type、本地化 Name（Name[zh_CN]）以及
 * 用户级覆盖（同名 .desktop 由靠前的目录胜出）。
 *
 * 为什么不用 KF6 的 KApplicationTrader：它依赖 ksycoca 缓存和
 * kbuildsycoca6 进程。在 kiosk/容器这类没有完整会话总线的环境里，
 * 首次查询可能卡住不返回（界面停在闪屏，看起来像死机），或者在缓存
 * 未生成时安静地返回空表（家长后台一个应用都没有）。直接读文件
 * 毫秒级完成、不依赖任何外部进程，代价只是少了 vendor 过滤等细节。
 *
 * 启动参数：Exec 里的字段代码（%f %U 等）在启动前统一剥掉，
 * 见 AppLauncher。这里保留原始 Exec 字符串，不预先改写，
 * 因为家长后台可能显示它用于排查。
 */
class AppCatalog : public QAbstractListModel
{
    Q_OBJECT

public:
    enum Roles {
        IdRole = Qt::UserRole + 1,
        NameRole,
        ExecRole,
        IconRole,
        CommentRole,
    };

    explicit AppCatalog(QObject *parent = nullptr);

    int rowCount(const QModelIndex &parent = QModelIndex()) const override;
    QVariant data(const QModelIndex &index, int role) const override;
    QHash<int, QByteArray> roleNames() const override;

    /// 重新扫描应用目录。家长后台打开时调用一次即可。
    Q_INVOKABLE void refresh();

    /// 取第 row 行，转成可直接存进配置的 map。
    Q_INVOKABLE QVariantMap get(int row) const;

private:
    struct Entry {
        QString id;
        QString name;
        QString exec;
        QString icon;
        QString comment;
    };

    /// 遍历 XDG 应用目录，逐个解析 .desktop。
    void scanDesktopFiles();

    QList<Entry> m_entries;
};
