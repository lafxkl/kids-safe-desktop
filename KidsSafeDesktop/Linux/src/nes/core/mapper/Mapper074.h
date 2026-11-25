#pragma once

#include "Mapper004.h"
namespace nes {
namespace device {

/**
 * @brief Mapper 074: Waixing MMC3 变体
 *
 * 寄存器与 MMC3 相同, 但 CHR bank 8/9 (1KB) 映射到板载 2KB CHR RAM
 * (FCEUX M74CW 行为)。中文 RPG (重装机兵/机甲战士等) 用这块 RAM
 * 存放动态汉字点阵; 若按普通 MMC3 处理会从 CHR ROM 取到错误图案。
 */
class Mapper074 : public Mapper004
{
    uint8_t mChrRam[2048] = {0}; ///< 板载 2KB CHR RAM (bank 8/9)

  public:
    using Mapper004::Mapper004;

    bool hasHardwiredMirroring() const override { return false; }

  protected:
    void setChr1K(int pos, int bank) override
    {
        // FCEUX M74CW: 1KB bank 值 8/9 → 板载 CHR RAM
        if (bank == 8 || bank == 9) {
            mChrBank[pos] = mChrRam + (bank - 8) * 1024;
        } else {
            SetPPUBank_1KB((uint8_t)pos, (uint8_t)bank);
        }
    }

  public:
    // 覆盖完整 save/restore: 基类以"指针相对 chrrom 的偏移"保存 bank 映射,
    // 而本 mapper 的 CHR bank 可能指向板载 RAM, 偏移无意义。改为只存寄存器
    // 与 RAM, 读档后按寄存器重建 bank 指针。
    void save(nes::io::SaveBundle* bundle) override
    {
        OnSave(bundle);
        bundle->saveBlock(mSaveRAM, sizeof(mSaveRAM));
        bundle->saveBlock(mExRAM, sizeof(mExRAM));
        bundle->saveBlock(mChrRam, sizeof(mChrRam));
    }

    void restore(nes::io::SaveBundle* bundle) override
    {
        OnRestore(bundle);
        bundle->restoreBlock(mSaveRAM, sizeof(mSaveRAM));
        bundle->restoreBlock(mExRAM, sizeof(mExRAM));
        bundle->restoreBlock(mChrRam, sizeof(mChrRam));
        UpdatePPUBanks();
        UpdateCPUBanks();
    }
};

} // namespace device
} // namespace nes
