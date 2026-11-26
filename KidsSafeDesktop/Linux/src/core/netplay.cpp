#include "netplay.h"
#include "settings.h"

#include <QJsonDocument>
#include <QJsonParseError>
#include <QJsonValue>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QTimer>
#include <QUrl>
#include <QUrlQuery>

// 版本号由 CMake 注入；单独编译这个文件时给个兜底，和 learnreport.cpp 一致。
#ifndef KIDSSAFE_APP_VERSION
#define KIDSSAFE_APP_VERSION "1.19.0"
#endif

namespace {
// 1.5 秒一次：棋类一步要好几秒，这个频率足够；再快只是白耗电。
constexpr int kPollMs = 1500;
constexpr int kTimeoutMs = 5000;
// 连续失败几次才改提示语：偶尔丢一个包不该让孩子看到「连不上」。
constexpr int kFailuresToWarn = 3;

/// 与 StoryService / LearnReport 同一套规则：补前缀、去尾部斜杠。
/// 三处各留一份是有意的 —— 它们属于三个独立模块，抽成公共函数反而让
/// 「改故事地址」和「改联机地址」绑在一起，将来想分开都难。
QString normalize(const QString &raw)
{
    QString s = raw.trimmed();
    if (s.isEmpty()) {
        return {};
    }
    if (!s.startsWith(QLatin1String("http://"), Qt::CaseInsensitive)
        && !s.startsWith(QLatin1String("https://"), Qt::CaseInsensitive)) {
        s.prepend(QLatin1String("http://"));
    }
    while (s.endsWith(QLatin1Char('/'))) {
        s.chop(1);
    }
    return s;
}
}

Netplay::Netplay(Settings *settings, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
    , m_net(new QNetworkAccessManager(this))
    , m_pollTimer(new QTimer(this))
{
    m_pollTimer->setInterval(kPollMs);
    connect(m_pollTimer, &QTimer::timeout, this, &Netplay::poll);

    // 家长在后台改开关要立刻生效。关掉就把孩子从房间里请出来：
    // 比「已经关了两小时后才生效」安全，也比悄悄继续发请求诚实。
    connect(m_settings, &Settings::changed, this, [this] {
        Q_EMIT changed();
        if (!canPlay() && !m_code.isEmpty()) {
            leave();
        }
    });
}

bool Netplay::enabled() const
{
    return canPlay();
}

bool Netplay::canPlay() const
{
    return m_settings->onlineContentEnabled()
        && m_settings->netplayEnabled()
        && !baseUrl().isEmpty();
}

QString Netplay::blockedReason() const
{
    if (baseUrl().isEmpty()) {
        return QStringLiteral("还没填服务器地址，请家长在后台设置");
    }
    if (!m_settings->onlineContentEnabled()) {
        return QStringLiteral("家长还没打开「在线故事与内容」");
    }
    if (!m_settings->netplayEnabled()) {
        return QStringLiteral("家长还没打开「联机对战」");
    }
    return QString();
}

QString Netplay::baseUrl() const
{
    return normalize(m_settings->serverUrl());
}

bool Netplay::myTurn() const
{
    if (m_state != QLatin1String("playing") || m_seat.isEmpty()) {
        return false;
    }
    return m_room.value(QStringLiteral("turn")).toString() == m_seat;
}

// ---- 状态 -----------------------------------------------------

void Netplay::setState(const QString &state, const QString &detail)
{
    if (m_state == state && m_detail == detail) {
        return;
    }
    m_state = state;
    m_detail = detail;
    Q_EMIT changed();
}

void Netplay::setDetail(const QString &detail)
{
    if (m_detail == detail) {
        return;
    }
    m_detail = detail;
    Q_EMIT changed();
}

void Netplay::resetLocal()
{
    m_pollTimer->stop();
    m_code.clear();
    m_seat.clear();
    m_token.clear();
    m_room.clear();
    m_failures = 0;
    Q_EMIT roomChanged();
}

void Netplay::startPolling(bool on)
{
    if (on && canPlay() && !m_code.isEmpty() && !m_token.isEmpty()) {
        // start() 而非 isActive 判断：每次都重置间隔，让「响应回来 + 1.5 秒」
        // 成为真实节奏，服务器慢的时候请求不会越堆越多。
        m_pollTimer->start();
    } else {
        m_pollTimer->stop();
    }
}

// ---- 房主 / 客人 -----------------------------------------------

void Netplay::createRoom(const QString &game, int level)
{
    if (!canPlay()) {
        const QString reason = blockedReason();
        setState(QStringLiteral("disabled"), reason);
        Q_EMIT failed(reason);
        return;
    }
    if (!m_code.isEmpty()) {
        // 已经在一个房间里：先退掉，别在服务器上留一个没人管的房间
        leave();
    }
    resetLocal();
    setState(QStringLiteral("creating"), QStringLiteral("正在开房间…"));

    QJsonObject body;
    body.insert(QStringLiteral("game"), game);
    body.insert(QStringLiteral("level"), level);
    post(QStringLiteral("create"), body);
}

void Netplay::joinRoom(const QString &code)
{
    const QString cleaned = code.trimmed();
    if (!canPlay()) {
        const QString reason = blockedReason();
        setState(QStringLiteral("disabled"), reason);
        Q_EMIT failed(reason);
        return;
    }
    if (cleaned.size() != 4) {
        Q_EMIT failed(QStringLiteral("房间号是 4 个数字"));
        return;
    }
    if (!m_code.isEmpty()) {
        leave();
    }
    resetLocal();
    setState(QStringLiteral("joining"), QStringLiteral("正在进房间…"));

    QJsonObject body;
    body.insert(QStringLiteral("code"), cleaned);
    post(QStringLiteral("join"), body);
}

void Netplay::leave()
{
    const QString code = m_code;
    const QString token = m_token;

    resetLocal();
    setState(QStringLiteral("idle"), QString());

    if (code.isEmpty() || token.isEmpty() || !canPlay()) {
        return;
    }

    QJsonObject body;
    body.insert(QStringLiteral("code"), code);
    body.insert(QStringLiteral("token"), token);

    QNetworkRequest request(QUrl(baseUrl() + QStringLiteral("/api/game/leave")));
    request.setTransferTimeout(kTimeoutMs);
    request.setHeader(QNetworkRequest::ContentTypeHeader, QStringLiteral("application/json"));

    // 不看响应：孩子已经点「离开」了，界面得马上走。这里只负责告诉服务器
    // 房间可以收掉；失败也无所谓，服务端 30 分钟会自己回收。
    if (QNetworkReply *reply = m_net->post(request, QJsonDocument(body).toJson(QJsonDocument::Compact))) {
        connect(reply, &QNetworkReply::finished, reply, &QObject::deleteLater);
    }
}

// ---- 对局中 ---------------------------------------------------

void Netplay::sendMove(int from, int to, const QString &promo)
{
    if (m_code.isEmpty() || m_token.isEmpty()) {
        return;
    }
    // 这里不预判「轮到我了吗」：本地的 turn 可能比服务器滞后半个轮询周期，
    // 预判错了就是「孩子点了却走不动」，而错误的那种情况反而更难查。
    // 轮次由服务端裁决，不是你的回合它返回 409，界面自己会静默掉。

    QJsonObject body;
    body.insert(QStringLiteral("code"), m_code);
    body.insert(QStringLiteral("token"), m_token);
    body.insert(QStringLiteral("from"), from);
    body.insert(QStringLiteral("to"), to);
    body.insert(QStringLiteral("promo"), promo);
    post(QStringLiteral("move"), body);
}

void Netplay::reportChessResult(const QString &result)
{
    if (m_code.isEmpty() || m_token.isEmpty()) {
        return;
    }
    QJsonObject body;
    body.insert(QStringLiteral("code"), m_code);
    body.insert(QStringLiteral("token"), m_token);
    body.insert(QStringLiteral("result"), result);
    post(QStringLiteral("finish"), body);
}

void Netplay::reportKlotskiFinish(int steps)
{
    if (m_code.isEmpty() || m_token.isEmpty()) {
        return;
    }
    QJsonObject body;
    body.insert(QStringLiteral("code"), m_code);
    body.insert(QStringLiteral("token"), m_token);
    body.insert(QStringLiteral("steps"), steps);
    post(QStringLiteral("finish"), body);
}

void Netplay::refresh()
{
    if (m_code.isEmpty() || m_token.isEmpty()) {
        return;
    }
    poll();
}

// ---- 传输 -----------------------------------------------------

void Netplay::poll()
{
    if (!canPlay() || m_code.isEmpty() || m_token.isEmpty()) {
        startPolling(false);
        return;
    }

    QUrl url(baseUrl() + QStringLiteral("/api/game/state"));
    QUrlQuery query;
    query.addQueryItem(QStringLiteral("code"), m_code);
    query.addQueryItem(QStringLiteral("token"), m_token);
    url.setQuery(query);

    QNetworkRequest request(url);
    request.setTransferTimeout(kTimeoutMs);
    request.setHeader(QNetworkRequest::UserAgentHeader,
                      QStringLiteral("KidsSafeDesktop/" KIDSSAFE_APP_VERSION " (linux)"));

    ++m_inflight;
    Q_EMIT changed();
    QNetworkReply *reply = m_net->get(request);
    connect(reply, &QNetworkReply::finished, this, [this, reply] {
        handle(reply, true);
    });
}

void Netplay::post(const QString &action, const QJsonObject &body)
{
    QNetworkRequest request(QUrl(baseUrl() + QStringLiteral("/api/game/") + action));
    request.setTransferTimeout(kTimeoutMs);
    request.setHeader(QNetworkRequest::ContentTypeHeader, QStringLiteral("application/json"));
    request.setHeader(QNetworkRequest::UserAgentHeader,
                      QStringLiteral("KidsSafeDesktop/" KIDSSAFE_APP_VERSION " (linux)"));

    ++m_inflight;
    Q_EMIT changed();
    QNetworkReply *reply = m_net->post(request, QJsonDocument(body).toJson(QJsonDocument::Compact));
    connect(reply, &QNetworkReply::finished, this, [this, reply] {
        handle(reply, false);
    });
}

void Netplay::handle(QNetworkReply *reply, bool fromPoll)
{
    reply->deleteLater();
    if (m_inflight > 0) {
        --m_inflight;
    }
    // 先通知一次「有一个请求回来了」：下面几个分支里 setDetail 遇到相同文案
    // 会提前返回，busy 就不会刷新，按钮会一直停在禁用态。
    Q_EMIT changed();

    const QByteArray raw = reply->readAll();
    const int status =
        reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();

    // status == 0：连 DNS 都没通、或者超时。房间和 token 都留着 ——
    // 孩子可能只是网抖了一下，恢复后下一轮轮询就能接上。
    if (status == 0) {
        if (++m_failures >= kFailuresToWarn) {
            setDetail(QStringLiteral("连不上服务器，正在重试…"));
        }
        if (!fromPoll && (m_state == QLatin1String("creating")
                          || m_state == QLatin1String("joining"))) {
            setState(QStringLiteral("error"), QStringLiteral("连不上服务器，检查一下网络"));
            Q_EMIT failed(QStringLiteral("连不上服务器，检查一下网络"));
        } else {
            Q_EMIT changed();
        }
        return;
    }

    m_failures = 0;

    QJsonObject obj;
    {
        QJsonParseError error;
        const QJsonDocument doc = QJsonDocument::fromJson(raw, &error);
        if (error.error == QJsonParseError::NoError && doc.isObject()) {
            obj = doc.object();
        }
    }

    if (status >= 400) {
        QString message = obj.value(QStringLiteral("error")).toString();
        if (message.isEmpty()) {
            message = QStringLiteral("服务器拒绝了这次请求（%1）").arg(status);
        }

        // 403 / 404 且手里已经有房间号：房间被回收了，或者服务端重启过。
        // 继续轮询没有意义，也不能假装还在对局里。
        if ((status == 403 || status == 404) && !m_code.isEmpty()) {
            resetLocal();
            setState(QStringLiteral("closed"), message);
            Q_EMIT failed(message);
            return;
        }

        if (m_state == QLatin1String("creating") || m_state == QLatin1String("joining")) {
            setState(QStringLiteral("error"), message);
            Q_EMIT failed(message);
        } else if (status != 409) {
            setDetail(message);
            Q_EMIT failed(message);
        } else {
            // 对局中的 409（还没轮到你走 / 这局已经结束）不弹提示：界面本来
            // 就拦着不该走的手，真弹出来只会让孩子以为软件坏了。记一笔就够了。
            setDetail(message);
        }
        return;
    }

    const QVariantMap room = obj.value(QStringLiteral("room")).toObject().toVariantMap();
    if (!room.isEmpty()) {
        applyRoom(room);
    } else {
        Q_EMIT changed();
    }
}

void Netplay::applyRoom(const QVariantMap &room)
{
    const int version = room.value(QStringLiteral("version")).toInt();
    const int current = m_room.value(QStringLiteral("version")).toInt();
    if (!m_room.isEmpty() && version < current) {
        // 迟到的旧响应。轮询和落子会同时在飞，没有这道闸门界面就会往回跳一格
        // （孩子刚走完的子消失，再点一次就变成两步）。
        return;
    }

    // token 只在建房/进房的响应里带一次，之后别被空值覆盖掉
    const QString token = room.value(QStringLiteral("token")).toString();
    if (!token.isEmpty()) {
        m_token = token;
    }

    m_room = room;
    m_code = room.value(QStringLiteral("code")).toString();
    m_seat = room.value(QStringLiteral("seat")).toString();

    const QString status = room.value(QStringLiteral("status")).toString();
    if (status == QLatin1String("waiting")) {
        setState(QStringLiteral("waiting"),
                 QStringLiteral("房间 %1 · 等小伙伴进来").arg(m_code));
        startPolling(true);
    } else if (status == QLatin1String("playing")) {
        setState(QStringLiteral("playing"), QStringLiteral("房间 %1").arg(m_code));
        startPolling(true);
    } else if (status == QLatin1String("done")) {
        setState(QStringLiteral("done"), QString());
        startPolling(false);
    }

    Q_EMIT roomChanged();
    Q_EMIT changed();
}
