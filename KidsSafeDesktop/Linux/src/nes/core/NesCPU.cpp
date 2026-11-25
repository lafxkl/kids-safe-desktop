#include "NesCPU.h"
#include <cstdio>
#include <cstdlib>
#include "common.h"
#include "Devices.h"
#include "Famicom.h"
#include <cassert>

namespace nes {
namespace cpu {

using namespace nes::cpu;

// 寻址模式函数指针别名 (经 _OP 宏填入 opcode 表; addrnone 表示无操作数)
constexpr auto addracc = &NesCPU::_addrAcc;
constexpr auto addrimm = &NesCPU::_addrImm;
constexpr auto addrzp = &NesCPU::_addrZp;
constexpr auto addrzpx = &NesCPU::_addrZpx;
constexpr auto addrzpy = &NesCPU::_addrZpy;
constexpr auto addrizx = &NesCPU::_addrIzx;
constexpr auto addrizy = &NesCPU::_addrIzy;
constexpr auto addrabs = &NesCPU::_addrAbs;
constexpr auto addrabx = &NesCPU::_addrAbsX;
constexpr auto addraby = &NesCPU::_addrAbsY;
constexpr auto addrind = &NesCPU::_addrInd;
constexpr auto addrrel = &NesCPU::_addrRel;
constexpr auto addrnone = nullptr;

// 指令定义宏
#define _OP(var0, var1, var2, var3, var4, var5)                                                                        \
    this->mOpcodes[var0] = OpInfo                                                                                      \
    {                                                                                                                  \
        &NesCPU::var1, var2, var3, var4, var5                                                                          \
    }

NesCPU::NesCPU(nes::cpu::NesCPUBus* bus) : mBus(bus)
{
    for (size_t i = 0; i < 256; i++) {
        _OP(i, opUnk, addrnone, 2, 0, "_NO_");
    }

// 寻址模式函数指针别名 (经 _OP 宏填入 opcode 表; addrnone 表示无操作数)
    //_OP(0x0, opBrk, addrnone, 7, 0, "brk");

    _OP(0x03, opSlo, addrizx, 8, 0, "slo");
    _OP(0x07, opSlo, addrzp, 5, 0, "slo");
    _OP(0x0f, opSlo, addrabs, 6, 0, "slo");
    _OP(0x13, opSlo, addrizy, 8, 0, "slo");
    _OP(0x17, opSlo, addrzpx, 6, 0, "slo");
    _OP(0x1b, opSlo, addraby, 7, 0, "slo");
    _OP(0x1f, opSlo, addrabx, 7, 0, "slo");

    _OP(0x80, opNop, addrimm, 2, 0, "nop");
    _OP(0x82, opNop, addrimm, 2, 0, "nop");
    _OP(0xc2, opNop, addrimm, 2, 0, "nop");
    _OP(0xe2, opNop, addrimm, 2, 0, "nop");

    _OP(0x04, opNop, addrzp, 3, 0, "nop");
    _OP(0x44, opNop, addrzp, 3, 0, "nop");
    _OP(0x64, opNop, addrzp, 3, 0, "nop");
    _OP(0x0c, opNop, addrabs, 4, 0, "nop");
    _OP(0x1c, opNop, addrabx, 4, 1, "nop");
    _OP(0x14, opNop, addrzpx, 4, 0, "nop");
    _OP(0x34, opNop, addrzpx, 4, 0, "nop");
    _OP(0x54, opNop, addrzpx, 4, 0, "nop");
    _OP(0x74, opNop, addrzpx, 4, 0, "nop");
    _OP(0xd4, opNop, addrzpx, 4, 0, "nop");
    _OP(0xf4, opNop, addrzpx, 4, 0, "nop");

    _OP(0x89, opNop, addrimm, 2, 0, "nop");

    _OP(0x1a, opNop, addrnone, 2, 0, "nop");
    _OP(0x3a, opNop, addrnone, 2, 0, "nop");
    _OP(0x5a, opNop, addrnone, 2, 0, "nop");
    _OP(0x7a, opNop, addrnone, 2, 0, "nop");
    _OP(0xda, opNop, addrnone, 2, 0, "nop");
    _OP(0xea, opNop, addrnone, 2, 0, "nop");
    _OP(0xfa, opNop, addrnone, 2, 0, "nop");

    _OP(0x0c, opNop, addrabs, 4, 0, "nop");
    _OP(0x1c, opNop, addrabx, 4, 1, "nop");
    _OP(0x3c, opNop, addrabx, 4, 1, "nop");
    _OP(0x5c, opNop, addrabx, 4, 1, "nop");
    _OP(0x7c, opNop, addrabx, 4, 1, "nop");
    _OP(0xdc, opNop, addrabx, 4, 1, "nop");
    _OP(0xfc, opNop, addrabx, 4, 1, "nop");

    _OP(0x23, opRla, addrizx, 8, 0, "RLA");
    _OP(0x33, opRla, addrizy, 8, 0, "RLA");
    _OP(0x27, opRla, addrzp, 5, 0, "RLA");
    _OP(0x37, opRla, addrzpx, 6, 0, "RLA");
    _OP(0x3b, opRla, addraby, 7, 0, "RLA");
    _OP(0x2f, opRla, addrabs, 6, 0, "RLA");
    _OP(0x3f, opRla, addrabx, 7, 0, "RLA");

    _OP(0x43, opSre, addrizx, 8, 0, "SRE");
    _OP(0x53, opSre, addrizy, 8, 0, "SRE");
    _OP(0x47, opSre, addrzp, 5, 0, "SRE");
    _OP(0x57, opSre, addrzpx, 6, 0, "SRE");
    _OP(0x5b, opSre, addraby, 7, 0, "SRE");
    _OP(0x4f, opSre, addrabs, 6, 0, "SRE");
    _OP(0x5f, opSre, addrabx, 7, 0, "SRE");

    _OP(0x63, opRra, addrizx, 8, 0, "RRA");
    _OP(0x73, opRra, addrizy, 8, 0, "RRA");
    _OP(0x67, opRra, addrzp, 5, 0, "RRA");
    _OP(0x77, opRra, addrzpx, 6, 0, "RRA");
    _OP(0x7b, opRra, addraby, 7, 0, "RRA");
    _OP(0x6f, opRra, addrabs, 6, 0, "RRA");
    _OP(0x7f, opRra, addrabx, 7, 0, "RRA");

    _OP(0x83, opSax, addrizx, 6, 0, "SAX");
    _OP(0x87, opSax, addrzp, 3, 0, "SAX");
    _OP(0x97, opSax, addrzpy, 4, 0, "SAX");
    _OP(0x8f, opSax, addrabs, 4, 0, "SAX");

    _OP(0xa3, opLax, addrizx, 6, 0, "LAX");
    _OP(0xb3, opLax, addrizy, 5, 1, "LAX");
    _OP(0xa7, opLax, addrzp, 3, 0, "LAX");
    _OP(0xb7, opLax, addrzpy, 4, 0, "LAX");
    _OP(0xab, opLax, addrimm, 2, 0, "LAX");
    _OP(0xaf, opLax, addrabs, 4, 0, "LAX");
    _OP(0xbf, opLax, addraby, 4, 1, "LAX");

    _OP(0xc3, opDcp, addrizx, 8, 0, "DCP");
    _OP(0xd3, opDcp, addrizy, 8, 0, "DCP");
    _OP(0xc7, opDcp, addrzp, 5, 0, "DCP");
    _OP(0xd7, opDcp, addrzpx, 6, 0, "DCP");
    _OP(0xcf, opDcp, addrabs, 6, 0, "DCP");
    _OP(0xdf, opDcp, addrabx, 7, 0, "DCP");
    _OP(0xdb, opDcp, addraby, 7, 0, "DCP");

    _OP(0xe3, opIsc, addrizx, 8, 0, "ISC");
    _OP(0xf3, opIsc, addrizy, 8, 0, "ISC");
    _OP(0xe7, opIsc, addrzp, 5, 0, "ISC");
    _OP(0xf7, opIsc, addrzpx, 6, 0, "ISC");
    _OP(0xfb, opIsc, addraby, 7, 0, "ISC");
    _OP(0xef, opIsc, addrabs, 6, 0, "ISC");
    _OP(0xff, opIsc, addrabx, 7, 0, "ISC");

    _OP(0x0b, opAnc, addrimm, 2, 0, "ANC");
    _OP(0x2b, opAnc, addrimm, 2, 0, "ANC");

    _OP(0x4b, opAlr, addrimm, 2, 0, "ALR");
    _OP(0x6b, opArr, addrimm, 2, 0, "ARR");
    _OP(0x8b, opXaa, addrimm, 2, 0, "XAA");
    _OP(0xcb, opAxs, addrimm, 2, 0, "AXS");
    _OP(0xeb, opSbc, addrimm, 2, 0, "SBC");

    _OP(0x93, opAhx, addrizy, 6, 0, "AHX");
    _OP(0x9f, opAhx, addraby, 5, 0, "AHX");
    _OP(0x9c, opShy, addrabx, 5, 0, "SHY");
    _OP(0x9e, opShx, addraby, 5, 0, "SHX");
    _OP(0x9b, opTas, addraby, 5, 0, "TAS");
    _OP(0xbb, opLas, addraby, 4, 1, "LAS");

    _OP(0x8b, opXaa, addrimm, 2, 1, "XAA");

    // ADC
    _OP(0x69, opAdc, addrimm, 2, 0, "ADC");
    _OP(0x65, opAdc, addrzp, 3, 0, "ADC");
    _OP(0x75, opAdc, addrzpx, 4, 0, "ADC");
    _OP(0x6d, opAdc, addrabs, 4, 0, "ADC");
    _OP(0x7d, opAdc, addrabx, 4, 1, "ADC");
    _OP(0x79, opAdc, addraby, 4, 1, "ADC");
    _OP(0x61, opAdc, addrizx, 6, 0, "ADC");
    _OP(0x71, opAdc, addrizy, 5, 1, "ADC");

    // SBC
    _OP(0xe9, opSbc1, addrimm, 2, 0, "SBC");
    _OP(0xe5, opSbc1, addrzp, 3, 0, "SBC");
    _OP(0xf5, opSbc1, addrzpx, 4, 0, "SBC");
    _OP(0xed, opSbc1, addrabs, 4, 0, "SBC");
    _OP(0xfd, opSbc1, addrabx, 4, 1, "SBC");
    _OP(0xf9, opSbc1, addraby, 4, 1, "SBC");
    _OP(0xe1, opSbc1, addrizx, 6, 0, "SBC");
    _OP(0xf1, opSbc1, addrizy, 5, 1, "SBC");

    // INC
    _OP(0xe6, opInc, addrzp, 5, 0, "INC");
    _OP(0xf6, opInc, addrzpx, 6, 0, "INC");
    _OP(0xee, opInc, addrabs, 6, 0, "INC");
    _OP(0xfe, opInc, addrabx, 7, 0, "INC");

    _OP(0xe8, opInx, addrnone, 2, 0, "INX");
    _OP(0xc8, opIny, addrnone, 2, 0, "INX");

    // DEC
    _OP(0xc6, opDec, addrzp, 5, 0, "DEC");
    _OP(0xd6, opDec, addrzpx, 6, 0, "DEC");
    _OP(0xce, opDec, addrabs, 6, 0, "DEC");
    _OP(0xde, opDec, addrabx, 7, 0, "DEC");

    _OP(0xca, opDex, addrnone, 2, 0, "DEX");
    _OP(0x88, opDey, addrnone, 2, 0, "DEY");

    // AND
    _OP(0x29, opAnd, addrimm, 2, 0, "AND");
    _OP(0x25, opAnd, addrzp, 3, 0, "AND");
    _OP(0x35, opAnd, addrzpx, 4, 0, "AND");
    _OP(0x2d, opAnd, addrabs, 4, 0, "AND");
    _OP(0x3d, opAnd, addrabx, 4, 1, "AND");
    _OP(0x39, opAnd, addraby, 4, 1, "AND");
    _OP(0x21, opAnd, addrizx, 6, 0, "AND");
    _OP(0x31, opAnd, addrizy, 5, 1, "AND");

    // EOR
    _OP(0x49, opEor, addrimm, 2, 0, "EOR");
    _OP(0x45, opEor, addrzp, 3, 0, "EOR");
    _OP(0x55, opEor, addrzpx, 4, 0, "EOR");
    _OP(0x4d, opEor, addrabs, 4, 0, "EOR");
    _OP(0x5d, opEor, addrabx, 4, 1, "EOR");
    _OP(0x59, opEor, addraby, 4, 1, "EOR");
    _OP(0x41, opEor, addrizx, 6, 0, "EOR");
    _OP(0x51, opEor, addrizy, 5, 1, "EOR");

    // ORA
    _OP(0x09, opOra, addrimm, 2, 0, "ORA");
    _OP(0x05, opOra, addrzp, 3, 0, "ORA");
    _OP(0x15, opOra, addrzpx, 4, 0, "ORA");
    _OP(0x0d, opOra, addrabs, 4, 0, "ORA");
    _OP(0x1d, opOra, addrabx, 4, 1, "ORA");
    _OP(0x19, opOra, addraby, 4, 1, "ORA");
    _OP(0x01, opOra, addrizx, 6, 0, "ORA");
    _OP(0x11, opOra, addrizy, 5, 1, "ORA");

    // BIT
    _OP(0x24, opBit, addrzp, 3, 0, "BIT");
    _OP(0x2c, opBit, addrabs, 4, 0, "BIT");

    // ASL
    _OP(0x0a, opAsl_acc, addracc, 2, 0, "ASL");
    _OP(0x06, opAsl, addrzp, 5, 0, "ASL");
    _OP(0x16, opAsl, addrzpx, 6, 0, "ASL");
    _OP(0x0e, opAsl, addrabs, 6, 0, "ASL");
    _OP(0x1e, opAsl, addrabx, 7, 0, "ASL");

    // LSR
    _OP(0x4a, opLsr_acc, addracc, 2, 0, "LSR");
    _OP(0x46, opLsr, addrzp, 5, 0, "LSR");
    _OP(0x56, opLsr, addrzpx, 6, 0, "LSR");
    _OP(0x4e, opLsr, addrabs, 6, 0, "LSR");
    _OP(0x5e, opLsr, addrabx, 7, 0, "LSR");

    // ROL
    _OP(0x2a, opRol_acc, addracc, 2, 0, "ROL");
    _OP(0x26, opRol, addrzp, 5, 0, "ROL");
    _OP(0x36, opRol, addrzpx, 6, 0, "ROL");
    _OP(0x2e, opRol, addrabs, 6, 0, "ROL");
    _OP(0x3e, opRol, addrabx, 7, 0, "ROL");

    // ROR
    _OP(0x6a, opRor_acc, addracc, 2, 0, "ROR");
    _OP(0x66, opRor, addrzp, 5, 0, "ROR");
    _OP(0x76, opRor, addrzpx, 6, 0, "ROR");
    _OP(0x6e, opRor, addrabs, 6, 0, "ROR");
    _OP(0x7e, opRor, addrabx, 7, 0, "ROR");

    // LDA
    _OP(0xa9, opLda, addrimm, 2, 0, "LDA");
    _OP(0xa5, opLda, addrzp, 3, 0, "LDA");
    _OP(0xb5, opLda, addrzpx, 4, 0, "LDA");
    _OP(0xad, opLda, addrabs, 4, 0, "LDA");
    _OP(0xbd, opLda, addrabx, 4, 1, "LDA");
    _OP(0xb9, opLda, addraby, 4, 1, "LDA");
    _OP(0xa1, opLda, addrizx, 6, 0, "LDA");
    _OP(0xb1, opLda, addrizy, 5, 1, "LDA");

    // LDX
    _OP(0xa2, opLdx, addrimm, 2, 0, "LDX");
    _OP(0xa6, opLdx, addrzp, 3, 0, "LDX");
    _OP(0xb6, opLdx, addrzpy, 4, 0, "LDX");
    _OP(0xae, opLdx, addrabs, 4, 0, "LDX");
    _OP(0xbe, opLdx, addraby, 4, 1, "LDX");

    // LDY
    _OP(0xa0, opLdy, addrimm, 2, 0, "LDY");
    _OP(0xa4, opLdy, addrzp, 3, 0, "LDY");
    _OP(0xb4, opLdy, addrzpx, 4, 0, "LDY");
    _OP(0xac, opLdy, addrabs, 4, 0, "LDY");
    _OP(0xbc, opLdy, addrabx, 4, 1, "LDY");

    // STA
    _OP(0x85, opSta, addrzp, 3, 0, "STA");
    _OP(0x95, opSta, addrzpx, 4, 0, "STA");
    _OP(0x8d, opSta, addrabs, 4, 0, "STA");
    _OP(0x9d, opSta, addrabx, 5, 0, "STA");
    _OP(0x99, opSta, addraby, 5, 0, "STA");
    _OP(0x81, opSta, addrizx, 6, 0, "STA");
    _OP(0x91, opSta, addrizy, 6, 0, "STA");

    // STX
    _OP(0x86, opStx, addrzp, 3, 0, "STX");
    _OP(0x96, opStx, addrzpy, 4, 0, "STX");
    _OP(0x8e, opStx, addrabs, 4, 0, "STX");

    // STY
    _OP(0x84, opSty, addrzp, 3, 0, "STY");
    _OP(0x94, opSty, addrzpx, 4, 0, "STY");
    _OP(0x8c, opSty, addrabs, 4, 0, "STY");

    //
    _OP(0xaa, opTax, addrnone, 2, 0, "TAX");
    _OP(0xa8, opTay, addrnone, 2, 0, "TAY");
    _OP(0xba, opTsx, addrnone, 2, 0, "TSX");
    _OP(0x8a, opTxa, addrnone, 2, 0, "TXA");
    _OP(0x9a, opTxs, addrnone, 2, 0, "TXS");
    _OP(0x98, opTya, addrnone, 2, 0, "TYA");

    // CMP
    _OP(0xc9, opCmp, addrimm, 2, 0, "CMP");
    _OP(0xc5, opCmp, addrzp, 3, 0, "CMP");
    _OP(0xd5, opCmp, addrzpx, 4, 0, "CMP");
    _OP(0xcd, opCmp, addrabs, 4, 0, "CMP");
    _OP(0xdd, opCmp, addrabx, 4, 1, "CMP");
    _OP(0xd9, opCmp, addraby, 4, 1, "CMP");
    _OP(0xc1, opCmp, addrizx, 6, 0, "CMP");
    _OP(0xd1, opCmp, addrizy, 5, 1, "CMP");

    // CPX
    _OP(0xe0, opCpx, addrimm, 2, 0, "CPX");
    _OP(0xe4, opCpx, addrzp, 3, 0, "CPX");
    _OP(0xec, opCpx, addrabs, 4, 0, "CPX");

    // CPY
    _OP(0xc0, opCpy, addrimm, 2, 0, "CPY");
    _OP(0xc4, opCpy, addrzp, 3, 0, "CPY");
    _OP(0xcc, opCpy, addrabs, 4, 0, "CPY");

    //
    _OP(0x90, opBcc, addrrel, 2, 2, "BCC");
    _OP(0xb0, opBcs, addrrel, 2, 2, "BCS");
    _OP(0xf0, opBeq, addrrel, 2, 2, "BEQ");
    _OP(0x30, opBmi, addrrel, 2, 2, "BMI");
    _OP(0xd0, opBne, addrrel, 2, 2, "BNE");
    _OP(0x10, opBpl, addrrel, 2, 2, "BPL");
    _OP(0x50, opBvc, addrrel, 2, 2, "BVC");
    _OP(0x70, opBvs, addrrel, 2, 2, "BVS");

    //
    _OP(0x00, opBrk, addrnone, 7, 0, "BRK");
    _OP(0x4c, opJmp, addrabs, 3, 0, "JMP");
    _OP(0x6c, opJmp, addrind, 5, 0, "JMP");
    _OP(0x20, opJsr, addrabs, 6, 0, "JSR");
    _OP(0x40, opRti, addrnone, 6, 0, "RTI");
    _OP(0x60, opRts, addrnone, 6, 0, "RTS");

    //
    _OP(0x18, opClc, addrnone, 2, 0, "CLC");
    _OP(0xd8, opCld, addrnone, 2, 0, "CLD");
    _OP(0x58, opCli, addrnone, 2, 0, "CLI");
    _OP(0xb8, opClv, addrnone, 2, 0, "CLV");
    _OP(0x38, opSec, addrnone, 2, 0, "SEC");
    _OP(0xf8, opSed, addrnone, 2, 0, "SED");
    _OP(0x78, opSei, addrnone, 2, 0, "SEI");

    //
    _OP(0x48, opPha, addrnone, 3, 0, "PHA");
    _OP(0x08, opPhp, addrnone, 3, 0, "PHP");
    _OP(0x68, opPla, addrnone, 4, 0, "PLA");
    _OP(0x28, opPlp, addrnone, 4, 0, "PLP");
}

/// <summary>
/// CPU
/// </summary>
/// <param name="requestCycles">CPU</param>
/// <returns>CPU</returns>
int32_t NesCPU::exec(int32_t requestCycles)
{
    mDeferCycles = 0;

    while (mDeferCycles < requestCycles) {
        // P
        //
        // P 寄存器唯一真身在 mFlags, 无需镜像到 mRegs

        //
        if (mNmiTrigger) {
            mNmiTrigger = 0;
            nmi();
            // DebugPrintf("TIGGLE nmi\n");
        } else if (mIrqTrigger) {
            mIrqTrigger = 0;
            irq();
            // DebugPrintf("TIGGLE irq\n");
        }

        //
        mThisPC = PC(); //
        mOpcode = readByteForPc();
        mPtrOpcode = &mOpcodes[mOpcode];
        
        // 指令追踪日志 (仅在 TRACE 级别启用)
        if (Logger::shouldLog(LogLevel::Trace)) {
            LOG_TRACE << "CPU [" << std::hex << "0x" << mThisPC 
                     << "] OP:0x" << static_cast<int>(mOpcode)
                     << " A:" << static_cast<int>(A())
                     << " X:" << static_cast<int>(X())
                     << " Y:" << static_cast<int>(Y())
                     << " SP:" << static_cast<int>(SP())
                     << " P:" << std::hex << "0x" << static_cast<int>(mFlags.GetRaw())
                     << " " << (mPtrOpcode->name ? mPtrOpcode->name : "???");
        }
        if (debugPcHook)
            debugPcHook(mThisPC, mOpcode);

        //
        (this->*(mPtrOpcode->handler))();
        // 跨页附加周期: 有效地址与未加索引的基址不在同一页时 +1
        // (基址高字节由寻址函数记录在 mIndexBaseHigh；分支指令在 op 内部自行处理，不走此路径)
        if (mPtrOpcode->morecycle & 0x01) {
            if ((mDataAddress >> 8) != mIndexBaseHigh)
                mDeferCycles++;
        }
        mDeferCycles += mPtrOpcode->cycle;

        //
        if (mInterruptFlags.IsSet(nes::InterruptType::NmiPending)) {
            mInterruptFlags.Clear(nes::InterruptType::NmiPending);
            mNmiTrigger = 0xff;
        } else if (!mFlags.IsSet(nes::cpu::StatusFlag::Interrupt) && mInterruptFlags.HasAny()) {
            mInterruptFlags.Clear(nes::InterruptType::IrqOneShot);
            mIrqTrigger = 0xff;
        }
    }

    // 累加实际执行的周期数到 mCpuCycleCount (使用 += 避免覆盖其他来源的周期,如 PPU DMA)
    mCpuCycleCount += mDeferCycles;
    return mDeferCycles;
}

/// <summary>
/// irq
/// </summary>
void NesCPU::clrIrq()
{
    mInterruptFlags.Clear(nes::InterruptType::IrqPending);
}

/// <summary>
/// irq
/// ?
/// </summary>
void NesCPU::tryIrq()
{
    mInterruptFlags.Set(nes::InterruptType::IrqPending);
}

/// <summary>
///
/// ?
/// </summary>
void NesCPU::tryNmi()
{
    mInterruptFlags.Set(nes::InterruptType::NmiPending);
}

inline void NesCPU::irq()
{
    debugIrqCount++;
    pushWord(PC());
    pushByte(mFlags.GetRaw() | UF);
    mFlags.Set(nes::cpu::StatusFlag::Interrupt);

    PC() = getBus()->readWord(0xFFFE);
}

void NesCPU::nmi()
{
    debugNmiCount++;
    pushWord(PC());
    // P
    pushByte(mFlags.GetRaw() & ~BF);
    mFlags.Set(nes::cpu::StatusFlag::Interrupt);

    PC() = getBus()->readWord(0xFFFA);
    mDeferCycles += 7;
}

/// <summary>
///
/// </summary>
inline void NesCPU::opBrk()
{
    readByteForPc(); //
    pushWord(PC());
    pushByte(mFlags.GetRaw() | BF);
    mFlags.Set(nes::cpu::StatusFlag::Interrupt);

    PC() = getBus()->readWord(0xFFFE);
    LOG_WARN << "执行 BRK 指令";
}

inline void NesCPU::opCli()
{
    mFlags.Clear(nes::cpu::StatusFlag::Interrupt);
}

inline void NesCPU::opRti()
{
    mFlags.SetRaw(popByte());
    mFlags.Clear(nes::cpu::StatusFlag::Break); // BF
    mFlags.Set(nes::cpu::StatusFlag::Unused);

    PC() = popWord();
}

/// <summary>
///
/// </summary>
void NesCPU::reset()
{
    if (!mBus) {
        LOG_ERROR << "CPU reset: mBus is null!";
        return;
    }

    A() = X() = Y() = 0;
    SP() = 0xFD; // 6502 复位规范值 (参见 nestest 首行 SP:FD)
    mFlags.ClearAll();
    mFlags.Set(nes::cpu::StatusFlag::Unused);
    mFlags.Set(nes::cpu::StatusFlag::Interrupt);

    PC() = getBus()->readWord(0xFFFC);

    mInterruptFlags.ClearAll();
    mIrqTrigger = 0;
    mNmiTrigger = 0;
    mCpuCycleCount = 0;
}

void NesCPU::debugSetState(uint16_t pc, uint8_t a, uint8_t x, uint8_t y, uint8_t sp, uint8_t p)
{
    PC() = pc;
    A() = a;
    X() = x;
    Y() = y;
    SP() = sp;
    mFlags.SetRaw(p);

}

/// <summary>
///
/// </summary>
void NesCPU::opUnk()
{
    LOG_ERROR << "不支持的指令: 0x" << std::hex << static_cast<int>(mOpcode) << " at PC: 0x" << mThisPC;
    // assert(!"");
}

inline void NesCPU::opAdc()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    mathAdd(this->mDataByte);
}

void NesCPU::opSbc1()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    mathSub(this->mDataByte);
}

void NesCPU::opInc()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    this->mDataByte++;
    getBus()->writeByte(this->mDataAddress, this->mDataByte);
    mFlags.SetFromResult(this->mDataByte);
}

void NesCPU::opInx()
{
    ++X();
    mFlags.SetFromResult(X());
}

void NesCPU::opDec()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    --mDataByte;
    getBus()->writeByte(mDataAddress, mDataByte);
    mFlags.SetFromResult(mDataByte);
}

void NesCPU::opDex()
{
    --X();
    mFlags.SetFromResult(X());
}

void NesCPU::opDey()
{
    --Y();
    mFlags.SetFromResult(Y());
}

void NesCPU::opAnd()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    A() &= mDataByte;
    mFlags.SetFromResult(A());
}

void NesCPU::opEor()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    A() ^= mDataByte;
    mFlags.SetFromResult(A());
}

void NesCPU::opOra()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    A() |= mDataByte;

    mFlags.SetFromResult(A());
}

void NesCPU::opBit()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    this->mathBit(this->mDataByte);
}

void NesCPU::opAsl_acc()
{
    A() = this->mathASL(A());
}

void NesCPU::opAsl()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    getBus()->writeByte(mDataAddress, mathASL(mDataByte));
}

void NesCPU::opLsr_acc()
{
    A() = this->mathLSR(A());
}

void NesCPU::opLsr()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    getBus()->writeByte(mDataAddress, mathLSR(mDataByte));
}

void NesCPU::opRol_acc()
{

    A() = this->mathROL(A());
}

void NesCPU::opRol()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    getBus()->writeByte(mDataAddress, mathROL(mDataByte));
}

void NesCPU::opRor_acc()
{
    A() = this->mathROR(A());
}

void NesCPU::opRor()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    getBus()->writeByte(mDataAddress, mathROR(mDataByte));
}

void NesCPU::opLda()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    A() = this->mDataByte;
    mFlags.SetFromResult(A());
}

void NesCPU::opSta()
{
    (this->*mPtrOpcode->address)();
    getBus()->writeByte(mDataAddress, A());
}

void NesCPU::opStx()
{
    (this->*mPtrOpcode->address)();
    getBus()->writeByte(mDataAddress, X());
}

void NesCPU::opSty()
{
    (this->*mPtrOpcode->address)();
    getBus()->writeByte(mDataAddress, Y());
}

void NesCPU::opTax()
{
    X() = A();
    mFlags.SetFromResult(X());
}

void NesCPU::opTay()
{
    Y() = A();
    mFlags.SetFromResult(Y());
}

void NesCPU::opTsx()
{
    X() = SP();
    mFlags.SetFromResult(X());
}

void NesCPU::opTxa()
{
    A() = X();
    mFlags.SetFromResult(A());
}

void NesCPU::opTxs()
{
    SP() = X();
}

void NesCPU::opTya()
{
    A() = Y();
    mFlags.SetFromResult(A());
}

void NesCPU::opLdx()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    X() = this->mDataByte;
    mFlags.SetFromResult(X());
}

void NesCPU::opLdy()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    Y() = this->mDataByte;
    mFlags.SetFromResult(Y());
}

void NesCPU::opCmp()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    mathCmp2(A(), this->mDataByte);
}

/// <summary>
/// 分支指令公共实现: 条件成立时跳转到相对地址,
/// 跳转 +1 周期, 跨页再 +1 周期
/// </summary>
inline void NesCPU::branchIf(bool condition)
{
    (this->*mPtrOpcode->address)();
    if (condition) {
        if ((PC() & 0xff00) != (this->mDataAddress & 0xff00))
            this->mDeferCycles += 1;
        PC() = this->mDataAddress;
        this->mDeferCycles += 1;
    }
}

void NesCPU::opBcc()
{
    branchIf(!mFlags.IsSet(nes::cpu::StatusFlag::Carry));
}

void NesCPU::opBcs()
{
    branchIf(mFlags.IsSet(nes::cpu::StatusFlag::Carry));
}

void NesCPU::opBeq()
{
    branchIf(mFlags.IsSet(nes::cpu::StatusFlag::Zero));
}

void NesCPU::opBmi()
{
    branchIf(mFlags.IsSet(nes::cpu::StatusFlag::Negative));
}

void NesCPU::opBne()
{
    branchIf(!mFlags.IsSet(nes::cpu::StatusFlag::Zero));
}

void NesCPU::opBpl()
{
    branchIf(!mFlags.IsSet(nes::cpu::StatusFlag::Negative));
}

void NesCPU::opBvc()
{
    branchIf(!mFlags.IsSet(nes::cpu::StatusFlag::Overflow));
}

void NesCPU::opBvs()
{
    branchIf(mFlags.IsSet(nes::cpu::StatusFlag::Overflow));
}

void NesCPU::opJmp()
{
    (this->*mPtrOpcode->address)();
    // PC
    PC() = this->mDataAddress;
}

void NesCPU::opJsr()
{
    (this->*mPtrOpcode->address)();
    pushWord(mThisPC + 2);
    PC() = this->mDataAddress;
    // DebugPrintf("opJsr\n");
}

void NesCPU::opRts()
{
    //  SP
    PC() = this->popWord() + 1;
}

void NesCPU::opClc()
{
    // mFlags.Clear(nes::cpu::StatusFlag::Carry);
    mFlags.Clear(nes::cpu::StatusFlag::Carry);
}

void NesCPU::opCld()
{
    // mFlags.Clear(nes::cpu::StatusFlag::Decimal);
    mFlags.Clear(nes::cpu::StatusFlag::Decimal);
}

void NesCPU::opClv()
{
    // mFlags.Clear(nes::cpu::StatusFlag::Overflow);
    mFlags.Clear(nes::cpu::StatusFlag::Overflow);
}

void NesCPU::opSec()
{
    // mFlags.Set(nes::cpu::StatusFlag::Carry);
    mFlags.Set(nes::cpu::StatusFlag::Carry);
}

void NesCPU::opSed()
{
    // mFlags.Set(nes::cpu::StatusFlag::Decimal);
    mFlags.Set(nes::cpu::StatusFlag::Decimal);
}

void NesCPU::opSei()
{
    mFlags.Set(nes::cpu::StatusFlag::Interrupt);
    // REG_TMP_IF = 0xFF;
}

void NesCPU::opPha()
{
    pushByte(A());
}

void NesCPU::opPhp()
{
    pushByte(mFlags.GetRaw() | BF);
}

void NesCPU::opPla()
{
    A() = this->popByte();
    mFlags.SetFromResult(A());
}

void NesCPU::opPlp()
{
    mFlags.SetRaw(popByte() & 0xef); // BF
    mFlags.Set(nes::cpu::StatusFlag::Unused);
}

void NesCPU::opCpx()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    this->mathCmp2(X(), this->mDataByte);
}

void NesCPU::opCpy()
{
    (this->*mPtrOpcode->address)();
    mDataByte = getBus()->readByte(mDataAddress);
    this->mathCmp2(Y(), this->mDataByte);
}

void NesCPU::opIny()
{
    Y() += 1;
    mFlags.SetFromResult(Y());
}

uint8_t NesCPU::readByteForPc()
{
    return getBus()->readByte(PC()++);
}

uint16_t NesCPU::readWordForPc()
{
    uint16_t val = getBus()->readWord(PC());
    PC() += 2;
    return val;
}

void NesCPU::pushByte(uint8_t val)
{
    getBus()->writeByte(0x100 | SP(), val);
    SP()--;
}

void NesCPU::pushWord(uint16_t val)
{
    pushByte(val >> 8);
    this->pushByte(val & 0xff);
}

uint8_t NesCPU::popByte()
{
    ++SP();
    return getBus()->readByte(0x100 | SP());
}

uint16_t NesCPU::popWord()
{
    return (uint16_t)popByte() | ((uint16_t)popByte() << 8);
}


void NesCPU::mathAdd(uint8_t op2)
{
    uint8_t op1 = A();
    uint16_t val = op1 + op2 + (mFlags.GetRaw() & 0x01);

    if (((op1 ^ val) & (op2 ^ val)) & 0x80)
        mFlags.Set(nes::cpu::StatusFlag::Overflow);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Overflow);

    //
    if (val & 0xFF00)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    mFlags.SetFromResult(static_cast<uint8_t>(val));

    A() = val & 0xff;
}

void NesCPU::mathSub(uint8_t op2)
{
    uint8_t op1 = A();
    short val = A() - op2 - (1 - (mFlags.GetRaw() & 0x01));

    //  1
    if (((op1 ^ op2) & (op1 ^ val)) & 0x80)
        mFlags.Set(nes::cpu::StatusFlag::Overflow);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Overflow);

    // 0
    if ((val & 0xff00) == 0)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    A() = val & 0xff;

    mFlags.SetFromResult(A());
}

void NesCPU::mathBit(uint8_t op1)
{
    if (op1 & 0x80)
        mFlags.Set(nes::cpu::StatusFlag::Negative);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Negative);

    if (op1 & 0x40)
        mFlags.Set(nes::cpu::StatusFlag::Overflow);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Overflow);
    // qDebug("%x",A() ^ op1);
    if ((A() & op1) == 0)
        mFlags.Set(nes::cpu::StatusFlag::Zero);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Zero);
}

uint8_t NesCPU::mathASL(uint8_t op1)
{
    uint8_t val = op1 << 1;

    //
    if (op1 & 0x80)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    mFlags.SetFromResult(static_cast<uint8_t>(val));

    return val;
}

uint8_t NesCPU::mathLSR(uint8_t op1)
{
    uint8_t val = op1 >> 1;

    //
    if (op1 & 0x01)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    mFlags.SetFromResult(static_cast<uint8_t>(val));

    return val;
}

uint8_t NesCPU::mathROL(uint8_t op1)
{
    uint8_t val = op1 << 1;

    if (mFlags.IsSet(nes::cpu::StatusFlag::Carry)) // CF,CF
        val |= 0x01;

    //
    if (op1 & 0x80)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    mFlags.SetFromResult(static_cast<uint8_t>(val));

    return val;
}

uint8_t NesCPU::mathROR(uint8_t op1)
{
    uint8_t val = op1 >> 1;

    if (mFlags.IsSet(nes::cpu::StatusFlag::Carry)) // CF,CF
        val |= 0x80;

    //
    if (op1 & 0x01)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    mFlags.SetFromResult(static_cast<uint8_t>(val));

    return val;
}

void NesCPU::mathCmp2(uint8_t op1, uint8_t op2)
{
    short val = op1 - op2;
    //
    if (val >= 0)
        mFlags.Set(nes::cpu::StatusFlag::Carry);
    else
        mFlags.Clear(nes::cpu::StatusFlag::Carry);

    mFlags.SetFromResult(static_cast<uint8_t>(val));
}

inline void NesCPU::_addrAbsX()
{
    uint16_t base = this->readWordForPc();
    mIndexBaseHigh = static_cast<uint8_t>(base >> 8);
    this->mDataAddress = base + X();
}

inline void NesCPU::_addrAbsY()
{
    uint16_t base = this->readWordForPc();
    mIndexBaseHigh = static_cast<uint8_t>(base >> 8);
    this->mDataAddress = base + Y();
}

inline void NesCPU::_addrAbs()
{
    this->mDataAddress = this->readWordForPc();
}

inline void NesCPU::_addrInd()
{
    uint16_t address = this->readWordForPc();
    uint16_t newaddress;
    if ((address & 0xff) == 0xff) // NESBUG
    {
        uint8_t b1 = getBus()->readByte(address);
        uint8_t b2 = getBus()->readByte(address & 0xff00);
        newaddress = (b2 << 8) | b1;
    } else {
        newaddress = getBus()->readWord(address);
    }
    this->mDataAddress = newaddress;
}

/// <summary>
///
/// </summary>
inline void NesCPU::_addrRel()
{
    auto val = (int8_t)this->readByteForPc(); //
    this->mDataAddress = PC() + val;
}

/// <summary>
///
/// </summary>
inline void NesCPU::_addrZp()
{
    this->mDataAddress = this->readByteForPc() & 0x00ff;
}

/// <summary>
/// Y
/// </summary>
inline void NesCPU::_addrZpy()
{
    // BUG  8
    this->mDataAddress = (uint16_t)(this->readByteForPc() + Y()) & 0xff;
}

/// <summary>
/// X
/// </summary>
inline void NesCPU::_addrZpx()
{
    // BUG  8
    this->mDataAddress = (uint16_t)(this->readByteForPc() + X()) & 0xff;
}

/// <summary>
///
/// </summary>
inline void NesCPU::_addrAcc()
{}

/// <summary>
///
/// </summary>
inline void NesCPU::_addrImm()
{
    this->mDataAddress = PC()++;
}

inline void NesCPU::_addrIzx()
{
    uint8_t val = this->readByteForPc();
    // bug
    uint8_t address = (uint16_t)val + X();

    // uint16_t newaddress;

    // world NES BUG
    uint8_t b1 = getBus()->readByte(address & 0xff);
    uint8_t b2 = getBus()->readByte((address + 1) & 0xff);

    uint16_t newaddress = (b2 << 8) | b1;
    this->mDataAddress = newaddress;
}

inline void NesCPU::_addrIzy()
{
    uint8_t val = this->readByteForPc();
    auto bus = getBus();
    uint8_t b1 = bus->readByte(val & 0xff);
    uint8_t b2 = bus->readByte((val + 1) & 0xff);
    uint16_t address = (b2 << 8) | b1;
    mIndexBaseHigh = static_cast<uint8_t>(address >> 8);
    uint16_t newaddress = (uint16_t)Y() + address;
    this->mDataAddress = newaddress;
}

void NesCPU::save(nes::io::SaveBundle* bundle)
{
    bundle->saveWord(mRegs.PC);
    bundle->saveByte(mRegs.SP);
    bundle->saveByte(mRegs.A);
    bundle->saveByte(mRegs.X);
    bundle->saveByte(mRegs.Y);

    bundle->saveByte(mFlags.GetRaw());
    bundle->saveByte(mInterruptFlags.GetRaw());
    bundle->saveByte(mNmiTrigger);
    bundle->saveByte(mIrqTrigger);
}

void NesCPU::restore(nes::io::SaveBundle* bundle)
{
    mRegs.PC = bundle->restoreWord();
    mRegs.SP = bundle->restoreByte();
    mRegs.A = bundle->restoreByte();
    mRegs.X = bundle->restoreByte();
    mRegs.Y = bundle->restoreByte();

    mFlags.SetRaw(bundle->restoreByte());
    mInterruptFlags.SetRaw(bundle->restoreByte());
    mNmiTrigger = bundle->restoreByte();
    mIrqTrigger = bundle->restoreByte();
}

} // namespace cpu
} // namespace nes