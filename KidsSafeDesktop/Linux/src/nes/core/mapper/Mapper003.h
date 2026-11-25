#pragma once
#include "../NesCartridge.h"
#include "Mapper.h"
namespace nes {
namespace device {
class Mapper003 : public Mapper
{
  public:
    using Mapper::Mapper;
    bool hasHardwiredMirroring() const override;

    // PPU 写不覆写: 使用基类实现, 保留 CHR ROM -> CHR RAM 动态升级
    virtual void prgWrite(uint16_t address, uint8_t data) override;
};
} // namespace device
} // namespace nes