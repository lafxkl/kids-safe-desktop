#include "learnreport.h"
#include "codelab.h"
#include "settings.h"

#include <QDateTime>
#include <QHash>
#include <QJsonArray>
#include <QJsonDocument>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QSet>
#include <QSysInfo>
#include <QTimer>
#include <QUrl>
#include <QVector>

// 版本号由 CMake 注入；单独编译这个文件时给个兜底，和 feedback.cpp 一致。
#ifndef KIDSSAFE_APP_VERSION
#define KIDSSAFE_APP_VERSION "1.19.0"
#endif

namespace {
// 攒一下再发：孩子连着过三关只发最后一次。
constexpr int kDebounceMs = 1500;
constexpr int kTimeoutMs = 8000;

/// 与 StoryService::normalize 同一套规则：补前缀、去尾部斜杠。
/// 两处各留一份是有意的——它们属于两个独立模块，合并成一个工具函数反而
/// 让「改故事地址」和「改上报地址」绑在一起。
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

LearnReport::LearnReport(Settings *settings, CodeLab *codeLab, QObject *parent)
    : QObject(parent)
    , m_settings(settings)
    , m_codeLab(codeLab)
    , m_net(new QNetworkAccessManager(this))
    , m_timer(new QTimer(this))
{
    m_timer->setSingleShot(true);
    connect(m_timer, &QTimer::timeout, this, &LearnReport::send);

    // 数据一变就排一次上报。两个信号都接：作业交卷、关卡过关。
    connect(m_codeLab, &CodeLab::homeworkChanged, this, &LearnReport::schedule);
    connect(m_codeLab, &CodeLab::progressChanged, this, &LearnReport::schedule);

    // 家长改了地址或开关时更新一次状态，省得界面上一直显示「没填地址」。
    connect(m_settings, &Settings::changed, this, [this] {
        if (!canReport()) {
            setState(QStringLiteral("disabled"), blockedReason());
        } else if (m_state == QLatin1String("disabled")) {
            setState(QStringLiteral("idle"), QString());
        }
    });

    if (!canReport()) {
        setState(QStringLiteral("disabled"), blockedReason());
    }
}

bool LearnReport::canReport() const
{
    return m_settings->onlineContentEnabled() && !baseUrl().isEmpty();
}

QString LearnReport::blockedReason() const
{
    // 两种情况分开说：一个是没配地址，一个是配了但开关关着 ——
    // 让家长知道该动哪一个，而不是笼统地「没开」。
    if (baseUrl().isEmpty()) {
        return QStringLiteral("还没填服务器地址，成绩只存在本机");
    }
    return QStringLiteral("「在线故事与内容」开关关着，成绩只存在本机");
}

QString LearnReport::baseUrl() const
{
    return normalize(m_settings->serverUrl());
}

void LearnReport::setState(const QString &state, const QString &detail)
{
    if (m_state == state && m_detail == detail) {
        return;
    }
    m_state = state;
    m_detail = detail;
    Q_EMIT stateChanged();
}

void LearnReport::schedule()
{
    if (!canReport()) {
        // 没配地址、或家长关了在线内容：这是常态，不是错误，安静待命
        setState(QStringLiteral("disabled"), blockedReason());
        return;
    }
    // 已经在排队就重新计时：连过三关只发最后那一次
    m_timer->start(kDebounceMs);
}

QByteArray LearnReport::buildPayload() const
{
    QJsonObject payload;
    payload.insert(QStringLiteral("child"), QStringLiteral("默认"));
    payload.insert(QStringLiteral("source"), QStringLiteral("linux"));
    // 只带主机名，不带用户名 / MAC：一家两台机器时家长能分辨就够了
    payload.insert(QStringLiteral("device"), QSysInfo::machineHostName());
    // homework 是本机的全量：家长在后台点「清空成绩」后，下一次上报要把
    // 服务端那些旧记录也带走，而不是留着（见服务器 learnreport 的 full 处理）。
    payload.insert(QStringLiteral("full"), true);
    payload.insert(QStringLiteral("progress"), progressJson());

    QJsonObject homework;
    const QVariantMap scores = m_codeLab->homeworkScores();
    for (auto it = scores.cbegin(); it != scores.cend(); ++it) {
        const QVariantMap record = it.value().toMap();
        const int total = record.value(QStringLiteral("total")).toInt();
        if (total <= 0) {
            continue;    // 坏数据不发，免得服务端统计里出现 0/0
        }
        QJsonObject entry;
        entry.insert(QStringLiteral("score"), record.value(QStringLiteral("score")).toInt());
        entry.insert(QStringLiteral("total"), total);
        entry.insert(QStringLiteral("best"), record.value(QStringLiteral("best")).toInt());
        entry.insert(QStringLiteral("at"), record.value(QStringLiteral("at")).toString());
        // 作业标题：家长后台网页没有题库，只靠 id 只能显示 hw-order
        entry.insert(QStringLiteral("title"), record.value(QStringLiteral("title")).toString());
        homework.insert(it.key(), entry);
    }
    payload.insert(QStringLiteral("homework"), homework);

    return QJsonDocument(payload).toJson(QJsonDocument::Compact);
}

QJsonObject LearnReport::progressJson() const
{
    // 口径要和 LessonStats.js 一致：教学关不判题，不算分母；
    // 已删除关卡的旧记录也不算分（否则服务端与家长后台页面对不上）。
    const QVariantList lessons = m_codeLab->lessons();
    QSet<QString> judgeable;
    for (const QVariant &item : lessons) {
        const QVariantMap lesson = item.toMap();
        if (lesson.value(QStringLiteral("type")).toString() == QLatin1String("teach")) {
            continue;
        }
        const QString id = lesson.value(QStringLiteral("id")).toString();
        if (!id.isEmpty()) {
            judgeable.insert(id);
        }
    }

    const QStringList passedIds = m_codeLab->passedIds();
    const QSet<QString> passedSet(passedIds.cbegin(), passedIds.cend());

    QJsonArray passedArray;
    for (const QString &id : passedIds) {
        if (judgeable.contains(id)) {
            passedArray.append(id);
        }
    }

    QVector<QJsonObject> rows;
    QHash<QString, int> indexOf;
    int done = 0;
    int total = 0;
    for (const QVariant &item : lessons) {
        const QVariantMap lesson = item.toMap();
        if (lesson.value(QStringLiteral("type")).toString() == QLatin1String("teach")) {
            continue;
        }
        const QString chapter = lesson.value(QStringLiteral("chapter")).toString();
        int index = indexOf.value(chapter, -1);
        if (index < 0) {
            index = rows.size();
            indexOf.insert(chapter, index);
            QJsonObject row;
            row.insert(QStringLiteral("name"), chapter);
            row.insert(QStringLiteral("done"), 0);
            row.insert(QStringLiteral("total"), 0);
            rows.append(row);
        }
        QJsonObject row = rows.at(index);
        row.insert(QStringLiteral("total"), row.value(QStringLiteral("total")).toInt() + 1);
        if (passedSet.contains(lesson.value(QStringLiteral("id")).toString())) {
            row.insert(QStringLiteral("done"), row.value(QStringLiteral("done")).toInt() + 1);
            ++done;
        }
        rows[index] = row;
        ++total;
    }

    QJsonArray chapters;
    for (const QJsonObject &row : rows) {
        chapters.append(row);
    }

    QJsonObject progress;
    progress.insert(QStringLiteral("done"), done);
    progress.insert(QStringLiteral("total"), total);
    progress.insert(QStringLiteral("passed"), passedArray);
    progress.insert(QStringLiteral("chapters"), chapters);
    return progress;
}

void LearnReport::send()
{
    if (!canReport()) {
        setState(QStringLiteral("disabled"), blockedReason());
        return;
    }
    const QString base = baseUrl();
    if (m_busy) {
        m_dirty = true;    // 这一轮发完再补一次，别丢中间那次改动
        return;
    }

    m_busy = true;
    setState(QStringLiteral("sending"), QStringLiteral("正在上报…"));

    QNetworkRequest request(QUrl(base + QStringLiteral("/api/learn/report")));
    request.setTransferTimeout(kTimeoutMs);
    request.setHeader(QNetworkRequest::ContentTypeHeader, QStringLiteral("application/json"));
    request.setHeader(QNetworkRequest::UserAgentHeader,
                      QStringLiteral("KidsSafeDesktop/" KIDSSAFE_APP_VERSION " (linux)"));

    QNetworkReply *reply = m_net->post(request, buildPayload());
    connect(reply, &QNetworkReply::finished, this, [this, reply] {
        reply->deleteLater();
        m_busy = false;

        const QByteArray body = reply->readAll();
        const int code = reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
        bool ok = reply->error() == QNetworkReply::NoError && code == 200;
        if (ok) {
            // HTTP 200 还不够：服务端的 {ok:false} 也算失败（比如字段被拒）
            const QJsonDocument doc = QJsonDocument::fromJson(body);
            ok = doc.object().value(QStringLiteral("ok")).toBool();
        }

        if (ok) {
            setState(QStringLiteral("ok"),
                     QStringLiteral("已上报 %1")
                         .arg(QDateTime::currentDateTime().toString(QStringLiteral("HH:mm"))));
        } else {
            // 断网 / 服务器没开都很常见，说人话，并且明确「本机还有」
            QString why = reply->errorString();
            if (why.isEmpty()) {
                why = QStringLiteral("服务器返回 %1").arg(code);
            }
            setState(QStringLiteral("error"),
                     QStringLiteral("上报失败：%1（成绩仍在本机，下次改动会重试）").arg(why));
        }

        if (m_dirty) {
            m_dirty = false;
            schedule();
        }
    });
}
