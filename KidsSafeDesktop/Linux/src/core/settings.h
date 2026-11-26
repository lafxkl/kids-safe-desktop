#pragma once

#include <QObject>
#include <QVariantList>
#include <KSharedConfig>

/**
 * 对应现有 C# 工程的 ISettingsService / ChildSettings。
 *
 * 存储位置：~/.config/kidssafe/kidssafe.conf（KConfig INI 格式，
 * 好处是系统管理员可以直接用 kreadconfig6/kwriteconfig6 排查和批量下发）。
 *
 * 与 C# 版本的差异：PIN 从「裸 SHA256」改为「每台机器随机盐 + SHA256」。
 * 原因是 Linux 上配置文件权限模型比 Windows DPAPI 弱，不加盐的话
 * 4 位数字 PIN 的哈希表几秒钟就能反查出来。
 */
class Settings : public QObject
{
    Q_OBJECT

    Q_PROPERTY(QString nickname READ nickname WRITE setNickname NOTIFY changed)
    Q_PROPERTY(QString avatarEmoji READ avatarEmoji WRITE setAvatarEmoji NOTIFY changed)
    Q_PROPERTY(int dailyLimitMinutes READ dailyLimitMinutes WRITE setDailyLimitMinutes NOTIFY changed)
    Q_PROPERTY(bool hasPin READ hasPin NOTIFY changed)
    Q_PROPERTY(bool gamesEnabled READ gamesEnabled WRITE setGamesEnabled NOTIFY changed)
    Q_PROPERTY(bool onboardingDone READ onboardingDone WRITE setOnboardingDone NOTIFY changed)
    Q_PROPERTY(QVariantList allowedApps READ allowedApps NOTIFY changed)

    /// 家长自建 KidsSafeServer 地址。留空表示不启用联网内容。
    Q_PROPERTY(QString serverUrl READ serverUrl WRITE setServerUrl NOTIFY changed)
    /// 是否允许从家长服务器拉取在线内容（故事 / 补充教材）。
    Q_PROPERTY(bool onlineContentEnabled READ onlineContentEnabled WRITE setOnlineContentEnabled NOTIFY changed)

    // ── 联机对战 ───────────────────────────────────────────────
    /// 允许孩子和小伙伴对联机（华容道竞速 / 国际象棋）。
    ///
    /// 默认关，而且必须配合「在线故事与内容」一起打开 —— 这两道门是叠加的：
    /// 前者决定「能不能连服务器」，后者决定「能不能和别人一起玩」。分开是
    /// 因为家长可能想让孩子看故事，但不想让他和外面的人下棋。
    /// 打开后孩子还需要知道对方的 4 位房间号，没有公开匹配大厅。
    Q_PROPERTY(bool netplayEnabled READ netplayEnabled WRITE setNetplayEnabled NOTIFY changed)

    // ── 天气 ───────────────────────────────────────────────────
    // 天气数据来自 Open-Meteo（免密钥、免注册），与家长服务器无关，
    // 所以单独给一个开关：不想要任何外部请求的家庭可以整块关掉。
    Q_PROPERTY(bool weatherEnabled READ weatherEnabled WRITE setWeatherEnabled NOTIFY changed)
    /// "auto" 或留空 = 按出口 IP 定位；其它值当作城市名去地理编码。
    Q_PROPERTY(QString weatherCity READ weatherCity WRITE setWeatherCity NOTIFY changed)

    // ── 护眼（暖色滤镜）────────────────────────────────────────
    // 与 C# 端 ChildSettings 的同名字段一一对应，唯一的差别是强度在这里用
    // 0~100 的整数存（KConfig 里手写 0.55 这种浮点很容易被写成 0 或 0.055，
    // 家长排查时也不好读）。实际不透明度由 EyeCare 换算，上限 55%。
    Q_PROPERTY(bool eyeCareEnabled READ eyeCareEnabled WRITE setEyeCareEnabled NOTIFY changed)
    Q_PROPERTY(int eyeCareIntensity READ eyeCareIntensity WRITE setEyeCareIntensity NOTIFY changed)
    Q_PROPERTY(bool eyeCareAutoEnabled READ eyeCareAutoEnabled WRITE setEyeCareAutoEnabled NOTIFY changed)
    Q_PROPERTY(int eyeCareStartHour READ eyeCareStartHour WRITE setEyeCareStartHour NOTIFY changed)
    Q_PROPERTY(int eyeCareEndHour READ eyeCareEndHour WRITE setEyeCareEndHour NOTIFY changed)

    // ── 语言 ───────────────────────────────────────────────────
    /// 启动时选的语言：`zh_CN`（默认）或 `en_US`。
    Q_PROPERTY(QString language READ language WRITE setLanguage NOTIFY changed)
    /// 是否英文界面。QML 里用它决定取哪一套文案（`Settings.t("key")`）。
    Q_PROPERTY(bool english READ english NOTIFY changed)
    /// 「故事剧场（吉里吉里）」入口是否允许出现。
    /// **简体中文模式下故意不出现**：这不是偏好，是内容合规要求 ——
    /// 吉里吉里游戏绝大多数是成人向视觉小说，中文分发版不能带这个入口。
    /// 英文模式不限制。
    Q_PROPERTY(bool vnAllowed READ vnAllowed NOTIFY changed)

public:
    explicit Settings(QObject *parent = nullptr);

    QString nickname() const { return m_nickname; }
    void setNickname(const QString &value);

    QString avatarEmoji() const { return m_avatarEmoji; }
    void setAvatarEmoji(const QString &value);

    int dailyLimitMinutes() const { return m_dailyLimitMinutes; }
    void setDailyLimitMinutes(int value);

    bool hasPin() const { return !m_pinHash.isEmpty(); }

    bool gamesEnabled() const { return m_gamesEnabled; }
    void setGamesEnabled(bool value);

    bool onboardingDone() const { return m_onboardingDone; }
    void setOnboardingDone(bool value);

    QVariantList allowedApps() const { return m_allowedApps; }

    QString serverUrl() const { return m_serverUrl; }
    void setServerUrl(const QString &value);

    bool onlineContentEnabled() const { return m_onlineContentEnabled; }
    void setOnlineContentEnabled(bool value);

    bool netplayEnabled() const { return m_netplayEnabled; }
    void setNetplayEnabled(bool value);

    bool weatherEnabled() const { return m_weatherEnabled; }
    void setWeatherEnabled(bool value);

    QString weatherCity() const { return m_weatherCity; }
    void setWeatherCity(const QString &value);

    bool eyeCareEnabled() const { return m_eyeCareEnabled; }
    void setEyeCareEnabled(bool value);

    int eyeCareIntensity() const { return m_eyeCareIntensity; }
    void setEyeCareIntensity(int value);

    bool eyeCareAutoEnabled() const { return m_eyeCareAutoEnabled; }
    void setEyeCareAutoEnabled(bool value);

    int eyeCareStartHour() const { return m_eyeCareStartHour; }
    void setEyeCareStartHour(int value);

    int eyeCareEndHour() const { return m_eyeCareEndHour; }
    void setEyeCareEndHour(int value);

    QString language() const { return m_language; }
    void setLanguage(const QString &value);

    bool english() const { return m_language.startsWith(QLatin1String("en"), Qt::CaseInsensitive); }

    bool vnAllowed() const { return english(); }

    /// 取一条两语文案。key 不存在时原样返回 key —— 漏翻译时界面显示的是
    /// 一个英文 key，比空白或崩溃都好定位。字典在 settings.cpp 里。
    Q_INVOKABLE QString t(const QString &key) const;

    /// 校验家长密码。未设置密码时返回 false（必须先设密码才能进家长后台）。
    Q_INVOKABLE bool verifyPin(const QString &pin) const;

    Q_INVOKABLE void setPin(const QString &pin);

    Q_INVOKABLE bool isAllowed(const QString &id) const;

    /// app 需包含 id / name / exec / icon / accent 五个键。
    Q_INVOKABLE void addApp(const QVariantMap &app);

    Q_INVOKABLE void removeApp(const QString &id);

    /// 从磁盘重读（供外部工具改过配置后刷新用）。
    Q_INVOKABLE void reload();

signals:
    void changed();

private:
    void load();
    void save();
    QString hashPin(const QString &pin) const;

    KSharedConfigPtr m_config;
    QString m_nickname = QStringLiteral("小朋友");
    QString m_avatarEmoji = QStringLiteral("🐯");
    int m_dailyLimitMinutes = 60;
    bool m_gamesEnabled = true;
    bool m_onboardingDone = false;
    QString m_pinHash;
    QString m_pinSalt;
    QVariantList m_allowedApps;
    QString m_serverUrl;
    bool m_onlineContentEnabled = false;
    bool m_netplayEnabled = false;
    bool m_weatherEnabled = true;
    QString m_weatherCity = QStringLiteral("auto");

    bool m_eyeCareEnabled = false;
    int m_eyeCareIntensity = 60;      // 0~100
    bool m_eyeCareAutoEnabled = false;
    int m_eyeCareStartHour = 19;
    int m_eyeCareEndHour = 7;

    /// 界面语言。默认中文：老用户升级上来不会突然变成英文，
    /// 也不会突然多出一个吉里吉里入口。
    QString m_language = QStringLiteral("zh_CN");
};
