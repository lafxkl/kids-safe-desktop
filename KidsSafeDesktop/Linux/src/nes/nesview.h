#pragma once

#include <QPointer>
#include <QQuickPaintedItem>

class NesEmulator;

/**
 * 把 NesEmulator 的一帧画到 QML 里。
 *
 * 为什么不用 `Image { source: "image://nes/..." }`：那样每帧都要换一个 URL
 * 才能让 Image 重新取图，60 次/秒的字符串与 URL 缓存查询不但浪费，
 * 而且在某些平台上 Image 会短暂保留旧纹理，表现为画面「抽一下」。
 * QQuickPaintedItem 由我们自己控制 update()，drawImage 直接上屏，
 * 缩放交给 painter —— 关掉平滑就是干净的点阵放大。
 *
 * 属性刻意写成 QObject* 而不是 NesEmulator*：NesEmulator 没有注册成 QML 类型
 * （它是通过 qmlRegisterSingletonInstance 暴露的单例），用具体类型做属性类型
 * 时 QML 侧的转换规则在版本间有过变化，QObject* + qobject_cast 最稳。
 */
class NesView : public QQuickPaintedItem
{
    Q_OBJECT

    Q_PROPERTY(QObject *emulator READ emulator WRITE setEmulator NOTIFY emulatorChanged)

public:
    explicit NesView(QQuickItem *parent = nullptr);

    /// 声明在头文件、定义在 .cpp：QPointer<T> 转 T* 再转 QObject* 需要 T 是
    /// 完整类型（编译器得知道 NesEmulator 公有继承自 QObject），而这里只有
    /// 前向声明。写成内联就会报 cannot convert ... to QObject*。
    QObject *emulator() const;
    void setEmulator(QObject *object);

    void paint(QPainter *painter) override;

signals:
    void emulatorChanged();

private:
    QPointer<NesEmulator> m_emulator;
    QMetaObject::Connection m_frameConnection;
};
