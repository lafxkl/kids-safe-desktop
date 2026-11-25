#pragma once
#include <stdint.h>
#include <string>

namespace nes {
namespace device {

class Mapper;
typedef struct FAMICOM_ROM
{
    uint8_t prg16kbCount; //??????????
    uint8_t chr8kbCount;

    uint32_t prgRomSize = 0;
    uint32_t chrRomSize = 0;

    uint8_t* prgrom = nullptr;
    uint8_t* chrrom = nullptr;

    uint8_t version;

    uint8_t mapperNumber;  // Mapper ???
    bool isVmirroring;     // ???Vertical Mirroring(?????)
    bool isFourScreen;     // ???FourScreen
    bool hasBatteryBacked; // ?????SRAM(???????)
    bool hasTrainer;
    uint32_t size;
    bool isChrRAM;
    std::string romPath;   // 来源文件名 (用于坏 iNES 头的镜像覆盖表)
    Mapper* mapper = nullptr;
} FcRom;

enum class Mirror : uint8_t {
    SINGLE_SCREEN_LOWER = 0,
    SINGLE_SCREEN_UPPER = 1,
    VERTICAL = 2,
    HORIZONTAL = 3,
    FOUR_SCREEN = 4
};

} // namespace device
} // namespace nes
