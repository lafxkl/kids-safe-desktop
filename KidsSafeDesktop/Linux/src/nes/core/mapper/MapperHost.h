#pragma once
#include "../Rom.h"
#include "../cpu/InterruptFlags.h"

namespace nes {
namespace device {

/**
 * @brief Mapper 对主机的依赖接口
 *
 * Mapper 只需要四类主机能力: 切换镜像、查询渲染状态、触发/清除 IRQ。
 * 由 Devices 实现, 避免 Mapper 依赖整个设备容器。
 */
class MapperHost
{
  public:
    virtual ~MapperHost() = default;

    /// @brief 切换 nametable 镜像
    virtual void setMirroring(Mirror mode) = 0;

    /// @brief 渲染是否开启 (MMC3 扫描线 IRQ 门控)
    virtual bool isRenderingEnabled() const = 0;

    /// @brief 触发 IRQ
    virtual void raiseIrq(nes::InterruptType type) = 0;

    /// @brief 清除 IRQ
    virtual void clearIrq(nes::InterruptType type) = 0;
};

} // namespace device
} // namespace nes
