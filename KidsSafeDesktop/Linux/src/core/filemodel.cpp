#include "core/filemodel.h"

#include <QDateTime>
#include <QDesktopServices>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QStandardPaths>
#include <QStorageInfo>
#include <QTimer>
#include <QUrl>
#include <QVariantMap>

#include <algorithm>

namespace {

/// 只读区域：能进去看，但不能删、不能改名、不能新建。
const QStringList &protectedRoots()
{
    static const QStringList roots = {
        QStringLiteral("/usr"),  QStringLiteral("/etc"),   QStringLiteral("/bin"),
        QStringLiteral("/sbin"), QStringLiteral("/lib"),   QStringLiteral("/lib32"),
        QStringLiteral("/lib64"), QStringLiteral("/libx32"),
        QStringLiteral("/boot"), QStringLiteral("/var"),   QStringLiteral("/opt"),
        QStringLiteral("/sys"),  QStringLiteral("/proc"),  QStringLiteral("/dev"),
        QStringLiteral("/run"),  QStringLiteral("/srv"),   QStringLiteral("/root"),
        QStringLiteral("/snap"),
    };
    return roots;
}

QString trashRoot()
{
    // 规范位置是 ~/.local/share/Trash，QStandardPaths 在 Linux 上正好给这个。
    const QString base = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation);
    return base + QStringLiteral("/Trash");
}

}  // namespace

FileModel::FileModel(QObject *parent)
    : QAbstractListModel(parent)
{
    // 常用的几个位置。用 QStandardPaths 而不是拼 ~/Desktop：中文系统里
    // 桌面目录真的叫「桌面」，硬编码英文名在那些机器上会得到一个不存在的路径。
    auto add = [this](const QString &name, const QString &path, const QString &glyph) {
        if (path.isEmpty() || !QFileInfo(path).isDir()) {
            return;
        }
        m_places.append(QVariantMap{
            { QStringLiteral("name"), name },
            { QStringLiteral("path"), path },
            { QStringLiteral("glyph"), glyph },
        });
    };

    add(QStringLiteral("我的东西"), QStandardPaths::writableLocation(QStandardPaths::HomeLocation),
        QStringLiteral("home"));
    add(QStringLiteral("桌面"), QStandardPaths::writableLocation(QStandardPaths::DesktopLocation),
        QStringLiteral("desktop"));
    add(QStringLiteral("文档"), QStandardPaths::writableLocation(QStandardPaths::DocumentsLocation),
        QStringLiteral("docs"));
    add(QStringLiteral("图片"), QStandardPaths::writableLocation(QStandardPaths::PicturesLocation),
        QStringLiteral("pictures"));
    add(QStringLiteral("下载"), QStandardPaths::writableLocation(QStandardPaths::DownloadLocation),
        QStringLiteral("download"));
    add(QStringLiteral("整个电脑"), QStringLiteral("/"), QStringLiteral("root"));

    /*
     * 目录一变就重读，但要合并。
     *
     * cp 一批文件进来会连续触发好几次 directoryChanged，每次都同步重扫目录，
     * 大批量拷贝时界面会一顿一顿的。攒 60ms 再读一次，人眼就只看得到「刷新了一下」。
     */
    connect(&m_watcher, &QFileSystemWatcher::directoryChanged, this, [this](const QString &) {
        QTimer::singleShot(60, this, &FileModel::reload);
    });

    m_path = QStandardPaths::writableLocation(QStandardPaths::HomeLocation);
    if (m_path.isEmpty() || !QFileInfo(m_path).isDir()) {
        m_path = QStringLiteral("/");
    }
    reload();
}

int FileModel::rowCount(const QModelIndex &parent) const
{
    return parent.isValid() ? 0 : int(m_rows.size());
}

QVariant FileModel::data(const QModelIndex &index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= m_rows.size()) {
        return QVariant();
    }
    const Row &row = m_rows.at(index.row());
    switch (role) {
    case NameRole:  return row.name;
    case PathRole:  return row.path;
    case IsDirRole: return row.isDir;
    case SizeRole:  return row.isDir ? QString() : humanSize(row.size);
    case SizeKbRole: return row.size;
    case MtimeRole: return QDateTime::fromSecsSinceEpoch(row.mtime).toString(QStringLiteral("yyyy-MM-dd HH:mm"));
    case KindRole:  return row.kind;
    case HiddenRole: return row.hidden;
    default:        return QVariant();
    }
}

QHash<int, QByteArray> FileModel::roleNames() const
{
    return {
        { NameRole,   "name" },
        { PathRole,   "filePath" },
        { IsDirRole,  "isDir" },
        { SizeRole,   "sizeText" },
        { SizeKbRole, "sizeBytes" },
        { MtimeRole,  "mtimeText" },
        { KindRole,   "kind" },
        { HiddenRole, "hidden" },
    };
}

QVariantList FileModel::crumbs() const
{
    QVariantList out;
    out.append(QVariantMap{
        { QStringLiteral("name"), QStringLiteral("全部文件") },
        { QStringLiteral("path"), QStringLiteral("/") },
    });
    if (m_path == QLatin1String("/")) {
        return out;
    }
    const QStringList parts = m_path.split(QLatin1Char('/'), Qt::SkipEmptyParts);
    QString acc;
    for (const QString &part : parts) {
        acc += QLatin1Char('/') + part;
        out.append(QVariantMap{
            { QStringLiteral("name"), part },
            { QStringLiteral("path"), acc },
        });
    }
    return out;
}

bool FileModel::canGoUp() const
{
    const QString up = QFileInfo(m_path).absolutePath();
    return !up.isEmpty() && up != m_path;
}

QString FileModel::kindOf(const QFileInfo &info)
{
    if (info.isDir()) {
        return QStringLiteral("dir");
    }
    const QString suffix = info.suffix().toLower();
    static const QStringList text = {
        QStringLiteral("txt"),  QStringLiteral("md"),   QStringLiteral("json"),
        QStringLiteral("js"),   QStringLiteral("ts"),   QStringLiteral("py"),
        QStringLiteral("c"),    QStringLiteral("cpp"),  QStringLiteral("h"),
        QStringLiteral("hpp"),  QStringLiteral("cs"),   QStringLiteral("java"),
        QStringLiteral("html"), QStringLiteral("htm"),  QStringLiteral("css"),
        QStringLiteral("xml"),  QStringLiteral("yml"),  QStringLiteral("yaml"),
        QStringLiteral("sh"),   QStringLiteral("csv"),  QStringLiteral("log"),
        QStringLiteral("ini"),  QStringLiteral("conf"), QStringLiteral("toml"),
        QStringLiteral("qml"),  QStringLiteral("desktop"),
    };
    static const QStringList image = {
        QStringLiteral("png"), QStringLiteral("jpg"),  QStringLiteral("jpeg"),
        QStringLiteral("gif"), QStringLiteral("webp"), QStringLiteral("bmp"),
        QStringLiteral("svg"), QStringLiteral("ico"),
    };
    static const QStringList audio = {
        QStringLiteral("mp3"), QStringLiteral("wav"), QStringLiteral("ogg"),
        QStringLiteral("flac"), QStringLiteral("m4a"),
    };
    static const QStringList video = {
        QStringLiteral("mp4"), QStringLiteral("webm"), QStringLiteral("mkv"),
        QStringLiteral("avi"), QStringLiteral("mov"),
    };
    static const QStringList archive = {
        QStringLiteral("zip"), QStringLiteral("tar"), QStringLiteral("gz"),
        QStringLiteral("7z"),  QStringLiteral("xz"),  QStringLiteral("rar"),
        QStringLiteral("bz2"),
    };

    if (text.contains(suffix))    return QStringLiteral("text");
    if (image.contains(suffix))   return QStringLiteral("image");
    if (audio.contains(suffix))   return QStringLiteral("audio");
    if (video.contains(suffix))   return QStringLiteral("video");
    if (archive.contains(suffix)) return QStringLiteral("archive");
    return QStringLiteral("other");
}

QString FileModel::humanSize(qint64 bytes)
{
    if (bytes < 1024) {
        return QStringLiteral("%1 B").arg(bytes);
    }
    const double kb = double(bytes) / 1024.0;
    if (kb < 1024.0) {
        return QStringLiteral("%1 KB").arg(kb, 0, 'f', kb < 10 ? 1 : 0);
    }
    const double mb = kb / 1024.0;
    if (mb < 1024.0) {
        return QStringLiteral("%1 MB").arg(mb, 0, 'f', 1);
    }
    return QStringLiteral("%1 GB").arg(mb / 1024.0, 0, 'f', 1);
}

bool FileModel::isProtected(const QString &absolutePath)
{
    if (absolutePath == QLatin1String("/")) {
        return true;
    }
    for (const QString &root : protectedRoots()) {
        if (absolutePath == root || absolutePath.startsWith(root + QLatin1Char('/'))) {
            return true;
        }
    }
    return false;
}

QString FileModel::guardReason() const
{
    if (isProtected(m_path)) {
        return QStringLiteral("这里是系统自己的地方，只能看不能改");
    }
    if (!m_writable) {
        return QStringLiteral("这个文件夹不让你改动");
    }
    return QString();
}

QVariantMap FileModel::at(int row) const
{
    if (row < 0 || row >= m_rows.size()) {
        return {};
    }
    const Row &r = m_rows.at(row);
    return QVariantMap{
        { QStringLiteral("name"), r.name },
        { QStringLiteral("path"), r.path },
        { QStringLiteral("isDir"), r.isDir },
        { QStringLiteral("size"), humanSize(r.size) },
        { QStringLiteral("kind"), r.kind },
        { QStringLiteral("mtime"), QDateTime::fromSecsSinceEpoch(r.mtime).toString(QStringLiteral("MM-dd HH:mm")) },
    };
}

void FileModel::watchCurrent()
{
    // addPath 一个已经盯着的路径会返回 false 并打一行 warning，先比一下再动。
    const QStringList watched = m_watcher.directories();
    if (watched.size() == 1 && watched.first() == m_path) {
        return;
    }
    if (!watched.isEmpty()) {
        m_watcher.removePaths(watched);
    }
    if (QFileInfo(m_path).isDir()) {
        m_watcher.addPath(m_path);
    }
}

void FileModel::reload()
{
    QDir dir(m_path);
    if (!dir.exists()) {
        return;
    }

    const QFileInfo dirInfo(m_path);
    m_writable = dirInfo.isWritable() && !isProtected(m_path);

    QList<Row> rows;
    const QFileInfoList entries = dir.entryInfoList(
        QDir::AllEntries | QDir::NoDotAndDotDot | (m_showHidden ? QDir::Hidden : QDir::NoFilter),
        QDir::NoSort);

    rows.reserve(entries.size());
    for (const QFileInfo &info : entries) {
        const QString name = info.fileName();
        const bool hidden = name.startsWith(QLatin1Char('.'));
        if (hidden && !m_showHidden) {
            continue;
        }
        // 搜索框：只匹配名字，不做全文检索 —— 「找我的作业.png」是真实需求，
        // 「在所有文件里搜某个字符串」在儿童机上既慢又容易搜出不该看的东西。
        if (!m_filter.isEmpty() && !name.contains(m_filter, Qt::CaseInsensitive)) {
            continue;
        }
        Row row;
        row.name = name;
        row.path = info.absoluteFilePath();
        row.isDir = info.isDir();
        row.size = row.isDir ? 0 : info.size();
        row.mtime = info.lastModified().toSecsSinceEpoch();
        row.kind = kindOf(info);
        row.hidden = hidden;
        rows.append(row);
    }

    // 文件夹排前面，同类型按名字排。用 localeAwareCompare，中文名才不会乱序。
    std::sort(rows.begin(), rows.end(), [](const Row &a, const Row &b) {
        if (a.isDir != b.isDir) {
            return a.isDir;
        }
        return QString::localeAwareCompare(a.name, b.name) < 0;
    });

    beginResetModel();
    m_rows = rows;
    endResetModel();

    const QStorageInfo storage(m_path);
    if (storage.isValid()) {
        m_diskText = QStringLiteral("还剩 %1，一共 %2")
                         .arg(humanSize(storage.bytesAvailable()), humanSize(storage.bytesTotal()));
    } else {
        m_diskText.clear();
    }

    watchCurrent();

    Q_EMIT countChanged();
    Q_EMIT pathChanged();
}

QString FileModel::navigate(const QString &target)
{
    QString want = target.trimmed();
    if (want.isEmpty()) {
        return QStringLiteral("路径是空的");
    }
    if (want == QLatin1String("~")) {
        want = QStandardPaths::writableLocation(QStandardPaths::HomeLocation);
    } else if (want.startsWith(QLatin1String("~/"))) {
        want = QStandardPaths::writableLocation(QStandardPaths::HomeLocation) + want.mid(1);
    }

    QFileInfo info(want);
    if (!info.isAbsolute()) {
        info = QFileInfo(QDir(m_path), want);
    }
    if (!info.exists()) {
        return QStringLiteral("找不到「%1」").arg(want);
    }
    if (!info.isDir()) {
        return QStringLiteral("「%1」不是文件夹").arg(info.fileName());
    }

    const QString resolved = info.canonicalFilePath();
    if (resolved.isEmpty() || resolved == m_path) {
        return QString();
    }
    m_path = resolved;
    reload();
    return QString();
}

QString FileModel::goUp()
{
    if (!canGoUp()) {
        return QString();
    }
    return navigate(QFileInfo(m_path).absolutePath());
}

void FileModel::refresh()
{
    reload();
}

void FileModel::setShowHidden(bool value)
{
    if (m_showHidden == value) {
        return;
    }
    m_showHidden = value;
    Q_EMIT showHiddenChanged();
    reload();
}

void FileModel::setFilter(const QString &value)
{
    // trim：孩子按空格比按字母容易，输入 "作业 " 后面带个空格不该搜不到
    const QString next = value.trimmed();
    if (m_filter == next) {
        return;
    }
    m_filter = next;
    Q_EMIT filterChanged();
    reload();
}

bool FileModel::canModify(int row) const
{
    if (row < 0 || row >= m_rows.size()) {
        return false;
    }
    if (isProtected(m_rows.at(row).path)) {
        return false;
    }
    // 家目录本身永远留着 —— 删掉它等于把孩子的所有东西一次清空。
    const QString home = QStandardPaths::writableLocation(QStandardPaths::HomeLocation);
    return m_rows.at(row).path != home;
}

QString FileModel::open(int row)
{
    if (row < 0 || row >= m_rows.size()) {
        return QStringLiteral("denied");
    }
    const Row item = m_rows.at(row);

    if (item.isDir) {
        navigate(item.path);
        return QStringLiteral("dir");
    }
    if (item.kind == QLatin1String("text")) {
        return QStringLiteral("text");
    }
    if (item.kind == QLatin1String("image")) {
        return QStringLiteral("image");
    }

    /*
     * 其余类型交给系统默认程序。
     *
     * 在 kiosk 会话里不一定有 xdg-open 的关联（比如没装看图软件），
     * openUrl 会返回 false —— 这时不能装作没事发生，界面要明确告诉孩子
     * 「这台电脑没有能打开它的程序」，否则他会一直双击。
     */
    if (QDesktopServices::openUrl(QUrl::fromLocalFile(item.path))) {
        return QStringLiteral("launch");
    }
    return QStringLiteral("denied");
}

QString FileModel::preview(int row, int maxBytes)
{
    if (row < 0 || row >= m_rows.size()) {
        return QString();
    }
    const Row &item = m_rows.at(row);
    QFile file(item.path);
    if (!file.open(QIODevice::ReadOnly)) {
        return QStringLiteral("打不开这个文件（可能没有权限）");
    }
    const QByteArray data = file.read(maxBytes);
    file.close();

    // 有 NUL 字节就当二进制处理：直接塞进 Text 会把界面画成一堆方块。
    if (data.contains('\0')) {
        return QStringLiteral("这是一个二进制文件，这里看不了它的内容");
    }
    QString text = QString::fromUtf8(data);
    if (text.contains(QChar(0xFFFD))) {
        // UTF-8 解出替换字符，说明不是 UTF-8，退回本地编码再试一次
        text = QString::fromLocal8Bit(data);
    }
    if (file.size() > maxBytes) {
        text += QStringLiteral("\n\n……文件太大了，只显示前面一部分");
    }
    return text;
}

QString FileModel::uniqueTrashName(const QString &baseName)
{
    const QString files = trashRoot() + QStringLiteral("/files");
    QDir().mkpath(files);
    QDir().mkpath(trashRoot() + QStringLiteral("/info"));

    QString candidate = baseName;
    int n = 1;
    while (QFileInfo::exists(files + QLatin1Char('/') + candidate)
           || QFileInfo::exists(trashRoot() + QStringLiteral("/info/") + candidate + QStringLiteral(".trashinfo"))) {
        candidate = QStringLiteral("%1.%2").arg(baseName).arg(n++);
    }
    return candidate;
}

QString FileModel::removeItem(int row)
{
    if (row < 0 || row >= m_rows.size()) {
        return QStringLiteral("没有选中东西");
    }
    if (!canModify(row)) {
        return QStringLiteral("这个是系统的东西，不能删");
    }

    const Row item = m_rows.at(row);
    const QString name = uniqueTrashName(item.name);
    const QString target = trashRoot() + QStringLiteral("/files/") + name;

    QFile::rename(item.path, target);
    if (!QFileInfo::exists(target)) {
        /*
         * rename 失败最常见的原因是跨文件系统（/home 和 U 盘是两个分区），
         * 这时只能先复制再删。复制一半失败就什么都不做，别留个残骸。
         */
        if (!QFile::copy(item.path, target)) {
            return QStringLiteral("删不掉「%1」，可能正被别的程序用着").arg(item.name);
        }
        if (item.isDir) {
            QDir(item.path).removeRecursively();
        } else {
            QFile::remove(item.path);
        }
    }

    // 写 .trashinfo，系统回收站靠它才知道这东西原来在哪
    QFile info(trashRoot() + QStringLiteral("/info/") + name + QStringLiteral(".trashinfo"));
    if (info.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        const QString when = QDateTime::currentDateTime().toString(Qt::ISODate);
        const QString original = QString::fromUtf8(QUrl::toPercentEncoding(item.path));
        info.write("[Trash Info]\nPath=");
        info.write(original.toUtf8());
        info.write("\nDeletionDate=");
        info.write(when.toUtf8());
        info.write("\n");
        info.close();
    }

    Q_EMIT notice(QStringLiteral("「%1」放进了回收站").arg(item.name));
    reload();
    return QString();
}

QString FileModel::makeFolder(const QString &name)
{
    const QString trimmed = name.trimmed();
    if (trimmed.isEmpty()) {
        return QStringLiteral("名字不能是空的");
    }
    if (trimmed.contains(QLatin1Char('/'))) {
        return QStringLiteral("名字里不能有斜杠");
    }
    if (!guardReason().isEmpty()) {
        return guardReason();
    }
    const QString full = m_path + QLatin1Char('/') + trimmed;
    if (QFileInfo::exists(full)) {
        return QStringLiteral("这里已经有一个叫「%1」的了").arg(trimmed);
    }
    if (!QDir().mkpath(full)) {
        return QStringLiteral("新建失败，可能是磁盘满了");
    }
    Q_EMIT notice(QStringLiteral("新建了「%1」").arg(trimmed));
    reload();
    return QString();
}

QString FileModel::renameItem(int row, const QString &newName)
{
    if (row < 0 || row >= m_rows.size()) {
        return QStringLiteral("没有选中东西");
    }
    if (!canModify(row)) {
        return QStringLiteral("这个是系统的东西，不能改名");
    }
    const QString trimmed = newName.trimmed();
    if (trimmed.isEmpty() || trimmed.contains(QLatin1Char('/'))) {
        return QStringLiteral("名字不合法");
    }
    const Row item = m_rows.at(row);
    if (trimmed == item.name) {
        return QString();
    }
    const QString target = m_path + QLatin1Char('/') + trimmed;
    if (QFileInfo::exists(target)) {
        return QStringLiteral("这里已经有一个叫「%1」的了").arg(trimmed);
    }
    if (!QFile::rename(item.path, target)) {
        return QStringLiteral("改名失败");
    }
    Q_EMIT notice(QStringLiteral("改好了：%1").arg(trimmed));
    reload();
    return QString();
}
