#pragma once
#include "Mapper.h"

namespace nes {
namespace device {
class Mapper007 : public Mapper
{
  public:
    using Mapper::Mapper;  // 继承基类构造函数
    void OnReset() override;
    void prgWrite(uint16_t address, uint8_t data) override;
};

} // namespace device
} // namespace nes