#include "settings.h"

#include <QCryptographicHash>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QRandomGenerator>
#include <utility>

#include <KConfigGroup>

namespace {
constexpr auto kGroup = "General";
constexpr auto kApps = "AllowedApps";
}

Settings::Settings(QObject *parent)
    : QObject(parent)
    , m_config(KSharedConfig::openConfig(QStringLiteral("kidssafe/kidssafe.conf")))
{
    load();
}

void Settings::load()
{
    KConfigGroup group(m_config, kGroup);

    m_nickname = group.readEntry("Nickname", QStringLiteral("小朋友"));
    m_avatarEmoji = group.readEntry("AvatarEmoji", QStringLiteral("🐯"));
    m_dailyLimitMinutes = qBound(5, group.readEntry("DailyLimitMinutes", 60), 600);
    m_gamesEnabled = group.readEntry("GamesEnabled", true);
    m_onboardingDone = group.readEntry("OnboardingDone", false);
    m_pinHash = group.readEntry("PinHash", QString());
    m_pinSalt = group.readEntry("PinSalt", QString());

    m_allowedApps.clear();
    const QByteArray raw = group.readEntry(kApps, QByteArray());
    if (!raw.isEmpty()) {
        const QJsonDocument doc = QJsonDocument::fromJson(raw);
        for (const QJsonValue &value : doc.array()) {
            m_allowedApps.append(value.toObject().toVariantMap());
        }
    }

    m_serverUrl = group.readEntry("ServerUrl", QString());
    m_onlineContentEnabled = group.readEntry("OnlineContentEnabled", false);
}

void Settings::save()
{
    KConfigGroup group(m_config, kGroup);

    group.writeEntry("Nickname", m_nickname);
    group.writeEntry("AvatarEmoji", m_avatarEmoji);
    group.writeEntry("DailyLimitMinutes", m_dailyLimitMinutes);
    group.writeEntry("GamesEnabled", m_gamesEnabled);
    group.writeEntry("OnboardingDone", m_onboardingDone);
    group.writeEntry("PinHash", m_pinHash);
    group.writeEntry("PinSalt", m_pinSalt);

    QJsonArray array;
    for (const QVariant &item : std::as_const(m_allowedApps)) {
        array.append(QJsonObject::fromVariantMap(item.toMap()));
    }
    group.writeEntry(kApps, QJsonDocument(array).toJson(QJsonDocument::Compact));

    group.writeEntry("ServerUrl", m_serverUrl);
    group.writeEntry("OnlineContentEnabled", m_onlineContentEnabled);

    m_config->sync();

    Q_EMIT changed();
}

QString Settings::hashPin(const QString &pin) const
{
    if (m_pinSalt.isEmpty()) {
        return QString();
    }
    const QByteArray data = m_pinSalt.toUtf8() + pin.toUtf8();
    return QString::fromLatin1(
        QCryptographicHash::hash(data, QCryptographicHash::Sha256).toHex());
}

bool Settings::verifyPin(const QString &pin) const
{
    if (m_pinHash.isEmpty()) {
        return false;
    }
    // 定长比较，避免逐字节比对的时序差异（虽然 4 位数字 PIN 的威胁模型很弱，
    // 但密码比较就该这么写，成本为零）。
    const QByteArray a = hashPin(pin).toLatin1();
    const QByteArray b = m_pinHash.toLatin1();
    if (a.size() != b.size()) {
        return false;
    }
    unsigned char diff = 0;
    for (int i = 0; i < a.size(); ++i) {
        diff |= static_cast<unsigned char>(a[i] ^ b[i]);
    }
    return diff == 0;
}

void Settings::setPin(const QString &pin)
{
    if (pin.isEmpty()) {
        m_pinHash.clear();
        m_pinSalt.clear();
        save();
        return;
    }

    if (m_pinSalt.isEmpty()) {
        QByteArray salt(16, Qt::Uninitialized);
        QRandomGenerator::system()->generate(salt.begin(), salt.end());
        m_pinSalt = QString::fromLatin1(salt.toHex());
    }
    m_pinHash = hashPin(pin);
    save();
}

bool Settings::isAllowed(const QString &id) const
{
    for (const QVariant &item : m_allowedApps) {
        if (item.toMap().value(QStringLiteral("id")).toString() == id) {
            return true;
        }
    }
    return false;
}

void Settings::addApp(const QVariantMap &app)
{
    const QString id = app.value(QStringLiteral("id")).toString();
    if (id.isEmpty() || isAllowed(id)) {
        return;
    }
    m_allowedApps.append(app);
    save();
}

void Settings::removeApp(const QString &id)
{
    for (int i = 0; i < m_allowedApps.size(); ++i) {
        if (m_allowedApps.at(i).toMap().value(QStringLiteral("id")).toString() == id) {
            m_allowedApps.removeAt(i);
            save();
            return;
        }
    }
}

void Settings::reload()
{
    load();
    Q_EMIT changed();
}

void Settings::setNickname(const QString &value)
{
    if (m_nickname == value) {
        return;
    }
    m_nickname = value;
    save();
}

void Settings::setAvatarEmoji(const QString &value)
{
    if (m_avatarEmoji == value) {
        return;
    }
    m_avatarEmoji = value;
    save();
}

void Settings::setDailyLimitMinutes(int value)
{
    value = qBound(5, value, 600);
    if (m_dailyLimitMinutes == value) {
        return;
    }
    m_dailyLimitMinutes = value;
    save();
}

void Settings::setGamesEnabled(bool value)
{
    if (m_gamesEnabled == value) {
        return;
    }
    m_gamesEnabled = value;
    save();
}

void Settings::setOnboardingDone(bool value)
{
    if (m_onboardingDone == value) {
        return;
    }
    m_onboardingDone = value;
    save();
}

void Settings::setServerUrl(const QString &value)
{
    if (m_serverUrl == value) {
        return;
    }
    m_serverUrl = value.trimmed();
    save();
}

void Settings::setOnlineContentEnabled(bool value)
{
    if (m_onlineContentEnabled == value) {
        return;
    }
    m_onlineContentEnabled = value;
    save();
}
