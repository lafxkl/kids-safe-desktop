#include "../common.h"
#include "MapperFactory.h"
#include "Mapper003.h"

namespace nes {
namespace device {

void Mapper003::prgWrite(uint16_t address, uint8_t data) {
    SetPPUBank_8KB(data);
}


bool Mapper003::hasHardwiredMirroring() const
{
    return true; // 镜像由卡带焊点决定, iNES 头位有效 (数据库修正白名单)
}

REGISTER_MAPPER(3, Mapper003);

} // namespace device
} // namespace nes
