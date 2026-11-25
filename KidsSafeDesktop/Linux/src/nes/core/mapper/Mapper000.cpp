#include "Mapper000.h"
#include "MapperFactory.h"
#include "../common.h"

namespace nes {
namespace device {

// $6000-$7FFF: 不覆写, 使用基类的 mSaveRAM 读写。
// 真机 NROM 该区域通常无设备, 但大量 homebrew (cc65 工具链) 把工作数据
// 放在 $6000, 依赖模拟器提供 8K WRAM (FCEUX/Mesen 同样兼容此行为)。
// $8000-$FFFF 写: 不覆写, 使用基类的"警告并忽略"(NROM 无 bank 寄存器)。

bool Mapper000::hasHardwiredMirroring() const
{
    return true; // 镜像由卡带焊点决定, iNES 头位有效 (数据库修正白名单)
}

REGISTER_MAPPER(0, Mapper000);

} // namespace device
} // namespace nes
