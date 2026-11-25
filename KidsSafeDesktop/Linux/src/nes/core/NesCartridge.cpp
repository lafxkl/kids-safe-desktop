#include "NesCartridge.h"
#include "common.h"
#include "mapper/Mapper.h"
#include "mapper/MapperFactory.h"
#include <iostream>
#include <fstream>
#include <vector>

namespace nes {
namespace device {

NesCartridge::NesCartridge()
{
}

NesCartridge::~NesCartridge()
{
    if (mRom) {
        releaseFcRom(mRom);
        mRom = nullptr;
    }
}

// === 静态工厂方法 ===
// 注意: 返回原始指针,调用者必须负责 delete
// 推荐使用 Famicom::insertCartridge() 让 Famicom 管理生命周期

NesCartridge* NesCartridge::loadFromFile(const std::string& path) {
    // 读取文件
    std::ifstream file(path, std::ios::binary | std::ios::ate);
    if (!file.is_open()) {
        LOG_ERROR << "无法打开 ROM 文件: " << path;
        return nullptr;
    }
    
    auto fileSize = file.tellg();
    file.seekg(0, std::ios::beg);
    
    std::vector<uint8_t> buffer(fileSize);
    if (!file.read(reinterpret_cast<char*>(buffer.data()), fileSize)) {
        LOG_ERROR << "读取 ROM 文件失败: " << path;
        return nullptr;
    }
    
    NesCartridge* cart = loadFromMemory(buffer.data(), buffer.size());
    if (cart && cart->getRom()) {
        cart->getRom()->romPath = path;
    }
    return cart;
}

NesCartridge* NesCartridge::loadFromMemory(const void* data, size_t size) {
    auto* cart = new NesCartridge();
    
    FcRom* rom = nullptr;
    // 注意: 此时还没有 host,先传入 nullptr,后续由 bindHost 设置
    if (cart->createFromData(&rom, const_cast<void*>(data), size)) {
        cart->mRom = rom;
        cart->mLoadedrom = true;
        return cart;
    }
    
    // 创建失败,清理内存
    delete cart;
    return nullptr;
}

void NesCartridge::bindHost(device::MapperHost* host) {
    // 如果已经有 ROM 且 Mapper 已创建,更新 Mapper 的主机能力
    if (mRom && mRom->mapper) {
        mRom->mapper->setHost(host);
    }
}

void NesCartridge::reset()
{
    if (this->mRom == nullptr)
        return;
    this->mRom->mapper->reset();
}

FcRom* NesCartridge::getRom()
{
    return mRom;
}

bool NesCartridge::isLoadedRom()
{
    return mLoadedrom;
}

uint8_t NesCartridge::prgNum()
{
    return mRom->prg16kbCount;
}

/// <summary>
/// 释放 ROM 资源
/// </summary>
void NesCartridge::releaseFcRom(FcRom* rom)
{
    if (rom->prgrom != nullptr)
        delete[] rom->prgrom;
    if (rom->chrrom != nullptr)
        delete[] rom->chrrom;
    if (rom->mapper != nullptr)
        delete rom->mapper;
    delete rom;
}

/// <summary>
/// 加载 ROM
/// </summary>
/// <param name="rom"></param>
/// <returns></returns>
int NesCartridge::loadRom(FcRom* rom)
{
    this->mRom = rom;
    this->mLoadedrom = (rom != nullptr);
    return this->mLoadedrom;
}

void NesCartridge::save(nes::io::SaveBundle* bundle)
{
    if (this->mRom != nullptr && this->mRom->mapper != nullptr)
        this->mRom->mapper->save(bundle);
}

void NesCartridge::restore(nes::io::SaveBundle* bundle)
{
    if (this->mRom != nullptr && this->mRom->mapper != nullptr)
        this->mRom->mapper->restore(bundle);
}

/// <summary>
/// 验证文件头并创建 ROM
/// </summary>
/// <param name="rom">输出 ROM 指针</param>
/// <param name="data">ROM 数据</param>
/// <param name="size">数据大小</param>
/// <returns>是否成功</returns>
bool NesCartridge::createFromData(FcRom** rom, void* data, size_t size)
{
    if (size < 16)
        return false;

    NesHeader* romhead = static_cast<NesHeader*>(data);

    uint8_t* nesdata = static_cast<uint8_t*>(data) + 16;

    romhead->nes[3] = '\0';
    if (strcmp((char const*)romhead->nes, "NES") != 0) {
        return false;
    }

    FcRom* fcrom = new FcRom;

    fcrom->prg16kbCount = romhead->PRGBankCount;
    fcrom->chr8kbCount = romhead->CHRBankCount;

    fcrom->isVmirroring = (romhead->flag1) & 0x1;
    fcrom->hasBatteryBacked = (romhead->flag1 >> 1) & 0x1;
    fcrom->hasTrainer = (romhead->flag1 >> 2) & 0x1;
    fcrom->isFourScreen = (romhead->flag1 >> 3) & 0x1;

    fcrom->version = (romhead->flag2 >> 2) & 0x3;

    uint8_t map = romhead->flag1 >> 4;
    map |= (romhead->flag2 & 0xf0);

    fcrom->mapperNumber = map;

    fcrom->prgRomSize = fcrom->prg16kbCount * 16 * 1024;
    fcrom->chrRomSize = fcrom->chr8kbCount * 8 * 1024;

    fcrom->size = fcrom->prgRomSize + fcrom->chrRomSize;
    if (size - 16 < fcrom->size)
        goto failed;

    fcrom->isChrRAM = fcrom->chr8kbCount == 0;

    fcrom->prgrom = new uint8_t[fcrom->prgRomSize];

    memcpy_s(fcrom->prgrom, fcrom->prgRomSize, nesdata, fcrom->prgRomSize);

    if (!fcrom->isChrRAM) {
        fcrom->chrrom = new uint8_t[fcrom->chrRomSize];
        memcpy(fcrom->chrrom, nesdata + fcrom->prgRomSize, fcrom->chrRomSize);
    } else {
        fcrom->chrrom = new uint8_t[8192];
        fcrom->chrRomSize = 8192;
        fcrom->chr8kbCount = 1;
    }

    LOG_INFO << "Mapper:" << static_cast<int>(fcrom->mapperNumber) 
               << " prg16kbCount:" << static_cast<int>(fcrom->prg16kbCount)
               << " chr8kbCount:" << static_cast<int>(fcrom->chr8kbCount)
               << " version:" << static_cast<int>(fcrom->version)
               << " hasBatteryBacked:" << fcrom->hasBatteryBacked
               << " hasTrainer:" << fcrom->hasTrainer;

    fcrom->mapper = MapperFactory::Factory(fcrom);
    if (fcrom->mapper == nullptr) { 
        goto failed;
    }
    
    *rom = fcrom;
    return true;
    
failed:
    LOG_ERROR << "ROM 创建失败";
    this->releaseFcRom(fcrom);
    return false;
}

} // namespace device
} // namespace nes
