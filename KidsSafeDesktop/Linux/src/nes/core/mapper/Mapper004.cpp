#include "../common.h"
#include <cstdio>
#include <cstdlib>
#include "MapperFactory.h"
#include "Mapper004.h"

namespace nes {
namespace device {


void Mapper004::OnReset() {

    mRegid = 0;
    mPrmode = 0;
    mCrmode = 0;
    latch = 0;
    counter = 0;
    mIrqEnable = 0;


    
    SetCPUBank_8KB(0, 0);
    SetCPUBank_8KB(1, 1);
    const int last = mROM->prg16kbCount * 2;
    SetCPUBank_8KB(2, last - 2);
    SetCPUBank_8KB(3, last - 1);

    // CHR-Rom
    for (int i = 0; i != 8; ++i)
        setChr1K(i, i);
}

void Mapper004::onScanline(uint32_t line) {
    if (!mHost->isRenderingEnabled())
        return;


    if (line > 239)
        return;

    if (mIrqReload) {
        counter = latch;
        mIrqReload = 0;
    }
    else if (counter > 0)
        counter--;

    if (counter == 0) {
        mIrqReload = 0xFF;
        if (mIrqEnable) {
            mHost->raiseIrq(nes::InterruptType::hsyncInt);
        }
    }
#if 0
    if (counter) {
        --counter;
        if (!counter && mIrqEnable) {
            famicom->cpu->tryIrq(nes::InterruptType::hsyncInt);
        }
    }
    else {
        counter = latch;
    }
#endif
}

void Mapper004::SelectBank(uint8_t data)
{
    mRegid = data & 7;
    mPrmode = (data >> 6) & 1;
    mCrmode = (data >> 7) & 1;
    UpdatePPUBanks();
    UpdateCPUBanks();

}

void Mapper004::UpdateBankData(uint8_t bank)
{
    if (mRegid <= 5) {
        mRegs[mRegid] = bank;
        UpdatePPUBanks();
    }
    else {
        mRegs[mRegid] = bank & 0x3F;
        UpdateCPUBanks();
    }
}

void Mapper004::UpdateCPUBanks() {
    const int last = mROM->prg16kbCount * 2;
    if (mPrmode) {
        // (-2) R7 R6 (-1)
        SetCPUBank_8KB(0, last - 2);
        SetCPUBank_8KB(1, mRegs[7]);
        SetCPUBank_8KB(2, mRegs[6]);
        SetCPUBank_8KB(3, last - 1);
    }
    else {
        // R6 R7 (-2) (-1)
        SetCPUBank_8KB(0, mRegs[6]);
        SetCPUBank_8KB(1, mRegs[7]);
        SetCPUBank_8KB(2, last - 2);
        SetCPUBank_8KB(3, last - 1);
    }
}

void Mapper004::UpdatePPUBanks() {
    if (mCrmode) {
        // R2R3R4R5R0r0R1r1
        setChr1K(0, mRegs[2]);
        setChr1K(1, mRegs[3]);
        setChr1K(2, mRegs[4]);
        setChr1K(3, mRegs[5]);
        setChr1K(4, mRegs[0] & 0xFE);
        setChr1K(5, mRegs[0] | 1);
        setChr1K(6, mRegs[1] & 0xFE);
        setChr1K(7, mRegs[1] | 1);
    }
    else {
        // R0r0R1r1R2R3R4R5
        setChr1K(0, mRegs[0] & 0xFE);
        setChr1K(1, mRegs[0] | 1);
        setChr1K(2, mRegs[1] & 0xFE);
        setChr1K(3, mRegs[1] | 1);
        setChr1K(4, mRegs[2]);
        setChr1K(5, mRegs[3]);
        setChr1K(6, mRegs[4]);
        setChr1K(7, mRegs[5]);
    }


}

void Mapper004::UpdateMirroring(uint8_t data)
{
    if (!mROM->isFourScreen) {
        mHost->setMirroring((data & 1)
                                        ? Mirror::HORIZONTAL
                                        : Mirror::VERTICAL);
    }

}

void Mapper004::prgWrite(uint16_t address, uint8_t data) {
    static const bool mmc3Trace = std::getenv("MMC3_TRACE") != nullptr;
    if (mmc3Trace)
        fprintf(stderr, "MMC3 W %04X=%02X\n", address, data);
    switch (address & 0xE001)
    {
    case 0x8000:
        SelectBank(data);
        break;
    case 0x8001:
        UpdateBankData(data);
        break;
    case 0xA000:
        // $A000-$BFFE Mirroring
        UpdateMirroring(data);
        break;
    case 0xA001:
        // $A001-$BFFF PRG RAM protect
        if (data & 0x80)
        {
            // enable save RAM $6000-$7FFF
        }
        else
        {
            // disable save RAM $6000-$7FFF
        }
        break;
    case 0xC000:
        // $C000-$DFFE IRQ latch
        latch = data;
        break;
    case 0xC001:
        // $C001-$DFFF IRQ reload
        mIrqReload = 0xFF;
        counter = 0;
        break;
    case 0xE000:
        // $E000-$FFFE IRQ disable
        mIrqEnable = 0;
        mHost->clearIrq(nes::InterruptType::hsyncInt);
        break;
    case 0xE001:
        // $E001-$FFFF IRQ enable
        mIrqEnable = 1;
        break;
    }
}

void Mapper004::OnSave(nes::io::SaveBundle *bundle)
{
    bundle->saveByte(mRegid);
    bundle->saveByte(mPrmode);
    bundle->saveByte(mCrmode);
    bundle->saveBlock(mRegs, 8);
    bundle->saveByte(latch);
    bundle->saveByte(counter);
    bundle->saveByte(mIrqEnable);
    bundle->saveByte(mIrqReload);
}

void Mapper004::OnRestore(nes::io::SaveBundle *bundle)
{
    mRegid = bundle->restoreByte();
    mPrmode = bundle->restoreByte();
    mCrmode = bundle->restoreByte();
    bundle->restoreBlock(mRegs, 8);
    latch = bundle->restoreByte();
    counter = bundle->restoreByte();
    mIrqEnable = bundle->restoreByte();
    mIrqReload = bundle->restoreByte();
}


REGISTER_MAPPER(4, Mapper004);

} // namespace device
} // namespace nes
