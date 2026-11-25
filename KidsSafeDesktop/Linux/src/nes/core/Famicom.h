#pragma once
#include <memory>
#include <stdio.h>
#include <string>
#include <vector>
#include "NesCartridge.h"
#include "cpu/InterruptFlags.h"  // 包含中断标志定义
namespace nes {

// Forward declarations
namespace device {
class NesCartridge;
}
class Devices;

class Devices;

/**
 * @class Famicom
 * @brief Main emulator facade class
 *
 * The Famicom class coordinates all emulator components:
 * - CPU (6502 processor)
 * - PPU (Picture Processing Unit)
 * - APU (Audio Processing Unit)
 * - Cartridge (ROM and Mapper)
 * - GamePad (Input)
 */
class Famicom
{
  private:
    Devices* mDevices; ///< Device manager (owns all hardware components)

    void execCPU(const int masterCycle);
    void renderPattern(uint8_t* mem) const;

  public:
    uint32_t mMasterClockCycle = 0; ///< Total master clock cycles elapsed
    uint32_t mLastCpuCycleCount = 0; ///< Previous CPU cycle count

    Famicom();
    ~Famicom();

    void userInput(uint8_t index, uint8_t state) const;
    uint8_t* getCanvas() const;

    /** @brief 获取音频采样
     * @param size 输出采样数量
     * @return 音频采样指针,nullptr表示读取完毕
     * @note 内部是队列管理，需要多次调用才能获取完所有采样
     */
    int16_t* getSamples(uint32_t* size) const;
    void emulationFrame();

    /**
     * @brief 保存模拟器状态
     * @return 包含状态数据的字节向量
     * @note 调用者拥有返回的 vector，可以自由管理其生命周期
     */
    std::vector<uint8_t> save() const;

    /**
     * @brief 恢复模拟器状态
     * @param data 状态数据
     * @return true 如果成功
     */
    bool restore(const std::vector<uint8_t>& data) const;

    void reset() const;


    /**
     * @param rom 输出 ROM 指针
     * @param data ROM 数据
     * @param size 数据大小
     * @return 是否成功
     */
    /**
     * @brief 插入卡带 (设置到 Devices,由调用者管理生命周期)
     * @param cartridge 卡带指针
     */
    void insertCartridge(nes::device::NesCartridge* cartridge);

    /**
     * @brief 拔出卡带 (从 Devices 移除,不删除卡带)
     */
    void ejectCartridge();

    /**
     * @brief 检查是否有卡带
     */
    bool hasCartridge() const;

    /**
     * @brief 获取当前卡带
     */
    nes::device::NesCartridge* getCartridge() const;
    inline Devices* getDevices() const
    {
        return mDevices;
    }

  private:
    /**
     * @brief 坏 iNES 头镜像修正 (CRC32 键控, 仅硬镜像板卡)
     */
    void applyMirrorOverride(nes::device::NesCartridge* cartridge) const;
};

} // namespace nes
