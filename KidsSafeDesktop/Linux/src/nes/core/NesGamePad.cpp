#include "common.h"
#include "NesGamePad.h"
#include "SaveBundle.h"

namespace nes {
namespace device {

NesGamePad::NesGamePad()
{
	mGamepadIndex_1 = 0;
	mGamepadIndex_2 = 0;
}

void NesGamePad::userInput(uint8_t index, unsigned state)
{
	if (index < 16)
		this->mGamepadStatus[index] = state;
}

void NesGamePad::cpuWriteRegister(uint16_t address, uint8_t data)
{
	switch (address) {
	case 0x4016: {
		if (data & 1) {
			mGamepadEn = 1;
			mGamepadIndex_1 = 0;
			mGamepadIndex_2 = 0;
		}
		else
			mGamepadEn = 0;
		break;
	}
	}
}

uint8_t NesGamePad::cpuReadRegister(uint16_t address)
{
	uint8_t data = 0;
	switch (address) {
	case 0x4016:
		if (mGamepadEn == 0)
		{
			data = mGamepadStatus[mGamepadIndex_1 % 8] | 0x40;
			mGamepadIndex_1++;
		}
		break;
	case 0x4017:
		if (mGamepadEn == 0)
		{
			data = mGamepadStatus[8 + mGamepadIndex_2 % 8];
			mGamepadIndex_2++;
		}
		break;
	}
	return data;
}

void NesGamePad::reset()
{
	 mGamepadEn = 0;
	 mGamepadIndex_1 = 0;
	 mGamepadIndex_2 = 0;
	 memset(mGamepadStatus,0,sizeof(mGamepadStatus));
}

void NesGamePad::save(io::SaveBundle* bundle)
{
	bundle->saveByte(mGamepadEn);
	bundle->saveByte(mGamepadIndex_1);
	bundle->saveByte(mGamepadIndex_2);
}

void NesGamePad::restore(io::SaveBundle* bundle)
{
	mGamepadEn = bundle->restoreByte();
	mGamepadIndex_1 = bundle->restoreByte();
	mGamepadIndex_2 = bundle->restoreByte();
}

} // namespace device
} // namespace nes
