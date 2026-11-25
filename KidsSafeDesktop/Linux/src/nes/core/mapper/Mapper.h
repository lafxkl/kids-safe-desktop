#pragma once
#include "../common.h"
#include "../Rom.h"
#include "../SaveBundle.h"
#include "MapperHost.h"
#include "assert.h"

namespace nes {
namespace device {

class Mapper
{
  protected:
    MapperHost* mHost = nullptr;  // 主机能力 (延迟绑定)
    FcRom* mROM;

    enum BANKSIZE {
        BANKSIZE_1KB = 1024,
        BANKSIZE_2KB = 2048,
        BANKSIZE_4KB = 4096,
        BANKSIZE_8KB = 8192,
        BANKSIZE_16KB = 16384,
        BANKSIZE_32KB = 32768
    };

    uint8_t* mPrgBank[4] = {nullptr};
    uint8_t* mChrBank[8] = {nullptr};

    uint8_t mSaveRAM[BANKSIZE_8KB] = {0};
    uint8_t mExRAM[BANKSIZE_4KB] = {0};
    uint8_t mBusRAM[512] = {0};

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    inline void SetCPUBank_8KB(const uint8_t pos, const uint8_t bank)
    {
        mPrgBank[pos] = mROM->prgrom + ((uint32_t)bank % (mROM->prg16kbCount << 1)) * BANKSIZE_8KB;
    }

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    inline void SetCPUBank_16KB(const uint8_t pos, const uint8_t bank)
    {
        uint8_t** prgbank = &mPrgBank[pos << 1];
        uint16_t bank8kb = ((uint32_t)bank % mROM->prg16kbCount) << 1;

        prgbank[0] = mROM->prgrom + (bank8kb + 0) * BANKSIZE_8KB;
        prgbank[1] = mROM->prgrom + (bank8kb + 1) * BANKSIZE_8KB;
    }

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    inline void SetCPUBank_32KB(const uint8_t bank)
    {
        uint16_t bank8kb = ((uint32_t)bank % (mROM->prg16kbCount >> 1)) << 2;

        mPrgBank[0] = mROM->prgrom + (bank8kb + 0) * BANKSIZE_8KB;
        mPrgBank[1] = mROM->prgrom + (bank8kb + 1) * BANKSIZE_8KB;
        mPrgBank[2] = mROM->prgrom + (bank8kb + 2) * BANKSIZE_8KB;
        mPrgBank[3] = mROM->prgrom + (bank8kb + 3) * BANKSIZE_8KB;
    }

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    inline void SetPPUBank_1KB(const uint8_t pos, const uint8_t bank)
    {
        mChrBank[pos] = mROM->chrrom + (((uint32_t)bank) * BANKSIZE_1KB);
    }

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    void SetPPUBank_2KB(const uint8_t pos, const uint8_t bank)
    {
        uint8_t** chrbank = mChrBank + (pos << 1);
        uint16_t bank1kb = ((uint32_t)bank % (mROM->chr8kbCount << 2)) << 1;

        chrbank[0] = mROM->chrrom + (bank1kb + 0) * BANKSIZE_1KB;
        chrbank[1] = mROM->chrrom + (bank1kb + 1) * BANKSIZE_1KB;
    }

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    inline void SetPPUBank_4KB(const uint8_t pos, const uint8_t bank)
    {
        uint8_t** chrbank = mChrBank + (pos << 2);
        uint16_t bank1kb = ((uint32_t)bank % (mROM->chr8kbCount << 1)) << 2;

        chrbank[0] = mROM->chrrom + (bank1kb + 0) * BANKSIZE_1KB;
        chrbank[1] = mROM->chrrom + (bank1kb + 1) * BANKSIZE_1KB;
        chrbank[2] = mROM->chrrom + (bank1kb + 2) * BANKSIZE_1KB;
        chrbank[3] = mROM->chrrom + (bank1kb + 3) * BANKSIZE_1KB;
    }

    /// <summary>
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="blank"></param>
    void SetPPUBank_8KB(const uint8_t bank);
    virtual void OnReset();
    ;

  public:
    Mapper() = delete;
    Mapper(FcRom* rom) : mROM(rom)
    {}
    virtual ~Mapper()
    {}

    /**
     * @brief 该板卡的 nametable 镜像是否由硬件硬连线 (而非 mapper 运行时控制)
     *
     * 只有镜像硬连线的经典板卡 (NROM/UxROM/CNROM/GxROM) 返回 true。
     * 数据库/覆盖表只允许对这类板卡应用镜像修正: 若 mapper 运行时
     * 自行切换镜像 (AxROM/MMC1/MMC3 等), 强加覆盖会冻结或损坏画面。
     * 默认 false = 安全默认 (漏标注只是放弃一次修正, 绝不会弄坏正常游戏)。
     */
    virtual bool hasHardwiredMirroring() const { return false; }
    
    /**
     * @brief 绑定主机能力 (镜像/IRQ/渲染状态)
     * @param host 由 Devices 实现
     */
    void setHost(MapperHost* host) { mHost = host; }

    /**
     * @brief 扫描线回调 (每行一次, 供 MMC3/VRC 等 IRQ 计数器使用)
     */
    virtual void onScanline(uint32_t line)
    {}
    void reset();

    // ========================================================================
    // CPU 总线 ($4020-$FFFF): 按地址窗口分派到各扩展点
    // ========================================================================
    uint8_t cpuRead(uint16_t address)
    {
        if (address >= 0x8000)
            return prgRead(address);
        if (address >= 0x6000)
            return prgRamRead(address);
        return expansionRead(address);
    }

    void cpuWrite(uint16_t address, uint8_t data)
    {
        if (address >= 0x8000) {
            prgWrite(address, data);
            return;
        }
        if (address >= 0x6000) {
            prgRamWrite(address, data);
            return;
        }
        expansionWrite(address, data);
    }

    // ========================================================================
    // PPU 总线 ($0000-$1FFF): CHR ROM/RAM
    // ========================================================================
    virtual uint8_t ppuRead(uint16_t address);

    virtual void ppuWrite(uint16_t address, uint8_t data);

    virtual void save(nes::io::SaveBundle* bundle);

    virtual void restore(nes::io::SaveBundle* bundle);

    virtual void OnSave(nes::io::SaveBundle* bundle);

    virtual void OnRestore(nes::io::SaveBundle* bundle);

  protected:
    // ========================================================================
    // 卡带各地址窗口的扩展点 (mapper 子类按需覆盖)
    // ========================================================================

    /// @brief $8000-$FFFF: PRG ROM 读取
    virtual uint8_t prgRead(uint16_t address)
    {
        uint16_t prgIndex = (address & 0x7FFF) >> 13;
        uint16_t offset = address & 0x1FFF;

        if (prgIndex >= sizeof(mPrgBank) / sizeof(mPrgBank[0])) {
            LOG_ERROR << "Mapper prgRead: prgIndex out of bounds: " << prgIndex << " at 0x" << std::hex << address;
            return 0;
        }

        if (!mPrgBank[prgIndex]) {
            LOG_ERROR << "Mapper prgRead: mPrgBank[" << prgIndex << "] is null at 0x" << std::hex << address;
            return 0;
        }

        return mPrgBank[prgIndex][offset];
    }

    /// @brief $8000-$FFFF: mapper 寄存器写 (默认拒绝)
    virtual void prgWrite(uint16_t address, uint8_t data);

    /// @brief $6000-$7FFF: PRG RAM
    virtual uint8_t prgRamRead(uint16_t address);
    virtual void prgRamWrite(uint16_t address, uint8_t data);

    /// @brief $4020-$5FFF: 扩展区 (VRC6 音频等)
    virtual uint8_t expansionRead(uint16_t address);
    virtual void expansionWrite(uint16_t address, uint8_t data);
};

} // namespace device
} // namespace nes
