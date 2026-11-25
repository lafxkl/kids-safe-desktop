#pragma once
#include <cstdint>
#include <functional>

namespace nes {

/**
 * @brief Modern event callback system for Famicom emulator
 *
 * Uses std::function for flexible callback registration,
 * replacing the traditional virtual interface approach.
 *
 * Benefits:
 * - No inheritance required
 * - Lambda support
 * - Multiple callbacks per event
 * - Better testability with mocks
 */
struct FamicomEvents
{
    /// Audio output event
    std::function<void(int16_t* buf, int size)> onAudioWrite;

    /**
     * @brief Check if audio callback is registered
     * @return true if callback is set
     */
    bool hasAudioCallback() const
    {
        return static_cast<bool>(onAudioWrite);
    }
};

} // namespace nes
