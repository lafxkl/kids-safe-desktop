#pragma once
#include "SaveBundle.h"
#include "Rom.h"

namespace nes {

namespace cpu { class NesCPU; }
namespace device { class NesCartridge; enum class Mirror : uint8_t; }
namespace io { class SaveBundle; }
namespace ppu {



/**
 * @class NesPPU
 * @brief 模拟 Ricoh 2C02 图像处理器 (PPU)
 * 
 * PPU 是 NES 的图形处理单元,负责:
 * - 从瓦片地图 (名称表) 渲染背景
 * - 渲染带有优先级的精灵 (对象)
 * - 管理滚动寄存器以实现平滑滚动
 * - 生成 VBlank 和精灵命中中断
 * 
 * 主要特性:
 * - 256x240 像素分辨率
 * - 64 个硬件精灵 (8x8 或 8x16)
 * - 4 个调色板组 (每组 4 色)
 * - 硬件滚动
 * - 精灵优先级和裁剪
 * 
 * @see https://www.nesdev.org/wiki/PPU 查看详细文档
 */

/// 位反转查找表,用于快速像素位操作
static const uint8_t gBitReverseTable256[] = {
    0x00, 0x80, 0x40, 0xC0, 0x20, 0xA0, 0x60, 0xE0, 0x10, 0x90, 0x50, 0xD0, 0x30, 0xB0, 0x70, 0xF0,
    0x08, 0x88, 0x48, 0xC8, 0x28, 0xA8, 0x68, 0xE8, 0x18, 0x98, 0x58, 0xD8, 0x38, 0xB8, 0x78, 0xF8,
    0x04, 0x84, 0x44, 0xC4, 0x24, 0xA4, 0x64, 0xE4, 0x14, 0x94, 0x54, 0xD4, 0x34, 0xB4, 0x74, 0xF4,
    0x0C, 0x8C, 0x4C, 0xCC, 0x2C, 0xAC, 0x6C, 0xEC, 0x1C, 0x9C, 0x5C, 0xDC, 0x3C, 0xBC, 0x7C, 0xFC,
    0x02, 0x82, 0x42, 0xC2, 0x22, 0xA2, 0x62, 0xE2, 0x12, 0x92, 0x52, 0xD2, 0x32, 0xB2, 0x72, 0xF2,
    0x0A, 0x8A, 0x4A, 0xCA, 0x2A, 0xAA, 0x6A, 0xEA, 0x1A, 0x9A, 0x5A, 0xDA, 0x3A, 0xBA, 0x7A, 0xFA,
    0x06, 0x86, 0x46, 0xC6, 0x26, 0xA6, 0x66, 0xE6, 0x16, 0x96, 0x56, 0xD6, 0x36, 0xB6, 0x76, 0xF6,
    0x0E, 0x8E, 0x4E, 0xCE, 0x2E, 0xAE, 0x6E, 0xEE, 0x1E, 0x9E, 0x5E, 0xDE, 0x3E, 0xBE, 0x7E, 0xFE,
    0x01, 0x81, 0x41, 0xC1, 0x21, 0xA1, 0x61, 0xE1, 0x11, 0x91, 0x51, 0xD1, 0x31, 0xB1, 0x71, 0xF1,
    0x09, 0x89, 0x49, 0xC9, 0x29, 0xA9, 0x69, 0xE9, 0x19, 0x99, 0x59, 0xD9, 0x39, 0xB9, 0x79, 0xF9,
    0x05, 0x85, 0x45, 0xC5, 0x25, 0xA5, 0x65, 0xE5, 0x15, 0x95, 0x55, 0xD5, 0x35, 0xB5, 0x75, 0xF5,
    0x0D, 0x8D, 0x4D, 0xCD, 0x2D, 0xAD, 0x6D, 0xED, 0x1D, 0x9D, 0x5D, 0xDD, 0x3D, 0xBD, 0x7D, 0xFD,
    0x03, 0x83, 0x43, 0xC3, 0x23, 0xA3, 0x63, 0xE3, 0x13, 0x93, 0x53, 0xD3, 0x33, 0xB3, 0x73, 0xF3,
    0x0B, 0x8B, 0x4B, 0xCB, 0x2B, 0xAB, 0x6B, 0xEB, 0x1B, 0x9B, 0x5B, 0xDB, 0x3B, 0xBB, 0x7B, 0xFB,
    0x07, 0x87, 0x47, 0xC7, 0x27, 0xA7, 0x67, 0xE7, 0x17, 0x97, 0x57, 0xD7, 0x37, 0xB7, 0x77, 0xF7,
    0x0F, 0x8F, 0x4F, 0xCF, 0x2F, 0xAF, 0x6F, 0xEF, 0x1F, 0x9F, 0x5F, 0xDF, 0x3F, 0xBF, 0x7F, 0xFF
};

/**
 * @brief PPU 控制寄存器 ($2000) 标志
 * 
 * 只写寄存器,控制各种 PPU 功能:
 * 
 * 位  | 名称      | 描述
 * ----|-----------|------------------------------------------
 * D0  |           | 基础名称表选择 (0=$2000, 1=$2400)
 * D1  |           | 附加名称表选择 (0/$1000/$2000/$3000)
 * D2  | VINC32    | VRAM 地址增量 (0=+1, 1=+32)
 * D3  | SPUSE1000 | 精灵图案表地址 (0=$0000, 1=$1000)
 * D4  | BGUSE1000 | 背景图案表地址 (0=$0000, 1=$1000)
 * D5  | SPH16     | 精灵大小 (0=8x8, 1=8x16)
 * D6  |           | PPU 主/从选择 (Famicom 中未使用)
 * D7  | NMI_EN    | NMI 使能 (1=在 VBlank 时生成 NMI)
 */
enum PPU_CTRL_Flags
{
    VINC32 = 1 << 2,    ///< VRAM 增量模式 (0=+1, 1=+32)
    SPUSE1000 = 1 << 3, ///< 精灵图案表地址选择
    BGUSE1000 = 1 << 4, ///< 背景图案表地址选择
    SPH16 = 1 << 5,     ///< 精灵大小 (0=8x8 点, 1=8x16 点)
    NMI_EN = 1 << 7     ///< VBlank 时 NMI 中断使能
};

/**
 * @brief PPU 状态寄存器 ($2002) 标志
 * 
 * 只读寄存器,提供状态信息:
 * 
 * 位   | 名称       | 描述
 * -----|------------|------------------------------------------
 * D0-D4|            | 未使用 (始终为 0)
 * D5   | PPU_SPOVER | 精灵溢出标志
 * D6   | PPU_SP0HIT | 精灵 0 命中标志
 * D7   | PPU_VBLANK | VBlank 标志
 */
enum PPU_STATUS_Flags
{
    PPU_SPOVER = 1 << 5,  ///< 精灵溢出标志 (扫描线上精灵 >8 时设置)
    PPU_SP0HIT = 1 << 6,  ///< 精灵 0 命中标志 (精灵 0 的非透明像素重叠时设置)
    PPU_VBLANK = 1 << 7   ///< VBlank 标志 (垂直消隐期间设置)
};

/**
 * @brief PPU 掩码寄存器 ($2001) 标志
 * 
 * 控制显示设置和颜色强调:
 * 
 * 位  | 名称 | 描述
 * ----|------|------------------------------------------
 * D0  |      | 灰度模式 (0=全彩, 1=灰度)
 * D1  |      | 在最左侧 8 像素显示背景 (0=隐藏, 1=显示)
 * D2  |      | 在最左侧 8 像素显示精灵 (0=隐藏, 1=显示)
 * D3  | BGEN | 启用背景显示 (0=关闭, 1=开启)
 * D4  | SPEN | 启用精灵显示 (0=关闭, 1=开启)
 * D5-D7|      | 颜色强调 (NTSC 为 RGB, PAL 为 GRB)
 */
enum PPU_MASK_Flags
{
    BGEN = 1 << 3,  ///< 背景启用标志
    SPEN = 1 << 4   ///< 精灵启用标志
};

class Famicom;

/**
 * @class NesPPU
 * @brief Emulates the Ricoh 2C02 Picture Processing Unit
 * 
 * The PPU is the graphics processing unit of the NES, responsible for:
 * - Rendering backgrounds from tile maps (nametables)
 * - Rendering sprites (objects) with priority
 * - Managing scroll registers for smooth scrolling
 * - Generating VBlank and sprite hit interrupts
 * 
 * Key features:
 * - 256x240 pixel resolution
 * - 64 hardware sprites (8x8 or 8x16)
 * - 4 palette banks (4 colors each)
 * - Hardware scrolling
 * - Sprite priority and clipping
 * 
 * @see https://www.nesdev.org/wiki/PPU for detailed documentation
 */
class NesPPU
{
private:
    cpu::NesCPU* mCPU;                    ///< CPU 指针 (用于中断和时钟)
    device::NesCartridge* mCartridge;     ///< 卡带指针 (用于读写 CHR ROM)
    uint8_t mPreviousData = 0;            ///< 上次读取的数据 (用于缓冲)

    /**
     * @brief PPU 控制寄存器 ($2000)
     *
     * 控制各种 PPU 功能,包括:
     * - 基础屏幕的名称表选择
     * - VRAM 地址增量模式
     * - 精灵和背景的图案表地址
     * - 精灵大小配置
     * - VBlank 时生成 NMI
     */
    uint8_t mPPUCtrl = 0;

    /**
     * @brief PPU 掩码寄存器 ($2001)
     *
     * 控制显示设置:
     * - 灰度模式
     * - 左列裁剪
     * - 背景和精灵启用
     * - 颜色强调位
     */
    uint8_t mPPUMask = 0;

    /**
     * @brief PPU 状态寄存器 ($2002)
     *
     * 提供状态信息:
     * - VBlank 标志 (读取时自动清除)
     * - 精灵 0 命中标志
     * - 精灵溢出标志
     */
    uint8_t mPPUStatus = 0;

public:

    /**
     * @brief 构造新的 NesPPU 对象
     * @param cpu CPU 指针 (用于中断和时钟)
     */
    explicit NesPPU(cpu::NesCPU* cpu = nullptr, device::NesCartridge* cartridge = nullptr);
    
    /**
     */
    void setCPU(cpu::NesCPU* cpu) { mCPU = cpu; }
    
    /**
     */
    void setCartridge(device::NesCartridge* cartridge) { mCartridge = cartridge; }

    /**
     * @brief 获取帧缓冲区指针
     * @return 指向 256x240 像素缓冲区的指针 (调色板索引)
     * 
     * 返回的缓冲区包含每个像素的调色板索引 (0-3)。
     * 如需转换为 RGB,请使用调色板。
     */
    uint8_t* getCanvas();

    // 诊断接口 (测试/调试用)
    uint8_t debugCtrl() const { return mPPUCtrl; }
    uint8_t debugMask() const { return mPPUMask; }
    uint16_t debugV() const { return mRegs.v; }
    uint16_t debugT() const { return mRegs.t; }
    uint8_t debugX() const { return mRegs.x; }
    uint8_t debugW() const { return mRegs.w; }
    const uint8_t* debugNametable(int index) const
    {
        return (index >= 0 && index < 4) ? mNametables[index] : nullptr;
    }
    const uint8_t* debugPalette() const { return mPalette; }

    // ========================================================================
    // 帧时序接口 (由 Famicom 帧循环调用, PPU 寄存器不直接暴露)
    // ========================================================================

    /**
     * @brief 背景与精灵渲染是否开启 ($2001 的 BGEN|SPEN 任一置位)
     */
    bool isRenderingEnabled() const { return (mPPUMask & (BGEN | SPEN)) != 0; }

    /**
     * @brief 是否允许 VBlank NMI ($2000 的 NMI_EN 置位)
     */
    bool isNmiEnabled() const { return (mPPUCtrl & NMI_EN) != 0; }

    /**
     * @brief 进入 VBlank: 置位 VBlank 标志 (由 Famicom 在第 240 行调用)
     */
    void beginVBlank() { mPPUStatus |= PPU_VBLANK; }

    /**
     * @brief 结束 VBlank: 清除 VBlank 与精灵 0 命中标志 (由 Famicom 在第 261 行调用)
     */
    void endVBlank()
    {
        mPPUStatus &= ~PPU_VBLANK;
        mPPUStatus &= ~PPU_SP0HIT;
    }

    /**
     * @brief 增加垂直位置 (V 寄存器 Y 分量)
     * 
     * 在扫描线渲染期间调用,前进到下一行。
     * 处理回绕和名称表边界。
     */
    void incVerticalPosition();

    /**
     * @brief 增加水平位置 (V 寄存器 X 分量)
     * 
     * 在像素渲染期间调用,前进到下一列。
     * 处理粗 X 滚动和细 X 滚动。
     */
    void incHorizontalPosition();

    /**
     * @brief 从 T 寄存器复制水平位到 V 寄存器
     * 
     * 在扫描线结束时调用,重置水平滚动
     * 为下一条扫描线做准备。
     */
    void copyHorizontalBits();

    /**
     * @brief 从 T 寄存器复制垂直位到 V 寄存器
     * 
     * 在 VBlank 期间调用,更新垂直滚动
     * 为下一帧做准备。
     */
    void copyVerticalBits();

    /**
     * @brief 渲染图案表到内存 (调试功能)
     * @param PatternMem 用于图案可视化的输出内存缓冲区
     */
    void renderPattern(uint8_t* PatternMem);

    /**
     * @brief 帧前初始化
     * 
     * 在每帧开始时调用,准备 PPU 状态。
     * 重置计数器并为扫描线渲染做准备。
     */
    void preEmulationFrame();

    /**
     * @brief 通过 CPU 读取 PPU 寄存器
     * @param address 寄存器地址 ($2000-$2007)
     * @return 从寄存器读取的数据字节
     * 
     * 实现 PPU 寄存器读取行为:
     * - $2002: 状态寄存器 (清除 VBlank 标志)
     * - $2004: OAM 数据
     * - $2007: VRAM 数据 (缓冲读取)
     */
    uint8_t cpuReadRegister(uint16_t address);

    /**
     * @brief 通过 CPU 写入 PPU 寄存器
     * @param address 寄存器地址 ($2000-$2007)
     * @param data 要写入的数据字节
     * 
     * 实现 PPU 寄存器写入行为:
     * - $2000: 控制寄存器
     * - $2001: 掩码寄存器
     * - $2003: OAM 地址
     * - $2004: OAM 数据
     * - $2005: 滚动寄存器 (双次写入)
     * - $2006: VRAM 地址 (双次写入)
     * - $2007: VRAM 数据
     */
    void cpuWriteRegister(uint16_t address, uint8_t data);

    /**
     * @brief 重置 PPU 到初始状态
     * 
     * 执行软复位:
     * - Clear control and 掩码寄存器s
     * - 重置滚动寄存器
     * - 清除状态标志
     */
    void reset();

    /**
     * @brief 切换镜像模式
     * @param mode 新的镜像模式
     * 
     * 改变名称表映射到 VRAM 的方式:
     * - 垂直镜像
     * - 水平镜像
     * - 单屏镜像
     * - 四屏镜像
     */
    void switchMirroring(device::Mirror mode);

    /**
     * @brief 保存 PPU 状态到存档包
     * @param bundle 存档包目标
     */
    void save(io::SaveBundle* bundle);

    /**
     * @brief 从存档包恢复 PPU 状态
     * @param bundle 存档包来源
     */
    void restore(io::SaveBundle* bundle);

    /**
     * @brief 渲染单条扫描线
     * @param line 扫描线编号 (0-261)
     * 
     * 每条扫描线调用的主渲染函数:
     * 1. 评估当前扫描线的精灵
     * 2. 获取背景瓦片数据
     * 3. 合成精灵和背景
     * 4. 处理精灵 0 命中检测
     * 5. 更新移位寄存器
     */
    void renderScanline(const uint16_t line);

private:
    /**
     * @struct Scanline
     * @brief 扫描线精灵评估结果
     */
    typedef struct {
        uint8_t spCount;          ///< 此扫描线上的精灵数量
        uint8_t spirits[8];       ///< 精灵索引 (每条扫描线最多 8 个)
    } Scanline;

    /**
     * @struct spirit_t
     * @brief 精灵属性结构
     * 
     * 表示一个硬件精灵,包含:
     * - 位置 (X, Y)
     * - 图案索引
     * - 属性 (优先级,翻转,调色板)
     */
    typedef struct spirit_t {
        uint8_t id = 0;           ///< OAM 中的精灵索引
        uint8_t x = 0;            ///< X 坐标
        uint16_t pattern = 0;     ///< 图案表地址 (计算得出)
        uint8_t info = 0;         ///< 属性字节 (优先级,翻转,调色板)
    } Spirit;

    /**
     * @struct REG_T
     * @brief PPU 内部寄存器
     * 
     * V 寄存器布局 (15 位):
     * @code
     * yyy NN YYYYY XXXXX
     * ||| || ||||| +++++-- coarse X scroll
     * ||| || +++++-------- coarse Y scroll
     * ||| ++-------------- nametable select
     * +++----------------- fine Y scroll
     * @endcode
     */
    typedef struct REG_T {
        uint16_t v = 0;    ///< 当前 VRAM 地址寄存器
        uint16_t t = 0;    ///< 临时 VRAM 地址寄存器
        uint8_t w = 0;     ///< 第一次/第二次写入切换
        uint8_t x = 0;     ///< 精细 X 滚动 (3 位)
    } REG;

    REG mRegs;                          ///< 内部 PPU 寄存器

    uint8_t mReadBuffer = 0;            ///< 读取缓冲区 (一字节延迟)
    uint8_t mScreenMemory[256 * 240];   ///< 帧缓冲区 (256x240 像素)
    uint8_t mOAM[256] = { 0 };          ///< 对象属性内存 (64 精灵 * 4 字节)
    uint8_t mSpHeight = 8;              ///< 精灵高度 (8 或 16)
    uint8_t mOamaddr;                   ///< OAM 地址 register
    Spirit mSpirits[64];                ///< 解码的精灵数据
    uint8_t mScanlineSpiritsCount = 0;  ///< 当前扫描线上的精灵
    uint16_t mPatternTables = 0;        ///< 背景图案表地址
    uint16_t mSpPatternTables;          ///< 精灵图案表地址
    uint8_t mBGMask[33 + 1] = { 0 };    ///< 每条扫描线的背景透明蒙版
    uint8_t mVRAM[2][1024] = { {0} };   ///< 板载 VRAM (2KB)
    uint8_t* mNametables[4] = { nullptr }; ///< 指向 4 个名称表的指针
    uint8_t mPalette[32] = { 0 };       ///< 调色板 RAM (32 项)

    /**
     * @brief 评估当前扫描线的精灵
     * @param line 扫描线编号
     * 
     * 扫描 OAM 以找到此扫描线上的可见精灵。
     * 如果适用,设置精灵 0 命中标志。
     */
    void scanSpirits(uint16_t line);

    /**
     * @brief 绘制当前扫描线上的精灵
     * @param line 扫描线编号
     * 
     * 将评估的精灵渲染到帧缓冲区。
     * 处理优先级和透明度。
     */
    void drawSpirits(uint16_t line);

    /**
     * @brief 绘制精灵像素单元
     * @param p1 图案位平面 1
     * @param p2 图案位平面 2
     * @param x 像素 X 坐标
     * @param y 像素 Y 坐标
     * @param attr 精灵属性字节
     * 
     * 从精灵图案渲染单个 1x8 像素条。
     */
    void drawSpiritsPixelUnit(
        uint8_t p1, uint8_t p2,
        uint8_t x, uint8_t y, uint8_t attr
    );

    /**
     * @brief 绘制背景像素单元
     * @param p1 图案位平面 1
     * @param p2 图案位平面 2
     * @param attrl2bit 属性 (调色板选择)
     * @param pCanvas 帧缓冲区指针
     * @param x 像素 X 坐标
     * 
     * 从背景图案渲染单个 1x8 像素条。
     */
    void drawPixelUnit(uint8_t p1, uint8_t p2, uint8_t attrl2bit, uint8_t* pCanvas, int x);

    /**
     * @brief 写字节到 PPU 地址空间 ($0000-$3FFF)
     * @param address PPU 地址
     * @param data 数据字节
     */
    void ppuBusWrite(uint16_t address, uint8_t data);

    /**
     * @brief 从 PPU 地址空间读取字节 ($0000-$3FFF)
     * @param address PPU 地址
     * @return 数据字节
     */
    uint8_t ppuBusRead(uint16_t address);

    /**
     * @brief $2007 数据端口读取 (带缓冲语义)
     * @param address PPU 地址
     * @return 缓冲的数据字节
     */
    uint8_t readDataPort(uint16_t address);
};

} // namespace ppu
} // namespace nes
