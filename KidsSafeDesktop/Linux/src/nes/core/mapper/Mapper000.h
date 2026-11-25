#pragma once
#include "Mapper.h"
namespace nes {
namespace device {
class Mapper000 : public Mapper
{

  public:
    using Mapper::Mapper;
    bool hasHardwiredMirroring() const override;
};

} // namespace device
} // namespace nes
