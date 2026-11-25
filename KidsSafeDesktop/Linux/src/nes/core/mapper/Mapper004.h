#pragma once
#include "Mapper.h"

namespace nes {

// Forward declarations at nes namespace level
namespace io { class SaveBundle; }

class Famicom;

namespace device {

/// <summary>
/// 
/// </summary>
class Mapper004 :public Mapper
{
protected:
    /**
     * @brief 设置 1KB CHR bank (虚函数: 派生 mapper 可重定向, 如 Mapper074 的 CHR RAM)
     */
    virtual void setChr1K(int pos, int bank) { SetPPUBank_1KB((uint8_t)pos, (uint8_t)bank); }

private:
    uint8_t mRegid = 0;
    uint8_t mPrmode = 0;
    uint8_t mCrmode = 0;
    uint8_t mRegs[8] = { 0,2,4,5,6,7,0,1 };
    uint8_t latch = 0xFF;
    uint8_t counter = 0;
    uint8_t mIrqEnable = 0;
    uint8_t mIrqReload = 0;

public:

    using Mapper::Mapper;
    virtual void OnReset() override;
    virtual void onScanline(uint32_t line) override;
    void SelectBank(uint8_t data);
    void UpdateBankData(uint8_t bank);
    void UpdateCPUBanks();
    void UpdatePPUBanks();
    void UpdateMirroring(uint8_t data);
    virtual void prgWrite(uint16_t address, uint8_t data) override;
    virtual void OnSave(nes::io::SaveBundle* bundle) override;
    virtual void OnRestore(nes::io::SaveBundle* bundle) override;

};

} // namespace device
} // namespace nes
