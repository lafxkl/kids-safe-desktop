#include "NesCPUBus.h"
#include <cstdio>
#include <cstdlib>
#include "common.h"
#include "NesPPU.h"      // 完整类型
#include "NesPAPU.h"     // 完整类型
#include "NesCartridge.h" // 完整类型
#include "NesGamePad.h"   // 完整类型

namespace nes {
namespace cpu {

NesCPUBus::NesCPUBus()
    : mPPU(nullptr), mAPU(nullptr), mCartridge(nullptr), mGamePad(nullptr)
{
}

/// <summary>
/// CPU 总线写: 按地址空间分派
/// </summary>
/// <param name="address">地址</param>
/// <param name="data">数据</param>
void NesCPUBus::writeByte(uint16_t address, uint8_t data)
{
    static const bool memTrace = std::getenv("MEM_TRACE") != nullptr;
    if (memTrace && address >= 0x0030 && address <= 0x0036)
        fprintf(stderr, "W %04X=%02X\n", address, data);

    if (address < 0x2000) { // $0000-$1FFF: 2KB RAM 镜像
        this->mMainRam[address & 0x07FF] = data;
        return;
    }
    if (address < 0x4000) { // $2000-$3FFF: PPU 寄存器
        if (!mPPU) {
            LOG_ERROR << "Bus writeByte: mPPU is null at address 0x" << std::hex << address;
            return;
        }
        mPPU->cpuWriteRegister(address, data);
        return;
    }
    if (address < 0x4020) { // $4000-$401F: APU/手柄/OAM DMA
        writeIoRegister(address, data);
        return;
    }
    // $4020-$FFFF: 卡带窗口 (mapper 内部再按 $4020/$6000/$8000 分派)
    if (!mCartridge) {
        LOG_ERROR << "Bus writeByte: mCartridge is null at address 0x" << std::hex << address;
        return;
    }
    mCartridge->cpuWrite(address, data);
}

/// <summary>
/// $4000-$401F 的寄存器写 (APU/手柄/OAM DMA)
/// </summary>
void NesCPUBus::writeIoRegister(uint16_t address, uint8_t data)
{
    if (address == 0x4014) {
        if (!mPPU) {
            LOG_ERROR << "Bus writeByte: mPPU is null at 0x4014";
            return;
        }
        mPPU->cpuWriteRegister(address, data);
    } else if (address == 0x4016) {
        if (!mGamePad) {
            LOG_ERROR << "Bus writeByte: mGamePad is null at 0x4016";
            return;
        }
        mGamePad->cpuWriteRegister(address, data);
    } else {
        if (!mAPU) {
            LOG_ERROR << "Bus writeByte: mAPU is null at address 0x" << std::hex << address;
            return;
        }
        mAPU->cpuWriteRegister(address, data);
    }
}

/// <summary>
/// </summary>
/// <param name="address">地址</param>
/// <param name="data">数据</param>
void NesCPUBus::writeWord(uint16_t address, uint16_t data)
{
    writeByte(address, data & 0xFF);
    writeByte(address + 1, (data >> 8) & 0xFF);
}

/// <summary>
/// CPU 总线读: 按地址空间分派
/// </summary>
/// <param name="address">地址</param>
/// <returns></returns>
uint8_t NesCPUBus::readByte(uint16_t address)
{
    if (address < 0x2000) // $0000-$1FFF: 2KB RAM 镜像
        return this->mMainRam[address & 0x07FF];
    if (address < 0x4000) { // $2000-$3FFF: PPU 寄存器
        if (!mPPU) {
            LOG_ERROR << "Bus readByte: mPPU is null at address 0x" << std::hex << address;
            return 0;
        }
        return mPPU->cpuReadRegister(address);
    }
    if (address < 0x4020) // $4000-$401F: APU/手柄/OAM DMA
        return readIoRegister(address);

    // $4020-$FFFF: 卡带窗口 (mapper 内部再按 $4020/$6000/$8000 分派)
    if (!mCartridge) {
        LOG_ERROR << "Bus readByte: mCartridge is null at address 0x" << std::hex << address;
        return 0;
    }
    return mCartridge->cpuRead(address);
}

/// <summary>
/// $4000-$401F 的寄存器读 (APU/手柄/OAM DMA)
/// </summary>
uint8_t NesCPUBus::readIoRegister(uint16_t address)
{
    if (address == 0x4014) {
        if (!mPPU) {
            LOG_ERROR << "Bus readByte: mPPU is null at 0x4014";
            return 0;
        }
        return mPPU->cpuReadRegister(address);
    }
    if (address == 0x4016 || address == 0x4017) {
        if (!mGamePad) {
            LOG_ERROR << "Bus readByte: mGamePad is null at address 0x" << std::hex << address;
            return 0;
        }
        return mGamePad->cpuReadRegister(address);
    }
    if (!mAPU) {
        LOG_ERROR << "Bus readByte: mAPU is null at address 0x" << std::hex << address;
        return 0;
    }
    return mAPU->cpuReadRegister(address);
}

/// <summary>
/// </summary>
/// <param name="address">地址</param>
/// <returns></returns>
uint16_t NesCPUBus::readWord(uint16_t address)
{
    return (readByte(address + 1) << 8 | (readByte(address) & 0x00ff)) & 0xFFFF;
}

/// <summary>
/// 从 CPU 总线批量读取 (OAM DMA 等)
/// </summary>
/// <param name="dst">目标缓存指针</param>
/// <param name="src">CPU 总线地址</param>
/// <param name="size">要复制的数据长度</param>
void NesCPUBus::readBlock(uint8_t* dst, uint16_t src, int size)
{
    for (int i = 0; i < size; i++) {
        *dst++ = readByte(src++);
    }
}

void NesCPUBus::save(nes::io::SaveBundle* bundle)
{
    bundle->saveBlock(mMainRam, sizeof(mMainRam));
}

void NesCPUBus::restore(nes::io::SaveBundle* bundle)
{
    bundle->restoreBlock(mMainRam, sizeof(mMainRam));
}

} // namespace cpu
} // namespace nes
