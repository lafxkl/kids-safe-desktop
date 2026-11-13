#pragma once

#include <QObject>
#include <QVariantList>
#include <QVariantMap>
#include <QUrl>

class QNetworkAccessManager;
class QNetworkReply;
class QJsonObject;
class Settings;

/**
 * 对应 C# 的 IStoryService / StoryService。
 *
 * 与 C# 版保持同一套服务端契约（家长自建的 KidsSafeServer）：
 *   GET /api/story/types                       → { categories: [{id, name}] }
 *   GET /api/story/list?typeId=&page=          → { items: [...], page, hasMore }
 *   GET /api/story/search?keyword=&page=       → { items: [...], page, hasMore }
 *   GET /api/story/detail?storyId=             → { id, title, type, length, readTime, content }
 *
 * 沿用原设计的一条铁律：任何网络失败都静默降级为空列表 + 一句人话错误提示，
 * 绝不弹异常打断儿童界面。故事拉不到只是少了内容，不能把桌面搞崩。
 */
class StoryService : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool onlineEnabled READ onlineEnabled NOTIFY onlineStateChanged)
    Q_PROPERTY(bool loading READ loading NOTIFY loadingChanged)
    Q_PROPERTY(QString errorText READ errorText NOTIFY errorTextChanged)
    Q_PROPERTY(QVariantList categories READ categories NOTIFY categoriesChanged)
    Q_PROPERTY(QVariantList items READ items NOTIFY itemsChanged)
    Q_PROPERTY(bool hasMore READ hasMore NOTIFY itemsChanged)
    Q_PROPERTY(QVariantMap detail READ detail NOTIFY detailChanged)
    Q_PROPERTY(bool detailLoading READ detailLoading NOTIFY detailLoadingChanged)

public:
    explicit StoryService(Settings *settings, QObject *parent = nullptr);

    bool onlineEnabled() const;
    bool loading() const { return m_loading; }
    QString errorText() const { return m_errorText; }
    QVariantList categories() const { return m_categories; }
    QVariantList items() const { return m_items; }
    bool hasMore() const { return m_hasMore; }
    QVariantMap detail() const { return m_detail; }
    bool detailLoading() const { return m_detailLoading; }

    /// 家长改过服务器地址或开关后调用，重新判定能否联网。
    Q_INVOKABLE void refreshOnlineState();

    Q_INVOKABLE void loadCategories();
    Q_INVOKABLE void loadList(int typeId, int page = 1);
    Q_INVOKABLE void loadMore();
    Q_INVOKABLE void search(const QString &keyword, int page = 1);
    Q_INVOKABLE void loadDetail(qint64 storyId);

signals:
    void onlineStateChanged();
    void loadingChanged();
    void errorTextChanged();
    void categoriesChanged();
    void itemsChanged();
    void detailChanged();
    void detailLoadingChanged();

private:
    QString baseUrl() const;
    QNetworkReply *get(const QString &path);
    void setLoading(bool value);
    void setError(const QString &text);
    void setDetailLoading(bool value);

    static QString categoryEmoji(const QString &name);
    static QVariantMap summaryFromJson(const QJsonObject &object);

    Settings *m_settings = nullptr;
    QNetworkAccessManager *m_net = nullptr;

    QVariantList m_categories;
    QVariantList m_items;
    QVariantMap m_detail;
    QString m_errorText;

    bool m_loading = false;
    bool m_detailLoading = false;
    bool m_hasMore = false;

    int m_currentTypeId = 0;
    int m_currentPage = 0;
    QString m_currentKeyword;
};
