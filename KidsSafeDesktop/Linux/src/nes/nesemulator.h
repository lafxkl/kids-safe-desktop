#pragma once

#include <QElapsedTimer>
#include <QByteArray>
#include <QImage>
#include <QMutex>
#include <QObject>
#include <QVariantList>

class QTimer;
class QIODevice;
#ifdef KIDSSAFE_NES_AUDIO
class QAudioSink;
#endif

namespace nes {
class Famicom;
namespace device {
class NesCartridge;
}
} // namespace nes

/**
 * 红白机（NES / Famicom）模拟器。核心来自 SimpleNes（MIT，纯 C++17，
 * 已内置在 src/nes/core），这里只做 Qt 侧的包装：
 *
 *   · 帧循环：QTimer 4ms 轮询 + QElapsedTimer 定帧（60.0988 fps）。
 *     不用 QML 的 Timer / Animation 驱动 —— 那些会被渲染阻塞节流，
 *     跑一会儿就会越来越慢。
 *   · 画面：核心给的是 256×240 的**调色板索引**（0~63），不是 RGB。
 *     这里用一张 64 项查找表翻成 QImage，NesView（QQuickPaintedItem）再画出来。
 *   · 声音：QtMultimedia 的 QAudioSink，44100Hz 单声道 16 位，
 *     每帧约 735 个采样，正好 44100/60。没装 QtMultimedia 就静音运行，
 *     其余功能不受影响（CMake 里是可选依赖）。
 *   · ROM 不放 qrc：版权原因不随包分发，家长自己把 .nes 丢进
 *     ~/.local/share/kidssafe/roms（或用 KIDSSAFE_ROM_DIR 指定）。
 *
 * 为什么帧循环跑在 GUI 线程：核心一帧约 1~3ms（C++ 逐像素），
 * 60fps 下占用可控；换成独立线程反而要处理与 QImage 的同步，
 * 而 QQuickPaintedItem 的截屏本来就在渲染线程做。
 */
class NesEmulator : public QObject
{
    Q_OBJECT

    Q_PROPERTY(QVariantList roms READ roms NOTIFY romsChanged)
    Q_PROPERTY(bool loaded READ loaded NOTIFY stateChanged)
    Q_PROPERTY(bool paused READ paused NOTIFY stateChanged)
    /// 正在玩的游戏名（ROM 文件名，去掉扩展名）。
    Q_PROPERTY(QString title READ title NOTIFY stateChanged)
    /// 给人看的一句话状态 / 错误原因。
    Q_PROPERTY(QString message READ message NOTIFY stateChanged)
    /// ROM 目录，家长要往这里放文件。
    Q_PROPERTY(QString romDir READ romDir CONSTANT)
    /// 音频是否真的打开了。false 时界面提示「没有声音」。
    Q_PROPERTY(bool audioReady READ audioReady NOTIFY stateChanged)

public:
    /// 手柄按键编号，与核心的 NesGamePad 顺序一致（也是硬件移位顺序）。
    enum Pad {
        PadA = 0,
        PadB = 1,
        PadSelect = 2,
        PadStart = 3,
        PadUp = 4,
        PadDown = 5,
        PadLeft = 6,
        PadRight = 7
    };
    Q_ENUM(Pad)

    explicit NesEmulator(QObject *parent = nullptr);
    ~NesEmulator() override;

    QVariantList roms() const { return m_roms; }
    bool loaded() const { return m_loaded; }
    bool paused() const { return m_paused; }
    QString title() const { return m_title; }
    QString message() const { return m_message; }
    QString romDir() const;
    bool audioReady() const { return m_audioReady; }

    /// 给 NesView 用：拿当前帧的副本（渲染线程会调用，必须加锁）。
    QImage grabFrame() const;

    Q_INVOKABLE void scanRoms();
    Q_INVOKABLE void openRom(const QString &path);
    Q_INVOKABLE void closeRom();
    Q_INVOKABLE void resetGame();
    Q_INVOKABLE void togglePause();
    /// 幂等版暂停：离开页面时用它，避免和 togglePause 互相打架。
    Q_INVOKABLE void setPaused(bool paused);
    /// 离开页面时用：只暂停「本来在跑的」游戏，并记住这次暂停是自动的。
    Q_INVOKABLE void pauseForLeave();
    /// 回到页面时用：把 pauseForLeave 造成的暂停悄悄撤掉。
    /// 孩子自己按的「暂停」不会被它恢复 —— 那个 m_pausedByLeave 是 false。
    Q_INVOKABLE void resumeFromLeave();
    Q_INVOKABLE void setButton(int button, bool pressed);
    Q_INVOKABLE void releaseButtons();
    /// Qt::Key → 手柄按键。返回是否被本模拟器消费。
    Q_INVOKABLE bool keyEvent(int key, bool pressed);
    Q_INVOKABLE bool saveState();
    Q_INVOKABLE bool loadState();

signals:
    void romsChanged();
    void stateChanged();
    /// 每渲染完一帧发一次，NesView 收到后 update()。
    void frameReady();

private:
    void tick();
    void composeFrame();
    void pumpAudio();
    void startAudio();
    void stopAudio();
    void stopFrameLoop();
    QString statePath() const;
    void setMessage(const QString &text);

    nes::Famicom *m_famicom = nullptr;
    nes::device::NesCartridge *m_cartridge = nullptr;

    QTimer *m_tickTimer = nullptr;
    QElapsedTimer m_clock;
    qint64 m_nextFrameNs = 0;

    mutable QMutex m_frameMutex;
    QImage m_image;

    QVariantList m_roms;
    bool m_loaded = false;
    bool m_paused = false;
    bool m_audioReady = false;
    /// 当前这次暂停是不是「离开页面」造成的。只有它为 true 时，
    /// 回到页面才自动恢复；孩子自己按的暂停必须由孩子自己点「继续」。
    bool m_pausedByLeave = false;
    QString m_title;
    QString m_message;
    QString m_romPath;
    /// 「已存档」这类提示要在几秒后自己消失，用序号避免误清掉新提示。
    int m_messageSeq = 0;

#ifdef KIDSSAFE_NES_AUDIO
    QAudioSink *m_sink = nullptr;
    QIODevice *m_audioOut = nullptr;
#endif
    /// 还没写进声卡的采样。QAudioSink 的缓冲满时 write() 只写一部分，
    /// 不接住剩下的那半块就是一次爆音。
    QByteArray m_audioPending;
};
