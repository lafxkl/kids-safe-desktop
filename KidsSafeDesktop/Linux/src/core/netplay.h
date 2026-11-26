#pragma once

#include <QJsonObject>
#include <QObject>
#include <QString>
#include <QVariantMap>

class QNetworkAccessManager;
class QNetworkReply;
class QTimer;
class Settings;

/*
 * 联机对战：和同一个小伙伴开一局（华容道竞速 / 国际象棋）。
 *
 * 只做「熟人房间」：一方建房拿到 4 位号码，念给对面，对面输进去。
 * 没有公开匹配、没有房间列表、没有聊天。这不是没做完 —— 是刻意的：
 * 一个儿童桌面上出现「随机匹配一个陌生人下棋」，比没有联机功能糟糕得多。
 *
 * 三条设计原则，和 LearnReport / StoryService 保持一致：
 *   1) 地址复用家长后台那个「故事服务器地址」，不另开输入框。地址没填、
 *      或者家长没同时打开「在线故事与内容」和「联机对战」，整个模块不发
 *      任何请求（enabled=false），游戏里就看不到「和小朋友玩」这一项。
 *      联机是把孩子的对局送到另一台机器上，理应受家长开关管。
 *   2) 断了不打断孩子：轮询连续失败只是把 detail 改成「正在重试」，
 *      房间和 token 都留着，网一回来下一轮就自动对上。只有服务器明确说
 *      「房间没了」（403/404）才收摊。
 *   3) 不阻塞界面：QNetworkAccessManager 异步，1.5 秒轮询一次。棋类一步
 *      要好几秒，这个频率孩子完全感觉不到延迟，也不必为此上 WebSocket。
 *
 * 关于并发：轮询和落子可能同时在飞，所以不用「同一时刻只发一个」的锁，
 * 改成按房间的 version 丢弃迟到的旧响应（见 applyRoom）。锁会让「孩子点了
 * 却没走成」变成偶发 bug，而 version 比较是确定性的。
 */
class Netplay : public QObject
{
    Q_OBJECT

    /// 家长开关 + 服务器地址都就绪才为 true；false 时整个模块静默。
    Q_PROPERTY(bool enabled READ enabled NOTIFY changed)

    /// idle / creating / joining / waiting / playing / done / closed / error
    Q_PROPERTY(QString state READ state NOTIFY changed)
    /// 给人看的一句话：正在建房 / 等对手进来 / 房间已关闭 ……
    Q_PROPERTY(QString detail READ detail NOTIFY changed)
    Q_PROPERTY(QString code READ code NOTIFY changed)
    /// "a" 或 "b"：a 是房主（国际象棋执白），b 是应战的一方（执黑）
    Q_PROPERTY(QString seat READ seat NOTIFY changed)
    /// 还有几个请求在路上；界面拿它禁用按钮，避免孩子连点两下走两步
    Q_PROPERTY(bool busy READ busy NOTIFY changed)

    /// 服务端房间视图，原样透给 QML：
    /// code / status / turn / moves / opponent / startAt / results / winner / now / version
    Q_PROPERTY(QVariantMap room READ room NOTIFY roomChanged)
    /// 现在轮到自己了吗（只对国际象棋有意义）
    Q_PROPERTY(bool myTurn READ myTurn NOTIFY roomChanged)

public:
    explicit Netplay(Settings *settings, QObject *parent = nullptr);

    bool enabled() const;
    QString state() const { return m_state; }
    QString detail() const { return m_detail; }
    QString code() const { return m_code; }
    QString seat() const { return m_seat; }
    bool busy() const { return m_inflight > 0; }
    QVariantMap room() const { return m_room; }
    bool myTurn() const;

    /// game 是 "chess" 或 "klotski"；level 只对华容道有意义（三档布局）
    Q_INVOKABLE void createRoom(const QString &game, int level);
    Q_INVOKABLE void joinRoom(const QString &code);
    Q_INVOKABLE void leave();

    /// 走出一步。promo 是升变选的棋子（"Q"/"R"/"B"/"N"），普通走子传空串。
    /// 刻意不给默认参数：QML 调 C++ 方法时不认默认值，少传一个参数会报
    /// 「Insufficient arguments」，不如让签名逼调用方写全三个。
    Q_INVOKABLE void sendMove(int from, int to, const QString &promo);
    /// 国际象棋结束：result 是 "a" / "b" / "draw"
    Q_INVOKABLE void reportChessResult(const QString &result);
    /// 华容道解出来了：报步数，耗时由服务端算
    Q_INVOKABLE void reportKlotskiFinish(int steps);

    /// 立刻拉一次（比如窗口从最小化回来，或孩子点了「刷新」）
    Q_INVOKABLE void refresh();

signals:
    void changed();
    void roomChanged();
    /// 出错时弹一句。界面只负责显示，不负责判断该不该继续。
    void failed(const QString &message);

private:
    void poll();
    void post(const QString &action, const QJsonObject &body);
    void handle(QNetworkReply *reply, bool fromPoll);
    void applyRoom(const QVariantMap &room);
    void setState(const QString &state, const QString &detail = QString());
    void setDetail(const QString &detail);
    void resetLocal();
    void startPolling(bool on);
    QString baseUrl() const;
    /// 能不能联机：地址填了 + 家长两个开关都开了
    bool canPlay() const;
    QString blockedReason() const;

    Settings *m_settings = nullptr;
    QNetworkAccessManager *m_net = nullptr;
    QTimer *m_pollTimer = nullptr;
    int m_inflight = 0;
    int m_failures = 0;   // 连续失败次数：到 3 次才提示「正在重试」

    QString m_state = QStringLiteral("idle");
    QString m_detail;
    QString m_code;
    QString m_seat;
    QString m_token;
    QVariantMap m_room;
};
