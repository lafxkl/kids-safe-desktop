#include "NesPAPU.h"
#include "common.h"
#include <cstdio>
#include <cstdlib>
#include "Devices.h"
#include "Famicom.h"

namespace nes {
namespace apu {

// REF: http://nesdev.com/apu_ref.txt

#if 1
static const uint8_t PL_SEQ[4][8] = {
    {0, 1, 0, 0, 0, 0, 0, 0},
    {0, 1, 1, 0, 0, 0, 0, 0},
    {0, 1, 1, 1, 1, 0, 0, 0},
    {1, 0, 0, 1, 1, 1, 1, 1},
};
static const uint8_t TRI_SEQ[] = {15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5,  4,  3,  2,  1,  0,
                                  0,  1,  2,  3,  4,  5,  6, 7, 8, 9, 10, 11, 12, 13, 14, 15};
#else
static const uint8_t PL_SEQ[4][8] = {
    {-1, 1, -1, -1, -1, -1, -1, -1},
    {-1, 1, 1, -1, -1, -1, -1, -1},
    {-1, 1, 1, 1, 1, -1, -1, -1},
    {1, -1, -1, 1, 1, 1, 1, 1},
};
static const uint8_t TRI_SEQ[] = {15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5,  4,  3,  2,  1,  0,
                                  0,  1,  2,  3,  4,  5,  6, 7, 8, 9, 10, 11, 12, 13, 14, 15};
#endif

static const uint8_t LENGTH_COUNTER_TABLE[] = {
    0x0A, 0xFE, 0x14, 0x02, 0x28, 0x04, 0x50, 0x06, 0xA0, 0x08, 0x3C, 0x0A, 0x0E, 0x0C, 0x1A, 0x0E,
    0x0C, 0x10, 0x18, 0x12, 0x30, 0x14, 0x60, 0x16, 0xC0, 0x18, 0x48, 0x1A, 0x10, 0x1C, 0x20, 0x1E,
};

static const uint16_t DMC_PERIOD_LIST_NP[] = {
    // NTSC
    428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54};

static const uint16_t NOISE_PERIOD_LIST[] = {
    4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068,
};

NesPAPU::NesPAPU(cpu::NesCPU* cpu) : mCPU(cpu)
{}

void NesPAPU::cpuWriteRegister(uint16_t address, uint8_t data)
{
    // DebugPrintf("APU::%x %x\n", address, data);
    switch (address) {
    case 0x4000:
    case 0x4004:
    {
        Pulse* pulse = &mPulses[address == 0x4000 ? 0 : 1];
        pulse->ctrl = data;
        // pulse->Volume = data & 0x0f;
        pulse->envelope.ctrl6 = data & 0x3f;
    } break;
    case 0x4001:
    case 0x4005:
    {
        Pulse* pulse = &mPulses[address == 0x4001 ? 0 : 1];
        pulse->sweep.enable = data >> 7;
        pulse->sweep.period = (data >> 4) & 0x7;
        pulse->sweep.negative = (data >> 3) & 0x1;
        pulse->sweep.shift = data & 0x7;
        pulse->sweep.reload = 1;
    } break;
    case 0x4002:
    case 0x4006:
    {
        Pulse* pulse = &mPulses[address == 0x4002 ? 0 : 1];
        pulse->period &= 0xff00;
        pulse->period |= data;
    } break;
    case 0x4003:
    case 0x4007:
    {
        Pulse* pulse = &mPulses[address == 0x4003 ? 0 : 1];
        pulse->period &= 0xff;
        pulse->period |= ((data & 0x7) << 8);
        if (pulse->enable)
            pulse->lengthCounter = LENGTH_COUNTER_TABLE[data >> 3];
        pulse->envelope.reload = 1;
        pulse->seqIndex = 0;
    } break;
    case 0x4008:
        // $4008 CRRR RRRR
        mTriangle.reloadValue = data & 0x7F;
        mTriangle.flagHalt = data >> 7;
        break;
    case 0x4009:
        break;
    case 0x400A:
        // $400A TTTT TTTT
        mTriangle.period = (mTriangle.period & 0xff00) | data;
        break;
    case 0x400B:
        // $400B LLLL LTTT
        if (mTriangle.enable)
            mTriangle.lengthCounter = LENGTH_COUNTER_TABLE[data >> 3];
        mTriangle.period = (mTriangle.period & 0x00ff) | ((data & 0x07) << 8);
        mTriangle.reload = 1;
        break;
    case 0x400C:
        // $400C --LC NNNN
        mNoise.envelope.ctrl6 = data & 0x3F;
        break;
    case 0x400D:
        break;
    case 0x400E:
        // $400E S--- PPPP
        mNoise.shortMode = data >> 7;
        mNoise.periodIndex = data & 0xf;
        break;
    case 0x400F:
        // $400E LLLL L---
        if (mNoise.enable)
            mNoise.lengthCounter = LENGTH_COUNTER_TABLE[data >> 3];
        mNoise.envelope.reload = 1;
        break;
    case 0x4010:
        // $4010 IL-- RRRR
        mDmc.irqEnable = (data >> 7) & 0x1;
        mDmc.irqLoop = (data >> 6) & 0x1;
        mDmc.period = DMC_PERIOD_LIST_NP[data & 0xF]; // rate 只在下次重载时生效, 不重置相位
        if (!mDmc.irqEnable) {
            mDmc.interruptFlag = false;
            mCPU->clrIrq(nes::InterruptType::DmcInt);
        }
        break;
    case 0x4011:
        // $4011 -DDD DDDD
        mDmc.volume = data & 0x7F;
        break;
    case 0x4012:
        // $4012 AAAA AAAA
        // 11AAAAAA.AA000000
        mDmc.orgaddr = 0xC000 | ((uint16_t)data << 6);
        break;
    case 0x4013:
        // $4013 LLLL LLLL
        // 0000LLLL.LLLL0001
        mDmc.length = ((uint16_t)data << 4) | 1;
        break;
    case 0x4015: {
        this->channelCtrl = data;
        if (channelCtrl & Pulse1Enable) {
            mPulses[0].enable = 1;
        } else {
            mPulses[0].lengthCounter = 0;
            mPulses[0].volume = 0;
            mPulses[0].enable = 0;
        }

        if (channelCtrl & Pulse2Enable) {
            mPulses[1].enable = 1;
        } else {
            mPulses[1].lengthCounter = 0;
            mPulses[1].volume = 0;
            mPulses[1].enable = 0;
        }
        if (channelCtrl & TriangleEnable) {
            mTriangle.enable = 1;
            mTriangle.volume = 1;
        } else {
            mTriangle.enable = 0;
            mTriangle.volume = 0;
            mTriangle.lengthCounter = 0;
        }
        if (channelCtrl & NoiseEnable) {
            mNoise.enable = 1;
        } else {
            mNoise.enable = 0;
            mNoise.volume = 0;
            mNoise.lengthCounter = 0;
        }

        if (channelCtrl & DMCEnable) {
            mDmc.enable = 1;
            // 真机行为: 仅当 bytesRemaining==0 时才重新装载样本。
            // 正在播放中再写 enable 不会打断当前播放 (blargg sync_dmc 依赖此行为)。
            if (mDmc.bytesRemaining == 0) {
                resetDMC();
            }
        } else {
            mDmc.enable = 0;
            mDmc.bytesRemaining = 0;
            mCPU->clrIrq(nes::InterruptType::DmcInt);
        }
        mDmc.interruptFlag = false;
    } break;
    case 0x4017:
    {
        // DebugPrintf("0x4017 address %x\n", data);
        mFrame5Step = data >> 7;
        mIRQDisable = !!(data & 0x40);

        if (mIRQDisable) {
            mCPU->clrIrq(nes::InterruptType::FrameInt);
            mFrameInterrupt = 0;
        }


        if (mFrame5Step) {
            processLengthCounter();
            processSweepUnit();
            processEnvelope();
            processLinearCounter();
        }
    } break;
    default:
        LOG_WARN << "未定义的 APU 地址: 0x" << std::hex << static_cast<int>(address);
        break;
    }
}

void NesPAPU::reset()
{
    mNoise.lfsr = 1;
    mDmc.period = DMC_PERIOD_LIST_NP[0];
    mDmc.clock = mDmc.period;
}

uint8_t NesPAPU::cpuReadRegister(uint16_t address)
{
    if (address == 0x4015) {
        uint8_t state = 0;
        if (mPulses[0].lengthCounter)
            state |= 0x1;
        if (mPulses[1].lengthCounter)
            state |= 0x2;
        if (mTriangle.lengthCounter)
            state |= 0x4;
        if (mNoise.lengthCounter)
            state |= 0x8;
        if (mDmc.bytesRemaining)
            state |= 0x10;
        if (mFrameInterrupt)
            state |= 0x40;
        if (mDmc.interruptFlag)
            state |= 0x80;

        // 清空中断标志
        mFrameInterrupt = 0;
        mCPU->clrIrq(nes::InterruptType::FrameInt);

        return state;
    }
    return 0;
}

// int16_t sbuffer[10 * 800];


inline void NesPAPU::processSweepUnit()
{
    NesPAPU::processPulseSweepUnit(&mPulses[0], true);
    NesPAPU::processPulseSweepUnit(&mPulses[1], false);
}

void NesPAPU::processFrameCounter()
{
    // FrameCounter++;
    if (mFrame5Step) {
        // l - l - - ->   ++%5   ->   1 2 3 4 0
        // e e e e - ->   ++%5   ->   1 2 3 4 0
        switch (mFrameClock % 5) {
        case 0:
            processEnvelope();
            processLinearCounter();
            break;
        case 1:
            processLengthCounter(); // 120hz
            processSweepUnit();
            processEnvelope();
            processLinearCounter();
            break;
        case 2:
            processEnvelope();
            processLinearCounter();
            break;
        case 3:
            processLengthCounter(); // 120hz
            processSweepUnit();
            processEnvelope();
            processLinearCounter();
            break;
        case 4:
            break;
        }
    } else {

        switch (mFrameClock % 4) {
        case 0:
            processEnvelope();
            processLinearCounter();
            break;
        case 1:
            processLengthCounter(); // 120hz
            processSweepUnit();
            processEnvelope();
            processLinearCounter();
            break;
        case 2:
            processEnvelope();
            processLinearCounter();
            break;
        case 3:
            if (!mIRQDisable) {
                // DebugPrintf("IRQ_FRAME_INT\n");
                mFrameInterrupt = 1;
                mCPU->tryIrq(nes::InterruptType::FrameInt);
            }
            processLengthCounter(); // 120hz
            processSweepUnit();
            processEnvelope();
            processLinearCounter();
            break;
        }
    }
    ++mFrameClock;
}

uint16_t NesPAPU::lfsrChange(uint16_t v, uint8_t c)
{
    const uint16_t bit = ((v >> 0) ^ (v >> c)) & 1;
    return (uint16_t)(v >> 1) | (uint16_t)(bit << 14);
}

int16_t NesPAPU::soundMixer()
{
#if 1
    float square_out = 95.88f / ((8128.f / (mOutputs[0] + mOutputs[1])) + 100.f);
    float tnd_out = 159.79f / (1.f / (mOutputs[2] / 8227.f + mOutputs[3] / 12241.f + mOutputs[4] / 22638.f) + 100.f);
    // DebugPrintf("square_out + tnt_output:%f\n", square_out + tnt_output);
    float outputf = (float)(square_out + tnd_out);
#else
    float pulse_out = 0.00752 * ((int64_t)mOutputs[0] + mOutputs[1]);
    float tnd_out = 0.00851 * mOutputs[2] + 0.00494 * mOutputs[3] + 0.00335 * mOutputs[4];
    // if ((float)(pulse_out + tnd_out) > 1.f)
    //{
    //	DebugPrintf("square_out + tnt_output:%f\n", pulse_out + tnd_out);
    // }
    float outputf = (float)(pulse_out + tnd_out);

#endif
    // int16_t output = outputf * 32767.f;
    int16_t output = static_cast<int16_t>(((outputf * 2.f) - 1.f) * 32767.f);
    if (output > 0x7FFF - 1)
        output = 0x7FFF - 1;
    else if (output < -0x8000 + 1)
        output = -0x8000 + 1;
    return output;
}

void NesPAPU::resetDMC()
{
    mDmc.address = mDmc.orgaddr;
    mDmc.bytesRemaining = mDmc.length;
}

void NesPAPU::updateDmcBit(Dmc* dmc)
{
    if (!dmc->enable)
        return;
    if (dmc->bitsRemaining <= 0) {
        if (dmc->bytesRemaining) {
            dmc->bitsRemaining = 8;
            dmc->sampleBuffer = mCPU->getBus()->readByte(dmc->address);
            dmc->address = (uint16_t)(dmc->address + 1) | (uint16_t)0x8000;
            --dmc->bytesRemaining;
            if (dmc->bytesRemaining <= 0) {
                if (dmc->irqLoop) {
                    resetDMC();
                } else {
                    if (dmc->irqEnable) {
                        dmc->interruptFlag = true;
                        mCPU->tryIrq(nes::InterruptType::DmcInt);
                    }
                }
            }
        }
    }
    if (dmc->bitsRemaining) {
        if (dmc->sampleBuffer & 1) {
            if (dmc->volume <= 125)
                dmc->volume += 2;
        } else {
            if (dmc->volume >= 2)
                dmc->volume -= 2;
        }
        dmc->sampleBuffer >>= 1;
        --dmc->bitsRemaining;
    }
}

uint8_t NesPAPU::makePulseSamples(Pulse* pulse)
{
    pulse->cycle += mCpuCyclePerSample * 0.5;
    const int cycle = static_cast<int>(pulse->cycle / (pulse->period + 1));
    pulse->cycle -= cycle * static_cast<double>(pulse->period + 1);
    pulse->seqIndex += cycle;
    pulse->seqIndex &= 7;
    return pulse->volume * (PL_SEQ[pulse->ctrl >> 6][pulse->seqIndex]);
}

uint8_t NesPAPU::makeTriangleSamples(Triangle* triangle)
{
    triangle->cycle += mCpuCyclePerSample;
    const uint16_t cycle = static_cast<uint16_t>(triangle->cycle / (triangle->period + 1));
    triangle->cycle -= cycle * static_cast<double>(triangle->period + 1);
    const uint8_t inc = triangle->incMask & cycle;
    // const uint8_t inc = (uint8_t)count;
    triangle->seqIndex += inc;
    triangle->seqIndex &= 31;

    return triangle->volume * TRI_SEQ[triangle->seqIndex];
}

uint8_t NesPAPU::makeNoiseSamples(Noise* noise)
{
    noise->cycle += mCpuCyclePerSample;
    const uint32_t period = mNoise.period;
    while (noise->cycle >= period) {
        noise->cycle -= period;
        noise->lfsr = lfsrChange(noise->lfsr, mNoise.bitsRemaining);
    }
    uint8_t mask = noise->lfsr & 1;
    --mask;
    return noise->volume & mask;
}

uint8_t NesPAPU::makeDmcSamples(Dmc* dmc, double extraCpuCycles)
{
    // DMC 必须在"生成输出采样"的时钟上推进: DMC 音量的每次变化都要落在对应的
    // 输出采样上。若改由 clockAPU (CPU 执行期间) 推进、采样时只读音量, 一帧内
    // 数百次音量变化会被压缩成每帧 3 个采样块的大台阶 → 语音波形被毁成爆音。
    // 每采样推进 mCpuCyclePerSample (29780.5/735), 与帧 CPU 周期总量一致,
    // 音高准确。
    dmc->clock += mCpuCyclePerSample + extraCpuCycles;
    const uint16_t period = dmc->period;
    while (dmc->clock >= period) {
        dmc->clock -= period;
        updateDmcBit(dmc);
    }
    return dmc->volume;
}

uint8_t NesPAPU::makeSamples()
{
    return this->makeSamples(mCPU->getCycles());
}

uint8_t NesPAPU::makeSamples(uint32_t cpuCycle)
{
    debugReceivedCycles += cpuCycle;
    updatePulseState(&mPulses[0]);
    updatePulseState(&mPulses[1]);
    updateTriangleState();
    updateNoiseState();
    mQueueOffset = mQueueOffset % 20;
    // 按 CPU 周期数决定本块生成多少采样: 每采样消耗 mCpuCyclePerSample 个 CPU 周期。
    // 之前固定 245 样本/块 × 3 块/帧 = 735 样本, 隐含帧率恰为 60.000Hz,
    // 而真机为 60.0988Hz (29780.5 CPU/帧), 导致 DMC 定时器比 CPU 快 0.164%,
    // blargg dmc_rates 测出 "Rate 0's period is too short"。
    const uint32_t nSampleSize =
        static_cast<uint32_t>((mRemnantCpuCycles + cpuCycle) / mCpuCyclePerSample);
    mRemnantCpuCycles =
        mRemnantCpuCycles + cpuCycle - nSampleSize * mCpuCyclePerSample;
    Sample* sample = &mSamplesQueue[mQueueOffset++];
    sample->size = nSampleSize;
    // float * buffer =
    for (size_t i = 0; i < nSampleSize; i++) {
        this->mOutputs[0] = this->makePulseSamples(&mPulses[0]);
        this->mOutputs[1] = this->makePulseSamples(&mPulses[1]);
        this->mOutputs[2] = this->makeTriangleSamples(&mTriangle);
        this->mOutputs[3] = this->makeNoiseSamples(&mNoise);
        // DMC 定时器单位是 CPU 周期, 采样网格 40.52 CPU/采样有零头, 块边界的
        // 零头留在 mRemnantCpuCycles 里不喂给 DMC 会让 DMC 比 CPU 慢几个周期,
        // 累计到 blargg dmc_rates 的 90 周期判定窗口就 "too long"。末采样把
        // 本块真实 CPU 周期与 nSampleSize×per 的差值补上。
        const double extra = (i + 1 == nSampleSize) ? mRemnantCpuCycles : 0.0;
        this->mOutputs[4] = this->makeDmcSamples(&mDmc, extra);

        // 合成最终的音频采样
        sample->buf[i] = this->soundMixer();
    }
    if (mAudioCallback) {
        mAudioCallback(sample->buf, sample->size);
    }
    return 0;
}

void NesPAPU::updateTriangleState()
{
    // mTriangle.volume = mTriangle.enable;
    mTriangle.incMask = (mTriangle.lengthCounter && mTriangle.linearCounter) ? 0xff : 0;
}

void NesPAPU::updatePulseState(Pulse* pulse)
{
    if (!(pulse->enable)) {
        pulse->volume = 0;
        return;
    }
    if (pulse->lengthCounter == 0) {
        pulse->volume = 0;
        return;
    }
    if (pulse->period < 8 || pulse->period > 0x7ff) {
        pulse->volume = 0;
    }
    // state.duty = famicom->apu.square1.ctrl >> 6;
    if (pulse->envelope.ctrl6 & 0x10)
        pulse->volume = pulse->envelope.ctrl6 & 0xf;
    else
        pulse->volume = pulse->envelope.counter;
}

void NesPAPU::updateNoiseState()
{
    mNoise.bitsRemaining = mNoise.shortMode ? 6 : 1;
    mNoise.period = NOISE_PERIOD_LIST[mNoise.periodIndex & 0xF];
    if (!mNoise.enable) {
        mNoise.volume = 0;
        return;
    }
    if (!mNoise.lengthCounter) {
        mNoise.volume = 0;
        return;
    }
    if (mNoise.envelope.ctrl6 & 0x10)
        mNoise.volume = mNoise.envelope.ctrl6 & 0xf;
    else
        mNoise.volume = mNoise.envelope.counter;
}

inline void NesPAPU::processLengthCounter()
{
    NesPAPU::processPulseLengthCounter(&mPulses[0]);
    NesPAPU::processPulseLengthCounter(&mPulses[1]);

    if (!(mTriangle.flagHalt) && mTriangle.lengthCounter)
        --mTriangle.lengthCounter;

    if (!(mNoise.envelope.ctrl6 & (uint8_t)0x20) && mNoise.lengthCounter)
        --mNoise.lengthCounter;
}

inline void NesPAPU::processLinearCounter()
{
    if (mTriangle.reload) {
        mTriangle.linearCounter = mTriangle.reloadValue;
    } else if (mTriangle.linearCounter) {
        --mTriangle.linearCounter;
    }
    if (!mTriangle.flagHalt)
        mTriangle.reload = 0;
}

inline void NesPAPU::processEnvelope()
{
    NesPAPU::processEnvelope(&mPulses[0].envelope);
    NesPAPU::processEnvelope(&mPulses[1].envelope);
    NesPAPU::processEnvelope(&mNoise.envelope);
}

void NesPAPU::processPulseLengthCounter(Pulse* pulse)
{
    if (!(pulse->ctrl & 0x20) && pulse->lengthCounter)
        pulse->lengthCounter--;
}

void NesPAPU::processPulseSweepUnit(Pulse* pulse, bool isOne)
{
    if (pulse->sweep.reload) {
        pulse->sweep.reload = 0;
        const uint8_t oldDivider = pulse->sweep.divider;
        pulse->sweep.divider = pulse->sweep.period;
        if (pulse->sweep.enable && !oldDivider)
            goto _sweep_reload;
        return;
    }

    if (!pulse->sweep.enable)
        return;

    if (pulse->sweep.divider)
        pulse->sweep.divider--;
    else {
        pulse->sweep.divider = pulse->sweep.period;
    _sweep_reload:

        if (!pulse->sweep.shift)
            return;

        uint16_t val1 = pulse->period >> pulse->sweep.shift;
        if (pulse->sweep.negative) {
            pulse->period -= val1;
            if (isOne)
                pulse->period -= 1;
        } else
            pulse->period += val1;
    }
}

void NesPAPU::processEnvelope(Envelope* envelope)
{
    if (envelope->reload) {
        envelope->reload = 0;
        envelope->counter = 15;
        envelope->divider = envelope->ctrl6 & 0x0F;
    }
    else {
        if (envelope->divider)
            envelope->divider--;
        else {
            envelope->divider = envelope->ctrl6 & 0x0F;
            const uint8_t loop = envelope->ctrl6 & 0x20;
            if (envelope->counter | loop) {
                --envelope->counter;
                envelope->counter &= 0xf;
            }
        }
    }
}

int NesPAPU::getSamplesQueue()
{
    // 读写端都在取用前对容量取模, 保证空/满判断在跨圈时一致
    return (mBufferIndex % 20) - (mQueueOffset % 20);
}

int16_t* NesPAPU::getSamples(uint32_t* size)
{
    if (mBufferIndex == mQueueOffset)
        return nullptr;

    mBufferIndex %= 20;
    *size = mSamplesQueue[mBufferIndex].size;
    return mSamplesQueue[mBufferIndex++].buf;
}

void NesPAPU::save(io::SaveBundle* bundle)
{
    bundle->saveBlock((uint8_t*)&mPulses[0], sizeof(Pulse));
    bundle->saveBlock((uint8_t*)&mPulses[1], sizeof(Pulse));
    bundle->saveBlock((uint8_t*)&mNoise, sizeof(Noise));
    bundle->saveBlock((uint8_t*)&mTriangle, sizeof(Triangle));
    bundle->saveBlock((uint8_t*)&mDmc, sizeof(Dmc));

    bundle->saveByte(mFrameClock);
    bundle->saveByte(mFrameInterrupt);
    bundle->saveByte(channelCtrl);
    bundle->saveByte(mFrame5Step);
    bundle->saveByte(mIRQDisable);
}

void NesPAPU::restore(io::SaveBundle* bundle)
{
    bundle->restoreBlock((uint8_t*)&mPulses[0], sizeof(Pulse));
    bundle->restoreBlock((uint8_t*)&mPulses[1], sizeof(Pulse));
    bundle->restoreBlock((uint8_t*)&mNoise, sizeof(Noise));
    bundle->restoreBlock((uint8_t*)&mTriangle, sizeof(Triangle));
    bundle->restoreBlock((uint8_t*)&mDmc, sizeof(Dmc));

    mFrameClock = bundle->restoreByte();
    mFrameInterrupt = bundle->restoreByte();
    channelCtrl = bundle->restoreByte();
    mFrame5Step = bundle->restoreByte();
    mIRQDisable = bundle->restoreByte();
}

} // namespace apu
} // namespace nes
