#include "core/feedback.h"

#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonArray>
#include <QJsonDocument>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QStandardPaths>
#include <QTimer>
#include <QUrl>
#include <QUuid>

#ifndef KIDSSAFE_APP_VERSION
#define KIDSSAFE_APP_VERSION "1.19.0"
#endif

namespace {

/// 官方地址。联调/自建时用环境变量覆盖，避免为了改一行地址重新编译。
QString defaultBase()
{
    const auto env = qEnvironmentVariable("KIDSSAFE_FEEDBACK_URL").trimmed();
    return env.isEmpty() ? QStringLiteral("https://kids.siguangblog.cc") : env;
}

QString newId()
{
    return QUuid::createUuid().toString(QUuid::WithoutBraces);
}

}

Feedback::Feedback(QObject *parent)
    : QObject(parent)
    , m_net(new QNetworkAccessManager(this))
{
    loadQueue();

    // 启动 3 秒后再补发：首屏要抢 CPU/网络，反馈晚一点没有任何影响
    QTimer::singleShot(3000, this, &Feedback::flush);
}

QString Feedback::baseUrl() const
{
    return defaultBase();
}

QString Feedback::queuePath() const
{
    auto dir = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    if (dir.isEmpty()) {
        dir = QDir::homePath() + QStringLiteral("/.local/share/kidssafe");
    }
    return dir + QStringLiteral("/feedback_queue.json");
}

QJsonObject Feedback::base(const QString &kind, const QString &contact, bool allowPublic) const
{
    QJsonObject o;
    o.insert(QStringLiteral("id"), newId());
    o.insert(QStringLiteral("kind"), kind);
    o.insert(QStringLiteral("platform"), QStringLiteral("linux"));
    o.insert(QStringLiteral("appVersion"), QStringLiteral(KIDSSAFE_APP_VERSION));
    o.insert(QStringLiteral("channel"), QStringLiteral("linux-parent-dashboard"));
    o.insert(QStringLiteral("public"), allowPublic);
    if (!contact.trimmed().isEmpty()) {
        o.insert(QStringLiteral("contact"), contact.trimmed());
    }
    return o;
}

void Feedback::submitFeedback(const QString &category, const QString &content,
                              const QString &contact, bool allowPublic)
{
    auto o = base(QStringLiteral("feedback"), contact, allowPublic);
    o.insert(QStringLiteral("content"), content.trimmed());
    o.insert(QStringLiteral("tags"), QJsonArray{category});
    submit(o);
}

void Feedback::submitSatisfaction(int rating, int nps, const QVariantMap &dims,
                                  const QStringList &tags, const QString &content,
                                  const QString &contact, bool allowPublic)
{
    auto o = base(QStringLiteral("satisfaction"), contact, allowPublic);
    o.insert(QStringLiteral("rating"), rating);
    if (nps >= 0 && nps <= 10) {
        o.insert(QStringLiteral("nps"), nps);
    }

    if (!dims.isEmpty()) {
        QJsonObject d;
        for (auto it = dims.constBegin(); it != dims.constEnd(); ++it) {
            bool ok = false;
            const int v = it.value().toInt(&ok);
            if (ok && v >= 1 && v <= 5) {
                d.insert(it.key(), v);
            }
        }
        if (!d.isEmpty()) {
            o.insert(QStringLiteral("dims"), d);
        }
    }

    if (!tags.isEmpty()) {
        QJsonArray arr;
        for (const auto &t : tags) {
            arr.append(t);
        }
        o.insert(QStringLiteral("tags"), arr);
    }

    if (!content.trimmed().isEmpty()) {
        o.insert(QStringLiteral("content"), content.trimmed());
    }
    submit(o);
}

void Feedback::submitSurvey(const QVariantMap &answers, const QString &content,
                            const QString &contact, bool allowPublic)
{
    auto o = base(QStringLiteral("survey"), contact, allowPublic);
    QJsonObject a;
    for (auto it = answers.constBegin(); it != answers.constEnd(); ++it) {
        const auto v = it.value().toString().trimmed();
        if (!v.isEmpty()) {
            a.insert(it.key(), v);
        }
    }
    o.insert(QStringLiteral("answers"), a);
    if (!content.trimmed().isEmpty()) {
        o.insert(QStringLiteral("content"), content.trimmed());
    }
    submit(o);
}

void Feedback::submit(QJsonObject payload)
{
    // 先入队、持久化，再发。这样即使发送途中进程被关掉，这条也不会丢。
    m_queue.append(payload);
    saveQueue();
    emit pendingChanged();
    sendNext();
}

void Feedback::flush()
{
    sendNext();
}

void Feedback::sendNext()
{
    if (m_busy || m_queue.isEmpty()) {
        return;
    }

    const auto payload = m_queue.first();
    const auto kind = payload.value(QStringLiteral("kind")).toString();

    QNetworkRequest req(QUrl(baseUrl() + QStringLiteral("/api/feedback/submit")));
    req.setHeader(QNetworkRequest::ContentTypeHeader, QStringLiteral("application/json"));
    req.setHeader(QNetworkRequest::UserAgentHeader,
                  QStringLiteral("KidsSafeDesktop/" KIDSSAFE_APP_VERSION " (linux)"));

    setBusy(true);
    auto *reply = m_net->post(req, QJsonDocument(payload).toJson(QJsonDocument::Compact));

    connect(reply, &QNetworkReply::finished, this, [this, reply, kind]() {
        reply->deleteLater();
        const int status = reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
        const auto body = reply->readAll();

        const bool networkError = (reply->error() != QNetworkReply::NoError) && status == 0;
        const bool retryable = networkError || status == 429 || status >= 500;

        if (status >= 200 && status < 300) {
            m_queue.removeFirst();
            saveQueue();
            emit pendingChanged();
            setStatus(kind == QStringLiteral("satisfaction") ? QStringLiteral("收到，谢谢你给的分数 🌟")
                      : kind == QStringLiteral("survey")     ? QStringLiteral("填完了，这份问卷会直接影响排期 📋")
                                                             : QStringLiteral("收到，我们会尽快看 👀"));
            setBusy(false);
            sendNext();          // 队列里还有就接着发
            return;
        }

        if (retryable) {
            // 保留在队列里，联网后自动补发
            setStatus(QStringLiteral("网络不太好，已存在本机，联网后自动补传（待发 %1 条）")
                          .arg(m_queue.size()));
            qWarning() << "[feedback] 发送失败，稍后重试：" << reply->errorString();
            setBusy(false);
            return;
        }

        // 4xx：数据本身不合格，重试一万次也没用，丢掉并说明原因
        QString reason;
        const auto doc = QJsonDocument::fromJson(body);
        if (doc.isObject()) {
            reason = doc.object().value(QStringLiteral("error")).toString();
        }
        if (reason.isEmpty()) {
            reason = QStringLiteral("提交失败（HTTP %1）").arg(status);
        }
        m_queue.removeFirst();
        saveQueue();
        emit pendingChanged();
        setStatus(reason);
        setBusy(false);
        sendNext();
    });
}

void Feedback::setStatus(const QString &text)
{
    if (m_status == text) {
        return;
    }
    m_status = text;
    emit statusChanged();
}

void Feedback::setBusy(bool value)
{
    if (m_busy == value) {
        return;
    }
    m_busy = value;
    emit busyChanged();
}

void Feedback::loadQueue()
{
    QFile f(queuePath());
    if (!f.open(QIODevice::ReadOnly)) {
        return;
    }

    const auto doc = QJsonDocument::fromJson(f.readAll());
    if (!doc.isArray()) {
        return;
    }

    for (const auto &v : doc.array()) {
        if (v.isObject()) {
            m_queue.append(v.toObject());
        }
    }
    if (!m_queue.isEmpty()) {
        qInfo() << "[feedback] 本机还有" << m_queue.size() << "条待补传的反馈";
        emit pendingChanged();
    }
}

void Feedback::saveQueue() const
{
    const auto path = queuePath();
    QDir().mkpath(QFileInfo(path).absolutePath());

    QJsonArray arr;
    for (const auto &o : m_queue) {
        arr.append(o);
    }

    QFile f(path);
    if (!f.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        qWarning() << "[feedback] 写不了队列文件：" << path;
        return;
    }
    f.write(QJsonDocument(arr).toJson(QJsonDocument::Compact));
}
