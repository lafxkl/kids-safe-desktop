#include "../common.h"
#include "MapperFactory.h"
#include "Mapper007.h"
///
/// </summary>
namespace nes {
namespace device {

void Mapper007::OnReset()
{
    SetCPUBank_32KB(0);
    SetPPUBank_8KB(0);
    mHost->setMirroring(Mirror::SINGLE_SCREEN_LOWER);
}

void Mapper007::prgWrite(uint16_t address, uint8_t data)
{
    SetCPUBank_32KB(data & 0x07);
    if (data & 0x10)
        mHost->setMirroring(Mirror::SINGLE_SCREEN_UPPER);
    else
        mHost->setMirroring(Mirror::SINGLE_SCREEN_LOWER);
}


REGISTER_MAPPER(7, Mapper007);

} // namespace device
} // namespace nes
