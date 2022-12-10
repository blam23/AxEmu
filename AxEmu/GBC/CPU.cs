using System;
using System.Reflection;

namespace AxEmu.GBC;

// Sharp LR25902 (clone of Z80)
internal partial class CPU
{
    //
    // Data
    //
    MemoryBus bus;

    //
    // Initialisation
    //
    public CPU(MemoryBus bus)
    {
        this.bus = bus;
        LoadInstructions();
    }

    //
    // Registers & Flags
    //
    // https://gbdev.gg8.se/wiki/articles/CPU_Registers_and_Flags

    internal struct Flags
    {
        internal bool Z; // zero
        internal bool C; // carry
        internal bool N; // (BCD) last instr was addition or subtraction?
        internal bool H; // (BCD) carry lower 4 bits


        // lower 3 bits always zero
        public byte AsByte => Byte.or(Byte.from(Z, 7), Byte.from(N, 6), Byte.from(H, 5), Byte.from(C, 4));

        public void Set(byte value)
        {
            Z = Byte.bit(value, 0x40);
            N = Byte.bit(value, 0x20);
            H = Byte.bit(value, 0x10);
            C = Byte.bit(value, 0x08);
        }
    }

    internal ushort SP;
    internal ushort PC;

    internal byte A;
    internal byte B;
    internal byte C;
    internal byte D;
    internal byte E;
    internal byte H;
    internal byte L;

    internal Flags flags;

    internal ushort AF
    {
        get { return Byte.combine(A, flags.AsByte); }
        set { A = Byte.upper(value); flags.Set(Byte.lower(value)); }
    }
    internal ushort BC
    {
        get { return Byte.combine(B, C); }
        set { B = Byte.upper(value); C = Byte.lower(value); }
    }
    internal ushort DE
    {
        get { return Byte.combine(D, E); }
        set { D = Byte.upper(value); E = Byte.lower(value); }
    }
    internal ushort HL
    {
        get { return Byte.combine(H, L); }
        set { H = Byte.upper(value); L = Byte.lower(value); }
    }

    bool IME = false;

    //
    // Current State
    //
    Instruction current;
    bool jumped;

    public uint CyclesLastClock { get; private set; }

    public void Clock()
    {
        // Reset state
        jumped = false;

        // Get instruction
        var op = bus.Read(PC);
        var instr = instructions[op];

        // Carry out instruction
        if (instr != null)
        {
            current = instr;
            instr.action();
        }
        else
            throw new NotImplementedException($"Unknown opcode: {op:X2}");

        // Store how many cycles the instruction took
        CyclesLastClock = (uint)(instr.Cycles + (jumped ? 4 : 0));

        // Move to next instruction
        if (!jumped)
            PC += instr.Size;
    }

    public void Reset()
    {
        // These are BGB default values, need to see if they're correct
        // Presumably, running boot rom would setup these values
        A = 0x01;
        B = 0x00;
        C = 0x13;
        D = 0x00;
        E = 0xD8;
        H = 0x01;
        L = 0x4D;

        SP = 0xFFFE;
        PC = 0x0100;
    }

    //
    // Helpers
    //
    private byte Imm() => bus.Read((ushort)(PC + 1));
    private ushort Abs() => bus.ReadWord((ushort)(PC + 1));

    private byte ParamByte()
    {
        return current.Param switch
        {
            Data.A => A,
            Data.B => B,
            Data.C => C,
            Data.D => D,
            Data.E => E,
            Data.H => H,
            Data.L => L,

            Data.Imm => Imm(),

            Data.Ind_Abs => bus.Read(Abs()),
            Data.Ind_Imm => bus.Read((ushort)(0xFF | Imm())),
            Data.Ind_C   => bus.Read((ushort)(0xFF | C)),
            Data.Ind_BC  => bus.Read(BC),
            Data.Ind_DE  => bus.Read(DE),
            Data.Ind_HL  => bus.Read(HL),

            Data.Inc_HL => bus.Read(HL++),
            Data.Dec_HL => bus.Read(HL--),

            _ => throw new InvalidOperationException()
        };
    }

    private ushort ParamWord()
    {
        return current.Param switch
        {
            Data.Abs => Abs(),

            _ => throw new InvalidOperationException()
        };
    }



    private void Write()
    {
        switch (current.Output)
        {
            case Data.None:
                throw new InvalidOperationException();

            case Data.A: A = ParamByte(); break;
            case Data.B: B = ParamByte(); break;
            case Data.C: C = ParamByte(); break;
            case Data.D: D = ParamByte(); break;
            case Data.E: E = ParamByte(); break;
            case Data.H: H = ParamByte(); break;
            case Data.L: L = ParamByte(); break;

            case Data.BC: BC = ParamWord(); break;
            case Data.DE: DE = ParamWord(); break;
            case Data.HL: HL = ParamWord(); break;
            case Data.SP: SP = ParamWord(); break;

            case Data.Ind_Abs: bus.Write(Abs(), ParamByte()); break;
            case Data.Ind_Imm: bus.Write((ushort)(0xFF | Imm()), ParamByte()); break;
            case Data.Ind_C:   bus.Write((ushort)(0xFF | C), ParamByte()); break;
            case Data.Ind_BC:  bus.Write(BC, ParamByte()); break;
            case Data.Ind_DE:  bus.Write(DE, ParamByte()); break;
            case Data.Ind_HL:  bus.Write(HL, ParamByte()); break;

            case Data.Inc_HL: bus.Write(HL++, ParamByte()); break;
            case Data.Dec_HL: bus.Write(HL--, ParamByte()); break;

            case Data.Imm: bus.Write(Imm(), ParamByte()); break;
            case Data.Abs: bus.Write(Abs(), ParamByte()); break;

            default:
                break;
        }
    }

    private bool GetCond()
    {
        return current.Condition switch
        {
            Conditional.Z  => flags.Z,
            Conditional.NZ => !flags.Z,
            Conditional.C  => flags.C,
            Conditional.NC => !flags.C,

            _ => throw new InvalidOperationException(),
        };
    }

    private void JmpCondImpl()
    {
        var cond = GetCond();

        if (cond)
        {
            jumped = true;
            JmpImpl();
        }
    }

    private void JmpImpl()
    {
        jumped = true;

        PC = current.Param switch
        {
            Data.Abs => ParamWord(),
            Data.Imm => (ushort)(PC + (sbyte)ParamByte() + 2),

            _ => throw new InvalidOperationException(),
        };
    }

    private void AddImpl(bool carry = false)
    {
        // Only A is used as output for addition
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = ParamByte() + ((carry && flags.C) ? 1 : 0);

        // Add operand to A and store in int so we can check if we need to carry
        var result = A + operand;

        // Cast back to byte and store in A
        A = (byte)(result & 0xFF);

        // Set flags
        flags.C = result > 0xFF;
        flags.H = result > 0x0F;
        flags.N = false;
        flags.Z = A == 0;
    }

    private void SubImpl(bool carry = false)
    {
        // Only A is used as output for subtraction
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = ParamByte() + ((carry && flags.C) ? 1 : 0);

        // Sub operand from A and store in int so we went negative
        var result = A - operand;

        // Cast back to byte and store in A
        A = (byte)(result & 0xFF);

        // Set flags
        flags.C = result < 0x00;
        flags.H = result < 0x0F;
        flags.N = true;
        flags.Z = A == 0;
    }

    private void XorImpl()
    {
        // Only A is used as output for XOR
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = ParamByte();

        // Do XOR
        A ^= operand;

        // Set flags
        flags.Z = A == 0;
    }

    private void DecImpl()
    {
        ushort result = current.Param switch
        {
            Data.A  => --A,
            Data.B  => --B,
            Data.C  => --C,
            Data.D  => --D,
            Data.E  => --E,
            Data.H  => --H,
            Data.L  => --L,
            Data.BC => --BC,
            Data.DE => --DE,
            Data.HL => --HL,
            Data.SP => --SP,
            _ => throw new InvalidOperationException(),
        };

        bool u16 = current.Param == Data.BC 
            || current.Param == Data.DE 
            || current.Param == Data.HL 
            || current.Param == Data.SP;

        // Don't set flags for u16 ops
        if (u16)
            return;

        flags.Z = result == 0;
        flags.N = true;
        flags.H = result < 0x0F;
    }

    private void IncImpl()
    {
        ushort result = current.Param switch
        {
            Data.A => ++A,
            Data.B => ++B,
            Data.C => ++C,
            Data.D => ++D,
            Data.E => ++E,
            Data.H => ++H,
            Data.L => ++L,
            Data.BC => ++BC,
            Data.DE => ++DE,
            Data.HL => ++HL,
            Data.SP => ++SP,
            _ => throw new InvalidOperationException(),
        };

        bool u16 = current.Param == Data.BC
            || current.Param == Data.DE
            || current.Param == Data.HL
            || current.Param == Data.SP;

        // Don't set flags for u16 ops
        if (u16)
            return;

        flags.Z = result == 0;
        flags.N = true;
        flags.H = result > 0x0F;
    }
}
