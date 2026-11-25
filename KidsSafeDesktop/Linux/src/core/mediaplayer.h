#pragma once

#include <QObject>
#include <QString>
#include <QVariantList>
#include <QVector>

#ifdef KIDSSAFE_MEDIA
class QMediaPlayer;
class QAudioOutput;
#endif

/**
 * 媒体中心（听一听）的播放服务。
 *
 * 内容全在 `assets/audio/library.json`：分类 + 曲目，曲目要么有 `file`
 * （随包音频，走 QtMultimedia），要么有 `text`（唐诗这类朗读内容，交给
 * Speech/spd-say，不占音频文件）。改内容只改这个 JSON，不用重新编译。
 *
 * 为什么播放逻辑放在 C++ 而不是 QML 的 `MediaPlayer`：
 *   1. QML 的媒体类型要额外装 qml6-module-qtmultimedia，只装 -dev 能编过、
 *      运行期 `import QtMultimedia` 直接失败，整页打不开；
 *   2. 与红白机的音频一样，用 C++ 时 QtMultimedia 是**可选依赖**——
 *      没装也能编译、能在界面里明确说「没有播放器」，而不是白屏。
 *
 * 自动接下一首只在**同一分类内**进行：儿歌播完接下一首儿歌，
 * 最后一首播完就停。跨分类（儿歌 → 雨声）会让孩子莫名听到别的东西。
 */
class Media : public QObject
{
    Q_OBJECT

    /// 有没有能用的播放器 + 至少一段内容。false 时页面只显示说明。
    Q_PROPERTY(bool available READ available NOTIFY stateChanged)
    Q_PROPERTY(QString detail READ detail NOTIFY stateChanged)
    /// [{id,title,glyph,accent,hint,count}]，给顶部切换用。
    Q_PROPERTY(QVariantList sections READ sections NOTIFY libraryChanged)
    /// 扁平曲目表 [{section,title,subtitle,file,text,loop}]，页面自己按 section 过滤。
    Q_PROPERTY(QVariantList tracks READ tracks NOTIFY libraryChanged)
    /// 当前曲目下标，-1 表示没在播。
    Q_PROPERTY(int current READ current NOTIFY playbackChanged)
    Q_PROPERTY(QString title READ title NOTIFY playbackChanged)
    Q_PROPERTY(QString subtitle READ subtitle NOTIFY playbackChanged)
    Q_PROPERTY(bool playing READ playing NOTIFY playbackChanged)
    Q_PROPERTY(qint64 position READ position NOTIFY positionChanged)
    Q_PROPERTY(qint64 duration READ duration NOTIFY playbackChanged)
    Q_PROPERTY(QString message READ message NOTIFY stateChanged)

public:
    explicit Media(QObject *parent = nullptr);
    ~Media() override;

    bool available() const { return m_available; }
    QString detail() const { return m_detail; }
    QVariantList sections() const { return m_sections; }
    QVariantList tracks() const { return m_tracksList; }
    int current() const { return m_current; }
    QString title() const;
    QString subtitle() const;
    bool playing() const;
    qint64 position() const { return m_position; }
    qint64 duration() const { return m_duration; }
    QString message() const { return m_message; }

    /// 播第 index 段。朗读类（只有 text）不归这里管，返回 false。
    Q_INVOKABLE bool play(int index);
    Q_INVOKABLE void toggle();
    Q_INVOKABLE void next();
    Q_INVOKABLE void previous();
    Q_INVOKABLE void stop();
    /// ratio ∈ [0,1]，给进度条拖动用。
    Q_INVOKABLE void seekTo(qreal ratio);
    /// 重新读 library.json（家长改完内容不用重启）。
    Q_INVOKABLE void refresh();

signals:
    void stateChanged();
    void libraryChanged();
    void playbackChanged();
    void positionChanged();

private:
    struct Track {
        QString section;
        QString title;
        QString subtitle;
        QString file;
        QString text;
        bool loop = false;
    };

    void loadLibrary();
    void advanceFromEnd();
    /// 同一分类内找下一首（step = +1 / -1），循环绕回；找不到返回 -1。
    int neighbour(int from, int step) const;
    void setMessage(const QString &text);

    QString m_dir;
    QString m_detail;
    bool m_available = false;

    QVector<Track> m_tracks;
    QVariantList m_sections;
    QVariantList m_tracksList;

    int m_current = -1;
    qint64 m_position = 0;
    qint64 m_duration = 0;
    QString m_message;
    int m_messageSeq = 0;

#ifdef KIDSSAFE_MEDIA
    QMediaPlayer *m_player = nullptr;
    QAudioOutput *m_output = nullptr;
#endif
};
