#pragma once

#include <QAbstractListModel>
#include <QFileSystemWatcher>
#include <QString>
#include <QStringList>
#include <QVariantList>

class QFileInfo;

/**
 * 文件管理器的目录列表。
 *
 * 一个模型只表示「当前这一个目录里的东西」，不是整棵树 —— 孩子的浏览器里
 * 不需要左侧目录树，他只要「进去、返回、再进去」。真要做树，就得处理展开状态、
 * 懒加载、跨目录刷新，成本翻几倍，收益是给大人用的。
 *
 * 几处刻意的设计：
 *
 * 1. **删除进回收站，不是 rm**。删错了能捞回来，这在儿童机上比省事重要得多。
 *    回收站按 freedesktop 规范写在 ~/.local/share/Trash（files/ 放文件本体，
 *    info/ 放 .trashinfo 记原始路径），所以文件管理器里删掉的东西，在系统的
 *    回收站里也能看到、也能还原 —— 不是我们自己造的一个私有垃圾桶。
 *
 * 2. **系统目录只读**。/usr /etc /boot 这些地方删东西的后果是「电脑打不开了」，
 *    孩子点错一次就够。进入可以，删除/重命名/新建一律拒绝并给一句解释。
 *
 * 3. **QFileSystemWatcher 只盯当前目录**。盯整棵树会耗尽 inotify 配额
 *    （默认 8192 个，家目录大一点就爆），而且我们只需要知道「现在这层变了没」。
 */
class FileModel : public QAbstractListModel
{
    Q_OBJECT

    Q_PROPERTY(QString path READ path NOTIFY pathChanged)
    /// 面包屑用的分段：/home/kid/Documents → ["/", "home", "kid", "Documents"]
    Q_PROPERTY(QVariantList crumbs READ crumbs NOTIFY pathChanged)
    Q_PROPERTY(bool canGoUp READ canGoUp NOTIFY pathChanged)
    Q_PROPERTY(bool writable READ writable NOTIFY pathChanged)
    Q_PROPERTY(int count READ count NOTIFY countChanged)
    Q_PROPERTY(bool showHidden READ showHidden WRITE setShowHidden NOTIFY showHiddenChanged)
    /// 名字过滤（搜索框）。空串表示不过滤，匹配不区分大小写。
    Q_PROPERTY(QString filter READ filter WRITE setFilter NOTIFY filterChanged)
    /// 「还剩多少空间」那句小字，取不到就空串
    Q_PROPERTY(QString diskText READ diskText NOTIFY pathChanged)
    /// 常用的几个位置（主页、桌面、文档、图片、下载、根目录）
    Q_PROPERTY(QVariantList places READ places CONSTANT)

public:
    enum Roles {
        NameRole = Qt::UserRole + 1,
        PathRole,
        IsDirRole,
        SizeRole,      ///< 格式化后的「1.2 MB」，目录是空串
        SizeKbRole,    ///< 原始大小，排序用
        MtimeRole,     ///< 格式化后的时间
        KindRole,      ///< dir / text / image / audio / video / archive / other
        HiddenRole,
    };

    explicit FileModel(QObject *parent = nullptr);

    int rowCount(const QModelIndex &parent = QModelIndex()) const override;
    QVariant data(const QModelIndex &index, int role) const override;
    QHash<int, QByteArray> roleNames() const override;

    QString path() const { return m_path; }
    QVariantList crumbs() const;
    bool canGoUp() const;
    bool writable() const { return m_writable; }
    int count() const { return int(m_rows.size()); }
    bool showHidden() const { return m_showHidden; }
    void setShowHidden(bool value);
    QString filter() const { return m_filter; }
    void setFilter(const QString &value);
    QString diskText() const { return m_diskText; }
    QVariantList places() const { return m_places; }

    /**
     * 跳到某个目录。成功返回空串，失败返回一句给孩子看的话。
     * 支持 ~ 开头、相对路径（相对当前目录）。
     */
    Q_INVOKABLE QString navigate(const QString &target);

    Q_INVOKABLE QString goUp();

    /// 重新读当前目录（新建/删除后调）。
    Q_INVOKABLE void refresh();

    /**
     * 「打开」第 row 行。
     *
     * 返回动作名，界面按它决定下一步：
     *   "dir"    —— 已经进去了，界面不用做事
     *   "text"   —— 内置文本预览，界面调 preview() 取内容
     *   "image"  —— 内置图片预览，界面直接用 file:// 加载
     *   "launch" —— 交给系统默认程序，调用成功
     *   "denied" —— 不认识的文件类型，或者系统里没有能打开它的程序
     */
    Q_INVOKABLE QString open(int row);

    /// 读文本文件的前 maxBytes 字节，给内置预览用。
    Q_INVOKABLE QString preview(int row, int maxBytes = 200000);

    Q_INVOKABLE QString makeFolder(const QString &name);
    Q_INVOKABLE QString renameItem(int row, const QString &newName);
    Q_INVOKABLE QString removeItem(int row);

    /// 这一行能不能删/改名（系统目录、只读目录里的东西不行）。
    Q_INVOKABLE bool canModify(int row) const;

    /// 当前目录本身能不能新建文件（和 canModify 是两件事）。
    Q_INVOKABLE QString guardReason() const;

    /**
     * 按行号取一行（name/path/isDir/size/kind/mtime）。
     *
     * QAbstractListModel 没有 ListModel 那种 get()，而界面在「删除确认」
     * 和「预览标题」里都要拿行数据 —— 那两个场景手里只有行号。越界返回空 map。
     */
    Q_INVOKABLE QVariantMap at(int row) const;

signals:
    void pathChanged();
    void countChanged();
    void showHiddenChanged();
    void filterChanged();
    /// 操作失败/成功的一句话，界面用一条小提示显示
    void notice(const QString &text);

private:
    struct Row {
        QString name;
        QString path;
        bool isDir = false;
        qint64 size = 0;
        qint64 mtime = 0;
        QString kind;
        bool hidden = false;
    };

    void reload();
    void watchCurrent();
    static QString kindOf(const QFileInfo &info);
    static QString humanSize(qint64 bytes);
    static bool isProtected(const QString &absolutePath);
    QString uniqueTrashName(const QString &baseName);

    QString m_path;
    QList<Row> m_rows;
    bool m_showHidden = false;
    QString m_filter;
    bool m_writable = false;
    QString m_diskText;
    QVariantList m_places;
    QFileSystemWatcher m_watcher;
};
