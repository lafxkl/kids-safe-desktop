#include "storyservice.h"
#include "settings.h"

#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QUrlQuery>

namespace {
constexpr int kTimeoutMs = 12000;

/// 与 C# 的 Normalize 一致：补 http:// 前缀、去掉尾部斜杠。
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

StoryService::StoryService(Settings *settings, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
    , m_net(new QNetworkAccessManager(this))
{
    connect(m_settings, &Settings::changed, this, &StoryService::refreshOnlineState);
}

QString StoryService::baseUrl() const
{
    return normalize(m_settings->serverUrl());
}

bool StoryService::onlineEnabled() const
{
    return m_settings->onlineContentEnabled() && !baseUrl().isEmpty();
}

void StoryService::refreshOnlineState()
{
    // 从「在线」变成「离线」时把已有内容清掉，避免界面上出现
    // 家长明明关了联网、孩子却还看得见旧列表的矛盾状态。
    if (!onlineEnabled()) {
        m_categories.clear();
        m_items.clear();
        m_hasMore = false;
        Q_EMIT categoriesChanged();
        Q_EMIT itemsChanged();
    }
    Q_EMIT onlineStateChanged();
}

QNetworkReply *StoryService::get(const QString &path)
{
    const QString base = baseUrl();
    if (base.isEmpty()) {
        return nullptr;
    }

    QNetworkRequest request(QUrl(base + path));
    request.setTransferTimeout(kTimeoutMs);
    request.setHeader(QNetworkRequest::UserAgentHeader, QStringLiteral("KidsSafeDesktop/1.19 (Linux)"));
    return m_net->get(request);
}

void StoryService::setLoading(bool value)
{
    if (m_loading == value) {
        return;
    }
    m_loading = value;
    Q_EMIT loadingChanged();
}

void StoryService::setDetailLoading(bool value)
{
    if (m_detailLoading == value) {
        return;
    }
    m_detailLoading = value;
    Q_EMIT detailLoadingChanged();
}

void StoryService::setError(const QString &text)
{
    if (m_errorText == text) {
        return;
    }
    m_errorText = text;
    Q_EMIT errorTextChanged();
}

QString StoryService::categoryEmoji(const QString &name)
{
    if (name.contains(QStringLiteral("睡前"))) return QStringLiteral("🌙");
    if (name.contains(QStringLiteral("格林"))) return QStringLiteral("🧚");
    if (name.contains(QStringLiteral("民间"))) return QStringLiteral("🏮");
    if (name.contains(QStringLiteral("益智"))) return QStringLiteral("🧩");
    if (name.contains(QStringLiteral("历史"))) return QStringLiteral("📜");
    if (name.contains(QStringLiteral("寓言"))) return QStringLiteral("🦊");
    if (name.contains(QStringLiteral("经典"))) return QStringLiteral("👑");
    if (name.contains(QStringLiteral("成语"))) return QStringLiteral("📚");
    if (name.contains(QStringLiteral("童话"))) return QStringLiteral("🧸");
    if (name.contains(QStringLiteral("儿童"))) return QStringLiteral("🧒");
    return QStringLiteral("📖");
}

QVariantMap StoryService::summaryFromJson(const QJsonObject &object)
{
    return {
        {QStringLiteral("id"), static_cast<qint64>(object.value(QStringLiteral("id")).toDouble())},
        {QStringLiteral("title"), object.value(QStringLiteral("title")).toString()},
        {QStringLiteral("type"), object.value(QStringLiteral("type")).toString()},
        {QStringLiteral("length"), object.value(QStringLiteral("length")).toInt()},
        {QStringLiteral("readTime"), object.value(QStringLiteral("readTime")).toString()},
        {QStringLiteral("shortDesc"), object.value(QStringLiteral("shortDesc")).toString()},
    };
}

// ── 分类 ────────────────────────────────────────────────────────

void StoryService::loadCategories()
{
    if (!onlineEnabled()) {
        setError(QStringLiteral("还没有连接故事服务器，请家长在后台填写地址"));
        return;
    }
    if (!m_categories.isEmpty()) {
        return;
    }

    QNetworkReply *reply = get(QStringLiteral("/api/story/types"));
    if (!reply) {
        return;
    }

    setLoading(true);
    setError(QString());

    connect(reply, &QNetworkReply::finished, this, [this, reply] {
        reply->deleteLater();
        setLoading(false);

        if (reply->error() != QNetworkReply::NoError) {
            setError(QStringLiteral("连不上故事服务器，检查一下家长后台的地址"));
            return;
        }

        const QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
        const QJsonArray array = doc.object().value(QStringLiteral("categories")).toArray();

        m_categories.clear();
        m_categories.reserve(array.size());
        for (const QJsonValue &value : array) {
            const QJsonObject item = value.toObject();
            const QString name = item.value(QStringLiteral("name")).toString();
            m_categories.append(QVariantMap{
                {QStringLiteral("id"), item.value(QStringLiteral("id")).toInt()},
                {QStringLiteral("name"), name},
                {QStringLiteral("emoji"), categoryEmoji(name)},
            });
        }

        if (m_categories.isEmpty()) {
            setError(QStringLiteral("服务器没有返回任何故事分类"));
        }
        Q_EMIT categoriesChanged();
    });
}

// ── 列表 / 搜索 ─────────────────────────────────────────────────

void StoryService::loadList(int typeId, int page)
{
    if (!onlineEnabled()) {
        setError(QStringLiteral("还没有连接故事服务器，请家长在后台填写地址"));
        return;
    }

    // 换分类时清空旧列表，否则翻页会把两个分类混在一起
    const bool newSession = (typeId != m_currentTypeId) || (page <= 1);
    m_currentTypeId = typeId;
    m_currentKeyword.clear();

    QUrlQuery query;
    query.addQueryItem(QStringLiteral("typeId"), QString::number(typeId));
    query.addQueryItem(QStringLiteral("page"), QString::number(page));

    QNetworkReply *reply = get(QStringLiteral("/api/story/list?") + query.toString(QUrl::FullyEncoded));
    if (!reply) {
        return;
    }

    if (newSession) {
        m_items.clear();
        Q_EMIT itemsChanged();
    }

    setLoading(true);
    setError(QString());

    connect(reply, &QNetworkReply::finished, this, [this, reply, page] {
        reply->deleteLater();
        setLoading(false);

        if (reply->error() != QNetworkReply::NoError) {
            setError(QStringLiteral("故事列表没拉到，可能是网络不通"));
            return;
        }

        const QJsonObject root = QJsonDocument::fromJson(reply->readAll()).object();
        const QJsonArray array = root.value(QStringLiteral("items")).toArray();

        if (page <= 1) {
            m_items.clear();
        }
        for (const QJsonValue &value : array) {
            m_items.append(summaryFromJson(value.toObject()));
        }

        m_currentPage = page;
        // 只认服务端给的 hasMore。早先写成 `hasMore || !array.isEmpty()`，
        // 最后一页也会被判成「还有更多」，翻页会反复拉同一页。
        m_hasMore = root.value(QStringLiteral("hasMore")).toBool();
        Q_EMIT itemsChanged();
    });
}

void StoryService::loadMore()
{
    if (m_currentTypeId == 0 || !m_hasMore) {
        return;
    }
    loadList(m_currentTypeId, m_currentPage + 1);
}

void StoryService::search(const QString &keyword, int page)
{
    if (!onlineEnabled()) {
        setError(QStringLiteral("还没有连接故事服务器，请家长在后台填写地址"));
        return;
    }
    if (keyword.trimmed().isEmpty()) {
        return;
    }

    m_currentTypeId = 0;
    m_currentKeyword = keyword.trimmed();

    QUrlQuery query;
    query.addQueryItem(QStringLiteral("keyword"), m_currentKeyword);
    query.addQueryItem(QStringLiteral("page"), QString::number(page));

    QNetworkReply *reply = get(QStringLiteral("/api/story/search?") + query.toString(QUrl::FullyEncoded));
    if (!reply) {
        return;
    }

    setLoading(true);
    setError(QString());

    connect(reply, &QNetworkReply::finished, this, [this, reply, page] {
        reply->deleteLater();
        setLoading(false);

        if (reply->error() != QNetworkReply::NoError) {
            setError(QStringLiteral("搜索失败了，稍后再试"));
            return;
        }

        const QJsonObject root = QJsonDocument::fromJson(reply->readAll()).object();
        const QJsonArray array = root.value(QStringLiteral("items")).toArray();

        if (page <= 1) {
            m_items.clear();
        }
        for (const QJsonValue &value : array) {
            m_items.append(summaryFromJson(value.toObject()));
        }

        m_currentPage = page;
        m_hasMore = root.value(QStringLiteral("hasMore")).toBool();
        if (m_items.isEmpty()) {
            setError(QStringLiteral("没有找到「%1」相关的故事").arg(m_currentKeyword));
        }
        Q_EMIT itemsChanged();
    });
}

// ── 连接测试（家长后台用）────────────────────────────────────────

void StoryService::testConnection()
{
    m_testResult.clear();
    Q_EMIT testResultChanged();

    const QString base = baseUrl();
    if (base.isEmpty()) {
        m_testing = false;
        m_testResult = QStringLiteral("地址是空的，先填一个再测");
        Q_EMIT testResultChanged();
        return;
    }

    m_testing = true;
    Q_EMIT testResultChanged();

    QNetworkReply *reply = get(QStringLiteral("/api/story/types"));
    if (!reply) {
        m_testing = false;
        m_testResult = QStringLiteral("地址无效，测不了");
        Q_EMIT testResultChanged();
        return;
    }

    connect(reply, &QNetworkReply::finished, this, [this, reply] {
        reply->deleteLater();
        m_testing = false;

        if (reply->error() != QNetworkReply::NoError) {
            // 家长最常遇到的就是这一条：地址填成了 127.0.0.1，或者服务没开。
            m_testResult = QStringLiteral("连不上（%1）。确认服务在跑、地址是局域网 IP、端口没被防火墙挡。")
                               .arg(reply->errorString());
            Q_EMIT testResultChanged();
            return;
        }

        const QJsonObject root = QJsonDocument::fromJson(reply->readAll()).object();
        const QJsonArray array = root.value(QStringLiteral("categories")).toArray();
        if (array.isEmpty()) {
            m_testResult = QStringLiteral("连上了，但服务器没有返回任何故事分类");
        } else {
            m_testResult = QStringLiteral("连接成功：服务器返回了 %1 个分类").arg(array.size());
            // 顺手把分类塞进缓存，家长回桌面进故事屋时不用再多一次请求。
            m_categories.clear();
            for (const QJsonValue &value : array) {
                const QJsonObject item = value.toObject();
                const QString name = item.value(QStringLiteral("name")).toString();
                m_categories.append(QVariantMap{
                    {QStringLiteral("id"), item.value(QStringLiteral("id")).toInt()},
                    {QStringLiteral("name"), name},
                    {QStringLiteral("emoji"), categoryEmoji(name)},
                });
            }
            Q_EMIT categoriesChanged();
        }
        Q_EMIT testResultChanged();
    });
}

// ── 正文 ────────────────────────────────────────────────────────

void StoryService::loadDetail(qint64 storyId)
{
    if (!onlineEnabled()) {
        setError(QStringLiteral("还没有连接故事服务器，请家长在后台填写地址"));
        return;
    }

    m_detail.clear();
    Q_EMIT detailChanged();
    setDetailLoading(true);
    setError(QString());

    QUrlQuery query;
    query.addQueryItem(QStringLiteral("storyId"), QString::number(storyId));

    QNetworkReply *reply = get(QStringLiteral("/api/story/detail?") + query.toString(QUrl::FullyEncoded));
    if (!reply) {
        setDetailLoading(false);
        return;
    }

    connect(reply, &QNetworkReply::finished, this, [this, reply] {
        reply->deleteLater();
        setDetailLoading(false);

        if (reply->error() != QNetworkReply::NoError) {
            setError(QStringLiteral("故事正文没打开，再试一次吧"));
            return;
        }

        const QJsonObject object = QJsonDocument::fromJson(reply->readAll()).object();
        if (object.isEmpty()) {
            setError(QStringLiteral("这个故事是空的"));
            return;
        }

        m_detail = {
            {QStringLiteral("id"), static_cast<qint64>(object.value(QStringLiteral("id")).toDouble())},
            {QStringLiteral("title"), object.value(QStringLiteral("title")).toString()},
            {QStringLiteral("type"), object.value(QStringLiteral("type")).toString()},
            {QStringLiteral("length"), object.value(QStringLiteral("length")).toInt()},
            {QStringLiteral("readTime"), object.value(QStringLiteral("readTime")).toString()},
            {QStringLiteral("content"), object.value(QStringLiteral("content")).toString()},
        };
        Q_EMIT detailChanged();
    });
}
