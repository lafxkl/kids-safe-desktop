#pragma once
#include <string>
#include <sstream>
#include <iostream>
#include <cstdint>

#ifdef _WIN32
#include <windows.h>
#endif

namespace nes {

/**
 * @brief 日志级别
 */
enum class LogLevel : uint8_t {
    Trace = 0,    ///< 追踪级别 - 最详细的信息
    Debug = 1,    ///< 调试级别 - 调试信息
    Info = 2,     ///< 信息级别 - 一般信息
    Warn = 3,     ///< 警告级别 - 警告信息
    Error = 4,    ///< 错误级别 - 错误信息
    Critical = 5, ///< 严重级别 - 严重错误
    Off = 6       ///< 关闭日志
};

/**
 * @brief 日志输出目标
 */
enum class LogTarget : uint8_t {
    Console = 0,   ///< 控制台输出
    DebugOutput = 1, ///< Windows Debug Output (仅 Windows)
    File = 2,      ///< 文件输出 (暂未实现)
    None = 3       ///< 无输出
};

/**
 * @brief 轻量级日志系统
 * 
 * 设计原则:
 * - 零外部依赖,仅使用 C++ 标准库
 * - 跨平台支持 (Windows/Linux/macOS/Android)
 * - 编译期优化,Release 模式可完全禁用
 * - 简单的流式 API
 * 
 * 使用示例:
 * @code
 * Logger::init(LogLevel::Debug);
 * LOG_INFO("模拟器启动");
 * LOG_DEBUG("Mapper 编号: " << mapperId);
 * LOG_ERROR("ROM 加载失败: " << filename);
 * @endcode
 */
class Logger {
  private:
    static LogLevel s_minLevel;
    static LogTarget s_target;
    
  public:
    /**
     * @brief 初始化日志系统
     * @param level 最小日志级别
     * @param target 输出目标
     */
    static void init(LogLevel level = LogLevel::Info, LogTarget target = LogTarget::Console);
    
    /**
     * @brief 设置日志级别
     */
    static void setLevel(LogLevel level) { s_minLevel = level; }
    
    /**
     * @brief 获取当前日志级别
     */
    static LogLevel getLevel() { return s_minLevel; }
    
    /**
     * @brief 设置输出目标
     */
    static void setTarget(LogTarget target) { s_target = target; }
    
    /**
     * @brief 检查是否可以输出指定级别的日志
     */
    static bool shouldLog(LogLevel level) { return level >= s_minLevel; }
    
    /**
     * @brief 内部日志输出方法
     */
    static void log(LogLevel level, const std::string& message);
    
    /**
     * @brief 将日志级别转换为字符串
     */
    static const char* levelToString(LogLevel level);
};

/**
 * @brief 日志流类 - 支持流式语法
 */
class LogStream {
  private:
    LogLevel m_level;
    std::ostringstream m_stream;
    
  public:
    explicit LogStream(LogLevel level) : m_level(level) {}
    
    ~LogStream() {
        if (Logger::shouldLog(m_level)) {
            Logger::log(m_level, m_stream.str());
        }
    }
    
    template<typename T>
    LogStream& operator<<(const T& value) {
        if (Logger::shouldLog(m_level)) {
            m_stream << value;
        }
        return *this;
    }
};

} // namespace nes

// 便捷的日志宏
#define LOG_TRACE ::nes::LogStream(::nes::LogLevel::Trace)
#define LOG_DEBUG ::nes::LogStream(::nes::LogLevel::Debug)
#define LOG_INFO  ::nes::LogStream(::nes::LogLevel::Info)
#define LOG_WARN  ::nes::LogStream(::nes::LogLevel::Warn)
#define LOG_ERROR ::nes::LogStream(::nes::LogLevel::Error)
#define LOG_CRIT  ::nes::LogStream(::nes::LogLevel::Critical)

// Release 模式下禁用调试和追踪日志 (可选)
#ifndef NDEBUG
#define LOG_D TRACE LOG_TRACE
#define LOG_DBUG LOG_DEBUG
#else
#define LOG_DTRACE (void)0
#define LOG_DBUG (void)0
#endif
