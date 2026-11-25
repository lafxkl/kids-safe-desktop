#pragma once
#include "Mapper.h"

namespace nes {
namespace device {

/**
 * @brief Mapper 023: Konami VRC2b / VRC4e / VRC4f
 *
 * 寄存器地址解码与 FCEUX vrc2and4.cpp 的 Mapper23 参数一致
 * (reg1mask=0x15, reg2mask=0x2a):
 * - $8000-$8FFF: 8KB PRG bank -> $8000
 * - $9000-$9FFF: 镜像 (bit1-0: 0=垂直 1=水平 2=单屏低 3=单屏高)
 * - $9002-$9FFF: PRG 交换模式 (bit1: 1=交换 $8000/$C000)
 * - $A000-$AFFF: 8KB PRG bank -> $A000
 * - $B000-$E003: 8KB CHR 分为 8 个 1KB bank, A1 选 bank, A0 选高低半字节
 * - $F000-$F003: IRQ (VRC4)
 */
class Mapper023 : public Mapper
{
    uint8_t mPrgReg[2] = {0, 0};   ///< $8000 / $A000 8KB PRG bank
    uint8_t mChrReg[8] = {0};      ///< 1KB CHR bank 低 4 位
    uint8_t mChrHi[8] = {0};       ///< 1KB CHR bank bit4 (高半字节写入时捕获)
    uint8_t mRegCmd = 0;           ///< $9002 PRG 交换模式
    uint8_t mMirror = 0;           ///< $9000 镜像值

    uint8_t mIrqLatch = 0;
    uint8_t mIrqCount = 0;
    uint8_t mIrqEnable = 0;
    uint8_t mIrqMode = 0;
    uint8_t mIrqCmd = 0;

    void Sync();

  public:
    using Mapper::Mapper;
    bool hasHardwiredMirroring() const override { return false; }

    void OnReset() override;
    void prgWrite(uint16_t address, uint8_t data) override;
    void onScanline(uint32_t line) override;

    void OnSave(nes::io::SaveBundle* bundle) override;
    void OnRestore(nes::io::SaveBundle* bundle) override;
};

} // namespace device
} // namespace nes
