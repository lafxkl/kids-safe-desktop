#pragma once
#include "common.h"
#include "SaveBundle.h"

namespace nes {

// 前向声明
namespace ppu { class NesPPU; }
namespace apu { class NesPAPU; }
namespace device { class NesCartridge; class NesGamePad; }

namespace cpu {

class NesCPUBus
{
  private:
    uint8_t mMainRam[2048] = {0}; // 2KB RAM
    
    ppu::NesPPU* mPPU;
    apu::NesPAPU* mAPU;
    device::NesCartridge* mCartridge;
    device::NesGamePad* mGamePad;

    /// @brief $4000-$401F: APU/手柄/OAM DMA 寄存器
    uint8_t readIoRegister(uint16_t address);
    void writeIoRegister(uint16_t address, uint8_t data);

  public:
    /**
     */
    NesCPUBus();
    
    /**
     */
    void addPPU(ppu::NesPPU* ppu) { mPPU = ppu; }
    
    /**
     */
    void addAPU(apu::NesPAPU* apu) { mAPU = apu; }
    
    /**
     * @brief 添加卡带
     */
    void addCartridge(device::NesCartridge* cartridge) { mCartridge = cartridge; }
    
    /**
     * @brief 添加手柄
     */
    void addGamePad(device::NesGamePad* gamePad) { mGamePad = gamePad; }
    
    void writeByte(uint16_t address, uint8_t data);
    void writeWord(uint16_t address, uint16_t data);
    uint8_t readByte(uint16_t address);
    uint16_t readWord(uint16_t address);
    void readBlock(uint8_t* dst, uint16_t src, int size);
    void save(nes::io::SaveBundle* bundle);
    void restore(nes::io::SaveBundle* bundle);
};

} // namespace cpu
} // namespace nes
