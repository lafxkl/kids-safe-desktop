#include "eyecare.h"
#include "settings.h"

#include <QTime>

namespace {
/// 定时检查间隔。一分钟足够：家长设的都是整点，最坏迟到一分钟。
constexpr int kTickMs = 60 * 1000;
}

EyeCare::EyeCare(Settings *settings, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
{
    // 家长在后台拖动强度滑块时 save() 会发 changed，这里跟着重算，
    // 遮罩就会实时变化——不需要「应用」按钮。
    connect(m_settings, &Settings::changed, this, &EyeCare::refresh);

    m_timer.setInterval(kTickMs);
    connect(&m_timer, &QTimer::timeout, this, &EyeCare::refresh);
    m_timer.start();
}

bool EyeCare::inAutoWindow() const
{
    const int start = m_settings->eyeCareStartHour();
    const int end = m_settings->eyeCareEndHour();

    // 起止相同视为「没设置」，而不是「全天」。全天遮罩会把画面一直压暗，
    // 家长误触成 19:00-19:00 的可能性远大于真想开全天。
    if (start == end) {
        return false;
    }

    const int hour = QTime::currentTime().hour();
    return start < end ? (hour >= start && hour < end)
                       : (hour >= start || hour < end);
}

bool EyeCare::active() const
{
    if (m_settings->eyeCareEnabled()) {
        return true;
    }
    if (!m_settings->eyeCareAutoEnabled()) {
        return false;
    }
    return inAutoWindow();
}

int EyeCare::opacityPercent() const
{
    if (!active()) {
        return 0;
    }
    const int intensity = qBound(0, m_settings->eyeCareIntensity(), 100);
    return qRound(intensity / 100.0 * kMaxOpacityPercent);
}

QString EyeCare::rangeText() const
{
    const int start = m_settings->eyeCareStartHour();
    const int end = m_settings->eyeCareEndHour();
    return QStringLiteral("%1:00 - %2:00")
        .arg(start, 2, 10, QLatin1Char('0'))
        .arg(end, 2, 10, QLatin1Char('0'));
}

QString EyeCare::statusText() const
{
    if (active()) {
        if (m_settings->eyeCareEnabled()) {
            return QStringLiteral("当前生效中（常开，强度 %1%）")
                .arg(opacityPercent());
        }
        return QStringLiteral("当前生效中（定时 %1，强度 %2%）")
            .arg(rangeText())
            .arg(opacityPercent());
    }

    if (m_settings->eyeCareAutoEnabled()) {
        if (m_settings->eyeCareStartHour() == m_settings->eyeCareEndHour()) {
            return QStringLiteral("起止时间相同，定时不会生效");
        }
        return QStringLiteral("当前未生效，%1 会自动开启").arg(rangeText());
    }

    return QStringLiteral("已关闭");
}

void EyeCare::refresh()
{
    // 值本身是算出来的，没有缓存要维护；发一次信号让 QML 重新求值即可。
    Q_EMIT changed();
}
