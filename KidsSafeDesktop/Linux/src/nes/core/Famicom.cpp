#include "Famicom.h"
#include "common.h"
#include <fstream>
#include <cstring>
#include <cstdlib>
#include "Devices.h"
#include "Rom.h"
#include "SaveBundle.h"
#include <functional>
namespace nes {

Famicom::Famicom() : mDevices(new nes::Devices())
{
    Logger::init(LogLevel::Debug, LogTarget::Console);
}

Famicom::~Famicom()
{
    delete mDevices;
}

void Famicom::userInput(uint8_t index, uint8_t state) const
{
    mDevices->pad()->userInput(index, state);
}

uint8_t* Famicom::getCanvas() const
{
    return mDevices->ppu()->getCanvas();
}

int16_t* Famicom::getSamples(uint32_t* size) const
{
    return mDevices->apu()->getSamples(size);
}

#define MASTER_CYCLE_PER_SCANLINE 1364 // 每行的时钟周期数
#define MASTER_CYCLE_PER_CPU 12        // CPU 每帧执行的时钟周期数(约 12 个时钟周期)
#define MASTER_CYCLE_PER_PPU 4         // PPU 每帧执行的时钟周期数(约 4 个时钟周期)

inline void Famicom::execCPU(const int masterCycle)
{
    mMasterClockCycle += masterCycle;
    auto cpu = mDevices->cpu();
    uint32_t cpuCyclethisRound = mMasterClockCycle / MASTER_CYCLE_PER_CPU - cpu->getCycles();
    const int32_t executed = cpu->exec(cpuCyclethisRound);
    // DMC 定时器与帧计数器由 CPU 时钟直接驱动 (真机行为)。
    // 必须用实际执行的周期 (exec 返回值): cpuCyclethisRound 是"补差"目标值,
    // 全帧总和趋近于 0, 不能作为 APU 时钟源。
    mDevices->apu()->clockAPU(static_cast<uint32_t>(executed));
}

void Famicom::emulationFrame()
{
    if (!mDevices->cartridge()) {
        LOG_ERROR << "No cartridge pointer!";
        return;
    }

    if (!mDevices->cartridge()->isLoaded()) {
        LOG_WARN << "Cartridge not loaded!";
        return;
    }

    mDevices->ppu()->preEmulationFrame();
    for (int line = 0; line < 262; line++) {
        auto ppu = mDevices->ppu();
        if (line >= 0 && line < 240) // 0~239 是可见区域
        {
            ppu->renderScanline(line);
            execCPU(MASTER_CYCLE_PER_PPU * 256);
            if (ppu->isRenderingEnabled()) {
                ppu->incVerticalPosition();
                ppu->copyHorizontalBits();
            }
            execCPU(MASTER_CYCLE_PER_PPU * 10);
            mDevices->cartridge()->onScanline(line);
            execCPU(MASTER_CYCLE_PER_PPU * 75);
        } else if (line == 240) // 空白扫描线
        {
            execCPU(MASTER_CYCLE_PER_PPU * 256);
            mDevices->cartridge()->onScanline(line);
            execCPU(MASTER_CYCLE_PER_PPU * 85);

            ppu->beginVBlank(); // 空白扫描线之后打开VBLANK
            if (ppu->isNmiEnabled())
                mDevices->cpu()->tryNmi(); // 生成 NMI 中断
        } else if (line == 261) {          // 261 是垂直同步结束线
            // 垂直同步结束
            ppu->endVBlank();

            execCPU(MASTER_CYCLE_PER_PPU * 246);
            if (ppu->isRenderingEnabled()) {
                // 这里必须先让CPU执行一段时间
                // 否则机械战警和赤色要塞会出现画面上下偏移
                ppu->copyVerticalBits();
            }
            // 这里也必须先执行一段时间，否则魂斗罗力量会出现问题
            execCPU(MASTER_CYCLE_PER_PPU * 10);
            mDevices->cartridge()->onScanline(line);
            execCPU(MASTER_CYCLE_PER_PPU * 85);
        } else {
            // 20 行垂直同步线
            execCPU(MASTER_CYCLE_PER_PPU * 256);
            mDevices->cartridge()->onScanline(line);
            execCPU(MASTER_CYCLE_PER_PPU * 85);
        }
        // 帧计数器已由 clockAPU 按 CPU 周期驱动 (见 execCPU), 此处不再按行触发
        // 每 87 行生成一次音频样本，共 262 行，即 262 个样本
        if (line % 87 == 86) {
            auto cpu = mDevices->cpu();
            mDevices->apu()->makeSamples(cpu->getCycles() - mLastCpuCycleCount);
            mLastCpuCycleCount = cpu->getCycles();
        }
    }

    // 帧末把 line 261 的剩余 CPU 周期也交给 APU: 否则每帧约 340 周期滞留到下帧,
    // 音频生产速率跟不上 SDL 44100Hz 的消费速率 (GUI 锁 60fps 时每秒亏 ~90 样本,
    // 缓冲持续排空导致断流饥饿 — 脉冲"偏细"、DMC 人声缺失)
    {
        auto cpu = mDevices->cpu();
        if (cpu->getCycles() > mLastCpuCycleCount) {
            mDevices->apu()->makeSamples(
                static_cast<uint32_t>(cpu->getCycles() - mLastCpuCycleCount));
        }
    }

    mMasterClockCycle = 0;

    auto cpu = mDevices->cpu();
    mLastCpuCycleCount = 0;
    cpu->addCycles(-static_cast<int64_t>(cpu->getCycles()));
}

void Famicom::renderPattern(uint8_t* mem) const
{
    return mDevices->ppu()->renderPattern(mem);
}

std::vector<uint8_t> Famicom::save() const
{
    if (!mDevices->cartridge() || !mDevices->cartridge()->isLoaded()) {
        LOG_WARN << "No cartridge loaded!";
        return {};
    }

    // 创建临时 SaveBundle
    nes::io::SaveBundle bundle;
    bundle.beginSave();

    // 序列化所有组件状态
    mDevices->cpu()->save(&bundle);
    mDevices->ppu()->save(&bundle);
    mDevices->bus()->save(&bundle);
    mDevices->pad()->save(&bundle);
    mDevices->cartridge()->save(&bundle);
    mDevices->apu()->save(&bundle);

    // 结束保存并返回数据副本
    bundle.endOperation();

    size_t size = 0;
    const uint8_t* data = bundle.getData(&size);
    return std::vector<uint8_t>(data, data + size);
}

bool Famicom::restore(const std::vector<uint8_t>& data) const
{
    if (!mDevices->cartridge() || !mDevices->cartridge()->isLoaded()) {
        LOG_WARN << "No cartridge loaded!";
        return false;
    }

    if (data.empty()) {
        LOG_WARN << "Invalid restore data";
        return false;
    }

    // 创建临时 SaveBundle 并加载数据
    nes::io::SaveBundle bundle;
    if (!bundle.beginRestore(data)) {
        LOG_WARN << "Failed to begin restore operation";
        return false;
    }

    // 反序列化所有组件状态
    mDevices->cpu()->restore(&bundle);
    mDevices->ppu()->restore(&bundle);
    mDevices->bus()->restore(&bundle);
    mDevices->pad()->restore(&bundle);
    mDevices->cartridge()->restore(&bundle);
    mDevices->apu()->restore(&bundle);

    // 结束恢复操作
    bundle.endOperation();

    return true;
}

void Famicom::reset() const
{
    // 顺序要求: PPU 先按 ROM 头初始化镜像, 再复位卡带/mapper —
    // mapper 的 OnReset 会按板卡类型覆盖镜像 (如 AxROM 单屏、VRC 系列),
    // 若顺序反过来, PPU::reset 会把 mapper 设置的镜像冲掉。
    auto ppu = mDevices->ppu();
    if (ppu) {
        ppu->reset();
    } else {
        LOG_ERROR << "PPU is null!";
    }

    auto cartridge = mDevices->cartridge();
    if (cartridge) {
        cartridge->reset();
    } else {
        LOG_ERROR << "Cartridge is null!";
    }

    auto cpu = mDevices->cpu();
    if (cpu) {
        cpu->reset();
    } else {
        LOG_ERROR << "CPU is null!";
    }

    auto apu = mDevices->apu();
    if (apu) {
        apu->reset();
    } else {
        LOG_ERROR << "APU is null!";
    }
}


/// <summary>
/// 坏 iNES 头镜像修正 (业界通行做法, 参考 Mesen/Nestopia 的 ROM 数据库):
/// 以 PRG+CHR 数据的 CRC32 为键查 games/mirror_overrides.txt, 每行 "<CRC32 hex>=<H|V>"。
/// CRC32 比文件名可靠 (改名/重打包不影响, 同一 dump 恒定)。
/// 只对镜像硬连线板卡 (NROM/UxROM/CNROM/GxROM) 生效; mapper 运行时自控镜像的
/// (MMC1/MMC3/AxROM 等) 一律拒绝, 防止把正常游戏钉死在错误镜像上。
/// </summary>
void Famicom::applyMirrorOverride(nes::device::NesCartridge* cartridge) const
{
    auto* rom = cartridge->getRom();
    if (!rom || !rom->mapper || !rom->mapper->hasHardwiredMirroring())
        return; // 非硬镜像板卡: 拒绝覆盖 (安全默认)

    // CRC32: 对 PRG+CHR 数据串接计算 (与常见 ROM 工具一致)
    auto crc32 = [](const uint8_t* data, size_t size) {
        uint32_t crc = 0xFFFFFFFF;
        for (size_t i = 0; i < size; i++) {
            crc ^= data[i];
            for (int k = 0; k < 8; k++)
                crc = (crc >> 1) ^ (0xEDB88320 & (-(crc & 1)));
        }
        return ~crc;
    };
    uint32_t crc = crc32(rom->prgrom, rom->prgRomSize);
    if (rom->chrRomSize > 0) {
        // 串接续算: 从 prg 的最终 crc 状态 (取反前) 继续
        uint32_t c = crc ^ 0xFFFFFFFF;
        const uint8_t* chr = rom->chrrom;
        for (uint32_t i = 0; i < rom->chrRomSize; i++) {
            c ^= chr[i];
            for (int k = 0; k < 8; k++)
                c = (c >> 1) ^ (0xEDB88320 & (-(c & 1)));
        }
        crc = ~c;
    }

    // 前端工作目录不定 (GUI 从 output/bin 启动, headless 从仓库根): 逐级向上探测
    std::ifstream overrideFile;
    for (const char* dir : {"games", "../games", "../../games", "../../../games"}) {
        std::string p = std::string(dir) + "/mirror_overrides.txt";
        overrideFile.open(p, std::ios::binary);
        if (overrideFile)
            break;
    }
    if (!overrideFile) {
        LOG_WARN << "mirror_overrides.txt 未找到 (镜像覆盖不可用)";
        return;
    }

    std::string line;
    while (std::getline(overrideFile, line)) {
        // 格式: <8位hex CRC>=<H|V>; '#' 之后为注释
        size_t hash = line.find('#');
        if (hash != std::string::npos)
            line.erase(hash);
        size_t eq = line.find('=');
        if (eq == std::string::npos)
            continue;
        const uint32_t entryCrc =
            static_cast<uint32_t>(strtoul(line.substr(0, eq).c_str(), nullptr, 16));
        if (entryCrc != crc)
            continue;
        const char mode = line[eq + 1];
        nes::device::Mirror m = (mode == 'V' || mode == 'v')
                                    ? nes::device::Mirror::VERTICAL
                                    : nes::device::Mirror::HORIZONTAL;
        mDevices->ppu()->switchMirroring(m);
        LOG_WARN << "镜像方式按 CRC 覆盖表强制为 " << static_cast<char>(mode);
        break;
    }
}

void Famicom::insertCartridge(nes::device::NesCartridge* cartridge)
{
    mDevices->setCartridge(cartridge);
    mDevices->bus()->addCartridge(cartridge);
    mDevices->ppu()->setCartridge(cartridge);

    if (cartridge) {
        cartridge->bindHost(mDevices);
        LOG_INFO << "Inserting cartridge: Mapper " << static_cast<int>(cartridge->mapperNumber());
    }

    // 执行一次复位
    reset();

    if (cartridge) {
        applyMirrorOverride(cartridge);
    }
}

void Famicom::ejectCartridge()
{
    if (mDevices->cartridge()) {
        LOG_INFO << "Ejecting cartridge...";
        mDevices->bus()->addCartridge(nullptr);
        mDevices->ppu()->setCartridge(nullptr);
        mDevices->setCartridge(nullptr);
    }
}

bool Famicom::hasCartridge() const
{
    auto cart = mDevices->cartridge();
    return cart != nullptr && cart->isLoaded();
}

nes::device::NesCartridge* Famicom::getCartridge() const
{
    return mDevices->cartridge();
}

} // namespace nes
