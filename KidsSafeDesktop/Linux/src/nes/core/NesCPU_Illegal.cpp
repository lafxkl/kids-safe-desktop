#include "common.h"
#include "NesCPU.h"
#include <cassert>
#include "Famicom.h"
#include "Devices.h"

using namespace nes::cpu;

void NesCPU::opSlo()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	if ((mDataByte >> 7) & 1)
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);

	mDataByte <<= 1;
	A() |= mDataByte;
	getBus()->writeByte(mDataAddress, mDataByte);

	mFlags.SetFromResult(A());
}
void NesCPU::opNop()
{
    // 单字节 NOP (0x1A/0x3A/...) 的寻址函数为空指针, 无操作数可取
    if (mPtrOpcode->address) {
        (this->*mPtrOpcode->address)();
        mDataByte = getBus()->readByte(mDataAddress);
    }
}

void NesCPU::opRla()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);

	uint8_t val = mDataByte << 1;

	if (mFlags.IsSet(nes::cpu::StatusFlag::Carry))
		val |= 0x01;

	if (mDataByte & 0x80)
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);

	A() &= val;

	getBus()->writeByte(mDataAddress, val);

	mFlags.SetFromResult(A());
}

void NesCPU::opSre()
{

	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);

	uint8_t val = mDataByte >> 1;

	if (mDataByte & 1)
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);

	A() ^= val;

	getBus()->writeByte(mDataAddress, val);
	mFlags.SetFromResult(A());
}

void NesCPU::opRra()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	uint8_t val = mDataByte >> 1;

	if (mFlags.IsSet(nes::cpu::StatusFlag::Carry))
		val |= 0x80;

	if (mDataByte & 0x01)
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);

	getBus()->writeByte(mDataAddress, val);

	this->mathAdd(val);

	mFlags.SetFromResult(A());
}

void NesCPU::opSax()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	uint8_t val = A() & X();
	getBus()->writeByte(mDataAddress, val);
}

void NesCPU::opLax()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	A() = X() = mDataByte;
	mFlags.SetFromResult(A());
}

void NesCPU::opDcp()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	uint8_t val = --mDataByte;

	getBus()->writeByte(mDataAddress, val);

	short val2 = A() - val;

	if (val2 >= 0)
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);

	mFlags.SetFromResult(static_cast<uint8_t>(val2 & 0xff));
}

void NesCPU::opIsc()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	uint8_t val = ++mDataByte;
	getBus()->writeByte(mDataAddress, val);
	this->mathSub(val);
}

void NesCPU::opAnc()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	A() &= mDataByte;
	mFlags.SetFromResult(A());
	if (mFlags.IsSet(nes::cpu::StatusFlag::Negative))
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);
}

void NesCPU::opAlr()
{
	// assert(!"no  implement");
}

void NesCPU::opArr()
{
	(this->*mPtrOpcode->address)();
	mDataByte = getBus()->readByte(mDataAddress);
	A() &= mDataByte;

	A() = A() >> 1;

	if (mFlags.IsSet(nes::cpu::StatusFlag::Carry))
		A() |= 0x80;

	mFlags.SetFromResult(A());
	if ((A() >> 6) & 1)
		mFlags.Set(nes::cpu::StatusFlag::Carry);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Carry);

	if ((A() >> 5) & (mFlags.IsSet(nes::cpu::StatusFlag::Carry) ? 1 : 0))
		mFlags.Set(nes::cpu::StatusFlag::Overflow);
	else
		mFlags.Clear(nes::cpu::StatusFlag::Overflow);
}

void NesCPU::opXaa()
{
	// assert(!"no  implement");
}

void NesCPU::opAxs()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    // X = (A & X) - imm，减法在 16 位域中计算以检测借位
    uint16_t val = static_cast<uint16_t>((A() & X()) - mDataByte);
    X() = static_cast<uint8_t>(val);
    if (val & 0x100)
        mFlags.Clear(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    mFlags.SetFromResult(X());
}

void NesCPU::opSbc()
{
    // 0xEB 是 SBC 的非官方别名, 行为与官方 SBC 完全一致
    this->mathSub(readByteForPc());
}

void NesCPU::opAhx()
{
	// assert(!"no  implement");
}

void NesCPU::opShy()
{
	// assert(!"no  implement");
}

void NesCPU::opShx()
{
	// assert(!"no  implement");
}

void NesCPU::opTas()
{
	// assert(!"no  implement");
}

void NesCPU::opLas()
{
	// assert(!"no  implement");
}
