#pragma once

#include <QObject>
#include <QString>
#include <QTimer>

class Settings;

/**
 * 护眼模式的计算层，对应 C# 的 Helpers/EyeCare.cs → EyeCareRules。
 *
 * 这里只负责回答三个问题：现在要不要开、开多大、家长该怎么看到状态。
 * 真正的遮罩画在 QML 里（Main.qml 顶层那个 Rectangle），理由和 Windows 版
 * 在原生窗口上叠一层是同一个：遮罩必须在所有页面之上、且不能拦截点击。
 *
 * 两条与 C# 保持一致的规则：
 *   1. 强度上限 55%，再高颜色会失真，孩子看绘本会觉得画面发黄发脏；
 *   2. 定时时段支持跨零点（19:00 - 07:00），判断方式是
 *      start < end ? (h >= start && h < end) : (h >= start || h < end)。
 *
 * Linux 侧的一处限制要说清楚：遮罩只覆盖儿童桌面自己的窗口。孩子被放行到
 * 外部应用时，那个应用的窗口不受我们控制——这与 Windows 版的行为一致，
 * 真正的全屏级滤镜需要合成器插件（KWin effect），不在这一版的范围里。
 */
class EyeCare : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool active READ active NOTIFY changed)
    /// 0~55，QML 里除以 100 当 opacity 用。
    Q_PROPERTY(int opacityPercent READ opacityPercent NOTIFY changed)
    Q_PROPERTY(QString statusText READ statusText NOTIFY changed)
    Q_PROPERTY(QString rangeText READ rangeText NOTIFY changed)

public:
    explicit EyeCare(Settings *settings, QObject *parent = nullptr);

    bool active() const;
    int opacityPercent() const;
    QString statusText() const;
    QString rangeText() const;

    static constexpr int kMaxOpacityPercent = 55;

    /// 每分钟重算一次，跨入/跨出定时时段时自动生效。家长改设置时也会调用。
    Q_INVOKABLE void refresh();

Q_SIGNALS:
    void changed();

private:
    bool inAutoWindow() const;

    Settings *m_settings = nullptr;
    QTimer m_timer;
};
