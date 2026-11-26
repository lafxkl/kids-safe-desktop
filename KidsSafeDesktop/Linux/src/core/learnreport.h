#pragma once

#include <QByteArray>
#include <QJsonObject>
#include <QObject>
#include <QString>

class QNetworkAccessManager;
class QTimer;
class CodeLab;
class Settings;

/*
 * 把「学习情况」上报到家长后台（自建的故事服务端）。
 *
 * 上报什么：关卡进度快照（每章过了几关）+ 每套编程作业的最高分。
 * 不上报什么：孩子的名字、账号、设备号。多孩子共用一台机器时靠
 * `child` 字段区分，当前固定是「默认」——这一版没有账号体系，
 * 编一个设备号当身份只会让家长看到一串看不懂的串。
 *
 * 三条设计原则，和 StoryService 保持一致：
 *   1) 服务端地址复用家长后台里那个「故事服务器地址」，不另开一个输入框。
 *      地址没填、或者家长把「在线故事与内容」的开关关掉，整个模块就不发请求
 *      （state=disabled），也不弹任何错误——大多数家庭根本不打算自建服务器。
 *      上报是把孩子的数据送出这台机器，理应跟内容走同一个开关，而不是偷偷发。
 *   2) 上报是「顺带的」：失败绝不影响孩子做题，成绩本来就已写进本机配置。
 *      所以只在一件事上重试：下次数据变化时再发一次（见 schedule）。
 *   3) 不阻塞界面：QNetworkAccessManager 异步，超时 8 秒，连不上就记一笔状态。
 *
 * 触发时机：交完一套作业（homeworkChanged）、过一关（progressChanged），
 * 都要先攒 1.5 秒再发 —— 孩子连过三关时只发最后一次，而不是发三次。
 */
class LearnReport : public QObject
{
    Q_OBJECT
    // idle（还没发过）/ disabled（没配地址）/ sending / ok / error
    Q_PROPERTY(QString state READ state NOTIFY stateChanged)
    Q_PROPERTY(QString detail READ detail NOTIFY stateChanged)

public:
    LearnReport(Settings *settings, CodeLab *codeLab, QObject *parent = nullptr);

    QString state() const { return m_state; }
    QString detail() const { return m_detail; }

    // 家长后台的「立即上报」按钮：改完服务器地址后不用等孩子再做一题。
    Q_INVOKABLE void send();

signals:
    void stateChanged();

private:
    void schedule();
    void setState(const QString &state, const QString &detail);
    QString baseUrl() const;
    // 能不能发：地址填了、并且家长打开了「在线故事与内容」开关。
    // 上报是把孩子的数据送出这台机器，得跟内容一样受那个开关管。
    bool canReport() const;
    QString blockedReason() const;
    QByteArray buildPayload() const;
    QJsonObject progressJson() const;

    Settings *m_settings = nullptr;
    CodeLab *m_codeLab = nullptr;
    QNetworkAccessManager *m_net = nullptr;
    QTimer *m_timer = nullptr;

    QString m_state = QStringLiteral("idle");
    QString m_detail;
    bool m_busy = false;
    bool m_dirty = false;    // 上报途中又来了新数据：发完再补一次
};
