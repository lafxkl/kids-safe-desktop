#pragma once
#include <memory>
#include "NesCartridge.h"
#include "NesCPU.h"
#include "NesCPUBus.h"
#include "NesGamePad.h"
#include "NesPAPU.h"
#include "NesPPU.h"
#include "mapper/MapperHost.h"

namespace nes {

class Devices : public device::MapperHost
{
  private:
    std::unique_ptr<cpu::NesCPU> fcCPU;
    std::unique_ptr<cpu::NesCPUBus> fcCPUBus;
    std::unique_ptr<ppu::NesPPU> fcPPU;
    device::NesCartridge* fcCartridge = nullptr;  // 原始指针,由外部管理生命周期
    std::unique_ptr<device::NesGamePad> fcGamePad;
    std::unique_ptr<apu::NesPAPU> fcAPU;

  public:
    Devices();  // 不再需要 Famicom 参数
    ~Devices() = default;  // unique_ptr 会自动释放

    // ========================================================================
    // MapperHost: Mapper 需要的主机能力 (镜像/渲染状态/IRQ)
    // ========================================================================
    void setMirroring(device::Mirror mode) override
    {
        fcPPU->switchMirroring(mode);
    }

    bool isRenderingEnabled() const override
    {
        return fcPPU->isRenderingEnabled();
    }

    void raiseIrq(nes::InterruptType type) override
    {
        fcCPU->tryIrq(type);
    }

    void clearIrq(nes::InterruptType type) override
    {
        fcCPU->clrIrq(type);
    }
    
    // 设备访问器
    cpu::NesCPU* cpu()
    {
        return fcCPU.get();
    }
    cpu::NesCPUBus* bus()
    {
        return fcCPUBus.get();
    }
    ppu::NesPPU* ppu()
    {
        return fcPPU.get();
    }
    device::NesCartridge* cartridge()
    {
        return fcCartridge;  // 直接返回原始指针
    }
    void setCartridge(device::NesCartridge* cart)
    {
        fcCartridge = cart;  // 设置原始指针,不拥有所有权
    }
    device::NesGamePad* pad()
    {
        return fcGamePad.get();
    }
    apu::NesPAPU* apu()
    {
        return fcAPU.get();
    }
};

} // namespace nes
