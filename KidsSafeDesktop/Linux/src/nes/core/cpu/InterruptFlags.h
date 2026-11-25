#pragma once
#include <cstdint>
#include "FlagRegister.h"

namespace nes {

/**
 * @brief CPU 中断触发类型标志
 * 
 * 用于管理 NES CPU 的中断待处理状态。
 */
enum class InterruptType : uint8_t {
    NmiPending = 0x1, ///< NMI 待处理标志
    IrqPending = 0x2, ///< IRQ 待处理标志
    IrqOneShot = 0x4, ///< IRQ 单次触发
    // 以下为了兼容性保留的别名
    DmcInt   = 0x2,   ///< DMC 中断 (IrqPending 的别名)
    hsyncInt = 0x2,   ///< H-Sync 中断 (IrqPending 的别名)
    FrameInt = 0x2    ///< Frame 中断 (IrqPending 的别名)
};

/**
 * @brief 中断标志寄存器包装类
 * 
 * 提供类型安全的中断标志访问，包含便捷的操作方法。
 * 
 * 特性：
 * - 类型安全的标志查询和修改
 * - 支持多种中断类型的组合管理
 * - constexpr 支持编译期优化
 * - 与 CPUFlags 类似的设计模式，保持一致性
 * 
 * @note 此类是 FlagRegister 的类型别名，保持 API 一致性
 */
using InterruptFlags = FlagRegister<InterruptType>;

} // namespace nes
