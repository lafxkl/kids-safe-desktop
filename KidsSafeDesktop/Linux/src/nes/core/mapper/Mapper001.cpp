#include "../common.h"
#include "MapperFactory.h"
#include "Mapper001.h"
#include "../Rom.h"

namespace nes {
namespace device {


/// <summary>
/// </summary>

void Mapper001::OnReset() {
    //CMapper::reset();

    mShifter = 0x10;
    mControl = 0;
    mPRGMode = 0;
    mIsCHR8kb = false;


    SetCPUBank_16KB(0, 0);
    SetCPUBank_16KB(1, mROM->prg16kbCount - 1);

    SetPPUBank_4KB(0, 0);
    SetPPUBank_4KB(1, 1);
}

void Mapper001::UpdateControl(uint8_t data) {
    this->mControl = data;
    switch (mControl & 0x3)
    {
    case 0x0:
        mHost->setMirroring(Mirror::SINGLE_SCREEN_LOWER);
        break;
    case 0x1:
        mHost->setMirroring(Mirror::SINGLE_SCREEN_UPPER);
        break;
    case 0x2:
        mHost->setMirroring(Mirror::VERTICAL);
        break;
    case 0x3:
        mHost->setMirroring(Mirror::HORIZONTAL);
        break;
    }

    mPRGMode = (mControl >> 2) & 0x3;
    mIsCHR8kb = !((mControl >> 4) & 0x1);
}

void Mapper001::UpdateChrbank0()
{
    if (mIsCHR8kb)
    {
        SetPPUBank_4KB(0, mShifter);
        SetPPUBank_4KB(1, mShifter + 1);
    }
    else
        SetPPUBank_4KB(0, mShifter);
}

void Mapper001::UpdateChrbank1()
{
    if (mIsCHR8kb)return;
    SetPPUBank_4KB(1, mShifter);
}

void Mapper001::UpdatePrgbank()
{
#if 0
    DebugPrintf("UpdatePrgbank %x %x\n", mPRGMode, mShifter);
#endif

    if (mPRGMode & 0x2)
    {
        if (mPRGMode & 0x1)
        {
            // 16K of Rom at $8000
            SetCPUBank_16KB(0, mShifter);
            SetCPUBank_16KB(1, mROM->prg16kbCount - 1);
        }
        else {
            // 16K of Rom at $C000
            SetCPUBank_16KB(0, 0);
            SetCPUBank_16KB(1, mShifter);
        }
    }
    else {
        // 32K of Rom at $8000
        SetCPUBank_16KB(0, mShifter);
        SetCPUBank_16KB(1, mShifter + 1);
    }
}

void Mapper001::WriteRegister(uint16_t address)
{
    switch ((address & 0x7FFF) >> 13)
    {
    case 0:
        UpdateControl(mShifter);
        break;
    case 1:
        UpdateChrbank0();
        break;
    case 2:
        UpdateChrbank1();
        break;
    case 3:
        UpdatePrgbank();
        break;
    }
}

void Mapper001::prgWrite(uint16_t address, uint8_t data) {

    if ((address & 0x6000) != (mLastWriteAddr & 0x6000))
    {
        mShifter = 0x10;
        UpdateControl(mControl | 0x0c);
    }

    mLastWriteAddr = address;

    if (data & 0x80) {
        mShifter = 0x10;
        UpdateControl(mControl | 0x0c);
    }
    else {
        const uint8_t finished = mShifter & 1;
        mShifter >>= 1;
        mShifter |= (data & 1) << 4;
        if (finished) {
            WriteRegister(address);
            mShifter = 0x10;
        }
    }
}

void Mapper001::OnSave(nes::io::SaveBundle *bundle)
{
    bundle->saveByte(mShifter);
    bundle->saveByte(mControl);
    bundle->saveByte(mPRGMode);
    bundle->saveByte(mIsCHR8kb);
    bundle->saveWord(mLastWriteAddr);
}

void Mapper001::OnRestore(nes::io::SaveBundle *bundle)
{
    mShifter = bundle->restoreByte();
    mControl = bundle->restoreByte();
    mPRGMode = bundle->restoreByte();
    mIsCHR8kb = bundle->restoreByte();
    mLastWriteAddr = bundle->restoreWord();
}


REGISTER_MAPPER(1, Mapper001);

} // namespace device
} // namespace nes
