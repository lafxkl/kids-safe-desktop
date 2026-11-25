#pragma once
#include "Mapper.h"
namespace nes {
namespace device {
/// <summary>
///
/// </summary>
class Mapper002 : public Mapper
{
  public:
    using Mapper::Mapper;
    bool hasHardwiredMirroring() const override;
    virtual void ppuWrite(uint16_t address, uint8_t data) override;
    virtual void prgWrite(uint16_t address, uint8_t data) override;
    virtual void OnSave(nes::io::SaveBundle* bundle) override;
    virtual void OnRestore(nes::io::SaveBundle* bundle) override;
};
} // namespace device
} // namespace nes