#pragma once
#include <cstdint>
#include "FlagRegister.h"

namespace nes {
namespace cpu {

/**
 * @brief CPU 状态标志枚举
 * 
 * 表示 6502 处理器状态寄存器中的 8 个标志位。
 * 每个标志对应 P 寄存器中的一个特定位。
 */
enum class StatusFlag : uint8_t {
    Carry     = 0b00000001,   ///< BIT0: 进位标志 (Carry)
    Zero      = 0b00000010,   ///< BIT1: 零标志 (Zero)
    Interrupt = 0b00000100,   ///< BIT2: 中断禁用标志 (Interrupt Disable)
    Decimal   = 0b00001000,   ///< BIT3: 十进制模式 (Decimal，NES 未使用)
    Break     = 0b00010000,   ///< BIT4: 中断命令标志 (Break)
    Unused    = 0b00100000,   ///< BIT5: 未使用标志 (始终为 1)
    Overflow  = 0b01000000,   ///< BIT6: 溢出标志 (Overflow)
    Negative  = 0b10000000    ///< BIT7: 负数标志 (Negative)
};

/**
 * @brief CPU 标志寄存器包装类
 * 
 * 提供类型安全的 CPU 状态标志访问，包含便捷的操作方法。
 * 
 * 特性：
 * - 类型安全的标志查询和修改
 * - 基于结果设置标志的便捷方法
 * - 存档序列化的原始访问接口
 * - constexpr 支持编译期优化
 * 
 * @note 继承自 FlagRegister，添加了 CPU 特定的便捷方法
 */
class CPUFlags : public FlagRegister<StatusFlag, static_cast<uint8_t>(StatusFlag::Unused)> {
public:
    /**
     * @brief 默认构造函数
     * 初始化标志位，Unused 位设为 1（符合 6502 复位状态）
     */
    constexpr CPUFlags() = default;
    
    // ========================================================================
    // CPU 特定的便捷方法
    // ========================================================================
    
    /**
     * @brief 根据运算结果设置零标志和负数标志
     * @param result 算术/逻辑运算的结果
     *
     * 这是大多数指令执行后的常见操作。
     * 如果结果为零则设置 Z 标志。
     * 如果结果的位 7 为 1 则设置 N 标志。
     */
    constexpr void SetFromResult(uint8_t result) noexcept {
        Update(StatusFlag::Zero, result == 0);
        Update(StatusFlag::Negative, (result & 0x80) != 0);
    }

    /**
     * @brief 根据有符号结果设置负数标志和零标志
     * @param result 运算的有符号结果
     */
    constexpr void SetFromResult(int8_t result) noexcept {
        SetFromResult(static_cast<uint8_t>(result));
    }
};

} // namespace cpu
} // namespace nes
