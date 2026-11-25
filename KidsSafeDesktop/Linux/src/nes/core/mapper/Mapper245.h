#pragma once

#include "../Rom.h"
#include "Mapper004.h"
namespace nes {
namespace device {
class Mapper245 : public Mapper004
{
  public:
    using Mapper004::Mapper004;  // 继承基类构造函数
};
} // namespace device
} // namespace nes