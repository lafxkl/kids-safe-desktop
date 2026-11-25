#pragma once
#include "../Rom.h"
#include "Mapper.h"
/// <summary>
/// </summary>
namespace nes {
class Famicom;

namespace device {
class Mapper001 : public Mapper
{
  private:
    uint8_t mShifter = 0x10;
    uint8_t mControl = 0;
    uint8_t mPRGMode = 0;
    uint8_t mIsCHR8kb = FALSE;
    uint16_t mLastWriteAddr;

    enum { MMC1_1024K, MMC1_512K, MMC1_SMALL };

  private:
    void UpdatePrgbank();
    void WriteRegister(uint16_t address);
    void UpdateControl(uint8_t data);
    void UpdateChrbank0();
    void UpdateChrbank1();

  public:
    using Mapper::Mapper;
    virtual void OnReset() override;
    virtual void prgWrite(uint16_t address, uint8_t data) override;
    virtual void OnSave(nes::io::SaveBundle* bundle) override;
    virtual void OnRestore(nes::io::SaveBundle* bundle) override;
};

} // namespace device
} // namespace nes