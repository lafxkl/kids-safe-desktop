#pragma once

#include <QObject>
#include <QString>

class QProcess;

/**
 * 对应 C# 的 Helpers/Tts.cs（Windows 用 SAPI，Android 用 TextToSpeech）。
 *
 * Linux 侧走 speech-dispatcher：这是各发行版通用的语音合成总线，
 * 后端可能是 espeak-ng、Piper、Festival 或云 TTS，对我们都是同一个接口。
 * 未安装 spd-say 时静默降级——语音是锦上添花，不能因为缺依赖让孩子
 * 的桌面直接报错。
 *
 * 为什么不能只查 `spd-say` 在不在 PATH 里：
 *   1. 只装了 speech-dispatcher 的客户端、没有音频输出模块时，spd-say 会
 *      「正常退出但不发声」，孩子按了「朗读」一点反应都没有，比按钮直接变灰
 *      更难排查；
 *   2. 服务没起来 / 输出模块只有 dummy 时同理。
 * 所以可用性分两步判定：先找可执行文件，再跑一次 `spd-say -O`（列出输出模块），
 * 两者都通过才算可用。检测是异步的，不阻塞启动；结果变化会发 stateChanged，
 * QML 里可以直接绑定 Speech.available。
 */
class Speech : public QObject
{
    Q_OBJECT

    /// 真正能出声（二进制 + 至少一个非 dummy 输出模块）
    Q_PROPERTY(bool available READ available NOTIFY stateChanged)

    /// 给人看的一句话状态，家长后台和 --self-test 都用它
    Q_PROPERTY(QString detail READ detail NOTIFY stateChanged)

public:
    explicit Speech(QObject *parent = nullptr);
    ~Speech() override;

    bool available() const { return m_available; }
    QString detail() const { return m_detail; }

    /// 命中的 spd-say 绝对路径；为空表示没装
    QString engine() const { return m_program; }

    /// 异步朗读一段文本，不阻塞 UI。
    Q_INVOKABLE void say(const QString &text);

    /// 打断当前朗读。
    Q_INVOKABLE void stop();

    /// 重新检测（家长装完 speech-dispatcher 后不用重启应用）。
    Q_INVOKABLE void recheck();

    /// 同步等待检测结束（只给 --self-test 用：那边没有事件循环，
    /// 异步探测的结果还没回来就打印，会永远显示「正在检测」）。
    void waitForReady(int timeoutMs = 5000);

signals:
    /// 可用性或原因文案变化时发出。Q_PROPERTY 里 notify 指向的信号必须真实存在，
    /// 否则 moc 会在编译期静态断言失败（"signal stateChanged does not exist"）。
    void stateChanged();

private:
    void probeBinary();
    void probeModules();
    void finish(bool available, const QString &detail);

    QString m_program;
    QString m_detail = QStringLiteral("正在检测…");
    bool m_available = false;
    bool m_probing = false;
    QProcess *m_probe = nullptr;
};
