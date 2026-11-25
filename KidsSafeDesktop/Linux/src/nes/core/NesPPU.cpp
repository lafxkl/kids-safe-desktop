#include "NesPPU.h"
#include "common.h"
#include "Devices.h"
#include "Famicom.h"
#include "Rom.h"
#include <cassert>

namespace nes {
namespace ppu {

#ifndef max
#define max(a, b) (((a) > (b)) ? (a) : (b))
#endif

#ifndef min
#define min(a, b) (((a) < (b)) ? (a) : (b))
#endif

NesPPU::NesPPU(cpu::NesCPU* cpu, device::NesCartridge* cartridge) : mCPU(cpu), mCartridge(cartridge)
{
    this->mPPUMask |= BGEN;
    this->mPPUMask |= SPEN;
    memset(mScreenMemory, 13, sizeof(mScreenMemory));
}

uint8_t NesPPU::cpuReadRegister(uint16_t address)
{
    // if(address != 0x2002)
    // DebugPrintf("cpuReadRegister %x \n", address);
    uint8_t data = 0;
    if (address < 0x4000) {
        switch (address & 0x7) {
        case 0:
        case 1:
        case 3:
        case 5:
        case 6:
            data = mPreviousData;
            break;
        case 2:
            data = this->mPPUStatus | (mPreviousData & 0x1F);
            mPPUStatus &= ~PPU_VBLANK;
            mRegs.w = 0;
            break;
        case 4:
            data = this->mOAM[mOamaddr];
            break;
        case 7:
            data = readDataPort(mRegs.v & 0x3FFF);
            mPreviousData = data;
            mRegs.v += ((mPPUCtrl & VINC32) ? 32 : 1);
            break;
        }
    }
    return data;
}

void NesPPU::cpuWriteRegister(uint16_t address, uint8_t data)
{
    // mCache = data;
    if (address < 0x4000) {
        // if(address != 2007)
        mPreviousData = data;
        switch (address & 0x7) {
        case 0:
            if ((mPPUStatus & PPU_VBLANK) && (data & 0x80) && ((mPPUCtrl & NMI_EN) == 0)) {
                mCPU->tryNmi();
            }
            // t: ...GH.. ........ <- d: ......GH
            //    <used elsewhere> < -d: ABCDEF..

            mPPUCtrl = data;

            mRegs.t = (mRegs.t & 0xF3FF) | ((data & 0x03) << 10);

            break;
        case 1:
            mPPUMask = data;
            break;
        case 3:
            mOamaddr = data;
            break;
        case 4:
            mOAM[mOamaddr++] = data;
            break;
        case 5:
            if (mRegs.w == 0) {
                /*
                t: ....... ...ABCDE <- d: ABCDE...
                x:              FGH <- d: .....FGH
                w:                  <- 1
                */
                mRegs.t = (mRegs.t & 0xFFE0) | ((uint16_t)data >> 3);
                mRegs.x = data & 0x07;
            } else {
                /*
                t: FGH..AB CDE..... <- d: ABCDEFGH
                w:                  <- 0
                */
                mRegs.t = (mRegs.t & 0x8fff) | (((uint16_t)data & 0x07) << 12); // 0x8fff  1000 1111 1111 1111
                mRegs.t = (mRegs.t & 0xfc1f) | (((uint16_t)data & 0xF8) << 2);  // 0xfc1f  1111 1100 0001 1111
            }
            mRegs.w ^= 1;
            break;
        case 6:
            if (mRegs.w == 0) {
                /*
                t: .CDEFGH ........ <- d: ..CDEFGH
                       <unused>     <- d: AB......
                t: Z...... ........ <- 0 (bit Z is cleared)
                w:                  <- 1
                */
                mRegs.t = (mRegs.t & 0x00FF) | (((uint16_t)data & 0x3F) << 8);
            } else {
                /*
                t: ....... ABCDEFGH <- d: ABCDEFGH
                v: <...all bits...> <- t: <...all bits...>
                w:                  <- 0
                */
                mRegs.t = (mRegs.t & 0xFF00) | data;
                mRegs.v = mRegs.t;
            }
            mRegs.w ^= 1; // 取反
            break;
        case 7:
            ppuBusWrite(mRegs.v & 0x3FFF, data);
            mRegs.v += ((mPPUCtrl & VINC32) ? 32 : 1);
            break;
        }
    } else if (address == 0x4014) {
        const uint16_t src = (data << 8) & 0xff00;
        mCPU->getBus()->readBlock(mOAM, src, 256);
        // https://www.nesdev.org/wiki/DMA
        mCPU->addCycles(513);
        mCPU->addCycles(mCPU->getCycles() & 1);
    }
}

inline void NesPPU::ppuBusWrite(uint16_t address, uint8_t data)
{
    if (address < 0x2000)
        mCartridge->ppuWrite(address, data);
    else if (address < 0x3F00)
        mNametables[(address >> 10) & 3][address & 0x3ff] = data;
    else {
        if (address & 0x03) {
            mPalette[address & 0x1f] = data;
        } else {
            uint16_t offset = address & 0x0f;
            mPalette[offset] = data;
            mPalette[offset | 0x10] = data;
        }
    }
}

inline uint8_t NesPPU::ppuBusRead(uint16_t address)
{
    if (address < 0x2000)
        return mCartridge->ppuRead(address);
    else if (address < 0x3F00)
        return mNametables[(address >> 10) & 3][address & 0x3ff];
    else
        return mPalette[address & 0x1f];
}

void NesPPU::reset()
{
    // 直接从 ROM 读取镜像模式 (无需依赖 Cartridge.reset 的顺序)
    if (mCartridge) {
        auto rom = mCartridge->getRom();
        if (rom) {
            switchMirroring(rom->isVmirroring ? nes::device::Mirror::VERTICAL : nes::device::Mirror::HORIZONTAL);
        } else {
            LOG_ERROR << "PPU reset: rom is null!";
        }
    } else {
        LOG_ERROR << "PPU reset: cartridge is null!";
    }
}

uint8_t* NesPPU::getCanvas()
{
    return this->mScreenMemory;
}

void NesPPU::incVerticalPosition()
{
    if ((mRegs.v & 0x7000) != 0x7000) {
        mRegs.v += 0x1000;
    } else {
        mRegs.v &= 0x8FFF;
        uint16_t y = (mRegs.v & 0x03E0) >> 5;
        if (y == 29) {
            y = 0;
            mRegs.v ^= 0x0800;
        } else if (y == 31)
            y = 0;
        else
            y++;
        // put coarse Y back into v
        mRegs.v = (mRegs.v & 0xFC1F) | (y << 5);
    }
}

void NesPPU::incHorizontalPosition()
{
    if ((mRegs.v & 0x001F) == 31) {
        mRegs.v &= ~0x001F;
        mRegs.v ^= 0x0400;
    } else {
        mRegs.v += 1;
    }
}

void NesPPU::copyHorizontalBits()
{
    // v: .....F.. ...EDCBA = t: .....F.. ...EDCBA
    mRegs.v = (mRegs.v & 0xFBE0) | (mRegs.t & 0x041F);
}

void NesPPU::copyVerticalBits()
{
    // v: GHIA.BC DEF..... <- t: GHIA.BC DEF.....
    mRegs.v = (mRegs.v & 0x841F) | (mRegs.t & 0x7BE0);
}

void NesPPU::scanSpirits(uint16_t line)
{
    // TODO: 这个模式效率太低，以后优化，先凑合用
    mPPUStatus &= ~PPU_SPOVER;
    mScanlineSpiritsCount = 0;
    for (uint8_t i = 0; i < 64; i++) {
        const uint8_t* spOAM = &mOAM[i << 2]; // * 4
        const uint16_t spY = spOAM[0] + 1;

        // 如果 Y 坐标不在扫描线的范围内就跳过
        if (!(spY <= line && line < spY + mSpHeight))
            continue;

        // 硬件行为: 每条扫描线最多评估 8 个精灵 (按 OAM 索引顺序),
        // 超出后停止入列并置溢出标志
        if (mScanlineSpiritsCount >= 8) {
            mPPUStatus |= PPU_SPOVER;
            break;
        }

        Spirit* spInfo = &mSpirits[mScanlineSpiritsCount++];
        spInfo->id = i;
        spInfo->x = spOAM[3];
        spInfo->info = spOAM[2];
        const uint8_t title = spOAM[1];
        if (mSpHeight == 16) {
            if (title & 0x1)
                spInfo->pattern = 0x1000 + ((title & 0xfe) << 4); // 注意这里有一个翻转！
            else
                spInfo->pattern = title << 4;
        } else {
            spInfo->pattern = mSpPatternTables + (title << 4);
        }

        const uint8_t offset = (spInfo->info & 0x80) ? mSpHeight - 1 - (line - spY) : line - spY;

        spInfo->pattern += offset + (offset & 0x8);
    }
}

void NesPPU::renderPattern(uint8_t* PatternMem)
{
    uint8_t bit0, bit1;
    for (size_t row = 0; row < 32; row++) {
        for (size_t col = 0; col < 16; col++) {
            for (size_t y = 0; y < 8; y++) {
                uint16_t address = (row * 16 + col) * 16 + y;
                bit0 = mCartridge->ppuRead(address);
                bit1 = mCartridge->ppuRead(address + 8);
                // read(PatternBytes((row * 16 + col) * 16 + y, &bit0, &bit1);
                drawPixelUnit(bit0, bit1, 0, PatternMem + ((row * 8 + y) * 128), col * 8);
            }
        }
    }
}

void NesPPU::preEmulationFrame()
{
    mPatternTables = (mPPUCtrl & BGUSE1000) ? 0x1000 : 0x0;
    mSpPatternTables = (mPPUCtrl & SPUSE1000) ? 0x1000 : 0x0;
    mSpHeight = (mPPUCtrl & SPH16) ? 16 : 8;
}

void NesPPU::renderScanline(const uint16_t line)
{
    // mPPUStatus |= PPU_SP0HIT;
    uint8_t* pBGMASK = mBGMask;
    if (mPPUMask & BGEN) {
        const uint8_t nametableSelect = (mRegs.v >> 10) & 0x3;
        const uint16_t scrolly = ((nametableSelect & 0x2) ? 240 : 0) + (((mRegs.v >> 5) & 0x1f) << 3) // Y * 8
                                 + (mRegs.v >> 12)
            ;

        const uint16_t scrollyIndex = scrolly / 240;
        const uint16_t scrollyOffset = scrolly % 240;

        const uint8_t curNametable = (nametableSelect & 0x1) | ((scrollyIndex & 1) << 1);

        uint8_t* const nametables[2] = {mNametables[curNametable], mNametables[curNametable ^ 1]};

        // 检查名称表指针是否有效
        if (!nametables[0] || !nametables[1]) {
            LOG_ERROR << "Invalid nametable pointer! curNametable=" << (int)curNametable 
                     << " nametables[0]=" << (void*)nametables[0] 
                     << " nametables[1]=" << (void*)nametables[1];
            return; // 避免崩溃，直接返回
        }

        const uint8_t voffset = scrollyOffset & 7;
        uint8_t attr4bit, attr2bit, ntOffset;
        uint16_t pattOffset;
        uint8_t bit0, bit1;

        int pixelOffset = -mRegs.x;

        const uint16_t start = mRegs.v & 0x1f;
        const uint16_t end = start + 33;
        const uint8_t attrBitShift = ((scrollyOffset & 31) >> 4) ? 4 : 0;
        // 名字表垂直偏移 thisScanline/8 * 32;
        const uint16_t ntBaseOffset = (scrollyOffset >> 3 << 5);
        // 属性表垂直偏移 thisScanline/32 * 8;
        const uint16_t atBaseOffset = (scrollyOffset >> 5 << 3);

        uint8_t *nametablebase, *nametable, *attrtable;

        for (uint16_t i = start; i < end; i++) {
            nametablebase = nametables[i >> 5];
            nametable = nametablebase + ntBaseOffset;       // 名字表基址偏移，得到当前扫描线对应的名字表偏移
            attrtable = nametablebase + 960 + atBaseOffset; // 属性表 + 属性表垂直偏移

            ntOffset = i & 0x1f;
            // 属性表偏移
            attr4bit = (attrtable[ntOffset >> 2] >> attrBitShift) & 0x0f;
            attr2bit = (attr4bit >> ((ntOffset % 4) > 1 ? 2 : 0)) & 0x3;

            // 图案表偏移
            pattOffset = ((mPPUCtrl & BGUSE1000) ? 0x1000 : 0x0) + ((nametable[ntOffset] << 4) + voffset);
            // pattOffset = mPatternTables + ((nametable[ntOffset] << 4) + voffset);

            bit0 = mCartridge->ppuRead(pattOffset);
            bit1 = mCartridge->ppuRead(pattOffset + 8);

            *pBGMASK = bit0 | bit1;

            drawPixelUnit(bit0, bit1, attr2bit, mScreenMemory + (line << 8), pixelOffset);

            pixelOffset += 8;

            pBGMASK++;
        }
    } else {
        // 关闭渲染，填充黑色
        memset(mScreenMemory + ((uint16_t)line << 8), 13, 256);
        memset(mBGMask, 0, sizeof(mBGMask));
    }

    if (mPPUMask & SPEN) {
        scanSpirits(line);
        drawSpirits(line);
    }
}

void NesPPU::drawSpirits(uint16_t line)
{
    uint8_t bit0, bit1;
    for (uint8_t i = 0; i != mScanlineSpiritsCount; i++) {
        Spirit* spInfo = &mSpirits[mScanlineSpiritsCount - i - 1];

        bit0 = mCartridge->ppuRead(spInfo->pattern);
        bit1 = mCartridge->ppuRead(spInfo->pattern + 8);

        if (spInfo->info & 0x40) // 判断是否水平翻转
        {
            bit0 = gBitReverseTable256[bit0];
            bit1 = gBitReverseTable256[bit1];
        }
        if (spInfo->id == 0 && !(mPPUStatus & PPU_SP0HIT)) {
            const uint16_t spOffsetX = spInfo->x + mRegs.x;
            uint8_t* pMaskpos = &mBGMask[spOffsetX >> 3];
            // 例如
            // mask: 7 6 5 4 3 2 1 0
            // 如果 mask 的第 3 位，就是当前位移
            // mask: 4 3 2 1 0
            // mask: 4 3 2 1 0
            uint8_t maskcode = pMaskpos[0] << (spOffsetX & 0x7);

            maskcode |= pMaskpos[1] >> (8 - (spOffsetX & 0x7));

            // 判断
            if ((bit0 | bit1) & maskcode)
                mPPUStatus |= PPU_SP0HIT;
        }
        // 渲染精灵
        drawSpiritsPixelUnit(bit0, bit1, spInfo->x, line, spInfo->info);
    }
}

void NesPPU::drawSpiritsPixelUnit(uint8_t p1, uint8_t p2, uint8_t x, uint8_t y, uint8_t attr)
{

    uint8_t bit, bith, colorOffset, bitShift;

    bitShift = 7;

    uint8_t* pScreen = mScreenMemory + ((uint16_t)y << 8);
    uint16_t pixelX = x;
    uint16_t end = min(x + 8, 256);

    for (; pixelX != end; pixelX++) {

        bit = (p1 >> bitShift) & 0x1;
        bit |= ((p2 >> bitShift) & 0x1) << 1;

        if (bit == 0)
            goto _draw_spirits_pixel_unit_skip;

        // 这个判断还是比较高效的，比直接判断快
        if (attr & 0x20) {
            if (pScreen[pixelX] != mPalette[0])
                goto _draw_spirits_pixel_unit_skip;
        };

        // 获取色度
        bith = ((attr & 0x3) << 2) & 0x0c;
        bith |= bit;

        // 直接返回色度
        colorOffset = ppuBusRead(0x3F10 + bith);

        pScreen[pixelX] = colorOffset;
    _draw_spirits_pixel_unit_skip:
        bitShift--;
    }
}

void NesPPU::drawPixelUnit(uint8_t p1, uint8_t p2, uint8_t attrl2bit, uint8_t* pScreen, int x)
{
    uint8_t bit, bith, colorOffset, bitShift = 7;
    // uint8_t* pCanvas = mCanvas + (y << 8);
    int16_t pixelX = x;
    int32_t end = min(x + 8, 256);

    for (; pixelX != end; pixelX++) {
        if (pixelX < 0)
            goto _draw_pixel_skip;

        bit = (p1 >> bitShift) & 0x1;
        bit |= ((p2 >> bitShift) & 0x1) << 1;

        if (!bit) {
            pScreen[pixelX] = mPalette[0];
        } else {
            // 获取色度
            bith = (attrl2bit << 2) & 0x0c;
            bith = (bith | bit) & 0xf;

            // 直接返回色度
            colorOffset = ppuBusRead(0x3F00 + bith);
            // assert(pixelX >= 0 && pixelX < 61440);
            pScreen[pixelX] = colorOffset;
        }
    _draw_pixel_skip:
        bitShift--;
    }
}

inline uint8_t NesPPU::readDataPort(uint16_t address)
{

    if (address < 0x3F00) {
        uint8_t data = mReadBuffer;

        mReadBuffer = ppuBusRead(address);
        return data;
    } else {
        mReadBuffer = ppuBusRead(address - 0x1000);
        return ppuBusRead(address);
    }
}

void NesPPU::switchMirroring(nes::device::Mirror mode)
{
    //	DebugPrintf("SwitchMirroring %d\n", mode);
    switch (mode) {
    case nes::device::Mirror::SINGLE_SCREEN_LOWER:
        mNametables[0] = mVRAM[0];
        mNametables[1] = mVRAM[0];
        mNametables[2] = mVRAM[0];
        mNametables[3] = mVRAM[0];
        break;
    case nes::device::Mirror::SINGLE_SCREEN_UPPER:
        mNametables[0] = mVRAM[1];
        mNametables[1] = mVRAM[1];
        mNametables[2] = mVRAM[1];
        mNametables[3] = mVRAM[1];
        break;
    case nes::device::Mirror::VERTICAL:
        mNametables[0] = mVRAM[0];
        mNametables[1] = mVRAM[1];
        mNametables[2] = mVRAM[0];
        mNametables[3] = mVRAM[1];
        break;
    case nes::device::Mirror::HORIZONTAL:
        mNametables[0] = mVRAM[0];
        mNametables[1] = mVRAM[0];
        mNametables[2] = mVRAM[1];
        mNametables[3] = mVRAM[1];
        break;
    case nes::device::Mirror::FOUR_SCREEN:
        break;
    }
}

void NesPPU::save(nes::io::SaveBundle* bundle)
{
    bundle->saveBlock(mVRAM[0], 1024);
    bundle->saveBlock(mVRAM[1], 1024);
    bundle->saveBlock(mPalette, sizeof(mPalette));
    bundle->saveBlock(mOAM, sizeof(mOAM));

    bundle->saveWord(mNametables[0] - (uint8_t*)mVRAM);
    bundle->saveWord(mNametables[1] - (uint8_t*)mVRAM);
    bundle->saveWord(mNametables[2] - (uint8_t*)mVRAM);
    bundle->saveWord(mNametables[3] - (uint8_t*)mVRAM);

    bundle->saveByte(mPreviousData);
    bundle->saveByte(mReadBuffer);

    bundle->saveBlock((uint8_t*)&mRegs, sizeof(mRegs));

    bundle->saveByte(mPPUCtrl);
    bundle->saveByte(mPPUStatus);
    bundle->saveByte(mPPUMask);
    bundle->saveByte(mOamaddr);
}

void NesPPU::restore(nes::io::SaveBundle* bundle)
{
    bundle->restoreBlock(mVRAM[0], 1024);
    bundle->restoreBlock(mVRAM[1], 1024);
    bundle->restoreBlock(mPalette, sizeof(mPalette));
    bundle->restoreBlock(mOAM, sizeof(mOAM));

    mNametables[0] = bundle->restoreWord() + (uint8_t*)mVRAM;
    mNametables[1] = bundle->restoreWord() + (uint8_t*)mVRAM;
    mNametables[2] = bundle->restoreWord() + (uint8_t*)mVRAM;
    mNametables[3] = bundle->restoreWord() + (uint8_t*)mVRAM;

    mPreviousData = bundle->restoreByte();
    mReadBuffer = bundle->restoreByte();

    bundle->restoreBlock((uint8_t*)&mRegs, sizeof(mRegs));

    mPPUCtrl = bundle->restoreByte();
    mPPUStatus = bundle->restoreByte();
    mPPUMask = bundle->restoreByte();
    mOamaddr = bundle->restoreByte();
}

} // namespace ppu
} // namespace nes
