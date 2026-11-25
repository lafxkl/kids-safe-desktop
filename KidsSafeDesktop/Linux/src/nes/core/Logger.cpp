#include "Logger.h"
#include <ctime>
#include <iomanip>

#ifdef __ANDROID__
#include <android/log.h>
#endif

namespace nes {

// 静态成员初始化
LogLevel Logger::s_minLevel = LogLevel::Info;
LogTarget Logger::s_target = LogTarget::Console;

void Logger::init(LogLevel level, LogTarget target)
{
    s_minLevel = level;
    s_target = target;
}

const char* Logger::levelToString(LogLevel level)
{
    switch (level) {
    case LogLevel::Trace:
        return "TRACE";
    case LogLevel::Debug:
        return "DEBUG";
    case LogLevel::Info:
        return "INFO";
    case LogLevel::Warn:
        return "WARN";
    case LogLevel::Error:
        return "ERROR";
    case LogLevel::Critical:
        return "CRITICAL";
    default:
        return "UNKNOWN";
    }
}

void Logger::log(LogLevel level, const std::string& message)
{
    if (level < s_minLevel) {
        return;
    }
    
    // 获取当前时间
    auto now = std::time(nullptr);
    auto tm = *std::localtime(&now);
    
    // 格式化时间戳 [HH:MM:SS]
    std::ostringstream timestamp;
    timestamp << std::put_time(&tm, "[%H:%M:%S]");
    
    // 构建完整的日志消息
    std::ostringstream fullMessage;
    fullMessage << timestamp.str() << " [" << levelToString(level) << "] " << message << "\n";

    const std::string output = fullMessage.str();

    // 根据目标输出
    switch (s_target) {
    case LogTarget::Console: {
        // 控制台输出 (跨平台)
        std::cerr << output << std::flush;
        break;
    }

    case LogTarget::DebugOutput: {
#ifdef _WIN32
        // Windows Debug Output
        OutputDebugStringA(output.c_str());
#elif defined(__ANDROID__)
        // Android logcat
        android_LogPriority priority = ANDROID_LOG_INFO;
        switch (level) {
        case LogLevel::Trace:
            priority = ANDROID_LOG_VERBOSE;
            break;
        case LogLevel::Debug:
            priority = ANDROID_LOG_DEBUG;
            break;
        case LogLevel::Info:
            priority = ANDROID_LOG_INFO;
            break;
        case LogLevel::Warn:
            priority = ANDROID_LOG_WARN;
            break;
        case LogLevel::Error:
            priority = ANDROID_LOG_ERROR;
            break;
        case LogLevel::Critical:
            priority = ANDROID_LOG_FATAL;
            break;
        default:
            priority = ANDROID_LOG_INFO;
            break;
        }
        __android_log_print(priority, "SimpleNes", "%s", message.c_str());
#else
        // 其他平台降级为控制台输出
        std::cerr << output;
#endif
        break;
    }

    case LogTarget::File: {
        // TODO: 实现文件输出
        std::cerr << output;
        break;
    }

    case LogTarget::None:
    default:
        break;
    }
}

} // namespace nes
