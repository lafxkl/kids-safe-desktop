#pragma once
#include "SaveBundle.h"
#include <functional>

namespace nes {

namespace cpu { class NesCPU; }
namespace io { class SaveBundle; }

namespace apu {

/**
 * @class NesPAPU
 * @brief 模拟 NES APU 音频芯片
 * 
 * APU 负责生成 NES 的所有音频输出。
 * 它包含 5 个独立的音频通道,混合后产生最终音频输出。
 * 
 * 每个通道都有自己的:
 * - 频率/周期控制
 * - 音量控制
 * - 包络发生器
 * - 长度计数器
 * 
 * @see https://www.nesdev.org/wiki/APU 查看详细文档
 */
class NesPAPU
{
    /**
     * @struct Sample
     * @brief 音频采样缓冲区结构
     * 
     * 保存输出前的缓冲音频采样。
     * 每个缓冲区最多 800 个采样 (44.1kHz 下约 18ms)。
     */
    typedef struct sample_t {
        short buf[800];   ///< 采样缓冲区 (立体声交错)
        short size = 0;   ///< 缓冲区中当前的采样数量
    } Sample;

    /**
     * @struct Envelope
     * @brief 包络发生器结构
     * 
     * 控制脉冲和噪声通道的音量包络。
     * 可以在循环模式或单次模式下运行。
     */
    typedef struct {
        uint8_t divider;    ///< 包络定时的时钟分频器
        uint8_t counter;    ///< 当前包络计数器值
        uint8_t reload;     ///< 重载标志 (触发包络重置)
        uint8_t ctrl6;      ///< 控制位 (循环,恒定音量,周期)
    } Envelope;

    /**
     * @struct Noise
     * @brief 噪声通道状态
     * 
     * 使用 LFSR (线性反馈移位寄存器) 生成伪随机噪声。
     * 用于打击乐、音效和白噪声。
     */
    typedef struct {
        double cycle;               ///< 当前周期计数器 (浮点累加避免截断漂移)
        uint8_t volume;             ///< 当前音量级别
        uint8_t bitsRemaining;      ///< 移位寄存器中剩余的位数
        uint32_t period;            ///< 噪声周期 (频率)
        uint8_t enable;             ///< 通道启用标志
        Envelope envelope;          ///< 包络发生器
        uint16_t lfsr;              ///< 线性反馈移位寄存器 (15 位)
        uint8_t lengthCounter;      ///< 长度计数器 (音符长度)
        uint8_t shortMode;          ///< 短模式标志 ($400E 的 D7)
        uint8_t periodIndex;        ///< 周期表索引 ($400E 的 D0-D3)
    } Noise;

    /**
     * @struct Dmc
     * @brief DMC (增量调制通道) 状态
     * 
     * 从 ROM 回放采样音频。
     * 使用增量编码重建波形。
     */
    typedef struct {
        uint8_t enable;             ///< 通道启用标志
        uint16_t orgaddr;           ///< 原始起始地址
        uint16_t address;           ///< 当前读取地址
        uint16_t length;            ///< 原始采样长度
        uint16_t bytesRemaining;    ///< 剩余要读取的字节数
        uint16_t period;            ///< 采样播放速率
        double clock = 428;         ///< DMC 定时器倒数计数 (距下次 bit 时钟的剩余 CPU 周期)
        uint8_t volume;             ///< 当前音量
        uint8_t irqEnable;          ///< IRQ 启用标志
        uint8_t irqLoop;            ///< 循环模式 / IRQ 生成
        uint8_t bitsRemaining;      ///< 当前字节中剩余的位数
        uint8_t sampleBuffer;       ///< 当前采样字节
        uint8_t interruptFlag;      ///< 中断挂起标志
    } Dmc;

    /**
     * @struct Triangle
     * @brief 三角波通道状态
     * 
     * 以固定音量生成三角波。
     * 常用于低音线和打击乐。
     */
    typedef struct {
        uint8_t enable;             ///< 通道启用标志
        double cycle;               ///< 当前周期计数器 (浮点累加避免截断漂移)
        uint16_t period;            ///< 三角波周期 (频率)
        uint8_t lengthCounter;      ///< 长度计数器
        uint8_t linearCounter;      ///< 线性计数器 (音量)
        uint8_t reloadValue;        ///< 线性计数器重载值
        uint8_t reload;             ///< 线性计数器重载标志
        uint8_t flagHalt;           ///< 停止标志 (长度/线性计数器)
        uint8_t seqIndex;           ///< 序列表索引
        uint8_t volume;             ///< 当前音量 (来自序列)
        uint8_t incMask;            ///< 序列的递增掩码
    } Triangle;

    /**
     * @struct Sweep
     * @brief 扫频单元结构
     * 
     * 为脉冲通道实现频率扫频。
     * 通过自动改变频率创建类似警笛的效果。
     */
    typedef struct {
        uint8_t enable;     ///< 扫频启用标志 (E)
        uint8_t period;     ///< 扫频周期 (PPP)
        uint8_t negative;   ///< 取反标志 (N) - 上/下扫频
        uint8_t shift;      ///< 移位量 (SSS)
        uint8_t divider;    ///< 时钟分频器
        uint8_t reload;     ///< 重载标志
    } Sweep;

    /**
     * @struct Pulse
     * @brief 脉冲通道状态
     * 
     * 生成具有可选占空比的方波。
     * 两个相同的脉冲通道用于和声。
     */
    typedef struct {
        uint8_t enable;         ///< 通道启用标志
        Sweep sweep;            ///< 扫频单元
        Envelope envelope;      ///< 包络发生器
        uint16_t period;        ///< 当前周期 (11 位)
        uint16_t curTimer;      ///< 当前定时器值
        uint8_t lengthCounter;  ///< 长度计数器 (5 位)
        uint8_t ctrl;           ///< 控制寄存器 ($4000/$4004)
        uint8_t seqIndex;       ///< 占空比序列索引
        uint8_t volume;         ///< 当前音量
        double cycle;           ///< 当前周期计数器 (浮点累加避免截断漂移)
    } Pulse;

    /// 通道启用标志 (每个通道一位)
    enum {
        DMCEnable = 1 << 4,       ///< DMC 通道启用
        NoiseEnable = 1 << 3,     ///< 噪声通道启用
        TriangleEnable = 1 << 2,  ///< 三角波通道启用
        Pulse2Enable = 1 << 1,    ///< 脉冲通道 2 启用
        Pulse1Enable = 1          ///< 脉冲通道 1 启用
    };

private:
    // Audio sampling configuration
    enum {
        NTSC_CPU_RATE = 1789773,      ///< NTSC CPU clock frequency (Hz)
        SAMPLES_PER_SEC = 44100,      ///< Output sample rate (Hz)
        SAMPLES_PER_FRAME = SAMPLES_PER_SEC / 60  ///< Samples per video frame
    };

    /// CPU cycles per audio sample (approximately 40.7)
    // 每采样 CPU 周期数按"每帧 735 样本"推导 (29780.5/735 = 40.5179):
    // 前端 VSync 锁 60.000fps, 每帧须恰好产出 735 样本才能与 SDL 44100Hz 消费速率
    // 平衡 (735×60=44100)。若按 NTSC_CPU_RATE/44100=40.586 推导, 每帧只产 733.7
    // 样本, GUI 下音频缓冲持续排空 → 断流饥饿 (脉冲"偏细"、DMC 人声缺失)。
    // 29780.5 = NTSC 每帧实际 CPU 周期 (1789773/60.0988), 帧率与采样率的
    // 1:735 关系由帧 CPU 总量决定, 与名义 60Hz 无关。
    // 通道时钟由 CPU 周期驱动 (clockAPU), 音高不受此值影响。
    static constexpr double CPU_CYCLES_PER_FRAME = 1789773.0 / 60.0988; // 29780.5
    const double mCpuCyclePerSample = CPU_CYCLES_PER_FRAME / (double)SAMPLES_PER_FRAME;

    uint32_t mQueueOffset = 0;        ///< Queue offset for sample buffering
    double mRemnantCpuCycles = 0;     ///< 采样生成后剩余的 CPU 周期余数 (double 避免截断漂移)
    uint32_t mBufferIndex = 0;       ///< Current buffer index

    Pulse mPulses[2] = {};       ///< Two pulse channels
    Triangle mTriangle = {};     ///< Triangle channel
    Noise mNoise = {};           ///< Noise channel
    Dmc mDmc = {};               ///< DMC channel

    int8_t mOutputs[5] = {};     ///< Channel output levels

    // Envelope processing
    void processEnvelope();
    static void processEnvelope(Envelope* envelope);
    
    // Sweep unit processing
    void processSweepUnit();
    static void processPulseSweepUnit(Pulse* pulse, bool isOne);
    
    // Length counter processing
    void processLengthCounter();
    static void processPulseLengthCounter(Pulse* pulse);
    
    // Linear counter processing
    void processLinearCounter();

    // Channel state updates
    void updateTriangleState();
    void updatePulseState(Pulse* pulse);
    void updateNoiseState();
    static uint16_t lfsrChange(uint16_t v, uint8_t c);
    
    // Sample generation
    int16_t soundMixer();

    // DMC processing
    void resetDMC();
    void updateDmcBit(Dmc* dmc);

    // Channel-specific sample generation
    uint8_t makePulseSamples(Pulse* pulse);
    uint8_t makeTriangleSamples(Triangle* triangle);
    uint8_t makeNoiseSamples(Noise* noise);
    uint8_t makeDmcSamples(Dmc* dmc, double extraCpuCycles = 0.0);

public:
    /**
     * @brief 按 CPU 周期推进 DMC 定时器 (由 Famicom 在 CPU 指令执行后调用)
     * @param cpuCycles 本次推进的 CPU 周期数
     *
     * 真机上 DMC 定时器由 CPU 时钟直接驱动, 与音频采样率无关。
     * 逐指令驱动保证 IRQ 触发时刻有指令级精度, blargg dmc_rates 依赖此行为。
     */
    void clockDmc(uint32_t cpuCycles)
    {
        mDmc.clock += cpuCycles;
        while (mDmc.clock >= mDmc.period) {
            mDmc.clock -= mDmc.period;
            updateDmcBit(&mDmc);
        }
    }

    cpu::NesCPU* mCPU;
    std::function<void(int16_t*, int)> mAudioCallback;

public:
    /**
     * @brief Construct a new NesPAPU object
     */
    explicit NesPAPU(cpu::NesCPU* cpu = nullptr);

    /**
     */
    void setCPU(cpu::NesCPU* cpu) { mCPU = cpu; }

    /**
     */
    void setAudioCallback(std::function<void(int16_t*, int)> callback) {
        mAudioCallback = std::move(callback);
    }

    /**
     * @brief 按 CPU 周期推进 APU 时序 (DMC 定时器 + 帧计数器)
     * @param cpuCycles 本次推进的 CPU 周期数
     *
     * 由 Famicom 在每轮 CPU 执行后调用。真机上 DMC 定时器与帧计数器
     * 均由 CPU 时钟直接驱动, 与音频采样率无关; 逐指令驱动保证 IRQ
     * 触发时刻有指令级精度 (blargg dmc_rates / irq 测试依赖此行为)。
     *
     * DMC 定时器为倒数模型: 从 period 倒数至 0 触发 bit 并重载。
     * rate 寄存器只在重载时生效 (写 $4010 立即重载)。
     */
    void clockAPU(uint32_t cpuCycles)
    {
        debugTotalClocked += cpuCycles;
        // 注意: DMC 定时器不在这里推进 — 它必须与输出采样时钟同步
        // (见 makeDmcSamples 的说明), 否则语音波形会被采样块粗化。

        // 帧计数器步进点 (NTSC, 帧内 CPU 周期):
        // 4 步: 7457 / 14913 / 22371 / 29829(+IRQ)
        // 5 步: 7457 / 14913 / 22371 / 29830 / 37282
        mFrameCycleAccum += cpuCycles;
        static const uint32_t STEPS_4STEP[] = {7457, 14913, 22371, 29830};
        static const uint32_t STEPS_5STEP[] = {7457, 14913, 22371, 29830, 37282};
        const uint32_t* steps = mFrame5Step ? STEPS_5STEP : STEPS_4STEP;
        const int stepCount = mFrame5Step ? 5 : 4;
        while (mFrameCycleAccum >= steps[mFrameStepIndex] && mFrameStepIndex < stepCount) {
            mFrameCycleAccum -= steps[mFrameStepIndex];
            ++mFrameStepIndex;
            processFrameCounter();
            if (mFrameStepIndex >= stepCount) {
                mFrameStepIndex = 0; // 帧边界, 重新从第一步计时
            }
        }
    }

    // 诊断接口 (测试用)
    uint64_t debugTotalClocked = 0; // clockAPU 累计收到的 CPU 周期
    uint32_t debugDmcBytesRemaining() const { return mDmc.bytesRemaining; }
    uint32_t debugDmcLoop() const { return mDmc.irqLoop; }
    uint32_t debugDmcVolume() const { return mDmc.volume; }
    uint32_t debugDmcEnable() const { return mDmc.enable; }
    uint32_t debugDmcBitsRemaining() const { return mDmc.bitsRemaining; }
    double debugDmcClock() const { return mDmc.clock; }
    uint16_t debugDmcPeriod() const { return mDmc.period; }

    double debugReceivedCycles = 0;   // 诊断: makeSamples 累计收到的 CPU 周期
    uint8_t mFrameClock = 0;          ///< Frame clock counter
    uint8_t mFrameInterrupt = 0;      ///< Frame interrupt flag
    uint8_t channelCtrl = 0;          ///< Channel enable register ($4015)
    uint8_t mFrame5Step = 0;          ///< 5-step frame sequence counter
    uint8_t mIRQDisable = 0;          ///< IRQ disable flag
    uint32_t mFrameCycleAccum = 0;    ///< 帧计数器 CPU 周期累计
    int mFrameStepIndex = 0;          ///< 当前帧内已执行的步数
    Sample mSamplesQueue[20];         ///< Sample queue (20 frames buffer)

    /**
     * @brief 重置 APU 到初始状态
     * 
     * 执行软复位:
     * - 清除所有通道状态
     * - 重置帧计数器
     * - 清除采样缓冲区
     */
    void reset();

    /**
     * @brief 通过 CPU 写入 APU 寄存器
     * @param address 寄存器地址 ($4000-$4017)
     * @param sampleBuffer 要写入的数据字节
     * 
     * 处理对以下寄存器的写入:
     * - $4000-$4003: 脉冲 1 寄存器
     * - $4004-$4007: 脉冲 2 寄存器
     * - $4008-$400B: 三角波寄存器
     * - $400C-$400F: 噪声寄存器
     * - $4010-$4013: DMC 寄存器
     * - $4015: 通道启用
     * - $4017: 帧计数器
     */
    void cpuWriteRegister(uint16_t address, uint8_t sampleBuffer);

    /**
     * @brief 通过 CPU 读取 APU 寄存器
     * @param address 寄存器地址 ($4000-$4017)
     * @return 从寄存器读取的数据字节
     * 
     * 目前仅实现 $4015 (通道状态)。
     */
    uint8_t cpuReadRegister(uint16_t address);

    /**
     * @brief 处理帧计数器序列
     * 
     * 定期调用以更新通道状态。
     * 实现 4 步或 5 步序列。
     */
    void processFrameCounter();

    /**
     * @brief 生成音频采样
     * @param cpuCycle 当前 CPU 周期数
     * @return 生成的采样值
     */
    uint8_t makeSamples(uint32_t cpuCycle);

    /**
     * @brief 生成音频采样 (无参数版本)
     * @return 生成的采样值
     */
    uint8_t makeSamples();

    /**
     * @brief 获取队列中的采样数
     * @return 可用采样数
     */
    int getSamplesQueue();

    /**
     * @brief 获取音频采样缓冲区
     * @param size 输出参数,采样数量
     * @return 指向采样缓冲区的指针
     */
    int16_t* getSamples(uint32_t* size);

    /**
     * @brief 保存 APU 状态到存档包
     * @param bundle 存档包目标
     */
    void save(io::SaveBundle* bundle);

    /**
     * @brief 从存档包恢复 APU 状态
     * @param bundle 存档包来源
     */
    void restore(io::SaveBundle* bundle);
};

} // namespace apu
} // namespace nes
