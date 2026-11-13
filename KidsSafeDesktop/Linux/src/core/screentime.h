#pragma once

#include <QObject>
#include <QDate>

class QTimer;
class Settings;

/**
 * 对应 C# 的 IScreenTimeService。
 *
 * 与 Windows/Android 版的差异：Linux 桌面没有「应用被系统切到后台」这个可靠信号，
 * 而 kiosk 会话下本应用就是唯一的 shell，所以这里按「进程存活即计时」处理，
 * 只在应用真的挂起（KWin 最小化/切换虚拟桌面，正常情况下不会发生）时由 QML 暂停。
 *
 * 跨天处理：每次 tick 检查日期戳，跨零点自动清零，避免孩子凌晨还能用昨天的余额。
 */
class ScreenTime : public QObject
{
    Q_OBJECT

    Q_PROPERTY(int remainingSeconds READ remainingSeconds NOTIFY remainingChanged)
    Q_PROPERTY(int usedSecondsToday READ usedSecondsToday NOTIFY remainingChanged)
    Q_PROPERTY(int limitSeconds READ limitSeconds NOTIFY remainingChanged)
    Q_PROPERTY(bool lockedOut READ lockedOut NOTIFY lockedOutChanged)
    Q_PROPERTY(QString remainingText READ remainingText NOTIFY remainingChanged)

public:
    explicit ScreenTime(Settings *settings, QObject *parent = nullptr);

    int remainingSeconds() const;
    int usedSecondsToday() const { return m_usedSeconds; }
    int limitSeconds() const;
    bool lockedOut() const { return remainingSeconds() <= 0; }
    QString remainingText() const;

    /// 开始计时（进入儿童桌面时调用）。
    Q_INVOKABLE void start();

    /// 暂停计时（打开外部应用时调用，把时间算给那个应用而不是桌面）。
    Q_INVOKABLE void pause();

    /// 家长延长时长（分钟）。
    Q_INVOKABLE void extend(int minutes);

    /// 清空今日已用时长。
    Q_INVOKABLE void resetToday();

signals:
    void remainingChanged();
    void lockedOutChanged();
    void limitReached();

private:
    void tick();
    void persist();
    void load();
    void rolloverIfNeeded();

    Settings *m_settings = nullptr;
    QTimer *m_timer = nullptr;
    QDate m_day;
    int m_usedSeconds = 0;
    bool m_running = false;
    bool m_limitEmitted = false;
};
