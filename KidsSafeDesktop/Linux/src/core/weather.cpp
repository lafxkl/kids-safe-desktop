#include "weather.h"
#include "settings.h"

#include <QByteArray>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QStandardPaths>
#include <QTimer>
#include <QUrl>
#include <QUrlQuery>
#include <QtMath>

// QNetworkInformation 自 Qt 6.1 起提供，用来监听「网络回来了」。
// 老一点的发行版可能裁掉了后端，所以整体做成可选：拿不到就退回纯定时重试。
#if defined(__has_include)
#  if __has_include(<QNetworkInformation>)
#    include <QNetworkInformation>
#    define KIDSSAFE_HAS_NETINFO 1
#  endif
#endif

namespace {

constexpr int kTimeoutMs = 8000;
constexpr int kRefreshMs = 30 * 60 * 1000;
constexpr int kHourCount = 4;

/// 失败后的重试间隔（秒）。第一次多半是开机时 Wi-Fi 还没连上，
/// 30 秒后再试基本就好了；越往后越稀疏，最长 15 分钟一次。
constexpr int kRetrySteps[] = {30, 60, 120, 300, 900};
constexpr int kRetryStepCount = int(sizeof(kRetrySteps) / sizeof(kRetrySteps[0]));

/// 固定放 ~/.local/share/kidssafe/，和日志同目录：出问题时家长只用看一个地方。
QString cachePath()
{
    return QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation)
           + QStringLiteral("/kidssafe/weather.json");
}

} // namespace

Weather::Weather(Settings *settings, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
    , m_net(new QNetworkAccessManager(this))
{
    loadCache();

    // 常规刷新：30 分钟一次。只有拿到数据才算「刷新成功」，
    // 失败的调度交给 m_retryTimer，两者互不干扰。
    m_timer = new QTimer(this);
    m_timer->setInterval(kRefreshMs);
    connect(m_timer, &QTimer::timeout, this, &Weather::refresh);
    m_timer->start();

    m_retryTimer = new QTimer(this);
    m_retryTimer->setSingleShot(true);
    connect(m_retryTimer, &QTimer::timeout, this, &Weather::refresh);

    // 网络恢复立刻补一次。台式机插网线、笔记本开热点、路由器重启完，
    // 用户不用等 30 分钟，也不用去家长后台点「刷新」。
#ifdef KIDSSAFE_HAS_NETINFO
    if (QNetworkInformation::loadDefaultBackend()) {
        if (auto *info = QNetworkInformation::instance()) {
            connect(info, &QNetworkInformation::reachabilityChanged, this,
                    [this](QNetworkInformation::Reachability reachability) {
                if (reachability != QNetworkInformation::Reachability::Online) {
                    return;
                }
                if (!m_settings->weatherEnabled()) {
                    return;
                }
                // 联网了就把退避清零，马上重来一次
                m_failures = 0;
                m_retryTimer->stop();
                refresh();
            });
        }
    }
#endif

    // 延迟 1.5s 再联网：先让首屏和磁贴画出来，别和 QML 加载抢同一帧。
    QTimer::singleShot(1500, this, &Weather::refresh);

    // 家长在后台改了城市 / 关了开关，立刻按新配置重来一次。
    // 不能无条件 refresh：Settings::changed 在任何设置写入时都会发（包括
    // 昵称里每敲一个字），那样会打出成串的天气请求。
    connect(m_settings, &Settings::changed, this, [this] {
        const bool enabled = m_settings->weatherEnabled();
        const QString city = m_settings->weatherCity().trimmed();
        if (enabled == m_enabledSnapshot && city == m_citySnapshot) {
            return;
        }
        m_enabledSnapshot = enabled;
        m_citySnapshot = city;

        if (!enabled) {
            cancelInFlight();
            m_available = false;
            m_hours.clear();
            m_lastProblem.clear();
            emit changed();
            return;
        }
        // 立刻按新配置重来，并清掉退避计数（家长手动改完不该等重试间隔）。
        // 缓存里的经纬度不清：城市解析失败时只有当「新城市 == 当前显示的城市」
        // 才会拿它兜底，改成别的城市就不会误用旧坐标。
        m_failures = 0;
        m_retryTimer->stop();
        refresh();
    });
    m_enabledSnapshot = m_settings->weatherEnabled();
    m_citySnapshot = m_settings->weatherCity().trimmed();
}

// available() / loading() / city() 在头文件里就是内联的（都是一行 return），
// 这里不要再定义一遍，否则「redefinition of ...”」。
QString Weather::tempText() const
{
    if (!m_available) {
        return {};
    }
    return QString::number(qRound(m_temp)) + QStringLiteral("°");
}

QString Weather::statusText() const
{
    if (!m_available) {
        return m_loading ? QStringLiteral("正在获取…") : QString();
    }

    // 跨天的缓存只写「20:10」会让人以为是今天的，把日期带上。
    QString stamp;
    if (m_updatedAt.isValid()) {
        const bool today = m_updatedAt.date() == QDateTime::currentDateTime().date();
        stamp = m_updatedAt.toString(today ? QStringLiteral("HH:mm")
                                          : QStringLiteral("MM-dd HH:mm"));
    }

    if (m_fromCache) {
        return stamp.isEmpty() ? QStringLiteral("离线")
                               : QStringLiteral("离线 · %1 的数据").arg(stamp);
    }
    return stamp.isEmpty() ? QString() : QStringLiteral("更新于 %1").arg(stamp);
}

// ---------------------------------------------------------------------------
// 请求调度
// ---------------------------------------------------------------------------

void Weather::refresh()
{
    if (!m_settings->weatherEnabled()) {
        cancelInFlight();
        setLoading(false);
        return;
    }

#ifdef KIDSSAFE_HAS_NETINFO
    // 系统明确告诉我们「现在没网」时不要发请求：那只会白等 8 秒再报一次超时，
    // 而 reachabilityChanged 变 Online 时我们会自动重来。
    //
    // 枚举里没有 Offline：Qt 6 的定义是
    //   Unknown / Disconnected / Local / Site / Online
    // 「断网」是 Disconnected。Unknown 不拦——后端没加载或刚启动时就是 Unknown，
    // 拦下来会导致「明明是好的网络却永远不发请求」。
    if (auto *info = QNetworkInformation::instance()) {
        if (info->reachability() == QNetworkInformation::Reachability::Disconnected) {
            cancelInFlight();
            setLoading(false);
            m_lastProblem = QStringLiteral("设备当前离线");
            if (m_available) {
                m_fromCache = true;
            }
            // 兜底：60 秒后再看一次。主路径是 reachabilityChanged 变 Online
            // 时立刻重来，但那条依赖系统后端真的报事件（有的有线网卡、
            // 有的容器里不会报）。不这么做的话，开机时没网就只能等 30 分钟
            // 的定时刷新。这一轮不发请求、不打日志，代价可以忽略。
            m_retryTimer->start(60 * 1000);
            emit changed();
            return;
        }
    }
#endif

    // 新请求取代旧请求。注意不能像早期版本那样「有在途请求就直接 return」——
    // 家长在请求途中把城市改成「上海」，那条请求会被直接吞掉，
    // 新城市要等 30 分钟后的定时器才生效。
    cancelInFlight();
    // 手动刷新 / 定时刷新进来时，把还挂着的退避重试取消掉：
    // 否则那条重试到点还会再发一次请求，白白多打一枪。
    m_retryTimer->stop();
    ++m_generation;
    setLoading(true);
    beginFetch();
}

void Weather::beginFetch()
{
    const QString want = m_settings->weatherCity().trimmed();
    const bool autoLocate = want.isEmpty()
        || want.compare(QLatin1String("auto"), Qt::CaseInsensitive) == 0;
    const QString key = autoLocate ? QStringLiteral("auto") : want;

    // 同一个城市设置、坐标已经解析过：直接取预报。
    // 定位 / 地理编码一旦成功，坐标在本次运行里不会变，没必要每 30 分钟
    // 再问一次第三方服务 —— 少一次请求，就少一次超时的机会。
    if (m_resolvedFor.compare(key, Qt::CaseInsensitive) == 0 && hasCachedLocation()) {
        fetchForecast(m_lat, m_lon, m_city, m_generation);
        return;
    }

    if (autoLocate) {
        locateByIp(m_generation);
    } else {
        geocode(want, m_generation);
    }
}

void Weather::locateByIp(int gen)
{
    QNetworkReply *reply = get(QUrl(QStringLiteral("https://ipwho.is/")));
    if (reply == nullptr) {
        fail(QStringLiteral("网络组件未就绪"));
        return;
    }

    connect(reply, &QNetworkReply::finished, this, [this, reply, gen] {
        reply->deleteLater();
        if (gen != m_generation) {
            return; // 已被新请求取代，这条回复作废
        }
        m_reply = nullptr;

        if (reply->error() != QNetworkReply::NoError) {
            // 定位服务不可达时，用缓存里的经纬度接着取预报：
            // 城市没变，坐标就没错，没必要因为一个第三方服务挂了就不显示天气。
            if (hasCachedLocation()) {
                qInfo() << "[weather] IP 定位失败，改用缓存坐标："
                        << describeNetworkError(reply);
                fetchForecast(m_lat, m_lon, m_city, gen);
                return;
            }
            fail(QStringLiteral("IP 定位失败：%1").arg(describeNetworkError(reply)));
            return;
        }

        const QJsonObject root = QJsonDocument::fromJson(reply->readAll()).object();
        // ipwho.is 失败时也返回 200，靠 success 字段区分
        if (!root.value(QStringLiteral("success")).toBool(true)) {
            fail(QStringLiteral("IP 定位被拒：%1")
                     .arg(root.value(QStringLiteral("message")).toString()));
            return;
        }
        const double lat = root.value(QStringLiteral("latitude")).toDouble();
        const double lon = root.value(QStringLiteral("longitude")).toDouble();
        if (lat == 0.0 && lon == 0.0) {
            fail(QStringLiteral("IP 定位没有返回经纬度"));
            return;
        }
        m_resolvedFor = QStringLiteral("auto");
        fetchForecast(lat, lon, root.value(QStringLiteral("city")).toString(), gen);
    });
}

void Weather::geocode(const QString &city, int gen)
{
    QUrl url(QStringLiteral("https://geocoding-api.open-meteo.com/v1/search"));
    QUrlQuery query;
    query.addQueryItem(QStringLiteral("name"), city);
    query.addQueryItem(QStringLiteral("count"), QStringLiteral("1"));
    query.addQueryItem(QStringLiteral("language"), QStringLiteral("zh"));
    query.addQueryItem(QStringLiteral("format"), QStringLiteral("json"));
    url.setQuery(query);

    QNetworkReply *reply = get(url);
    if (reply == nullptr) {
        fail(QStringLiteral("网络组件未就绪"));
        return;
    }

    connect(reply, &QNetworkReply::finished, this, [this, reply, city, gen] {
        reply->deleteLater();
        if (gen != m_generation) {
            return;
        }
        m_reply = nullptr;

        if (reply->error() != QNetworkReply::NoError) {
            // 同一个城市之前查过坐标，就先拿旧坐标顶着，等下一轮再纠正。
            if (city == m_city && hasCachedLocation()) {
                qInfo() << "[weather] 城市解析失败，改用缓存坐标："
                        << describeNetworkError(reply);
                fetchForecast(m_lat, m_lon, m_city, gen);
                return;
            }
            fail(QStringLiteral("城市解析失败：%1").arg(describeNetworkError(reply)));
            return;
        }

        const QJsonObject root = QJsonDocument::fromJson(reply->readAll()).object();
        const QJsonArray results = root.value(QStringLiteral("results")).toArray();
        if (results.isEmpty()) {
            // 接口通了、就是没这个城市：填错字重试一百次也没用，不参与退避重试。
            fail(QStringLiteral("找不到城市「%1」").arg(city), false);
            return;
        }
        const QJsonObject hit = results.at(0).toObject();
        const QString name = hit.value(QStringLiteral("name")).toString(city);
        m_resolvedFor = city;
        fetchForecast(hit.value(QStringLiteral("latitude")).toDouble(),
                      hit.value(QStringLiteral("longitude")).toDouble(),
                      name,
                      gen);
    });
}

void Weather::fetchForecast(double latitude, double longitude, const QString &cityName, int gen)
{
    QUrl url(QStringLiteral("https://api.open-meteo.com/v1/forecast"));
    QUrlQuery query;
    query.addQueryItem(QStringLiteral("latitude"), QString::number(latitude, 'f', 4));
    query.addQueryItem(QStringLiteral("longitude"), QString::number(longitude, 'f', 4));
    query.addQueryItem(QStringLiteral("current"),
                       QStringLiteral("temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m"));
    query.addQueryItem(QStringLiteral("hourly"),
                       QStringLiteral("temperature_2m,weather_code,precipitation_probability"));
    query.addQueryItem(QStringLiteral("forecast_days"), QStringLiteral("1"));
    // timezone=auto：返回的时间就是本地时间，卡片上不用再换算时区
    query.addQueryItem(QStringLiteral("timezone"), QStringLiteral("auto"));
    url.setQuery(query);

    QNetworkReply *reply = get(url);
    if (reply == nullptr) {
        fail(QStringLiteral("网络组件未就绪"));
        return;
    }

    connect(reply, &QNetworkReply::finished, this,
            [this, reply, cityName, latitude, longitude, gen] {
        reply->deleteLater();
        if (gen != m_generation) {
            return;
        }
        m_reply = nullptr;

        if (reply->error() != QNetworkReply::NoError) {
            fail(QStringLiteral("天气请求失败：%1").arg(describeNetworkError(reply)));
            return;
        }
        applyPayload(reply->readAll(), cityName, QDateTime::currentDateTime(),
                     latitude, longitude);
    });
}

void Weather::applyPayload(const QByteArray &body,
                           const QString &cityName,
                           const QDateTime &stamp,
                           double latitude,
                           double longitude)
{
    const QJsonObject root = QJsonDocument::fromJson(body).object();
    const QJsonObject current = root.value(QStringLiteral("current")).toObject();
    if (current.isEmpty()) {
        fail(QStringLiteral("天气接口没有返回 current 段"));
        return;
    }

    m_temp = current.value(QStringLiteral("temperature_2m")).toDouble();
    m_code = current.value(QStringLiteral("weather_code")).toInt();
    m_desc = describe(m_code);
    m_glyph = glyphFor(m_code);
    if (!cityName.isEmpty()) {
        m_city = cityName;
    }
    m_lat = latitude;
    m_lon = longitude;

    m_hours.clear();
    const QJsonObject hourly = root.value(QStringLiteral("hourly")).toObject();
    const QJsonArray times = hourly.value(QStringLiteral("time")).toArray();
    const QJsonArray temps = hourly.value(QStringLiteral("temperature_2m")).toArray();
    const QJsonArray codes = hourly.value(QStringLiteral("weather_code")).toArray();
    const QJsonArray precips = hourly.value(QStringLiteral("precipitation_probability")).toArray();

    // current.time 形如 2026-11-20T20:15，hourly.time 形如 2026-11-20T20:00，
    // 取左边 13 个字符（到小时）对比，找到「当前这个小时」的下标。
    const QString nowKey = current.value(QStringLiteral("time")).toString().left(13);
    int start = 0;
    for (int i = 0; i < times.size(); ++i) {
        if (times.at(i).toString().left(13) == nowKey) {
            start = i;
            break;
        }
    }

    for (int i = start; i < times.size() && m_hours.size() < kHourCount; ++i) {
        const QString time = times.at(i).toString();
        // 三个数组理论上等长，但接口偶尔会缺尾部数据，越界取值会让整页崩掉。
        if (i >= temps.size() || i >= codes.size()) {
            break;
        }
        const double temp = temps.at(i).toDouble();
        const int code = codes.at(i).toInt();

        QVariantMap hour;
        hour.insert(QStringLiteral("label"),
                    i == start ? QStringLiteral("现在") : time.mid(11, 5));
        hour.insert(QStringLiteral("temp"), QString::number(qRound(temp)) + QStringLiteral("°"));
        hour.insert(QStringLiteral("glyph"), glyphFor(code));
        hour.insert(QStringLiteral("precip"),
                    i < precips.size()
                        ? QString::number(precips.at(i).toInt()) + QStringLiteral("%")
                        : QString());
        m_hours.append(hour);
    }

    m_available = true;
    m_fromCache = false;
    m_updatedAt = stamp;
    succeed();
}

void Weather::succeed()
{
    m_failures = 0;
    m_retryTimer->stop();
    m_lastProblem.clear();
    // 成功后清掉日志去重状态，下次真出问题还能看到第一行 WARN
    m_warnedProblem.clear();
    m_warnRepeat = 0;

    setLoading(false);
    saveCache();
    emit changed();
}

void Weather::fail(const QString &reason, bool retry)
{
    m_reply = nullptr;
    m_lastProblem = reason;

    // 有旧数据就继续显示（角标标成离线），没有就整块不出现。
    if (m_available) {
        m_fromCache = true;
    }

    // 日志去重：同样的原因只打一次，后面的折叠成「又失败了 N 次」。
    // kiosk 常年不关，网络不好时每 30 秒一行 WARN 能把日志刷到几十兆。
    if (reason == m_warnedProblem) {
        ++m_warnRepeat;
    } else {
        if (m_warnRepeat > 0) {
            qWarning().noquote() << QStringLiteral("[weather] 上一个问题又出现了 %1 次")
                                        .arg(m_warnRepeat);
        }
        m_warnedProblem = reason;
        m_warnRepeat = 0;
        qWarning().noquote() << QStringLiteral("[weather]") << reason;
    }

    if (retry) {
        ++m_failures;
        scheduleRetry();
    } else {
        m_failures = 0;
    }

    setLoading(false);
    emit changed();
}

void Weather::scheduleRetry()
{
    int index = m_failures - 1;
    if (index < 0) {
        index = 0;
    }
    if (index >= kRetryStepCount) {
        index = kRetryStepCount - 1;
    }
    const int seconds = kRetrySteps[index];
    qInfo().noquote() << QStringLiteral("[weather] %1 秒后重试（第 %2 次）")
                             .arg(seconds)
                             .arg(m_failures);
    m_retryTimer->start(seconds * 1000);
}

void Weather::cancelInFlight()
{
    if (m_reply == nullptr) {
        return;
    }
    QNetworkReply *reply = m_reply;
    m_reply = nullptr;
    // 先断开再 abort：abort 之后 finished 还会再来一次，
    // 不先断开就会走进「失败」分支，把刚发起的新请求判成失败。
    reply->disconnect(this);
    reply->abort();
    reply->deleteLater();
}

void Weather::setLoading(bool value)
{
    if (m_loading == value) {
        return;
    }
    m_loading = value;
    emit changed();
}

QNetworkReply *Weather::get(const QUrl &url)
{
    QNetworkRequest request(url);
    // 注意：传输超时到点后 Qt 是 abort，错误码为 OperationCanceledError、
    // errorString 是 "Operation canceled"，中文即「操作被取消」。
    // 它跟「被谁取消」无关，纯粹是超时，翻译见 describeNetworkError()。
    request.setTransferTimeout(kTimeoutMs);
    request.setHeader(QNetworkRequest::UserAgentHeader,
                      QStringLiteral("KidsSafeDesktop/1.0 (Linux)"));

    QNetworkReply *reply = m_net->get(request);
    if (reply == nullptr) {
        return nullptr;
    }
    m_reply = reply;
    return reply;
}

QString Weather::describeNetworkError(QNetworkReply *reply)
{
    if (reply == nullptr) {
        return QStringLiteral("未知错误");
    }
    switch (reply->error()) {
    case QNetworkReply::OperationCanceledError:
        // 走到这里说明不是我们自己 abort 的（自己 abort 的回复已被代次过滤掉），
        // 那就是 setTransferTimeout 到点了。
        return QStringLiteral("超时（%1 秒没有响应）").arg(kTimeoutMs / 1000);
    case QNetworkReply::TimeoutError:
        return QStringLiteral("超时（%1 秒没有响应）").arg(kTimeoutMs / 1000);
    case QNetworkReply::HostNotFoundError:
        return QStringLiteral("域名解析失败（没联网？）");
    case QNetworkReply::NetworkSessionFailedError:
    case QNetworkReply::UnknownNetworkError:
        return QStringLiteral("网络未连接");
    case QNetworkReply::SslHandshakeFailedError:
        return QStringLiteral("HTTPS 握手失败（系统时间或证书有问题）");
    case QNetworkReply::TemporaryNetworkFailureError:
        return QStringLiteral("网络临时中断");
    case QNetworkReply::ProxyConnectionRefusedError:
    case QNetworkReply::ProxyNotFoundError:
    case QNetworkReply::ProxyTimeoutError:
        return QStringLiteral("代理服务器不可用");
    case QNetworkReply::ContentNotFoundError:
        return QStringLiteral("接口地址不存在（HTTP 404）");
    default:
        return reply->errorString();
    }
}

void Weather::loadCache()
{
    QFile file(cachePath());
    if (!file.open(QIODevice::ReadOnly)) {
        return;
    }
    const QJsonObject root = QJsonDocument::fromJson(file.readAll()).object();
    file.close();

    if (root.isEmpty()) {
        return;
    }
    m_city = root.value(QStringLiteral("city")).toString();
    m_temp = root.value(QStringLiteral("temp")).toDouble();
    m_code = root.value(QStringLiteral("code")).toInt();
    m_desc = root.value(QStringLiteral("desc")).toString();
    m_glyph = root.value(QStringLiteral("glyph")).toString(QStringLiteral("cloud"));
    m_updatedAt = QDateTime::fromString(root.value(QStringLiteral("updated")).toString(),
                                        Qt::ISODate);
    m_hours = root.value(QStringLiteral("hours")).toArray().toVariantList();
    // 老版本缓存没有经纬度，读出来是 0，hasCachedLocation() 自然为 false。
    m_lat = root.value(QStringLiteral("latitude")).toDouble();
    m_lon = root.value(QStringLiteral("longitude")).toDouble();
    m_available = !m_desc.isEmpty();
    m_fromCache = true;

    if (m_available) {
        qInfo() << "[weather] 载入缓存" << m_city << m_temp << m_desc;
    }
}

void Weather::saveCache() const
{
    QJsonObject root;
    root.insert(QStringLiteral("city"), m_city);
    root.insert(QStringLiteral("temp"), m_temp);
    root.insert(QStringLiteral("code"), m_code);
    root.insert(QStringLiteral("desc"), m_desc);
    root.insert(QStringLiteral("glyph"), m_glyph);
    root.insert(QStringLiteral("updated"), m_updatedAt.toString(Qt::ISODate));
    root.insert(QStringLiteral("hours"), QJsonArray::fromVariantList(m_hours));
    // 经纬度一起存：定位服务挂掉时还能按旧坐标取预报
    root.insert(QStringLiteral("latitude"), m_lat);
    root.insert(QStringLiteral("longitude"), m_lon);

    const QString path = cachePath();
    QDir().mkpath(QFileInfo(path).absolutePath());
    QFile file(path);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        qWarning() << "[weather] 缓存写入失败" << path;
        return;
    }
    file.write(QJsonDocument(root).toJson(QJsonDocument::Compact));
}

QString Weather::describe(int code)
{
    // WMO Weather interpretation codes，与 C# 端 MapDescription 一致
    switch (code) {
    case 0: return QStringLiteral("晴");
    case 1: return QStringLiteral("晴间多云");
    case 2: return QStringLiteral("多云");
    case 3: return QStringLiteral("阴");
    case 45:
    case 48: return QStringLiteral("雾");
    case 51:
    case 53:
    case 55: return QStringLiteral("毛毛雨");
    case 56:
    case 57: return QStringLiteral("冻雨");
    case 61: return QStringLiteral("小雨");
    case 63: return QStringLiteral("中雨");
    case 65: return QStringLiteral("大雨");
    case 66:
    case 67: return QStringLiteral("冻雨");
    case 71: return QStringLiteral("小雪");
    case 73: return QStringLiteral("中雪");
    case 75: return QStringLiteral("大雪");
    case 77: return QStringLiteral("米雪");
    case 80: return QStringLiteral("阵雨");
    case 81: return QStringLiteral("强阵雨");
    case 82: return QStringLiteral("暴雨");
    case 85:
    case 86: return QStringLiteral("阵雪");
    case 95: return QStringLiteral("雷阵雨");
    case 96:
    case 99: return QStringLiteral("雷雨冰雹");
    default: return QStringLiteral("多云");
    }
}

QString Weather::glyphFor(int code)
{
    if (code == 1) {
        return QStringLiteral("partly");
    }
    if (code == 2 || code == 3) {
        return QStringLiteral("cloud");
    }
    if (code == 45 || code == 48) {
        return QStringLiteral("fog");
    }
    if ((code >= 71 && code <= 77) || code == 85 || code == 86) {
        return QStringLiteral("snow");
    }
    if (code >= 95) {
        return QStringLiteral("thunder");
    }
    if (code >= 51) {
        return QStringLiteral("rain");
    }
    return QStringLiteral("sun");
}
