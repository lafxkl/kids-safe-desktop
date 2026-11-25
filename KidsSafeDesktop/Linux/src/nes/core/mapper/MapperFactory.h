#pragma once
#include "Mapper.h"
#include "../Rom.h"
#include <functional>
#include <map>

namespace nes {
namespace device {

/**
 * @brief 注册式 Mapper 工厂
 *
 * 新增 mapper 只需: 1) 实现 Mapper 子类  2) 在其 .cpp 中调用 REGISTER_MAPPER(N, ClassName)。
 * 工厂通过注册表按 mapper 号查找创建函数, 无需修改工厂本身。
 */
class MapperFactory
{
  public:
    using Creator = std::function<Mapper*(FcRom*)>;

    static std::map<int, Creator>& registry()
    {
        static std::map<int, Creator> s_registry;
        return s_registry;
    }

    static void registerMapper(int number, Creator creator)
    {
        registry()[number] = std::move(creator);
    }

    static Mapper* Factory(FcRom* rom)
    {
        auto& reg = registry();
        auto it = reg.find(rom->mapperNumber);
        if (it == reg.end()) {
            return nullptr;
        }
        return it->second(rom);
    }
};

} // namespace device
} // namespace nes

/**
 * @brief Mapper 静态自注册宏 (放在 mapper 实现文件的全局作用域)
 */
#define REGISTER_MAPPER(NUMBER, CLASS)                                                          \
    static bool s_reg_##CLASS = [] {                                                            \
        nes::device::MapperFactory::registerMapper(                                             \
            (NUMBER), [](FcRom* rom) -> Mapper* { return new CLASS(rom); });                    \
        return true;                                                                            \
    }()
