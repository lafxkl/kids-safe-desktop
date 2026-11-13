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
};
