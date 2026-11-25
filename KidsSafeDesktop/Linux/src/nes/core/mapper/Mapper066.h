#pragma once
#include "Mapper.h"
namespace nes {
namespace device {
class Mapper066 : public Mapper
{
  public:
    using Mapper::Mapper;
    bool hasHardwiredMirroring() const override;
    void OnReset() override;
    virtual void prgRamWrite(uint16_t address, uint8_t data) override;
    virtual void prgWrite(uint16_t address, uint8_t data) override;

  private:
    void switchBanks(uint8_t data);
};
} // namespace device
} // namespace nes