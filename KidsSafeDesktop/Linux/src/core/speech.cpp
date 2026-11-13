#include "speech.h"

#include <QProcess>
#include <QStandardPaths>

Speech::Speech(QObject *parent)
    : QObject(parent)
    , m_program(QStandardPaths::findExecutable(QStringLiteral("spd-say")))
{
}

void Speech::say(const QString &text)
{
    if (m_program.isEmpty() || text.trimmed().isEmpty()) {
        return;
    }

    // -w 等待朗读结束（由子进程自己等，不影响 UI 线程）
    // -l zh 指定中文；某些发行版没有装中文语音包时会自动回退到默认语音，
    // 而不是报错，所以这里不加 --strict。
    QProcess::startDetached(m_program, {
        QStringLiteral("-l"), QStringLiteral("zh"),
        QStringLiteral("-r"), QStringLiteral("-10"),
        QStringLiteral("-w"),
        text,
    });
}

void Speech::stop()
{
    if (m_program.isEmpty()) {
        return;
    }
    QProcess::startDetached(m_program, {QStringLiteral("--cancel")});
}
