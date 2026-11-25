#pragma once
#include "cpu/CPUFlags.h"
#include "cpu/InterruptFlags.h" // 包含中断标志类
#include "SaveBundle.h"

/**
 * @file NesCPU.h
 * @brief NES 6502 CPU emulator header
 *
 * This file declares the NesCPU class which emulates the MOS 6502 processor
 * used in the Nintendo Entertainment System. The CPU runs at 1.789773 MHz (NTSC).
 */

namespace nes {

namespace cpu {
class NesCPUBus; // 前向声明

/**
 * @class NesCPU
 * @brief Emulates the MOS 6502 CPU in the NES
 *
 * The 6502 is an 8-bit microprocessor with a 16-bit address bus.
 * Key features:
 * - 151 official instructions across 256 opcode slots
 * - 13 addressing modes (implied, immediate, zero page, absolute, indexed,
 *   indirect, relative, etc.)
 * - Status register with 8 flags (N, V, -, B, D, I, Z, C)
 *
 * @note This implementation supports all legal and most illegal opcodes,
 *       verified against the nestest golden log (tools/nestest).
 *
 * @see NesCPU::exec() for execution loop
 * @see NesCPU::opAdc() for example opcode implementation
 */
class NesCPU
{
    /**
     * @brief Processor status flags enumeration
     *
     * @deprecated 仅作为 StatusFlag 的单字节别名供指令实现使用,
     *             新代码请直接使用 nes::cpu::StatusFlag
     */
    enum Flags {
        CF = static_cast<uint8_t>(nes::cpu::StatusFlag::Carry),     ///< Carry flag
        ZF = static_cast<uint8_t>(nes::cpu::StatusFlag::Zero),      ///< Zero flag
        IF = static_cast<uint8_t>(nes::cpu::StatusFlag::Interrupt), ///< Interrupt disable
        DF = static_cast<uint8_t>(nes::cpu::StatusFlag::Decimal),   ///< Decimal mode (unused)
        BF = static_cast<uint8_t>(nes::cpu::StatusFlag::Break),     ///< Break flag
        UF = static_cast<uint8_t>(nes::cpu::StatusFlag::Unused),    ///< Unused flag (always 1)
        VF = static_cast<uint8_t>(nes::cpu::StatusFlag::Overflow),  ///< Overflow flag
        NF = static_cast<uint8_t>(nes::cpu::StatusFlag::Negative)   ///< Negative flag
    };

  private:
    cpu::NesCPUBus* mBus;        ///< CPU 总线引用 (唯一外部依赖)
    uint64_t mCpuCycleCount = 0; ///< CPU 时钟周期计数 (从 Famicom 移入)

    /**
     * @struct OpInfo
     * @brief Opcode information structure
     *
     * Contains metadata for each of the 256 opcodes:
     * - handler: Function that executes the opcode
     * - address: Function that calculates effective address
     * - cycle: Base cycle count
     * - morecycle: Additional cycles for certain addressing modes
     * - name: Human-read(able opcode name (for debugging)
     */
    typedef struct
    {
        void (NesCPU::*handler)(); ///< Opcode execution handler
        void (NesCPU::*address)(); ///< Addressing mode calculator
        uint8_t cycle;             ///< Base clock cycles
        uint8_t morecycle;         ///< Extra cycles (page crossing, etc.)
        const char* name;          ///< Opcode name (debugging)
    } OpInfo;

    /**
     * @struct mRegs
     * @brief CPU 寄存器状态
     *
     * 包含所有程序员可见的寄存器:
     * - PC: 程序计数器(16-bit)
     * - SP: 栈指针(8-bit, 栈位$0100-$01FF)
     * - A: 累加器(8-bit)
     * - X: X 索引寄存器(8-bit)
     * - Y: Y 索引寄存器(8-bit)
     *
     * 状态寄存器 P 的唯一真身是 mFlags (CPUFlags), 不在此重复存储。
     */
    struct
    {
        uint16_t PC; ///< 程序计数器
        uint8_t SP;  ///< 栈指针
        uint8_t A;   ///< 累加器
        uint8_t X;   ///< X 索引寄存器
        uint8_t Y;   ///< Y 索引寄存器
    } mRegs;

    nes::cpu::CPUFlags mFlags;

    int mDeferCycles = 0; ///< Deferred cycles (for instruction timing)

    uint16_t mThisPC;      ///< Current PC (before instruction fetch)
    uint16_t mDataAddress; ///< Effective address for current instruction
    uint8_t mDataByte;     ///< Data byte fetched from memory
    uint8_t mIndexBaseHigh = 0; ///< 变址寻址的基址高字节 (用于跨页判断)

    nes::InterruptFlags mInterruptFlags; ///< 中断标志寄存器包装类
    OpInfo mOpcodes[256] = {};           ///< Opcode table (256 entries)
    uint8_t mOpcode = 0;                 ///< Current opcode
    OpInfo* mPtrOpcode = nullptr;        ///< Pointer to current opcode info
    uint8_t mNmiTrigger = 0;              ///< NMI interrupt trigger flag
    uint8_t mIrqTrigger = 0;              ///< IRQ interrupt trigger flag

  public:
    /**
     * @brief 构造NesCPU 对象
     * @param bus CPU 总线指针 (可为 nullptr,稍后通过 setBus 设置)
     */
    explicit NesCPU(nes::cpu::NesCPUBus* bus = nullptr);

    /**
     * @brief 设置 CPU 总线
     * @param bus CPU 总线指针
     */
    void setBus(nes::cpu::NesCPUBus* bus)
    {
        mBus = bus;
    }

    // ========================================================================
    // 寄存器访问方法（替代已删除的宏）
    // ========================================================================

    /**
     * @brief 获取累加器A的引用
     * @return uint8_t& 累加器A的引用
     */
    [[nodiscard]] inline uint8_t& A() noexcept
    {
        return mRegs.A;
    }

    /**
     * @brief 获取 X 寄存器的引用
     * @return uint8_t& X 寄存器的引用
     */
    [[nodiscard]] inline uint8_t& X() noexcept
    {
        return mRegs.X;
    }

    /**
     * @brief 获取 Y 寄存器的引用
     * @return uint8_t& Y 寄存器的引用
     */
    [[nodiscard]] inline uint8_t& Y() noexcept
    {
        return mRegs.Y;
    }

    /**
     * @brief 获取栈指针SP的引用
     * @return uint8_t& 栈指针SP的引用
     */
    [[nodiscard]] inline uint8_t& SP() noexcept
    {
        return mRegs.SP;
    }

    /**
     * @brief 获取程序计数器PC的引用
     * @return uint16_t& 程序计数器PC的引用
     */
    [[nodiscard]] inline uint16_t& PC() noexcept
    {
        return mRegs.PC;
    }

    /**
     * @brief 获取 CPU 标志寄存器的引用
     * @return nes::cpu::CPUFlags& CPU 标志寄存器的引用
     */
    [[nodiscard]] inline nes::cpu::CPUFlags& Flags() noexcept
    {
        return mFlags;
    }

    // Const 版本
    [[nodiscard]] inline const uint8_t& A() const noexcept
    {
        return mRegs.A;
    }
    [[nodiscard]] inline const uint8_t& X() const noexcept
    {
        return mRegs.X;
    }
    [[nodiscard]] inline const uint8_t& Y() const noexcept
    {
        return mRegs.Y;
    }
    [[nodiscard]] inline const uint8_t& SP() const noexcept
    {
        return mRegs.SP;
    }
    [[nodiscard]] inline const uint16_t& PC() const noexcept
    {
        return mRegs.PC;
    }

    // ========================================================================
    // 内部辅助方法 (性能优化)
    // ========================================================================

    /**
     * @brief 获取 CPU Bus 引用 (内部使用)
     * @return NesCPUBus* 总线指针
     */
    [[nodiscard]] inline cpu::NesCPUBus* getBus() noexcept
    {
        return mBus;
    }

    /**
     * @brief 分支指令公共实现
     * @param condition 分支条件是否成立
     *
     * 成立时跳转: +1 周期, 跨页再 +1 周期
     */
    void branchIf(bool condition);

    /**
     * @brief 获取当前 CPU 时钟周期
     * @return uint64_t 时钟周期计数
     */
    [[nodiscard]] inline uint64_t getCycles() const noexcept
    {
        return mCpuCycleCount;
    }

    /**
     * @brief 增加 CPU 时钟周期
     * @param cycles 要增加的周期
     */
    inline void addCycles(uint64_t cycles) noexcept
    {
        mCpuCycleCount += cycles;
    }

    /**
     * @brief Execute CPU instructions for specified number of cycles
     * @param requestCycles Number of clock cycles to execute
     * @return Actual number of cycles executed
     *
     * This is the main CPU execution loop. It fetches, decodes, and executes
     * instructions until the requested cycle count is reached.
     *
     * Example:
     * @code
     * int cyclesExecuted = cpu->exec(1000);
     * @endcode
     */
    int32_t exec(int32_t requestCycles);

    /**
     * @brief reset CPU to initial state
     *
     * Performs a soft reset:
     * - Load PC from reset vector ($FFFC-$FFFD)
     * - Set registers to default values
     * - Clear interrupt flags
     */
    void reset();

    /**
     * @brief 直接设置寄存器状态 (仅用于测试, 如 nestest 自动模式起始态)
     */
    void debugSetState(uint16_t pc, uint8_t a, uint8_t x, uint8_t y, uint8_t sp, uint8_t p);

    /**
     * @brief 每条指令执行前的 PC 采样回调 (诊断工具用, 正常情况为空)
     */
    void (*debugPcHook)(uint16_t pc, uint8_t opcode) = nullptr;

    /**
     * @brief NMI/IRQ 触发计数 (诊断用)
     */
    uint64_t debugNmiCount = 0;
    uint64_t debugIrqCount = 0;

    /**
     * @brief Trigger IRQ interrupt
     *
     * IRQ is masked by the I flag in status register
     */
    void irq();

    /**
     * @brief Attempt to trigger NMI interrupt
     *
     * NMI cannot be masked and will always interrupt the CPU
     */
    void tryNmi();

    /**
     * @brief Execute NMI (Non-Maskable Interrupt)
     *
     * NMI sequence:
     * - Push PC (high byte first)
     * - Push P (without B flag)
     * - Load PC from NMI vector ($FFFA-$FFFB)
     */
    void nmi();

    /**
     * @brief Attempt to trigger IRQ interrupt
     */
    void tryIrq();

    /**
     * @brief Attempt to trigger IRQ with specific interrupt type
     * @param type Interrupt type
     */
    inline void tryIrq(InterruptType type) noexcept
    {
        mInterruptFlags.Set(type);
    }

    /**
     * @brief Clear IRQ interrupt flag
     */
    void clrIrq();

    /**
     * @brief Clear specific interrupt type
     * @param type Interrupt type to clear
     */
    inline void clrIrq(InterruptType type) noexcept
    {
        mInterruptFlags.Clear(type);
    }

    /**
     * @brief read( byte from program counter
     * @return Byte at (PC++)
     */
    uint8_t readByteForPc();

    /**
     * @brief read( word (2 bytes) from program counter
     * @return Word at (PC += 2)
     */
    uint16_t readWordForPc();

    /**
     * @brief Push byte onto stack
     * @param val Value to push
     */
    void pushByte(uint8_t val);

    /**
     * @brief Push word onto stack
     * @param val Value to push
     */
    void pushWord(uint16_t val);

    /**
     * @brief Pop byte from stack
     * @return Byte from (SP++)
     */
    uint8_t popByte();

    /**
     * @brief Pop word from stack
     * @return Word from (SP += 2)
     */
    uint16_t popWord();

    // ========================================================================
    // Arithmetic Operations
    // ========================================================================

    /**
     * @brief Addition with carry (ADC)
     * @param op2 Operand 2
     */
    void mathAdd(uint8_t op2);

    /**
     * @brief Subtraction with borrow (SBC)
     * @param op2 Operand 2
     */
    void mathSub(uint8_t op2);

    /**
     * @brief Bitwise AND test (BIT)
     * @param op1 Operand to test with accumulator
     */
    void mathBit(uint8_t op1);

    /**
     * @brief Arithmetic Shift Left (ASL)
     * @param op1 Value to shift
     * @return Shifted result
     */
    uint8_t mathASL(uint8_t op1);

    /**
     * @brief Logical Shift Right (LSR)
     * @param op1 Value to shift
     * @return Shifted result
     */
    uint8_t mathLSR(uint8_t op1);

    /**
     * @brief Rotate Left (ROL)
     * @param op1 Value to rotate
     * @return Rotated result
     */
    uint8_t mathROL(uint8_t op1);

    /**
     * @brief Rotate Right (ROR)
     * @param op1 Value to rotate
     * @return Rotated result
     */
    uint8_t mathROR(uint8_t op1);

    /**
     * @brief Compare two values and set flags
     * @param op1 First operand (usually A, X, or Y)
     * @param op2 Second operand
     */
    void mathCmp2(uint8_t op1, uint8_t op2);

    // ========================================================================
    // Opcode Implementations - Official Opcodes
    // ========================================================================

    void opAdc();     ///< Add with Carry
    void opSbc1();    ///< Subtract with Borrow (0xEB 别名 opSbc, 见 NesCPU_Illegal.cpp)
    void opUnk();     ///< Unknown/Unofficial Opcode (was OPUNK)
    void opInc();     ///< Increment Memory
    void opInx();     ///< Increment X
    void opIny();     ///< Increment Y
    void opDec();     ///< Decrement Memory
    void opDex();     ///< Decrement X
    void opDey();     ///< Decrement Y
    void opAnd();     ///< Logical AND
    void opEor();     ///< Exclusive OR
    void opOra();     ///< Logical Inclusive OR
    void opBit();     ///< Bit Test
    void opAsl_acc(); ///< ASL Accumulator
    void opAsl();     ///< Arithmetic Shift Left
    void opLsr_acc(); ///< LSR Accumulator
    void opLsr();     ///< Logical Shift Right
    void opRol_acc(); ///< ROL Accumulator
    void opRol();     ///< Rotate Left
    void opRor_acc(); ///< ROR Accumulator
    void opRor();     ///< Rotate Right
    void opLda();     ///< Load Accumulator
    void opSta();     ///< Store Accumulator
    void opStx();     ///< Store X
    void opSty();     ///< Store Y
    void opTax();     ///< Transfer A to X
    void opTay();     ///< Transfer A to Y
    void opTsx();     ///< Transfer S to X
    void opTxa();     ///< Transfer X to A
    void opTxs();     ///< Transfer X to S
    void opTya();     ///< Transfer Y to A
    void opLdx();     ///< Load X
    void opLdy();     ///< Load Y
    void opCmp();     ///< Compare Accumulator
    void opCpx();     ///< Compare X
    void opCpy();     ///< Compare Y
    void opBcc();     ///< Branch if Carry Clear
    void opBcs();     ///< Branch if Carry Set
    void opBeq();     ///< Branch if Equal
    void opBmi();     ///< Branch if Minus
    void opBne();     ///< Branch if Not Equal
    void opBpl();     ///< Branch if Plus
    void opBvc();     ///< Branch if Overflow Clear
    void opBvs();     ///< Branch if Overflow Set
    void opBrk();     ///< Break
    void opJmp();     ///< Jump
    void opJsr();     ///< Jump to Subroutine
    void opRti();     ///< Return from Interrupt
    void opRts();     ///< Return from Subroutine
    void opClc();     ///< Clear Carry
    void opCld();     ///< Clear Decimal
    void opCli();     ///< Clear Interrupt Disable
    void opClv();     ///< Clear Overflow
    void opSec();     ///< Set Carry
    void opSed();     ///< Set Decimal
    void opSei();     ///< Set Interrupt Disable
    void opPha();     ///< Push Accumulator
    void opPhp();     ///< Push Processor Status
    void opPla();     ///< Pull Accumulator
    void opPlp();     ///< Pull Processor Status

    // ========================================================================
    // Opcode Implementations - Unofficial/Illegal Opcodes
    // ========================================================================

    void opSlo();  ///< Shift Left then OR (ASL + ORA)
    void opNop();  ///< No Operation (含所有非法 NOP 变体)
    void opRla();  ///< Rotate Left then AND (ROL + AND)
    void opSre();  ///< Shift Right then XOR (LSR + EOR)
    void opRra();  ///< Rotate Right then ADC (ROR + ADC)
    void opSax();  ///< Store A XOR X (STA + STX)
    void opLax();  ///< Load A XOR X (LDA + LDX)
    void opDcp();  ///< Decrement then Compare (DEC + CMP)
    void opIsc();  ///< Increment then SBC (INC + SBC)
    void opAnc();  ///< AND then set Carry (AND + ASL)
    void opAlr();  ///< AND then LSR right
    void opArr();  ///< AND then ROR right
    void opXaa();  ///< Transfer X to A with AND
    void opAxs();  ///< A XOR S then store in X
    void opSbc();  ///< Subtract with Borrow (unofficial)
    void opAhx();  ///< Store A AND H at effective address
    void opShy();  ///< Store Y shifted by X
    void opShx();  ///< Store X shifted by Y
    void opTas();  ///< Transfer A+S to S then store
    void opLas();  ///< Load A, S from memory

    // ========================================================================
    // Addressing Modes
    // ========================================================================

    void _addrInd();  ///< Indirect ($ZZ)
    void _addrRel();  ///< Relative ($XX)
    void _addrAcc();  ///< Accumulator
    void _addrZp();   ///< Zero Page ($ZZ)
    void _addrZpy();  ///< Zero Page,Y ($ZZ,Y)
    void _addrZpx();  ///< Zero Page,X ($ZZ,X)
    void _addrImm();  ///< Immediate #$VV
    void _addrIzx();  ///< Indexed Indirect ($ZZ,X)
    void _addrIzy();  ///< Indirect Indexed ($ZZ),Y
    void _addrAbs();  ///< Absolute ($HHLL)
    void _addrAbsX(); ///< Absolute,X ($HHLL,X)
    void _addrAbsY(); ///< Absolute,Y ($HHLL,Y)

    // ========================================================================
    // Save State Support
    // ========================================================================

    /**
     * @brief Save CPU state to bundle
     * @param bundle Save bundle destination
     */
    void save(nes::io::SaveBundle* bundle);

    /**
     * @brief Restore CPU state from bundle
     * @param bundle Save bundle source
     */
    void restore(nes::io::SaveBundle* bundle);
};

} // namespace cpu
} // namespace nes
