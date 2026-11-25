#include "nesemulator.h"

#include "Famicom.h"
#include "Logger.h"
#include "NesCartridge.h"

#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QIODevice>
#include <QStandardPaths>
#include <QStringList>
#include <QTimer>
#include <QVariantMap>

#include <algorithm>
#include <string>
#include <vector>

#ifdef KIDSSAFE_NES_AUDIO
#  include <QAudioFormat>
#  include <QAudioSink>
#  include <QMediaDevices>
#endif

namespace {

constexpr int kWidth = 256;
constexpr int kHeight = 240;

/// 一帧的时长。NES 是 60.0988 fps（不是整数 60），用整数 16666666ns 跑久了
/// 会和音频对不上：735 采样 × 60 = 44100 只在 60.00fps 时成立。这里取
/// 1e9/60.0988 ≈ 16.639ms，音频与画面就能长期锁住。
constexpr qint64 kFrameNs = 16639000;

/// 一帧最多追几帧。拖动窗口 / 系统卡顿时不要疯狂补帧，
/// 否则回来那一瞬间会「快进」好几秒。
constexpr int kMaxCatchUp = 3;

/// NES 2C02 的 64 色调色板。核心只给索引（0~63），颜色表在 SimpleNes 的
/// 前端里是单独一个 palette.h（那份没随核心一起分发），这里内置一份标准表。
constexpr QRgb kPalette[64] = {
    0xFF7C7C7C, 0xFF0000FC, 0xFF0000BC, 0xFF4428BC, 0xFF940084, 0xFFA80020, 0xFFA81000, 0xFF881400,
    0xFF503000, 0xFF007800, 0xFF006800, 0xFF005800, 0xFF004058, 0xFF000000, 0xFF000000, 0xFF000000,
    0xFFBCBCBC, 0xFF0078F8, 0xFF0058F8, 0xFF6844FC, 0xFFD800CC, 0xFFE40058, 0xFFF83800, 0xFFE45C10,
    0xFFAC7C00, 0xFF00B800, 0xFF00A800, 0xFF00A844, 0xFF008888, 0xFF000000, 0xFF000000, 0xFF000000,
    0xFFF8F8F8, 0xFF3CBCFC, 0xFF6888FC, 0xFF9878F8, 0xFFF878F8, 0xFFF85898, 0xFFF87858, 0xFFFCA044,
    0xFFF8B800, 0xFFB8F818, 0xFF58D854, 0xFF58F898, 0xFF00E8D8, 0xFF787878, 0xFF000000, 0xFF000000,
    0xFFFCFCFC, 0xFFA4E4FC, 0xFFB8B8F8, 0xFFD8B8F8, 0xFFF8B8F8, 0xFFF8A4C0, 0xFFF0D0B0, 0xFFFCE0A8,
    0xFFF8D878, 0xFFD8F878, 0xFFB8F8B8, 0xFFB8F8D8, 0xFF00FCFC, 0xFFF8D8F8, 0xFF000000, 0xFF000000,
};

QString sizeText(qint64 bytes)
{
    if (bytes >= 1024 * 1024) {
        return QStringLiteral("%1 MB").arg(bytes / 1024.0 / 1024.0, 0, 'f', 1);
    }
    return QStringLiteral("%1 KB").arg(qMax<qint64>(1, bytes / 1024));
}

} // namespace

NesEmulator::NesEmulator(QObject *parent)
    : QObject(parent)
{
    m_image = QImage(kWidth, kHeight, QImage::Format_RGB32);
    m_image.fill(Qt::black);

    m_tickTimer = new QTimer(this);
    // PreciseTimer：默认的粗粒度定时器在 5ms 间隔下抖动会有 ±5ms，
    // 累积起来就是一秒少几帧，声音会「嘟」一下。
    m_tickTimer->setTimerType(Qt::PreciseTimer);
    m_tickTimer->setInterval(4);
    connect(m_tickTimer, &QTimer::timeout, this, &NesEmulator::tick);

    scanRoms();
}

NesEmulator::~NesEmulator()
{
    // 析构里关信号：closeRom 会 emit stateChanged，而 QML 那边可能正好在
    // 这个回调里访问已经半销毁的成员（换页面时就是这条路径）。
    blockSignals(true);
    closeRom();
}

QString NesEmulator::romDir() const
{
    const QString override = qEnvironmentVariable("KIDSSAFE_ROM_DIR");
    if (!override.isEmpty()) {
        return override;
    }
    return QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation)
           + QStringLiteral("/kidssafe/roms");
}

QImage NesEmulator::grabFrame() const
{
    QMutexLocker locker(&m_frameMutex);
    return m_image;
}

// ---------------------------------------------------------------------------
// ROM 列表
// ---------------------------------------------------------------------------

void NesEmulator::scanRoms()
{
    const QString dir = romDir();
    QDir().mkpath(dir);

    QDir folder(dir);
    folder.setFilter(QDir::Files | QDir::Readable);
    // .nes 大小写都收：Windows 上烤出来的文件常是 .NES。
    folder.setNameFilters({QStringLiteral("*.nes"), QStringLiteral("*.NES")});

    QFileInfoList entries = folder.entryInfoList(QDir::Files, QDir::NoSort);
    std::sort(entries.begin(), entries.end(), [](const QFileInfo &a, const QFileInfo &b) {
        const int byName = QString::localeAwareCompare(a.fileName(), b.fileName());
        return byName < 0;
    });

    QVariantList list;
    list.reserve(entries.size());
    for (const QFileInfo &info : entries) {
        QVariantMap item;
        item.insert(QStringLiteral("name"), info.completeBaseName());
        item.insert(QStringLiteral("file"), info.absoluteFilePath());
        item.insert(QStringLiteral("size"), sizeText(info.size()));
        list.append(item);
    }

    m_roms = list;
    emit romsChanged();
}

// ---------------------------------------------------------------------------
// 开关卡带
// ---------------------------------------------------------------------------

void NesEmulator::openRom(const QString &path)
{
    closeRom();

    if (path.isEmpty()) {
        return;
    }
    if (!QFileInfo::exists(path)) {
        setMessage(QStringLiteral("找不到这个游戏文件"));
        return;
    }

    // 核心用的是 std::string 路径。Linux 上 encodeName 就是 UTF-8，
    // 中文文件名的 ROM 也能正确打开。
    const std::string nativePath = QFile::encodeName(path).toStdString();
    nes::device::NesCartridge *cart = nes::device::NesCartridge::loadFromFile(nativePath);
    if (cart == nullptr) {
        setMessage(QStringLiteral("这不是有效的 NES 游戏（需要 .nes 格式）"));
        return;
    }

    auto *famicom = new nes::Famicom();
    // Famicom 构造函数里把日志级别设成了 Debug（开发期方便），一帧几百行
    // iostream 输出会直接把帧率拖死。这里压到 Error：
    // 不取 Warn 是因为核心每次插卡都会去找 games/mirror_overrides.txt
    // （少数坏 dump 的镜像修正表），找不到就打一行 WARN —— 我们没法控制
    // 启动目录，那行警告必然出现且没有意义。
    nes::Logger::init(nes::LogLevel::Error, nes::LogTarget::Console);
    famicom->insertCartridge(cart);

    m_cartridge = cart;
    m_famicom = famicom;
    m_loaded = true;
    m_paused = false;
    m_pausedByLeave = false;
    m_romPath = path;
    m_title = QFileInfo(path).completeBaseName();
    m_message.clear();

    {
        QMutexLocker locker(&m_frameMutex);
        m_image.fill(Qt::black); // 先黑屏，别让上一局的最后一帧留在屏幕上
    }

    m_clock.start();
    m_nextFrameNs = 0;
    startAudio();
    m_tickTimer->start();

    qInfo() << "[nes] 打开游戏" << m_title << "mapper" << int(cart->mapperNumber());
    emit stateChanged();
}

void NesEmulator::closeRom()
{
    stopFrameLoop();

    if (m_famicom != nullptr) {
        m_famicom->ejectCartridge();
        delete m_famicom;
        m_famicom = nullptr;
    }
    delete m_cartridge;
    m_cartridge = nullptr;

    stopAudio();

    const bool wasLoaded = m_loaded;
    m_loaded = false;
    m_paused = false;
    m_pausedByLeave = false;
    m_title.clear();
    m_romPath.clear();
    if (wasLoaded) {
        emit stateChanged();
    }
}

void NesEmulator::stopFrameLoop()
{
    if (m_tickTimer != nullptr) {
        m_tickTimer->stop();
    }
    m_nextFrameNs = 0;
}

void NesEmulator::resetGame()
{
    if (m_famicom == nullptr) {
        return;
    }
    // 暂停状态下点「重来」：顺带恢复，否则定时器是停着的，画面会一直定住。
    if (m_paused) {
        togglePause();
    }
    m_famicom->reset();
    m_nextFrameNs = 0;
    m_clock.start();
    emit stateChanged();
}

void NesEmulator::togglePause()
{
    if (!m_loaded) {
        return;
    }
    // 走到这里就是「人按了暂停/继续」，之前那次自动暂停的标记立刻作废。
    m_pausedByLeave = false;
    m_paused = !m_paused;
    if (m_paused) {
        m_tickTimer->stop();
#ifdef KIDSSAFE_NES_AUDIO
        if (m_sink != nullptr) {
            m_sink->suspend();
        }
#endif
    } else {
        // 恢复时把时间基准重置，否则会把暂停的几秒钟当成「欠了 300 帧」。
        m_clock.start();
        m_nextFrameNs = 0;
        m_tickTimer->start();
#ifdef KIDSSAFE_NES_AUDIO
        if (m_sink != nullptr) {
            m_sink->resume();
        }
#endif
    }
    emit stateChanged();
}

void NesEmulator::setPaused(bool paused)
{
    if (!m_loaded || m_paused == paused) {
        return;
    }
    togglePause();
}

void NesEmulator::pauseForLeave()
{
    // 本来就在暂停（孩子自己按的）：什么都不做，回来时也不该自动跑起来。
    if (!m_loaded || m_paused) {
        return;
    }
    togglePause();
    // togglePause 会把标记清掉，所以要在它之后设回来。
    m_pausedByLeave = true;
}

void NesEmulator::resumeFromLeave()
{
    if (!m_loaded || !m_paused || !m_pausedByLeave) {
        return;
    }
    togglePause();
}

// ---------------------------------------------------------------------------
// 输入
// ---------------------------------------------------------------------------

void NesEmulator::setButton(int button, bool pressed)
{
    if (m_famicom == nullptr || button < 0 || button > 7) {
        return;
    }
    m_famicom->userInput(static_cast<uint8_t>(button),
                         static_cast<uint8_t>(pressed ? 1 : 0));
}

void NesEmulator::releaseButtons()
{
    for (int i = 0; i < 8; ++i) {
        setButton(i, false);
    }
}

bool NesEmulator::keyEvent(int key, bool pressed)
{
    int button = -1;
    switch (key) {
    case Qt::Key_Left:
        button = PadLeft;
        break;
    case Qt::Key_Right:
        button = PadRight;
        break;
    case Qt::Key_Up:
        button = PadUp;
        break;
    case Qt::Key_Down:
        button = PadDown;
        break;
    // A / B 各给两个键：孩子习惯 Z / X，玩过模拟器的大人习惯 J / K。
    case Qt::Key_Z:
    case Qt::Key_J:
    case Qt::Key_Space:
        button = PadA;
        break;
    case Qt::Key_X:
    case Qt::Key_K:
        button = PadB;
        break;
    case Qt::Key_Return:
    case Qt::Key_Enter:
        button = PadStart;
        break;
    case Qt::Key_Shift:
    case Qt::Key_Backslash:
        button = PadSelect;
        break;
    default:
        return false;
    }

    setButton(button, pressed);
    return true;
}

// ---------------------------------------------------------------------------
// 存档
// ---------------------------------------------------------------------------

QString NesEmulator::statePath() const
{
    if (m_romPath.isEmpty()) {
        return {};
    }
    const QString dir = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation)
        + QStringLiteral("/kidssafe/saves");
    return dir + QLatin1Char('/') + QFileInfo(m_romPath).completeBaseName()
        + QStringLiteral(".nes.sav");
}

bool NesEmulator::saveState()
{
    if (m_famicom == nullptr || !m_loaded) {
        return false;
    }
    const std::vector<uint8_t> data = m_famicom->save();
    if (data.empty()) {
        return false;
    }
    const QString path = statePath();
    if (path.isEmpty()) {
        return false;
    }
    QDir().mkpath(QFileInfo(path).absolutePath());
    QFile file(path);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        setMessage(QStringLiteral("存档失败：磁盘不可写"));
        return false;
    }
    file.write(reinterpret_cast<const char *>(data.data()), qint64(data.size()));
    file.close();
    setMessage(QStringLiteral("已存档"));
    return true;
}

bool NesEmulator::loadState()
{
    if (m_famicom == nullptr || !m_loaded) {
        return false;
    }
    const QString path = statePath();
    QFile file(path);
    if (path.isEmpty() || !file.exists()) {
        setMessage(QStringLiteral("这个游戏还没有存档"));
        return false;
    }
    if (!file.open(QIODevice::ReadOnly)) {
        setMessage(QStringLiteral("存档读取失败"));
        return false;
    }
    const QByteArray raw = file.readAll();
    file.close();

    std::vector<uint8_t> data(raw.size());
    std::copy(raw.begin(), raw.end(), data.begin());
    if (!m_famicom->restore(data)) {
        setMessage(QStringLiteral("存档不兼容（换过 ROM？）"));
        return false;
    }
    m_nextFrameNs = 0;
    m_clock.start();
    setMessage(QStringLiteral("已读档"));
    return true;
}

// ---------------------------------------------------------------------------
// 帧循环
// ---------------------------------------------------------------------------

void NesEmulator::tick()
{
    if (m_famicom == nullptr || !m_loaded || m_paused) {
        return;
    }

    const qint64 now = m_clock.nsecsElapsed();
    if (m_nextFrameNs == 0) {
        m_nextFrameNs = now;
    }

    int frames = 0;
    while (now >= m_nextFrameNs && frames < kMaxCatchUp) {
        m_famicom->emulationFrame();
        pumpAudio();
        m_nextFrameNs += kFrameNs;
        ++frames;
    }

    // 落后超过 5 帧：说明刚从卡顿里出来（拖动窗口、系统冻结、断点）。
    // 直接对齐到「现在」，不要追帧——否则画面会突然快进。
    if (now - m_nextFrameNs > kFrameNs * 5) {
        m_nextFrameNs = now + kFrameNs;
    }

    if (frames > 0) {
        composeFrame();
        emit frameReady();
    }
}

void NesEmulator::composeFrame()
{
    if (m_famicom == nullptr) {
        return;
    }
    const uint8_t *canvas = m_famicom->getCanvas();
    if (canvas == nullptr) {
        return;
    }

    QMutexLocker locker(&m_frameMutex);
    for (int y = 0; y < kHeight; ++y) {
        auto *dst = reinterpret_cast<QRgb *>(m_image.scanLine(y));
        const uint8_t *src = canvas + y * kWidth;
        for (int x = 0; x < kWidth; ++x) {
            // 索引理论上只在 0~63，但 PPU 的调色板读取会带出 open-bus 高位，
            // 直接拿它当下标会越界 —— 这里掩一下最保险。
            dst[x] = kPalette[src[x] & 0x3F];
        }
    }
}

// ---------------------------------------------------------------------------
// 声音
// ---------------------------------------------------------------------------

void NesEmulator::startAudio()
{
#ifdef KIDSSAFE_NES_AUDIO
    if (m_sink != nullptr) {
        if (m_audioOut == nullptr) {
            m_audioOut = m_sink->start();
            m_audioReady = (m_audioOut != nullptr);
        }
        return;
    }

    QAudioFormat format;
    format.setSampleRate(44100);
    format.setChannelCount(1); // 核心输出单声道
    format.setSampleFormat(QAudioFormat::Int16);

    const QAudioDevice device = QMediaDevices::defaultAudioOutput();
    if (device.isNull() || !device.isFormatSupported(format)) {
        m_audioReady = false;
        qInfo() << "[nes] 没有可用的音频输出，静音运行";
        return;
    }

    m_sink = new QAudioSink(device, format, this);
    // 约 180ms 的缓冲：太小会因 GUI 线程抖动断音，太大按键声会明显延迟。
    m_sink->setBufferSize(44100 / 5 * 2);
    m_audioOut = m_sink->start();
    m_audioReady = (m_audioOut != nullptr);
#else
    m_audioReady = false;
#endif
}

void NesEmulator::stopAudio()
{
    m_audioPending.clear();
#ifdef KIDSSAFE_NES_AUDIO
    m_audioOut = nullptr;
    if (m_sink != nullptr) {
        m_sink->stop();
        // 直接 delete 而不是 deleteLater：stopAudio 只从 closeRom 调进来，
        // 不在 QAudioSink 自己的信号回调里，删掉是安全的，也不会在退出时
        // 留下一个待处理的事件。
        delete m_sink;
        m_sink = nullptr;
    }
#endif
    m_audioReady = false;
}

void NesEmulator::pumpAudio()
{
#ifdef KIDSSAFE_NES_AUDIO
    if (m_famicom == nullptr) {
        return;
    }

    // 1) 把核心这一帧产生的采样全部收下来。队列是 20 块的环形缓冲，
    //    一次取一块，最多取 8 块就够（一帧只产 3~4 块）。
    int guard = 0;
    while (guard++ < 8) {
        uint32_t count = 0;
        int16_t *samples = m_famicom->getSamples(&count);
        if (samples == nullptr || count == 0) {
            break;
        }
        m_audioPending.append(reinterpret_cast<const char *>(samples),
                              qsizetype(count) * 2);
    }

    if (m_audioOut == nullptr) {
        // 没有声卡（或者 KIDSSAFE_NES_AUDIO 没开）：别攒着，攒了也不会播。
        m_audioPending.clear();
        return;
    }

    // 2) 尽量写出去。QAudioSink 缓冲满的时候 write() 只写一部分，
    //    这里必须用返回值推进，否则剩下的半块就丢了 —— 那就是一次爆音。
    if (!m_audioPending.isEmpty()) {
        const qint64 wrote = m_audioOut->write(m_audioPending.constData(),
                                              m_audioPending.size());
        if (wrote > 0) {
            m_audioPending.remove(0, qsizetype(wrote));
        }
    }

    // 3) 兜底：画面按 60.0988fps 跑，每帧 735 个采样，合 44173 样本/秒，
    //    比声卡的 44100 多出 0.16%。长期下来待播数据会慢慢积压，
    //    表现为「按键声越来越晚」。攒到 1 秒就丢掉最旧的，宁可偶尔缺一点。
    constexpr int kMaxPending = 44100 * 2;
    if (m_audioPending.size() > kMaxPending) {
        m_audioPending.remove(0, m_audioPending.size() - kMaxPending);
    }
#endif
}

void NesEmulator::setMessage(const QString &text)
{
    m_message = text;
    emit stateChanged();

    if (text.isEmpty()) {
        return;
    }
    // 三秒后自动消失。存一份序号：这期间如果又来了新提示，别把新的也清了。
    const int seq = ++m_messageSeq;
    QTimer::singleShot(3000, this, [this, seq] {
        if (seq != m_messageSeq) {
            return;
        }
        m_message.clear();
        emit stateChanged();
    });
}
