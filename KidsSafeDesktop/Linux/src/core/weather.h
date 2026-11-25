#pragma once

#include <QDateTime>
#include <QObject>
#include <QPointer>
#include <QVariantList>

class QNetworkAccessManager;
class QNetworkReply;
class QTimer;
class QUrl;
class Settings;

/**
 * 首页天气卡的数据源。对应 C# 的 IWeatherService / WeatherService。
 *
 * 数据来自 Open-Meteo（免注册、免密钥、无需 AppKey），分两步：
 *   1. 定位：城市留空或 "auto" → ipwho.is 按出口 IP 定位；
 *            否则 → open-meteo 地理编码按城市名查经纬度。
 *   2. 取数：current（实况）+ hourly（逐小时），一次请求拿全。
 *
 * 设计要点：
 *   · 失败绝不弹窗、不报错。儿童桌面上跳出「网络异常」只会让孩子手足无措，
 *     拿不到新数据就静默沿用上一次的缓存，卡片角标写成「离线」。
 *   · 缓存落在 ~/.local/share/kidssafe/weather.json，**带经纬度**。
 *     这台机器开机后往往 Wi-Fi 还没连上，第一次请求必然失败；缓存让
 *     「上周三的 19°」也总比空白强，也让定位服务挂掉时仍能按旧坐标取预报。
 *   · 「操作被取消」不是被谁取消，而是 QNetworkRequest::setTransferTimeout
 *     到点后 abort 出来的 OperationCanceledError。必须翻译成人话，否则家长
 *     以为是自己误触了什么地方。
 *   · 失败按 30s → 60s → 2min → 5min → 15min 退避重试，成功即归零。
 *     原来失败一次要空等 30 分钟，开机那一下网络没起来就整块空白半小时。
 *   · 换城市 / 改配置要立刻生效：新请求取代旧请求（abort + 代数校验），
 *     旧回复晚到也不会覆盖新结果。原来用「在途就 return」挡重复请求，
 *     家长改完城市还得等 30 分钟。
 *
 * 天气图标不给 emoji：deepin 上没有彩色 emoji 字体，🌤 会渲染成黑白线稿，
 * 这里只回一个图标名（sun / partly / cloud / rain / snow / thunder / fog），
 * 由 QML 的 GlyphIcon 手绘。
 */
class Weather : public QObject
{
    Q_OBJECT

    /// 是否已有可显示的数据（含缓存）。
    Q_PROPERTY(bool available READ available NOTIFY changed)
    Q_PROPERTY(bool loading READ loading NOTIFY changed)
    /// 定位到的城市名，例如「达州」。
    Q_PROPERTY(QString city READ city NOTIFY changed)
    /// 已取整的温度文案，例如「19°」。
    Q_PROPERTY(QString tempText READ tempText NOTIFY changed)
    /// 中文天气描述，例如「阴」。
    Q_PROPERTY(QString descText READ descText NOTIFY changed)
    Q_PROPERTY(QString glyph READ glyph NOTIFY changed)
    /// 卡片右下角的小字：「更新于 20:10」/「离线 · 20:10 的数据」。
    Q_PROPERTY(QString statusText READ statusText NOTIFY changed)
    /// 最近一次失败的人话原因（家长后台显示）。成功或有缓存时为空。
    Q_PROPERTY(QString problemText READ problemText NOTIFY changed)
    /// 逐小时预报：[{ label, temp, glyph, precip }]，第一项是「现在」。
    Q_PROPERTY(QVariantList hours READ hours NOTIFY changed)

public:
    explicit Weather(Settings *settings, QObject *parent = nullptr);

    bool available() const { return m_available; }
    bool loading() const { return m_loading; }
    QString city() const { return m_city; }
    QString tempText() const;
    QString descText() const { return m_desc; }
    QString glyph() const { return m_glyph; }
    QString statusText() const;
    QString problemText() const { return m_lastProblem; }
    QVariantList hours() const { return m_hours; }

    /// 手动刷新：立刻作废在途请求，按当前设置重来。
    Q_INVOKABLE void refresh();

signals:
    void changed();

private:
    void beginFetch();
    void locateByIp(int gen);
    void geocode(const QString &city, int gen);
    void fetchForecast(double latitude, double longitude, const QString &cityName, int gen);
    void applyPayload(const QByteArray &body,
                      const QString &cityName,
                      const QDateTime &stamp,
                      double latitude,
                      double longitude);
    void succeed();
    void fail(const QString &reason, bool retry = true);
    void scheduleRetry();
    void cancelInFlight();
    void setLoading(bool value);
    void loadCache();
    void saveCache() const;
    bool hasCachedLocation() const { return m_lat != 0.0 || m_lon != 0.0; }

    QNetworkReply *get(const QUrl &url);

    /// 把 QNetworkReply 的错误码翻成人话。重点是把 OperationCanceledError
    /// 说成「超时」——见头文件顶部说明。
    static QString describeNetworkError(QNetworkReply *reply);
    static QString describe(int code);
    static QString glyphFor(int code);

    Settings *m_settings = nullptr;
    QNetworkAccessManager *m_net = nullptr;
    QTimer *m_timer = nullptr;      ///< 30 分钟常规刷新
    QTimer *m_retryTimer = nullptr; ///< 失败后的退避重试

    QPointer<QNetworkReply> m_reply; ///< 当前在途请求（用于主动作废）
    int m_generation = 0;            ///< 请求代次：旧代次的回复一律丢弃
    int m_failures = 0;
    bool m_loading = false;
    bool m_available = false;
    bool m_fromCache = false;

    // Settings::changed 的去重快照：只有「城市 / 开关真的变了」才重新请求
    bool m_enabledSnapshot = true;
    QString m_citySnapshot;

    // 日志去重：同样的失败原因只打第一次，之后折叠成计数，
    // 免得 kiosk 挂一晚上刷几千行一样的 WARN。
    QString m_warnedProblem;
    int m_warnRepeat = 0;

    QString m_lastProblem;
    QString m_city;
    /// 当前 m_lat/m_lon 是「按哪个城市设置」解析出来的（"auto" 或城市名）。
    /// 同一个设置再次刷新时直接取预报，省掉一次定位/地理编码请求 ——
    /// 请求少一次，超时的概率就低一半。只在本次运行内有效，不写进缓存。
    QString m_resolvedFor;
    double m_temp = 0.0;
    int m_code = 0;
    QString m_desc;
    QString m_glyph = QStringLiteral("cloud");
    QVariantList m_hours;
    QDateTime m_updatedAt;

    // 最近一次成功取数用的坐标，缓存里也存一份
    double m_lat = 0.0;
    double m_lon = 0.0;
};
