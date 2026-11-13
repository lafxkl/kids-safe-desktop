#pragma once

#include <QObject>
#include <QString>

/**
 * 对应 C# 的 Helpers/Tts.cs（Windows 用 SAPI，Android 用 TextToSpeech）。
 *
 * Linux 侧走 speech-dispatcher：这是各发行版通用的语音合成总线，
 * 后端可能是 espeak-ng、Piper、Festival 或云 TTS，对我们都是同一个接口。
 * 未安装 spd-say 时静默降级——语音是锦上添花，不能因为缺依赖让孩子
 * 的桌面直接报错。
 */
class Speech : public QObject
{
    Q_OBJECT

    Q_PROPERTY(bool available READ available CONSTANT)

public:
    explicit Speech(QObject *parent = nullptr);

    bool available() const { return !m_program.isEmpty(); }

    /// 异步朗读一段文本，不阻塞 UI。
    Q_INVOKABLE void say(const QString &text);

    /// 打断当前朗读。
    Q_INVOKABLE void stop();

private:
    QString m_program;
};
