#include "SaveBundle.h"
#include <cassert>
#include <cstring>

namespace nes {
namespace io {

SaveBundle::SaveBundle()
{
    mBuffer.reserve(65536); // 预分配 64KB
}

SaveBundle::~SaveBundle()
{
}

void SaveBundle::reset()
{
    mBuffer.clear();
    mPosition = 0;
    mIsSaving = false;
}

void SaveBundle::resetRestore()
{
    mPosition = 0;
}

void SaveBundle::resetSave()
{
    mBuffer.clear();
    mPosition = 0;
}

bool SaveBundle::beginSave()
{
    resetSave();
    mIsSaving = true;
    return true;
}

bool SaveBundle::beginRestore(const std::vector<uint8_t>& data)
{
    if (data.empty()) {
        return false;
    }
    
    mBuffer = data;  // 直接复制 vector
    mPosition = 0;
    mIsSaving = false;
    return true;
}

void SaveBundle::endOperation()
{
    mIsSaving = false;
}

const uint8_t* SaveBundle::getData(size_t* outSize) const
{
    if (outSize) {
        *outSize = mBuffer.size();
    }
    return mBuffer.data();
}

void SaveBundle::saveByte(uint8_t data)
{
    assert(mIsSaving && "Must call beginSave() before saving");
    mBuffer.push_back(data);
}

void SaveBundle::saveWord(uint16_t data)
{
    saveByte(static_cast<uint8_t>(data & 0xFF));
    saveByte(static_cast<uint8_t>((data >> 8) & 0xFF));
}

void SaveBundle::savePtr(uint32_t data)
{
    saveByte(static_cast<uint8_t>(data & 0xFF));
    saveByte(static_cast<uint8_t>((data >> 8) & 0xFF));
    saveByte(static_cast<uint8_t>((data >> 16) & 0xFF));
    saveByte(static_cast<uint8_t>((data >> 24) & 0xFF));
}

void SaveBundle::saveBlock(const std::vector<uint8_t>& data)
{
    assert(mIsSaving && "Must call beginSave() before saving");
    
    mBuffer.insert(mBuffer.end(), data.begin(), data.end());
}

void SaveBundle::saveBlock(const uint8_t* data, size_t size)
{
    assert(mIsSaving && "Must call beginSave() before saving");
    assert(data != nullptr && "Data pointer cannot be null");
    
    mBuffer.insert(mBuffer.end(), data, data + size);
}

uint8_t SaveBundle::restoreByte()
{
    assert(!mIsSaving && "Must call beginRestore() before restoring");
    assert(mPosition < mBuffer.size() && "Restore position out of bounds");
    
    return mBuffer[mPosition++];
}

uint16_t SaveBundle::restoreWord()
{
    return static_cast<uint16_t>(restoreByte()) | 
           (static_cast<uint16_t>(restoreByte()) << 8);
}

uint32_t SaveBundle::restorePtr()
{
    return static_cast<uint32_t>(restoreByte()) | 
           (static_cast<uint32_t>(restoreByte()) << 8) |
           (static_cast<uint32_t>(restoreByte()) << 16) |
           (static_cast<uint32_t>(restoreByte()) << 24);
}

void SaveBundle::restoreBlock(uint8_t* dst, size_t size)
{
    assert(!mIsSaving && "Must call beginRestore() before restoring");
    assert(dst != nullptr && "Destination pointer cannot be null");
    assert(mPosition + size <= mBuffer.size() && "Restore would exceed buffer bounds");
    
    std::memcpy(dst, mBuffer.data() + mPosition, size);
    mPosition += size;
}

} // namespace io
} // namespace nes
