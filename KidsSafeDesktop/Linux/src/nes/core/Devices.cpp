#include "Devices.h"

namespace nes {

Devices::Devices()
{
    // 1. 独立创建所有组件 (无依赖顺序)
    auto mGamePad = std::make_unique<device::NesGamePad>();
    auto mPPU = std::make_unique<ppu::NesPPU>();
    auto mAPU = std::make_unique<apu::NesPAPU>();
    auto mCPUBus = std::make_unique<cpu::NesCPUBus>();
    auto mCPU = std::make_unique<cpu::NesCPU>();
    
    // 2. 通过 Setter 建立连接 (顺序无关!)
    mCPUBus->addPPU(mPPU.get());
    mCPUBus->addAPU(mAPU.get());
    // 注意: Cartridge 由外部管理,此处不设置
    mCPUBus->addCartridge(nullptr);  // 初始为空,等待 insertCartridge
    mCPUBus->addGamePad(mGamePad.get());
    
    mCPU->setBus(mCPUBus.get());
    mPPU->setCPU(mCPU.get());
    // 注意: Cartridge 由外部管理,此处不设置
    mPPU->setCartridge(nullptr);  // 初始为空,等待 insertCartridge
    mAPU->setCPU(mCPU.get());
    
    // 3. 移动到成员变量
    fcCartridge = nullptr;  // 卡带插槽初始为空
    fcGamePad = std::move(mGamePad);
    fcPPU = std::move(mPPU);
    fcAPU = std::move(mAPU);
    fcCPUBus = std::move(mCPUBus);
    fcCPU = std::move(mCPU);
}

// 析构函数使用 default，unique_ptr 会自动释放资源

} // namespace nes
