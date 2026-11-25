#pragma once
#include <cstdint>
#include <type_traits>

namespace nes {

/**
 * @brief 通用标志寄存器模板类
 * 
 * 提供类型安全的标志位管理，适用于各种标志寄存器。
 * 
 * 模板参数：
 * - FlagEnum: 标志枚举类型（必须是 uint8_t 底层的 enum class）
 * - DefaultFlags: 默认设置的标志位组合
 * 
 * 特性：
 * - 类型安全的标志查询和修改
 * - constexpr 支持编译期优化
 * - 统一的接口设计
 * - 支持原始字节访问（用于序列化）
 * 
 * @example
 * @code
 * // 定义标志枚举
 * enum class MyFlag : uint8_t {
 *     Flag1 = 0x01,
 *     Flag2 = 0x02,
 *     Flag3 = 0x04
 * };
 * 
 * // 使用标志寄存器
 * FlagRegister<MyFlag> flags;
 * flags.Set(MyFlag::Flag1);
 * if (flags.IsSet(MyFlag::Flag1)) { ... }
 * @endcode
 */
template<typename FlagEnum, uint8_t DefaultFlags = 0>
class FlagRegister {
    static_assert(std::is_enum_v<FlagEnum>, "FlagEnum must be an enum type");
    static_assert(sizeof(FlagEnum) == sizeof(uint8_t), "FlagEnum must have uint8_t underlying type");

private:
    uint8_t mFlags = DefaultFlags;  ///< 标志位存储

public:
    /**
     * @brief 默认构造函数
     * 使用指定的默认标志值初始化
     */
    constexpr FlagRegister() = default;
    
    // ========================================================================
    // 标志查询
    // ========================================================================
    
    /**
     * @brief 检查特定标志是否已设置
     * @param flag 要检查的标志
     * @return true 如果标志已设置，false 否则
     */
    [[nodiscard]] constexpr bool IsSet(FlagEnum flag) const noexcept {
        return (mFlags & static_cast<uint8_t>(flag)) != 0;
    }
    
    /**
     * @brief 检查特定标志是否未设置
     * @param flag 要检查的标志
     * @return true 如果标志未设置，false 否则
     */
    [[nodiscard]] constexpr bool IsClear(FlagEnum flag) const noexcept {
        return (mFlags & static_cast<uint8_t>(flag)) == 0;
    }
    
    /**
     * @brief 检查是否有任何标志被设置
     * @return true 如果有任意标志被设置
     */
    [[nodiscard]] constexpr bool HasAny() const noexcept {
        return mFlags != 0;
    }
    
    // ========================================================================
    // 标志修改
    // ========================================================================
    
    /**
     * @brief 设置特定标志
     * @param flag 要设置的标志
     */
    constexpr void Set(FlagEnum flag) noexcept {
        mFlags |= static_cast<uint8_t>(flag);
    }
    
    /**
     * @brief 清除特定标志
     * @param flag 要清除的标志
     */
    constexpr void Clear(FlagEnum flag) noexcept {
        mFlags &= ~static_cast<uint8_t>(flag);
    }
    
    /**
     * @brief 根据布尔值更新标志
     * @param flag 要更新的标志
     * @param value true 设置标志，false 清除标志
     */
    constexpr void Update(FlagEnum flag, bool value) noexcept {
        value ? Set(flag) : Clear(flag);
    }
    
    /**
     * @brief 清除所有标志
     */
    constexpr void ClearAll() noexcept {
        mFlags = 0;
    }
    
    /**
     * @brief 设置所有标志
     */
    constexpr void SetAll() noexcept {
        mFlags = 0xFF;
    }
    
    // ========================================================================
    // 原始访问（用于存档状态）
    // ========================================================================
    
    /**
     * @brief 获取原始标志字节
     * @return 8 位标志值
     */
    [[nodiscard]] constexpr uint8_t GetRaw() const noexcept {
        return mFlags;
    }
    
    /**
     * @brief 设置原始标志字节
     * @param raw 新的标志值
     */
    constexpr void SetRaw(uint8_t raw) noexcept {
        mFlags = raw;
    }
};

} // namespace nes
