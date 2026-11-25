#pragma once

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN             // 从 Windows 头文件中排除极少使用的内容
#endif
#include <windows.h>
#include <tchar.h>
#endif

#include <stdint.h>
#include <string.h>
#include <stdio.h>

#if defined(WIN32) || defined(WIN64)
#else
#define sprintf_s sprintf
// memcpy_s 是 C11 附录 K 的可选函数，glibc 至今没有实现（只有 MSVC 和
// 少数带 Annex K 的库提供）。NesCartridge.cpp 用它拷 PRG ROM，不补这个宏
// 在 Linux 上就是「memcpy_s was not declared in this scope」，编译直接失败。
// 参数顺序与语义和 memcpy 一致，这里丢掉 dstSize 只是因为它在本项目里
// 恒等于 count（同一块 prgRomSize 分配的缓冲区）。
#define memcpy_s(dst, dstSize, src, count) memcpy((dst), (src), (count))
#endif
#if !defined(FALSE)
#define FALSE false
#endif
#if !defined(TRUE)
#define TRUE true
#endif

// 包含新的日志系统
#include "Logger.h"

//typedef struct {
//	int(*open)();
//	int(*close)();
//	void(*write)(char* buf, int size);
//	void(*read()(char* buf, int size);
//}savebundle_t;

