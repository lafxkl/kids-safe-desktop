#include "../common.h"
#include "MapperFactory.h"
#include "Mapper002.h"

namespace nes {
namespace device {

void Mapper002::ppuWrite(uint16_t address, uint8_t data) {
    Mapper::ppuWrite(address, data);
}

void Mapper002::prgWrite(uint16_t address, uint8_t data) {
    if (Logger::shouldLog(LogLevel::Trace)) {
        LOG_TRACE << "Mapper002 BankSwitch: addr=0x" << std::hex << address 
                 << " data=0x" << static_cast<int>(data);
    }
    SetCPUBank_16KB(0, data);
}

void Mapper002::OnSave(nes::io::SaveBundle *bundle) {
    if (mROM->isChrRAM) {
        bundle->saveBlock(mROM->chrrom, 8192);
    }
}

void Mapper002::OnRestore(nes::io::SaveBundle *bundle) {
    if (mROM->isChrRAM) {
        bundle->restoreBlock(mROM->chrrom, 8192);
    }
}


bool Mapper002::hasHardwiredMirroring() const
{
    return true; // 镜像由卡带焊点决定, iNES 头位有效 (数据库修正白名单)
}

REGISTER_MAPPER(2, Mapper002);

} // namespace device
} // namespace nes
