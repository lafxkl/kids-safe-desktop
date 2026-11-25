#pragma once
#include "mapper/Mapper.h"
#include "Rom.h"
#include <memory>
#include <string>

namespace nes {

namespace ppu {
class NesPPU;
}

namespace device {

class NesCartridge
{
  private:
    typedef struct
    {
        uint8_t nes[4];
        uint8_t PRGBankCount;
        uint8_t CHRBankCount;
        uint8_t flag1;
        uint8_t flag2;
    } NesHeader;

    FcRom* mRom = nullptr;
    bool mLoadedrom = false;

  public:
    NesCartridge();
    ~NesCartridge();

    /**
     * @brief 从文件加载 ROM
     * @param path ROM 文件路径
     * @return Cartridge 原始指针,失败返回 nullptr
     * @warning 调用者必须负责删除返回的指针 (使用 delete)
     * @note 推荐用法: auto* cart = loadFromFile(path); famicom.insertCartridge(cart);
     *       insertCartridge 只设置引用,不拥有所有权,但仍需调用者管理生命周期
     */
    static NesCartridge* loadFromFile(const std::string& path);

    /**
     * @brief 从内存数据加载 ROM
     * @param data ROM 数据指针
     * @param size 数据大小
     * @return Cartridge 原始指针,失败返回 nullptr
     * @warning 调用者必须负责删除返回的指针 (使用 delete)
     * @note 推荐用法: auto* cart = loadFromMemory(data, size); famicom.insertCartridge(cart);
     *       insertCartridge 只设置引用,不拥有所有权,但仍需调用者管理生命周期
     */
    static NesCartridge* loadFromMemory(const void* data, size_t size);

    uint8_t prgNum();
    void releaseFcRom(FcRom* fcrom);

    FcRom* getRom();

    bool isLoadedRom();

    void reset();

    /**
     * @brief 绑定主机能力 (由 Famicom::insertCartridge 调用)
     * @param host 镜像/IRQ/渲染状态接口, 由 Devices 实现
     */
    void bindHost(device::MapperHost* host);

    /**
     * @brief 检查是否已加载 ROM
     */
    bool isLoaded() const
    {
        return mLoadedrom;
    }

    /**
     * @brief 获取 Mapper 编号
     */
    uint8_t mapperNumber() const
    {
        return mRom ? mRom->mapperNumber : 0;
    }

    int loadRom(FcRom* rom);

  private:
    /**
     * @brief 从数据创建 ROM (内部方法)
     * @param rom 输出 ROM 指针
     * @param data ROM 数据
     * @param size 数据大小
     * @return 是否成功
     */
    bool createFromData(FcRom** rom, void* data, size_t size);

  public:
    void save(nes::io::SaveBundle* bundle);
    void restore(nes::io::SaveBundle* bundle);

    /// <summary>
    /// 扫描线回调 (每行一次)
    /// </summary>
    inline void onScanline(uint32_t line)
    {
        if (mRom && mRom->mapper)
            mRom->mapper->onScanline(line);
    }

    /// <summary>
    /// CPU 总线读取 ($4020-$FFFF)
    /// </summary>
    inline uint8_t cpuRead(uint16_t address)
    {
        if (!mRom || !mRom->mapper) {
            LOG_ERROR << "Cartridge cpuRead: mapper is null at 0x" << std::hex << address;
            return 0;
        }
        return mRom->mapper->cpuRead(address);
    }

    /// <summary>
    /// CPU 总线写入 ($4020-$FFFF)
    /// </summary>
    inline void cpuWrite(uint16_t address, uint8_t data)
    {
        if (!mRom || !mRom->mapper) {
            LOG_ERROR << "Cartridge cpuWrite: mapper is null at 0x" << std::hex << address;
            return;
        }
        mRom->mapper->cpuWrite(address, data);
    }

    /// <summary>
    /// PPU 总线读取 ($0000-$1FFF, pattern table)
    /// </summary>
    inline uint8_t ppuRead(uint16_t address)
    {
        if (!mRom || !mRom->mapper) {
            LOG_ERROR << "Cartridge ppuRead: mapper is null at 0x" << std::hex << address;
            return 0;
        }
        return mRom->mapper->ppuRead(address);
    }

    /// <summary>
    /// PPU 总线写入 ($0000-$1FFF, CHR RAM/CHR bank)
    /// </summary>
    inline void ppuWrite(uint16_t address, uint8_t data)
    {
        if (!mRom || !mRom->mapper) {
            LOG_ERROR << "Cartridge ppuWrite: mapper is null at 0x" << std::hex << address;
            return;
        }
        mRom->mapper->ppuWrite(address, data);
    }
};

} // namespace device
} // namespace nes
