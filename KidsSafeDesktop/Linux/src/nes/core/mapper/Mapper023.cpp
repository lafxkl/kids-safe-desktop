#include "../common.h"
#include "MapperFactory.h"
#include "Mapper023.h"
#include "../NesPPU.h"

namespace nes {
namespace device {

void Mapper023::OnReset()
{
    mPrgReg[0] = mPrgReg[1] = 0;
    for (int i = 0; i < 8; i++) {
        mChrReg[i] = 0;
        mChrHi[i] = 0;
    }
    mRegCmd = 0;
    mMirror = 0;
    mIrqLatch = mIrqCount = mIrqEnable = mIrqMode = mIrqCmd = 0;
    Sync();
}

void Mapper023::Sync()
{
    // 8KB PRG 布局: (swap? $C000 : $8000) = bank0, 另一处 = 倒数第二
    //               $A000 = bank1, $E000 = 最后一 bank
    // 0x1E/0x1F 经 SetCPUBank_8KB 的取模换算, 对任意 PRG 大小都指向倒数两个 bank
    if (mRegCmd & 0x02) {
        SetCPUBank_8KB(0, 0x1E);
        SetCPUBank_8KB(2, mPrgReg[0]);
    } else {
        SetCPUBank_8KB(0, mPrgReg[0]);
        SetCPUBank_8KB(2, 0x1E);
    }
    SetCPUBank_8KB(1, mPrgReg[1]);
    SetCPUBank_8KB(3, 0x1F);

    for (int i = 0; i < 8; i++) {
        SetPPUBank_1KB((uint8_t)i, (uint8_t)(mChrHi[i] | mChrReg[i]));
    }

    switch (mMirror & 0x03) {
    case 0:
        mHost->setMirroring(Mirror::VERTICAL);
        break;
    case 1:
        mHost->setMirroring(Mirror::HORIZONTAL);
        break;
    case 2:
        mHost->setMirroring(Mirror::SINGLE_SCREEN_LOWER);
        break;
    case 3:
        mHost->setMirroring(Mirror::SINGLE_SCREEN_UPPER);
        break;
    }
}

void Mapper023::prgWrite(uint16_t address, uint8_t data)
{
    // 地址折叠 (FCEUX Mapper23 解码): 组内只用 A0/A1, 其余位决定寄存器组
    const uint16_t A = static_cast<uint16_t>(
        (address & 0xF000) | ((address & 0x2A) ? 0x02 : 0x00) | ((address & 0x15) ? 0x01 : 0x00));

    if (A >= 0xB000 && A <= 0xE003) {
        // CHR 1KB bank: i = 4KB 组内 bank 号 (0-7), A0 选低/高半字节
        const int i = ((A >> 1) & 1) | ((A - 0xB000) >> 11);
        const int nibble = (A & 1) << 2;
        mChrReg[i] = static_cast<uint8_t>(
            (mChrReg[i] & (0xF0 >> nibble)) | ((data & 0x0F) << nibble));
        if (nibble)
            mChrHi[i] = static_cast<uint8_t>((data & 0x10) << 4);
        Sync();
        return;
    }

    switch (A & 0xF003) {
    case 0x8000:
        mPrgReg[0] = data & 0x1F;
        Sync();
        break;
    case 0xA000:
        mPrgReg[1] = data & 0x1F;
        Sync();
        break;
    case 0x9000:
        if (data != 0xFF)
            mMirror = data;
        Sync();
        break;
    case 0x9002:
        mRegCmd = data;
        Sync();
        break;
    case 0xF000:
        mIrqLatch = static_cast<uint8_t>((mIrqLatch & 0xF0) | (data & 0x0F));
        break;
    case 0xF001:
        mIrqLatch = static_cast<uint8_t>((mIrqLatch & 0x0F) | (data << 4));
        break;
    case 0xF002:
        mIrqCount = mIrqLatch;
        mIrqMode = data & 0x04;
        mIrqEnable = data & 0x02;
        mIrqCmd = data & 0x01;
        break;
    case 0xF003:
        mIrqEnable = mIrqCmd;
        break;
    default:
        break;
    }
}

void Mapper023::onScanline(uint32_t line)
{
    (void)line;
    if (!mIrqEnable)
        return;
    // 扫描线级近似: VRC4 的 cycle/scanline 两种模式在我们 1 行 = 341 周期的
    // 粒度下等价为每行计一次
    if (++mIrqCount == 0) {
        mHost->raiseIrq(nes::InterruptType::hsyncInt);
        mIrqCount = mIrqLatch;
    }
}

void Mapper023::OnSave(nes::io::SaveBundle* bundle)
{
    bundle->saveBlock(mPrgReg, sizeof(mPrgReg));
    bundle->saveBlock(mChrReg, sizeof(mChrReg));
    bundle->saveBlock(mChrHi, sizeof(mChrHi));
    bundle->saveByte(mRegCmd);
    bundle->saveByte(mMirror);
    bundle->saveByte(mIrqLatch);
    bundle->saveByte(mIrqCount);
    bundle->saveByte(mIrqEnable);
    bundle->saveByte(mIrqMode);
    bundle->saveByte(mIrqCmd);
}

void Mapper023::OnRestore(nes::io::SaveBundle* bundle)
{
    bundle->restoreBlock(mPrgReg, sizeof(mPrgReg));
    bundle->restoreBlock(mChrReg, sizeof(mChrReg));
    bundle->restoreBlock(mChrHi, sizeof(mChrHi));
    mRegCmd = bundle->restoreByte();
    mMirror = bundle->restoreByte();
    mIrqLatch = bundle->restoreByte();
    mIrqCount = bundle->restoreByte();
    mIrqEnable = bundle->restoreByte();
    mIrqMode = bundle->restoreByte();
    mIrqCmd = bundle->restoreByte();
    Sync();
}

REGISTER_MAPPER(23, Mapper023);

} // namespace device
} // namespace nes
