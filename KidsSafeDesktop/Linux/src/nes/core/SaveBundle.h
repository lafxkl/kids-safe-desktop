#pragma once
#include "common.h"
#include <vector>
#include <cstddef>

namespace nes {
class Famicom;

namespace io {

class SaveBundle
{
  private:
    std::vector<uint8_t> mBuffer; ///< 内部状态缓冲区
    size_t mPosition = 0;         ///< 当前读写位置
    bool mIsSaving = false;       ///< 当前是否处于保存模式

  public:
    SaveBundle();
    ~SaveBundle();

    /**
     * @brief 重置保存缓冲区（清空数据）
     */
    void reset();

    /**
     * @brief 重置读取位置（用于恢复操作）
     */
    void resetRestore();

    /**
     * @brief 重置保存位置（用于新的保存操作）
     */
    void resetSave();

    /**
     * @brief 开始保存操作
     * @return true 如果成功
     */
    bool beginSave();

    /**
     * @brief 开始恢复操作
     * @param data 外部传入的状态数据
     * @return true 如果成功
     */
    bool beginRestore(const std::vector<uint8_t>& data);

    /**
     * @brief 结束保存/恢复操作
     */
    void endOperation();

    /**
     * @brief 获取保存后的数据指针和大小
     * @param outSize 输出数据大小
     * @return 数据指针（由 SaveBundle 内部管理，不要手动释放）
     */
    const uint8_t* getData(size_t* outSize) const;

    /// <summary>
    /// 保存一个字节的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <param name="data">要保存的数据</param>
    void saveByte(uint8_t data);

    /// <summary>
    /// 保存一个字（两个字节）的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <param name="data">要保存的数据</param>
    void saveWord(uint16_t data);

    /// <summary>
    /// 保存一个双字（四个字节）的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <param name="data">要保存的数据</param>
    void savePtr(uint32_t data);

    /// <summary>
    /// 保存指定字节数的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <param name="data">要保存的数据</param>
    void saveBlock(const std::vector<uint8_t>& data);

    /// <summary>
    /// 保存指定字节数的数据（原始指针版本）
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <param name="data">要保存的数据指针</param>
    /// <param name="size">要保存的数据字节数</param>
    void saveBlock(const uint8_t* data, size_t size);

    /// <summary>
    /// 恢复一个字节的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <returns>返回恢复的数据</returns>
    uint8_t restoreByte();

    /// <summary>
    /// 恢复一个字（两个字节）的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <returns>返回恢复的数据</returns>
    uint16_t restoreWord();

    /// <summary>
    /// 恢复一个双字（四个字节）的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <returns>返回恢复的数据</returns>
    uint32_t restorePtr();

    /// <summary>
    /// 恢复指定字节数的数据
    /// 注意：内部实现是一个简单堆栈，
    /// 遵循先进后出原则
    /// 因此保存和恢复的顺序必须相同
    /// </summary>
    /// <param name="dst">要恢复的目标指针</param>
    /// <param name="size">要恢复的数据字节数</param>
    void restoreBlock(uint8_t* dst, size_t size);
};

} // namespace io
} // namespace nes
