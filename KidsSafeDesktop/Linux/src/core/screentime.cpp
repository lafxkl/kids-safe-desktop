#include "screentime.h"
#include "settings.h"

#include <QTimer>
#include <KConfigGroup>
#include <KSharedConfig>

namespace {
constexpr auto kGroup = "ScreenTime";

KConfigGroup screenTimeGroup()
{
    auto config = KSharedConfig::openConfig(QStringLiteral("kidssafe/kidssafe.conf"));
    return KConfigGroup(config, kGroup);
}
}

ScreenTime::ScreenTime(Settings *settings, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
    , m_timer(new QTimer(this))
{
    load();

    m_timer->setInterval(1000);
    m_timer->setTimerType(Qt::CoarseTimer);
    connect(m_timer, &QTimer::timeout, this, &ScreenTime::tick);

    // 家长在后台改时长的同时，剩余时间要立刻反映到首页
    connect(m_settings, &Settings::changed, this, [this] {
        Q_EMIT remainingChanged();
        Q_EMIT lockedOutChanged();
    });
}

void ScreenTime::load()
{
    KConfigGroup group = screenTimeGroup();
    m_day = QDate::fromString(group.readEntry("Day", QString()), Qt::ISODate);
    m_usedSeconds = group.readEntry("UsedSeconds", 0);
    rolloverIfNeeded();
}

void ScreenTime::persist()
{
    KConfigGroup group = screenTimeGroup();
    group.writeEntry("Day", m_day.toString(Qt::ISODate));
    group.writeEntry("UsedSeconds", m_usedSeconds);
    group.config()->sync();
}

void ScreenTime::rolloverIfNeeded()
{
    const QDate today = QDate::currentDate();
    if (m_day != today) {
        m_day = today;
        m_usedSeconds = 0;
        m_limitEmitted = false;
        persist();
    }
}

int ScreenTime::limitSeconds() const
{
    return m_settings->dailyLimitMinutes() * 60;
}

int ScreenTime::remainingSeconds() const
{
    return qMax(0, limitSeconds() - m_usedSeconds);
}

QString ScreenTime::remainingText() const
{
    const int total = remainingSeconds();
    return QStringLiteral("%1:%2")
        .arg(total / 60)
        .arg(total % 60, 2, 10, QLatin1Char('0'));
}

void ScreenTime::start()
{
    rolloverIfNeeded();
    m_running = true;
    if (!m_timer->isActive()) {
        m_timer->start();
    }
    Q_EMIT remainingChanged();
    Q_EMIT lockedOutChanged();
}

void ScreenTime::pause()
{
    m_running = false;
    m_timer->stop();
    persist();
}

void ScreenTime::tick()
{
    if (!m_running) {
        return;
    }

    rolloverIfNeeded();

    if (remainingSeconds() > 0) {
        ++m_usedSeconds;
    }

    // 每 15 秒落盘一次，避免每秒写磁盘（KConfig 的 sync 是 fsync 级别）
    if (m_usedSeconds % 15 == 0) {
        persist();
    }

    Q_EMIT remainingChanged();

    if (lockedOut() && !m_limitEmitted) {
        m_limitEmitted = true;
        persist();
        Q_EMIT lockedOutChanged();
        Q_EMIT limitReached();
    }
}

void ScreenTime::extend(int minutes)
{
    m_usedSeconds = qMax(0, m_usedSeconds - minutes * 60);
    m_limitEmitted = false;
    persist();
    Q_EMIT remainingChanged();
    Q_EMIT lockedOutChanged();
}

void ScreenTime::resetToday()
{
    m_usedSeconds = 0;
    m_day = QDate::currentDate();
    m_limitEmitted = false;
    persist();
    Q_EMIT remainingChanged();
    Q_EMIT lockedOutChanged();
}
