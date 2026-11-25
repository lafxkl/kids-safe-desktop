#include "Mapper.h"
#include "../common.h"
#include "../Rom.h"
#include "../SaveBundle.h"
#include "assert.h"

namespace nes {
namespace device {

void Mapper::SetPPUBank_8KB(const uint8_t bank)
{
    uint16_t bank1kb = ((uint32_t)bank % mROM->chr8kbCount) << 3;
    mChrBank[0] = mROM->chrrom + (bank1kb + 0) * BANKSIZE_1KB;
    mChrBank[1] = mROM->chrrom + (bank1kb + 1) * BANKSIZE_1KB;
    mChrBank[2] = mROM->chrrom + (bank1kb + 2) * BANKSIZE_1KB;
    mChrBank[3] = mROM->chrrom + (bank1kb + 3) * BANKSIZE_1KB;
    mChrBank[4] = mROM->chrrom + (bank1kb + 4) * BANKSIZE_1KB;
    mChrBank[5] = mROM->chrrom + (bank1kb + 5) * BANKSIZE_1KB;
    mChrBank[6] = mROM->chrrom + (bank1kb + 6) * BANKSIZE_1KB;
    mChrBank[7] = mROM->chrrom + (bank1kb + 7) * BANKSIZE_1KB;
}

void Mapper::OnReset()
{
    SetCPUBank_16KB(0, 0);
    SetCPUBank_16KB(1, mROM->prg16kbCount - 1);
    SetPPUBank_4KB(0, 0);
    SetPPUBank_4KB(1, 1);
}

void Mapper::reset()
{
    memset(mSaveRAM, 0, sizeof(mSaveRAM));
    memset(mExRAM, 0, sizeof(mExRAM));
    memset(mBusRAM, 0, sizeof(mBusRAM));
    OnReset();
}

uint8_t Mapper::prgRamRead(uint16_t address)
{
    return mSaveRAM[address & 0x1FFF];
}

void Mapper::prgRamWrite(uint16_t address, uint8_t data)
{
    mSaveRAM[address & 0x1FFF] = data;
}

uint8_t Mapper::expansionRead(uint16_t address)
{
    if (address <= 0x41FF)
        return mBusRAM[address & 0x1FF];
    if (address >= 0x5000)
        return mExRAM[address & 0xFFF];
    return 0;
}

void Mapper::expansionWrite(uint16_t address, uint8_t data)
{
    if (address <= 0x41FF) {
        mBusRAM[address & 0x1FF] = data;
    } else if (address >= 0x5000) {
        mExRAM[address & 0xFFF] = data;
    }
}

void Mapper::prgWrite(uint16_t address, uint8_t data)
{
    // 基类默认拒绝写入: ROM 不可写。支持 bank switch 的 mapper 必须自行覆盖此方法。
    LOG_WARN << "Mapper 写入未实现: 0x" << std::hex << address << " = 0x"
             << static_cast<int>(data) << " (mapper 号 " << static_cast<int>(mROM->mapperNumber) << ")";
}

uint8_t Mapper::ppuRead(uint16_t address)
{
    return mChrBank[address >> 10][address & 0x3FF];
}

void Mapper::ppuWrite(uint16_t address, uint8_t data)
{
    // 真 CHR ROM 写不进去; 但部分 homebrew (Neslib/cc65 工具链) 头里放全零壳 CHR,
    // 运行时按 CHR RAM 上传图形 (如 Mineshaft/Invaders)。检测到写即动态升级为
    // CHR RAM, 商业游戏从不写此区域不受影响。
    if (!mROM->isChrRAM) {
        mROM->isChrRAM = true;
        LOG_INFO << "PPU 写入 CHR 区域: CHR ROM 动态升级为 CHR RAM (壳 CHR 的 homebrew)";
    }
    mChrBank[address >> 10][address & 0x3FF] = data;
}

void Mapper::save(nes::io::SaveBundle* bundle)
{

    this->OnSave(bundle);

    bundle->saveBlock(mSaveRAM, sizeof(mSaveRAM));
    bundle->saveBlock(mExRAM, sizeof(mExRAM));

    // 保存CPU BANK相对指针偏移
    for (size_t i = 0; i < 4; i++) {
        bundle->savePtr(mPrgBank[i] - mROM->prgrom);
    }

    // 保存PPU BANK相对指针偏移
    for (size_t i = 0; i < 8; i++) {
        bundle->savePtr(mChrBank[i] - mROM->chrrom);
    }
}

void Mapper::restore(nes::io::SaveBundle* bundle)
{
    this->OnRestore(bundle);

    bundle->restoreBlock(mSaveRAM, sizeof(mSaveRAM));
    bundle->restoreBlock(mExRAM, sizeof(mExRAM));

    for (size_t i = 0; i < 4; i++) {
        mPrgBank[i] = bundle->restorePtr() + mROM->prgrom;
    }
    for (size_t i = 0; i < 8; i++) {
        mChrBank[i] = bundle->restorePtr() + mROM->chrrom;
    }
}

void Mapper::OnSave(nes::io::SaveBundle* bundle)
{
    // TODO
}

void Mapper::OnRestore(nes::io::SaveBundle* bundle)
{
    // TODO
}

} // namespace device
} // namespace nes
