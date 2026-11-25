#include "nesview.h"

#include "nesemulator.h"

#include <QPainter>

NesView::NesView(QQuickItem *parent)
    : QQuickPaintedItem(parent)
{
    // 不透明：省掉一层 alpha 合成的开销；抗锯齿关掉是刻意的，
    // 像素游戏放大要的就是硬边（开着抗锯齿会糊成一团）。
    setOpaquePainting(true);
    setAntialiasing(false);
}

QObject *NesView::emulator() const
{
    // 这里 NesEmulator 已经完整定义，QPointer → NesEmulator* → QObject*
    // 的派生类指针转换才成立。
    return m_emulator.data();
}

void NesView::setEmulator(QObject *object)
{
    auto *emulator = qobject_cast<NesEmulator *>(object);
    if (m_emulator == emulator) {
        return;
    }
    if (m_frameConnection) {
        disconnect(m_frameConnection);
    }
    m_emulator = emulator;
    if (m_emulator != nullptr) {
        // 帧准备好就重绘。信号来自 GUI 线程，update() 也就在 GUI 线程排队，
        // 真正的绘制发生在渲染线程的 paint()。
        m_frameConnection = connect(m_emulator, &NesEmulator::frameReady, this,
                                    [this] { update(); });
    }
    emit emulatorChanged();
    update();
}

void NesView::paint(QPainter *painter)
{
    const QRectF area = contentsBoundingRect();
    painter->fillRect(area, Qt::black);

    if (m_emulator == nullptr) {
        return;
    }
    const QImage frame = m_emulator->grabFrame();
    if (frame.isNull()) {
        return;
    }

    // 保持 256:240 的比例居中放大，不拉伸变形。
    painter->setRenderHint(QPainter::SmoothPixmapTransform, false);
    const double scale = qMin(area.width() / frame.width(), area.height() / frame.height());
    if (scale <= 0.0) {
        return;
    }
    const QSizeF target(frame.width() * scale, frame.height() * scale);
    const QRectF dst(area.x() + (area.width() - target.width()) / 2.0,
                     area.y() + (area.height() - target.height()) / 2.0,
                     target.width(),
                     target.height());
    painter->drawImage(dst, frame);
}
