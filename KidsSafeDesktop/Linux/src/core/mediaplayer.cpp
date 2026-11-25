#include "mediaplayer.h"

#include <QCoreApplication>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QHash>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTimer>
#include <QUrl>
#include <QVariantMap>

#include <utility>

#ifdef KIDSSAFE_MEDIA
#include <QAudioOutput>
#include <QMediaPlayer>
#endif

Media::Media(QObject *parent)
    : QObject(parent)
{
#ifdef KIDSSAFE_MEDIA
    m_player = new QMediaPlayer(this);
    m_output = new QAudioOutput(this);
    // 0.8 而不是 1.0：音乐盒音色的峰值偏高，满音量在小喇叭上会破。
    m_output->setVolume(0.8);
    m_player->setAudioOutput(m_output);

    connect(m_player, &QMediaPlayer::positionChanged, this, [this](qint64 pos) {
        m_position = pos;
        emit positionChanged();
    });
    connect(m_player, &QMediaPlayer::durationChanged, this, [this](qint64 dur) {
        m_duration = dur;
        emit playbackChanged();
    });
    connect(m_player, &QMediaPlayer::playbackStateChanged, this,
            [this](QMediaPlayer::PlaybackState) { emit playbackChanged(); });
    connect(m_player, &QMediaPlayer::mediaStatusChanged, this,
            [this](QMediaPlayer::MediaStatus status) {
                if (status != QMediaPlayer::EndOfMedia) {
                    return;
                }
                // 循环曲目由播放器自己接（setLoops(Infinite)），走到这里说明
                // 是普通曲目放完了。
                advanceFromEnd();
            });
    connect(m_player, &QMediaPlayer::errorOccurred, this,
            [this](QMediaPlayer::Error error, const QString &errorString) {
                if (error == QMediaPlayer::NoError) {
                    return;
                }
                qWarning() << "[media] 播放失败：" << errorString;
                setMessage(QStringLiteral("这段放不了：%1").arg(
                    errorString.isEmpty() ? QStringLiteral("解码器不支持") : errorString));
                m_current = -1;
                emit playbackChanged();
            });
#endif

    loadLibrary();
}

Media::~Media()
{
#ifdef KIDSSAFE_MEDIA
    if (m_player != nullptr) {
        m_player->stop();
    }
#endif
}

QString Media::title() const
{
    if (m_current < 0 || m_current >= m_tracks.size()) {
        return QString();
    }
    return m_tracks.at(m_current).title;
}

QString Media::subtitle() const
{
    if (m_current < 0 || m_current >= m_tracks.size()) {
        return QString();
    }
    return m_tracks.at(m_current).subtitle;
}

bool Media::playing() const
{
#ifdef KIDSSAFE_MEDIA
    return m_player != nullptr && m_player->playbackState() == QMediaPlayer::PlayingState;
#else
    return false;
#endif
}

void Media::refresh()
{
    loadLibrary();
    emit libraryChanged();
    emit stateChanged();
}

void Media::loadLibrary()
{
    m_sections.clear();
    m_tracksList.clear();
    m_tracks.clear();
    m_dir.clear();
    m_available = false;
    m_current = -1;
    m_position = 0;
    m_duration = 0;

    // 找资源目录，顺序与 Playland 一致：环境变量 → 二进制旁边的 share →
    // 常见安装前缀 → 源码树（cmake --build 完直接跑也能用）。
    QStringList candidates;
    const QString envDir = qEnvironmentVariable("KIDSSAFE_AUDIO_DIR");
    if (!envDir.isEmpty()) {
        candidates << envDir;
    }
    const QString appDir = QCoreApplication::applicationDirPath();
    if (!appDir.isEmpty()) {
        candidates << appDir + QStringLiteral("/../share/kidssafe/audio");
        candidates << appDir + QStringLiteral("/share/kidssafe/audio");
    }
    candidates << QStringLiteral("/usr/share/kidssafe/audio");
    candidates << QStringLiteral("/usr/local/share/kidssafe/audio");
#ifdef KIDSSAFE_AUDIO_SRC
    candidates << QStringLiteral(KIDSSAFE_AUDIO_SRC);
#endif

    QString libraryPath;
    for (const QString &raw : candidates) {
        const QString dir = QDir::cleanPath(raw);
        const QFileInfo file(dir + QStringLiteral("/library.json"));
        if (file.exists() && file.isFile()) {
            m_dir = dir;
            libraryPath = file.absoluteFilePath();
            break;
        }
    }

    if (libraryPath.isEmpty()) {
        m_detail = QStringLiteral("找不到音频资源 library.json（应该装在 share/kidssafe/audio）");
        qWarning() << "[media]" << m_detail;
        return;
    }

    QFile file(libraryPath);
    if (!file.open(QIODevice::ReadOnly)) {
        m_detail = QStringLiteral("打不开内容表：%1").arg(libraryPath);
        qWarning() << "[media]" << m_detail;
        return;
    }

    QJsonParseError error{};
    const QJsonDocument doc = QJsonDocument::fromJson(file.readAll(), &error);
    if (error.error != QJsonParseError::NoError || !doc.isObject()) {
        m_detail = QStringLiteral("内容表格式不对：%1（第 %2 字节）")
                       .arg(error.errorString())
                       .arg(error.offset);
        qWarning() << "[media]" << m_detail;
        return;
    }

    const QJsonObject root = doc.object();

    // 先收曲目，再按分类统计数量 —— 分类里的 count 是算出来的，
    // 不是手写的，改曲目时不用同步维护第二个数字。
    QHash<QString, int> counts;
    const QJsonArray tracks = root.value(QStringLiteral("tracks")).toArray();
    for (const QJsonValue &value : tracks) {
        const QJsonObject object = value.toObject();
        Track track;
        track.section = object.value(QStringLiteral("section")).toString();
        track.title = object.value(QStringLiteral("title")).toString();
        track.subtitle = object.value(QStringLiteral("subtitle")).toString();
        track.file = object.value(QStringLiteral("file")).toString();
        track.text = object.value(QStringLiteral("text")).toString();
        track.loop = object.value(QStringLiteral("loop")).toBool(false);
        if (track.title.isEmpty() || track.section.isEmpty()) {
            continue;
        }
        // 有 file 但文件真的不在，就当没有：卡片还在（孩子能看到名字），
        // 点下去会明确提示，而不是静默什么都不发生。
        m_tracks.append(track);
        ++counts[track.section];
    }

    const QJsonArray sections = root.value(QStringLiteral("sections")).toArray();
    for (const QJsonValue &value : sections) {
        const QJsonObject object = value.toObject();
        const QString id = object.value(QStringLiteral("id")).toString();
        if (id.isEmpty()) {
            continue;
        }
        QVariantMap item;
        item.insert(QStringLiteral("id"), id);
        item.insert(QStringLiteral("title"), object.value(QStringLiteral("title")).toString(id));
        item.insert(QStringLiteral("glyph"), object.value(QStringLiteral("glyph")).toString(QStringLiteral("music")));
        item.insert(QStringLiteral("accent"), object.value(QStringLiteral("accent")).toString(QStringLiteral("#EF6C8B")));
        item.insert(QStringLiteral("hint"), object.value(QStringLiteral("hint")).toString());
        item.insert(QStringLiteral("count"), counts.value(id, 0));
        m_sections.append(item);
    }

    for (const Track &track : std::as_const(m_tracks)) {
        QVariantMap item;
        item.insert(QStringLiteral("section"), track.section);
        item.insert(QStringLiteral("title"), track.title);
        item.insert(QStringLiteral("subtitle"), track.subtitle);
        item.insert(QStringLiteral("file"), track.file);
        item.insert(QStringLiteral("text"), track.text);
        item.insert(QStringLiteral("loop"), track.loop);
        m_tracksList.append(item);
    }

#ifndef KIDSSAFE_MEDIA
    m_detail = QStringLiteral(
        "本次构建没有 QtMultimedia，音频放不了（deepin 上装 qt6-multimedia-dev 后重新编译）。"
        "国学启蒙里的朗读仍然可用。");
    qInfo() << "[media] 未链接 QtMultimedia，只能显示内容、不能播放";
    return;
#else
    m_available = !m_tracks.isEmpty();
    if (m_available) {
        m_detail = QStringLiteral("%1 段内容 · %2 个分类").arg(m_tracks.size()).arg(m_sections.size());
        qInfo() << "[media] 内容目录：" << m_dir << "|" << m_detail;
    } else {
        m_detail = QStringLiteral("内容表是空的：%1").arg(libraryPath);
        qWarning() << "[media]" << m_detail;
    }
#endif
}

bool Media::play(int index)
{
    if (index < 0 || index >= m_tracks.size()) {
        return false;
    }

    const Track &track = m_tracks.at(index);
    if (track.file.isEmpty()) {
        // 朗读类走 QML + Speech，Media 不参与，避免两套「正在播放」状态打架。
        return false;
    }

#ifdef KIDSSAFE_MEDIA
    const QString path = m_dir + QLatin1Char('/') + track.file;
    if (!QFileInfo::exists(path)) {
        setMessage(QStringLiteral("找不到音频文件：%1").arg(track.file));
        return false;
    }

    m_current = index;
    m_position = 0;
    m_duration = 0;
    m_player->setSource(QUrl::fromLocalFile(path));
    // 环境音要一直响：Infinite 让播放器自己循环，不用我们在 QML 里重播。
    m_player->setLoops(track.loop ? QMediaPlayer::Infinite : 1);
    m_player->play();
    emit playbackChanged();
    qInfo() << "[media] 播放" << track.title << path;
    return true;
#else
    Q_UNUSED(index)
    setMessage(QStringLiteral("这个版本没有播放器，只能看内容"));
    return false;
#endif
}

void Media::toggle()
{
#ifdef KIDSSAFE_MEDIA
    if (m_player == nullptr || m_current < 0) {
        return;
    }
    if (m_player->playbackState() == QMediaPlayer::PlayingState) {
        m_player->pause();
    } else {
        m_player->play();
    }
    emit playbackChanged();
#endif
}

void Media::stop()
{
#ifdef KIDSSAFE_MEDIA
    if (m_player != nullptr) {
        m_player->stop();
    }
#endif
    if (m_current >= 0) {
        m_current = -1;
        m_position = 0;
        m_duration = 0;
        emit playbackChanged();
    }
}

void Media::next()
{
    const int target = neighbour(m_current, +1);
    if (target >= 0) {
        play(target);
    }
}

void Media::previous()
{
#ifdef KIDSSAFE_MEDIA
    // 播了 3 秒以上先回到开头，这是所有播放器的共同习惯；否则才切上一首。
    if (m_player != nullptr && m_player->position() > 3000) {
        m_player->setPosition(0);
        return;
    }
#endif
    const int target = neighbour(m_current, -1);
    if (target >= 0) {
        play(target);
    }
}

void Media::seekTo(qreal ratio)
{
#ifdef KIDSSAFE_MEDIA
    if (m_player == nullptr || m_duration <= 0) {
        return;
    }
    const qreal clamped = qBound<qreal>(0.0, ratio, 1.0);
    m_player->setPosition(static_cast<qint64>(m_duration * clamped));
#endif
    Q_UNUSED(ratio)
}

int Media::neighbour(int from, int step) const
{
    if (m_tracks.isEmpty()) {
        return -1;
    }
    if (from < 0 || from >= m_tracks.size()) {
        // 还没在播：从当前分类的第一首开始（step 为正时）。
        return step > 0 ? 0 : m_tracks.size() - 1;
    }

    const QString section = m_tracks.at(from).section;
    const int count = m_tracks.size();
    for (int i = 1; i <= count; ++i) {
        const int candidate = (from + step * i + count * 2) % count;
        if (m_tracks.at(candidate).section != section) {
            // 绕一圈回到同一个分类时继续找；下面这行靠循环次数兜底，
            // 不会死循环：最多走 count 次。
            continue;
        }
        if (m_tracks.at(candidate).file.isEmpty()) {
            continue;
        }
        return candidate;
    }
    return -1;
}

void Media::advanceFromEnd()
{
    if (m_current < 0) {
        return;
    }
    const QString section = m_tracks.at(m_current).section;

    for (int i = m_current + 1; i < m_tracks.size(); ++i) {
        const Track &track = m_tracks.at(i);
        // 不 break：曲目在表里不一定按分类连续排，跳过别的分类继续找同分类的
        // 下一首才是安全语义；找不到自然走到 stop()。
        if (track.section != section || track.file.isEmpty()) {
            continue;
        }
        play(i);
        return;
    }

    // 分类里最后一首播完：停住，别自动跨到雨声里去。
    stop();
}

void Media::setMessage(const QString &text)
{
    m_message = text;
    emit stateChanged();

    const int seq = ++m_messageSeq;
    QTimer::singleShot(3500, this, [this, seq] {
        if (m_messageSeq != seq) {
            return;
        }
        m_message.clear();
        emit stateChanged();
    });
}
