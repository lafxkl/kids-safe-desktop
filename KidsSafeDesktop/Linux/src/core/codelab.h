#pragma once

#include <QObject>
#include <QSet>
#include <QString>
#include <QStringList>
#include <QVariantList>
#include <QVariantMap>

#include <memory>

class QProcess;
class QTemporaryDir;
class QTimer;

/*
 * 小小程序员：把「积木」翻译成 C 语言，交给系统 GCC 编译，再运行。
 *
 * 为什么是「生成源码 + 调编译器」而不是内置解释器：
 *   孩子搭的东西最后真的是一段能编译的 C 程序，界面上能看见、能带走、
 *   能在终端里自己 gcc 一遍 —— 这是「编程」而不是「点按钮」。
 *
 * 安全边界（重要）：
 *   1. 代码不是孩子手写的，而是由本类的模板拼出来。文字参数经过 C 字符串
 *      转义（引号 / 反斜杠 / 换行），不存在注入；数值一律夹紧到 -999..999。
 *   2. 编译出的程序在独立临时目录里运行，setChildProcessModifier 里设
 *      RLIMIT_CPU / RLIMIT_AS / RLIMIT_FSIZE / RLIMIT_CORE：
 *      跑飞了会被内核按 CPU 时间杀掉，内存最多 256 MB，写文件最多 1 MB。
 *   3. 外面还有 3 秒超时与 8 KB 输出上限，无限循环不会把界面卡死。
 *
 * QML 侧的契约：程序是一段扁平的块序列（QVariantList），每块是一个 map，
 * 字段固定为 kind / text / slot / value / times / op / forKind。
 * 循环与判断用「开始块 + 结束块」配对表达，和界面上看到的夹心结构一致。
 */
class CodeLab : public QObject
{
    Q_OBJECT

    Q_PROPERTY(QString code READ code NOTIFY codeChanged)
    Q_PROPERTY(QString output READ output NOTIFY outputChanged)
    Q_PROPERTY(QString status READ status NOTIFY statusChanged)
    Q_PROPERTY(QString hint READ hint NOTIFY hintChanged)
    Q_PROPERTY(bool running READ running NOTIFY runningChanged)
    Q_PROPERTY(bool compilerAvailable READ compilerAvailable CONSTANT)
    Q_PROPERTY(QString compilerName READ compilerName CONSTANT)

    // 关卡：从 assets/codelab/lessons.json 读，改关不用重编译。
    Q_PROPERTY(QVariantList lessons READ lessons NOTIFY lessonsChanged)
    Q_PROPERTY(QString verdict READ verdict NOTIFY verdictChanged)
    Q_PROPERTY(QString verdictText READ verdictText NOTIFY verdictChanged)
    Q_PROPERTY(QStringList passedIds READ passedIds NOTIFY progressChanged)

    // 编程作业的成绩：作业 id → { score, total, best, at }。
    // 作业题库在 src/qml/HomeworkData.js（QML 侧的数据 + 判题），这里只负责
    // 「做完一套就记一笔」，成绩和家长后台、下次打开对得上。
    Q_PROPERTY(QVariantMap homeworkScores READ homeworkScores NOTIFY homeworkChanged)

public:
    explicit CodeLab(QObject *parent = nullptr);
    ~CodeLab() override;

    QString code() const { return m_code; }
    QString output() const { return m_output; }
    QString status() const { return m_status; }
    QString hint() const { return m_hint; }
    bool running() const { return m_stage != Idle; }
    bool compilerAvailable() const { return !m_compiler.isEmpty(); }
    QString compilerName() const;

    QVariantList lessons() const { return m_lessons; }
    QString verdict() const { return m_verdict; }
    QString verdictText() const { return m_verdictText; }
    QStringList passedIds() const { return m_passed; }
    QVariantMap homeworkScores() const { return m_homework; }

    // 界面每次改完积木调用它：重新生成源码，但不编译。
    Q_INVOKABLE void setProgram(const QVariantList &blocks);
    // 当前关卡。传空 map 表示自由创作/教学浏览（不判题）。
    Q_INVOKABLE void setTask(const QVariantMap &task);
    Q_INVOKABLE bool isPassed(const QString &id) const;
    Q_INVOKABLE void resetProgress();
    // 作业交卷时调用：记下本次成绩，并保留历史最高。
    // title 只是给家长后台看的（题库改版后 id 没变、名字可能变），可以不传。
    Q_INVOKABLE void saveHomeworkScore(const QString &id, int score, int total,
                                       const QString &title = QString());
    Q_INVOKABLE void resetHomeworkScores();
    // 用当前源码编译 + 运行。异步，结果通过 output/status 通知。
    Q_INVOKABLE void run();
    Q_INVOKABLE void stop();
    Q_INVOKABLE void clearOutput();

signals:
    void codeChanged();
    void outputChanged();
    void statusChanged();
    void hintChanged();
    void runningChanged();
    void lessonsChanged();
    void verdictChanged();
    void progressChanged();
    void homeworkChanged();

private:
    enum Stage {
        Idle,
        Compiling,
        Running
    };

    // 把积木翻成 C 源码（未收口的循环会自动补齐花括号）。
    QString generate(const QVariantList &blocks) const;

    QVariantList loadLessons();
    void readProgress();
    void saveProgress();
    void readHomework();
    void saveHomeworkFile();
    bool judge(const QVariantMap &task, QString *why) const;
    void evaluateTask();
    void setVerdict(const QString &verdict, const QString &text);

    void setOutput(const QString &text);
    void appendOutput(const QString &text);
    void setStatus(const QString &text);
    void setHint(const QString &text);
    void cleanupProcess();
    void onStartFailed(const QString &what);
    // 编译成功但程序起不来（execve 失败）时走这里：先查文件状态，再说人话。
    void onRunStartFailed();

    void onCompileFinished(int exitCode, int status);
    void onRunFinished(int exitCode, int status);
    void onTimeout();

    QString m_compiler;
    QString m_code;
    QString m_output;
    QString m_status;
    QString m_hint;
    int m_blockCount = 0;

    QVariantList m_lessons;
    QVariantMap m_task;          // 空 = 不判题
    QSet<QString> m_usedKinds;   // 本程序用到的积木种类，供 require 检查
    QString m_verdict;           // "" / "pass" / "fail"
    QString m_verdictText;
    QStringList m_passed;
    QVariantMap m_homework;      // 作业 id → { score, total, best, at }

    QString m_progPath;          // 本次编译出的可执行文件，启动失败时要拿它做诊断

    Stage m_stage = Idle;
    QProcess *m_proc = nullptr;
    QTimer *m_timer = nullptr;
    std::unique_ptr<QTemporaryDir> m_dir;
};
