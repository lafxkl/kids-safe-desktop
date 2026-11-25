#include "Mapper066.h"
#include "MapperFactory.h"
#include "../common.h"
#include <assert.h>
namespace nes {
namespace device {

void Mapper066::OnReset()
{
    SetCPUBank_16KB(0, 0);
    SetCPUBank_16KB(1, mROM->prg16kbCount - 1);
    SetPPUBank_8KB(0);
}

void Mapper066::prgRamWrite(uint16_t address, uint8_t data)
{
    // 历史行为: $6000-$7FFF 的写也触发 bank 切换 (保留, 避免改变已有游戏行为)
    switchBanks(data);
}

void Mapper066::prgWrite(uint16_t address, uint8_t data)
{
    switchBanks(data);
}

void Mapper066::switchBanks(uint8_t data)
{
    uint8_t prgbank = (data & 0xF0) >> 4 << 1;
    SetCPUBank_16KB(0, prgbank);
    SetCPUBank_16KB(1, prgbank + 1);
    SetPPUBank_8KB(data & 0x0F);
}


bool Mapper066::hasHardwiredMirroring() const
{
    return true; // 镜像由卡带焊点决定, iNES 头位有效 (数据库修正白名单)
}

REGISTER_MAPPER(66, Mapper066);

} // namespace device
} // namespace nes
