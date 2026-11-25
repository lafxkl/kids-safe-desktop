#include "settings.h"

#include <QCryptographicHash>
#include <QHash>
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

    m_weatherEnabled = group.readEntry("WeatherEnabled", true);
    m_weatherCity = group.readEntry("WeatherCity", QStringLiteral("auto"));

    m_eyeCareEnabled = group.readEntry("EyeCareEnabled", false);
    m_eyeCareIntensity = qBound(0, group.readEntry("EyeCareIntensity", 60), 100);
    m_eyeCareAutoEnabled = group.readEntry("EyeCareAutoEnabled", false);
    m_eyeCareStartHour = qBound(0, group.readEntry("EyeCareStartHour", 19), 23);
    m_eyeCareEndHour = qBound(0, group.readEntry("EyeCareEndHour", 7), 23);

    // 默认 zh_CN。老配置里没有这一项，读出来就是中文 —— 这样升级上来的
    // 用户不会突然变成英文界面，也不会突然看到吉里吉里入口。
    m_language = group.readEntry("Language", QStringLiteral("zh_CN"));
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
    group.writeEntry("WeatherEnabled", m_weatherEnabled);
    group.writeEntry("WeatherCity", m_weatherCity);

    group.writeEntry("EyeCareEnabled", m_eyeCareEnabled);
    group.writeEntry("EyeCareIntensity", m_eyeCareIntensity);
    group.writeEntry("EyeCareAutoEnabled", m_eyeCareAutoEnabled);
    group.writeEntry("EyeCareStartHour", m_eyeCareStartHour);
    group.writeEntry("EyeCareEndHour", m_eyeCareEndHour);

    group.writeEntry("Language", m_language);

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

void Settings::setWeatherEnabled(bool value)
{
    if (m_weatherEnabled == value) {
        return;
    }
    m_weatherEnabled = value;
    save();
}

void Settings::setWeatherCity(const QString &value)
{
    const QString normalized = value.trimmed();
    if (m_weatherCity == normalized) {
        return;
    }
    m_weatherCity = normalized;
    save();
}

void Settings::setEyeCareEnabled(bool value)
{
    if (m_eyeCareEnabled == value) {
        return;
    }
    m_eyeCareEnabled = value;
    save();
}

void Settings::setEyeCareIntensity(int value)
{
    value = qBound(0, value, 100);
    if (m_eyeCareIntensity == value) {
        return;
    }
    m_eyeCareIntensity = value;
    save();
}

void Settings::setEyeCareAutoEnabled(bool value)
{
    if (m_eyeCareAutoEnabled == value) {
        return;
    }
    m_eyeCareAutoEnabled = value;
    save();
}

void Settings::setEyeCareStartHour(int value)
{
    value = qBound(0, value, 23);
    if (m_eyeCareStartHour == value) {
        return;
    }
    m_eyeCareStartHour = value;
    save();
}

void Settings::setEyeCareEndHour(int value)
{
    value = qBound(0, value, 23);
    if (m_eyeCareEndHour == value) {
        return;
    }
    m_eyeCareEndHour = value;
    save();
}

void Settings::setLanguage(const QString &value)
{
    // 只接受这两个值：配置文件是纯文本，家长手改错一个字符不该让界面
    // 变成「既不是中文也不是英文」的中间态。
    const QString normalized =
        value.startsWith(QLatin1String("en"), Qt::CaseInsensitive) ? QStringLiteral("en_US")
                                                                  : QStringLiteral("zh_CN");
    if (m_language == normalized) {
        return;
    }
    m_language = normalized;
    save();
}

QString Settings::t(const QString &key) const
{
    // 两语文案表。不引 Qt 的 .ts/.qm 一套，原因：
    //   1. 这里只需要「中/英」两套、条目数可控，.ts 的编译链（lupdate/lrelease）
    //      在任何一台构建机上都要额外装工具，而我们的构建说明已经够长了；
    //   2. 文案和用法在同一个文件里，改一句不用切工具。
    // 条目多了再迁到 .ts 也不迟 —— 调用点写的是 Settings.t("key")，替换实现即可。
    struct Entry {
        const char *zh;
        const char *en;
    };
    static const QHash<QString, Entry> dictionary{
        // 语言选择
        {QStringLiteral("lang.title"), {"选择语言", "Choose your language"}},
        {QStringLiteral("lang.hint"), {"之后可以在家长后台修改", "You can change this later in Parent settings"}},
        {QStringLiteral("lang.zh"), {"简体中文", "Simplified Chinese"}},
        {QStringLiteral("lang.en"), {"English", "English"}},
        {QStringLiteral("lang.continue"), {"继续", "Continue"}},

        // 引导
        {QStringLiteral("onboard.welcome"), {"欢迎来到你的小天地", "Welcome to your little world"}},
        {QStringLiteral("onboard.nameAsk"), {"先告诉电脑，你叫什么名字？", "What should we call you?"}},
        {QStringLiteral("onboard.namePlaceholder"), {"小朋友", "Little friend"}},
        {QStringLiteral("onboard.next"), {"下一步", "Next"}},
        {QStringLiteral("onboard.setPin"), {"设置家长密码", "Set a parent PIN"}},
        {QStringLiteral("onboard.confirmPin"), {"再输一次确认", "Enter it again"}},
        {QStringLiteral("onboard.pinHint"),
         {"这个密码用来打开家长后台，别告诉小朋友哦",
          "This PIN opens Parent settings — don't tell the kids"}},
        {QStringLiteral("onboard.pinMismatch"),
         {"两次输入不一样，请重新设置", "The two entries differ, please try again"}},
        {QStringLiteral("onboard.pickApps"),
         {"哪些应用可以出现在桌面上？", "Which apps may appear on the desktop?"}},
        {QStringLiteral("onboard.pickAppsHint"),
         {"现在不选也没关系，之后在家长后台随时能改",
          "You can skip this — change it anytime in Parent settings"}},
        {QStringLiteral("onboard.start"), {"开始使用", "Start"}},

        // 通用
        {QStringLiteral("common.back"), {"返回", "Back"}},

        // 首页
        {QStringLiteral("home.hello"), {"你好，", "Hello, "}},
        {QStringLiteral("home.subtitle"), {"今天也要开开心心", "Have a happy day"}},
        {QStringLiteral("home.parent"), {"家长", "Parents"}},
        {QStringLiteral("home.myApps"), {"我的应用", "My apps"}},
        {QStringLiteral("banner.story"), {"故事屋", "Stories"}},
        {QStringLiteral("banner.storySub"),
         {"睡前故事、成语典故、古诗…", "Bedtime stories, idioms, poems…"}},
        {QStringLiteral("banner.media"), {"听一听", "Listen"}},
        {QStringLiteral("banner.mediaSub"), {"儿歌 · 雨声 · 国学", "Songs · rain · poems"}},
        {QStringLiteral("banner.safety"), {"安全学堂", "Safety School"}},
        {QStringLiteral("banner.safetySub"), {"10 个主题 · 40 道问答", "10 topics · 40 quizzes"}},
        {QStringLiteral("banner.games"), {"游戏乐园", "Playground"}},
        {QStringLiteral("banner.gamesSub"),
         {"小游戏 · 编程 · 网页 15 款", "Mini games · coding · 15 web games"}},
        {QStringLiteral("banner.eye"), {"眼保健操", "Eye Exercises"}},
        {QStringLiteral("banner.eyeSub"), {"闭眼 · 按揉 · 远眺", "Close eyes · massage · look far"}},
    };

    const auto it = dictionary.constFind(key);
    if (it == dictionary.constEnd()) {
        return key;
    }
    return english() ? QString::fromUtf8(it->en) : QString::fromUtf8(it->zh);
}
