#pragma once
#include "SaveBundle.h"

namespace nes {
namespace device {

class NesGamePad
{
private:
    //D7??????????0 ????1????
    uint8_t mGamepadEn = 0;
    uint8_t mGamepadIndex_1 = 0;
    uint8_t mGamepadIndex_2 = 0;
    uint8_t mGamepadStatus[16] = {0};
public:
    /**
     * @brief 构造手柄对象
     */
    NesGamePad();
    
    /**
     * @brief 处理用户输入
     * @param index 手柄索引 (0 或 1)
     * @param state 按键状态
     */
    void userInput(uint8_t index, unsigned state);
    
    /**
     * @brief 通过 CPU 写入寄存器
     * @param address 寄存器地址
     * @param data 数据字节
     */
    void cpuWriteRegister(uint16_t address, uint8_t data);
    
    /**
     * @brief 通过 CPU 读取寄存器
     * @param address 寄存器地址
     * @return 读取的数据字节
     */
    uint8_t cpuReadRegister(uint16_t address);

    /**
     * @brief 读取原始按键状态 (仅用于诊断显示)
     * @param index 按键索引 0-15
     */
    uint8_t debugStatus(uint8_t index) const
    {
        return index < 16 ? mGamepadStatus[index] : 0;
    }

    void reset();
    void save(nes::io::SaveBundle* bundle);
    void restore(nes::io::SaveBundle* bundle);
};

} // namespace device
} // namespace nes

