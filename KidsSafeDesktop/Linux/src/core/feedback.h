#pragma once

#include <QJsonObject>
#include <QList>
#include <QObject>
#include <QString>

class QNetworkAccessManager;
class QNetworkReply;

/**
 * 用户反馈上报（意见反馈 / 满意度 / 问卷）。
 *
 * 与 Windows / Android / 鸿蒙用同一个接口（POST /api/feedback/submit），
 * 数据落在开发者服务器，官网「反馈与问卷」页读同一份数据做公开统计。
 *
 * 三条与手机端一致的原则：
 *  1) 地址固定官网，不跟家长自建服务器走（那台机器开发者看不到）；
 *     联调时用环境变量 KIDSSAFE_FEEDBACK_URL 覆盖。
 *  2) 断网不丢：发不出去就写进本机队列，下次启动或下次提交时自动补发。
 *  3) 不猜身份：只带平台与版本号，不带设备号、昵称、账号。
 */
class Feedback : public QObject
{
    Q_OBJECT
    Q_PROPERTY(int pending READ pending NOTIFY pendingChanged)
    Q_PROPERTY(QString status READ status NOTIFY statusChanged)
    Q_PROPERTY(bool busy READ busy NOTIFY busyChanged)

public:
    explicit Feedback(QObject *parent = nullptr);

    int pending() const { return m_queue.size(); }
    QString status() const { return m_status; }
    bool busy() const { return m_busy; }

    /// 意见反馈：category 是分类（问题反馈 / 功能建议 / …）。
    Q_INVOKABLE void submitFeedback(const QString &category, const QString &content,
                                    const QString &contact, bool allowPublic);

    /// 满意度：rating 1–5 必填；nps < 0 表示没选；dims 的 key 见 DimRow。
    Q_INVOKABLE void submitSatisfaction(int rating, int nps, const QVariantMap &dims,
                                        const QStringList &tags, const QString &content,
                                        const QString &contact, bool allowPublic);

    /// 问卷：answers 的 key 是题号（中文题干），value 是选项文字。
    Q_INVOKABLE void submitSurvey(const QVariantMap &answers, const QString &content,
                                  const QString &contact, bool allowPublic);

    /// 把本机队列里的补发掉（启动后延迟调用，避免和首屏抢网络）。
    Q_INVOKABLE void flush();

signals:
    void pendingChanged();
    void statusChanged();
    void busyChanged();

private:
    QJsonObject base(const QString &kind, const QString &contact, bool allowPublic) const;
    void submit(QJsonObject payload);
    void sendNext();
    void setStatus(const QString &text);
    void setBusy(bool value);
    void saveQueue() const;
    void loadQueue();

    QString queuePath() const;
    QString baseUrl() const;

    QNetworkAccessManager *m_net = nullptr;
    QList<QJsonObject> m_queue;
    QString m_status;
    bool m_busy = false;
};
