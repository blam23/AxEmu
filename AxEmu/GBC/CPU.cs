using System;
using System.Reflection;

namespace AxEmu.GBC;

// Sharp LR25902 (clone of Z80)
internal partial class CPU
{
    //
    // Data
    //
    readonly MemoryBus bus;


    //
    // Initialisation
    //
    public CPU(MemoryBus bus)
    {
        this.bus = bus;
        LoadInstructions();

        bus.RegisterIOProperties(GetType(), this);
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
            Z = Byte.bit(value, 0x80);
            N = Byte.bit(value, 0x40);
            H = Byte.bit(value, 0x20);
            C = Byte.bit(value, 0x10);
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

    bool IME    = false;
    bool halted = false;

    //
    // Interrupts
    //

    [IO(Address = 0xFFFF)]
    public byte IE { get; set; } = 0;

    [IO(Address = 0xFF0F)]
    public byte IF { get; set; } = 0;

    internal enum Interrupt
    {
        VBlank,
        Stat,
        Timer,
        Serial,
        Joypad,
    }

    public void RequestInterrupt(Interrupt type)
    {
        switch(type)
        {
            case Interrupt.VBlank:  IF |= 0x01; break;
            case Interrupt.Stat:    IF |= 0x02; break;
            case Interrupt.Timer:   IF |= 0x04; break;
            case Interrupt.Serial:  IF |= 0x08; break;
            case Interrupt.Joypad:  IF |= 0x10; break;
        }
    }

    private void ServiceInterrupt()
    {
        IME = false;
        halted = false;
        CyclesLastClock += 5;

        PushWord(PC);

        // Only service the first enabled interrupt
        var eif = IF | IE;

        if (eif == 0)
            throw new InvalidOperationException();
        
        if ((eif & 0x01) == 0x01)
        {
            IF &= 0xFE; // Clear bit
            PC  = 0x40; // Goto VBlank handler
        }
        else if ((eif & 0x02) == 0x02)
        {
            IF &= 0xFD; // Clear bit
            PC  = 0x48; // Goto Stat handler
        }
        else if ((eif & 0x04) == 0x04)
        {
            IF &= 0xFB; // Clear bit
            PC  = 0x50; // Goto Timer handler
        }
        else if ((eif & 0x08) == 0x08)
        {
            IF &= 0xF7; // Clear bit
            PC  = 0x58; // Goto Serial handler
        }
        else if ((eif & 0x10) == 0x10)
        {
            IF &= 0xEF; // Clear bit
            PC  = 0x60; // Goto Joypad handler
        }
    }


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

        if(IME && (IF | IE) > 0)
        {
            ServiceInterrupt();
        }

        if (!halted)
        {
            // Get instruction
            var op = bus.Read(PC);

            var prefixed = false;
            if (op == 0xCB)
            {
                op = bus.Read(++PC);
                prefixed = true;
            }

            var instr = prefixed ? prefixInstructions[op] : instructions[op];

            // Carry out instruction
            if (instr != null)
            {
                current = instr;
                PC += current.Size;
                instr.action();
            }
            else
                throw new NotImplementedException($"Unknown opcode: {op:X2}, prefixed? {(prefixed ? 'Y' : 'N')}");

            // Store how many cycles the instruction took
            CyclesLastClock = (uint)(instr.Cycles + (jumped ? 4 : 0));
        }
        else
        {
            CyclesLastClock = 4;
        }
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

        flags.C = true;
        flags.H = true;
        flags.N = false;
        flags.Z = true;

        SP = 0xFFFE;
        PC = 0x0100;
    }

    //
    // Helpers
    //
    private void PushByte(byte value)
    {
        bus.Write(--SP, value);
    }

    private void PushWord(ushort value)
    {
        bus.Write(--SP, Byte.upper(value));
        bus.Write(--SP, Byte.lower(value));
    }

    private byte PopByte()
    {
        return bus.Read(SP++);
    }

    private ushort PopWord()
    {
        return Byte.combineR(PopByte(), PopByte());
    }

    private byte Imm() => bus.Read((ushort)(PC - 1));
    private ushort Abs() => bus.ReadWord((ushort)(PC - 2));

    private byte DataAsByte(Data d)
    {
        return d switch
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
            Data.Ind_Imm => bus.Read(Byte.combine(0xFF, Imm())),
            Data.Ind_C => bus.Read(Byte.combine(0xFF, C)),
            Data.Ind_BC => bus.Read(BC),
            Data.Ind_DE => bus.Read(DE),
            Data.Ind_HL => bus.Read(HL),

            Data.Inc_HL => bus.Read(HL++),
            Data.Dec_HL => bus.Read(HL--),

            _ => throw new InvalidOperationException()
        };
    }

    private byte OutByte()
    {
        return DataAsByte(current.Output);
    }

    private byte InByte()
    {
        return DataAsByte(current.Input);
    }

    private ushort InWord()
    {
        return current.Input switch
        {
            Data.Abs => Abs(),
            Data.BC  => BC,
            Data.DE  => DE,
            Data.HL  => HL,
            Data.AF  => AF,
            Data.SP  => SP,

            Data.SP_Plus_Imm => GetSPWithImmOffsetAndSetFlags(),

            _ => throw new InvalidOperationException()
        };
    }

    private void WriteByte(byte val)
    {
        switch (current.Output)
        {
            case Data.None:
                throw new InvalidOperationException();

            case Data.A: A = val; break;
            case Data.B: B = val; break;
            case Data.C: C = val; break;
            case Data.D: D = val; break;
            case Data.E: E = val; break;
            case Data.H: H = val; break;
            case Data.L: L = val; break;

            case Data.Ind_Abs: bus.Write(Abs(), val); break;
            case Data.Ind_Imm: bus.Write(Byte.combine(0xFF, Imm()), val); break;
            case Data.Ind_C:  bus.Write(Byte.combine(0xFF, C), val); break;
            case Data.Ind_BC: bus.Write(BC, val); break;
            case Data.Ind_DE: bus.Write(DE, val); break;
            case Data.Ind_HL: bus.Write(HL, val); break;

            case Data.Inc_HL: bus.Write(HL++, val); break;
            case Data.Dec_HL: bus.Write(HL--, val); break;

            case Data.Imm: bus.Write(Imm(), val); break;
            case Data.Abs: bus.Write(Abs(), val); break;

            default:
                throw new InvalidOperationException();
        }
    }

    private void WriteWord(ushort val)
    {
        switch (current.Output)
        {
            case Data.None:
                throw new InvalidOperationException();

            case Data.BC: BC = val; break;
            case Data.DE: DE = val; break;
            case Data.HL: HL = val; break;
            case Data.AF: AF = val; break;
            case Data.SP: SP = val; break;

            case Data.Ind_Abs: bus.WriteWord(Abs(), val); break;

            default:
                throw new InvalidOperationException();
        }
    }

    private void Write()
    {
        if (current.Input == Data.SP
            || current.Input == Data.BC
            || current.Input == Data.DE
            || current.Input == Data.HL
            && current.Output == Data.Ind_Abs) 
        {
            WriteWord(InWord()); 
            return;
        }

        switch (current.Output)
        {
            case Data.None:
                throw new InvalidOperationException();

            case Data.A:
            case Data.B:
            case Data.C:
            case Data.D:
            case Data.E:
            case Data.H:
            case Data.L:
            case Data.Ind_Abs:
            case Data.Ind_Imm:
            case Data.Ind_C:  
            case Data.Ind_BC: 
            case Data.Ind_DE: 
            case Data.Ind_HL: 
            case Data.Inc_HL:
            case Data.Dec_HL:
            case Data.Imm:
            case Data.Abs:
                WriteByte(InByte());
                break;

            case Data.BC:
            case Data.DE:
            case Data.HL:
            case Data.SP:
                WriteWord(InWord());
                break;

            default:
                break;
        }
    }

    private bool GetCond()
    {
        return current.Condition switch
        {
            Conditional.Always => true,
            Conditional.Z      => flags.Z,
            Conditional.NZ     => !flags.Z,
            Conditional.C      => flags.C,
            Conditional.NC     => !flags.C,

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

        // Jump with offset
        if (current.Input == Data.Imm)
            PC = (ushort)(PC + (sbyte)InByte());
        else // Jump using 16 bit data (such as an absolute value or combined register)
            PC = InWord();
    }

    private void RetImpl()
    {
        var cond = GetCond();

        if (cond)
        {
            jumped = true;

            PC = PopWord();
        }
    }

    private ushort GetSPWithImmOffsetAndSetFlags()
    {
        var operand = (sbyte)Imm();
        var result = SP + operand;

        flags.Z = false;
        flags.C = (((SP & 0xFF) + (operand & 0xFF)) & 0x100) == 0x100;
        //flags.C = result == 0x0100 || result == 0x8000 || result > 0xFFFF || result == 0x0000;
        flags.N = false;
        flags.H = (((SP & 0xF) + (operand & 0xF)) & 0x10) == 0x10;

        return (ushort)(result & 0xFFFF);
    }

    // Easier to have this as a specific method as it has various quirks
    private void AddSPImmImpl()
    {
        if (current.Output != Data.SP && current.Input != Data.Imm)
            throw new InvalidOperationException();

        SP = GetSPWithImmOffsetAndSetFlags();
    }

    private void Add16Impl()
    {
        // Only HL is used as output for 16-bit addition
        if (current.Output != Data.HL)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InWord();

        // Add operand to HL and store in int so we can check if we need to carry
        var result = HL + operand;

        // Set flags
        flags.C = result > 0xFFFF;
        flags.N = false;
        flags.H = ((HL & 0x0FFF) + (operand & 0x0FFF)) > 0x0FFF;

        // Cast back to ushort and store in HL
        HL = (ushort)(result & 0xFFFF);
    }

    private void AddImpl(bool carry = false)
    {
        // Only A is used as output for addition
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InByte() + ((carry && flags.C) ? 1 : 0);

        // Add operand to A and store in int so we can check if we need to carry
        var result = A + operand;

        // Set flags
        flags.C = result > 0xFF;
        flags.N = false;
        flags.H = (((A & 0xF) + (operand & 0xF)) & 0x10) == 0x10;

        // Cast back to byte and store in A
        A = (byte)(result & 0xFF);
        flags.Z = A == 0;
    }

    private void SubImpl(bool carry = false)
    {
        // Only A is used as output for subtraction
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InByte() + ((carry && flags.C) ? 1 : 0);

        // Sub operand from A and store in int so we went negative
        var result = A - operand;

        // Set flags
        flags.C = result < 0x00;
        flags.N = true;
        flags.H = ((((A & 0xF) - (operand & 0xF)) & 0x10) == 0x10);

        // Cast back to byte and store in A
        A = (byte)(result & 0xFF);
        flags.Z = A == 0;
    }

    private void CpImpl()
    {
        // Only A is used as output for subtraction
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InByte() + (flags.C ? 1 : 0);

        // Sub operand from A and store in int so we went negative
        var result = A - operand;

        // Set flags
        flags.C = result < 0x00;
        flags.H = (((A & 0xF) - (operand & 0xF)) & 0x10) == 0x10;
        flags.N = true;
        flags.Z = result == 0;
    }

    private void DaaImpl()
    {
        if (flags.N) // After Subtraction
        {
            if (flags.H)
                A -= 0x60;

            if (flags.H)
                A -= 0x06;
        }
        else // After Addition
        {
            if (flags.C || A > 0x99)
            {
                A += 0x60;
                flags.C = true;
            }
            if (flags.H || (A & 0x0F) > 0x09)
            {
                A += 0x06;
            }
        }

        // Set flags
        flags.H = false;
        flags.Z = A == 0;
    }

    private void XorImpl()
    {
        // Only A is used as output for XOR
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InByte();

        // Do XOR
        A ^= operand;

        // Set flags
        flags.Z = A == 0;
        flags.N = false;
        flags.H = false;
        flags.C = false;
    }

    private void CplImpl()
    {
        // Only A is used as output for CPL
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        A = (byte)~A;

        flags.N = true;
        flags.H = true;
    }

    private byte GetBit()
    {
        return current.Input switch
        {
            Data.Bit0 => 0b00000001,
            Data.Bit1 => 0b00000010,
            Data.Bit2 => 0b00000100,
            Data.Bit3 => 0b00001000,
            Data.Bit4 => 0b00010000,
            Data.Bit5 => 0b00100000,
            Data.Bit6 => 0b01000000,
            Data.Bit7 => 0b10000000,

            _ => throw new InvalidOperationException(),
        };
    }

    private void BitImpl()
    {
        var bit = GetBit();
        var val = OutByte();

        flags.Z = (bit & val) == 0;
        flags.N = false;
        flags.H = true;
    }

    private void SetBitImpl()
    {
        var bit = GetBit();
        var val = OutByte();

        WriteByte((byte)(val | bit));
    }

    private void ResBitImpl()
    {
        var bit = ~GetBit();
        var val = OutByte();

        WriteByte((byte)(val & bit));
    }

    private void OrImpl()
    {
        // Only A is used as output for XOR
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InByte();

        // Do OR
        A |= operand;

        // Set flags
        flags.Z = A == 0;
        flags.N = false;
        flags.H = false;
        flags.C = false;
    }

    private void AndImpl()
    {
        // Only A is used as output for XOR
        if (current.Output != Data.A)
            throw new InvalidOperationException();

        // Get our operand
        var operand = InByte();

        // Do OR
        A &= operand;

        // Set flags
        flags.Z = A == 0;
        flags.N = false;
        flags.H = true;
        flags.C = false;
    }

    private void DecImpl()
    {
        ushort result = current.Input switch
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

            Data.Ind_HL =>
                new Func<ushort>(() =>
                {
                    var t = (byte)(bus.Read(HL) - 1);
                    bus.Write(HL, t);
                    return t;
                })(),

            _ => throw new InvalidOperationException(),
        };

        bool u16 = current.Input == Data.BC 
            || current.Input == Data.DE 
            || current.Input == Data.HL 
            || current.Input == Data.SP;

        // Don't set flags for u16 ops
        if (u16)
            return;

        flags.Z = result == 0;
        flags.N = true;

        flags.H = ((((result+1) & 0xF) - 1) & 0x10) == 0x10;
    }

    private void IncImpl()
    {
        var result = current.Input switch
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

            Data.Ind_HL =>
                new Func<ushort>(() =>
                {
                    var t = (byte)(bus.Read(HL) + 1);
                    bus.Write(HL, t);
                    return t;
                })(),

            _ => throw new InvalidOperationException(),
        };



        bool u16 = current.Input == Data.BC
            || current.Input == Data.DE
            || current.Input == Data.HL
            || current.Input == Data.SP;

        // Don't set flags for u16 ops
        if (u16)
            return;

        flags.Z = result == 0;
        flags.N = false;

        flags.H = ((((result - 1) & 0xF) + 1) & 0x10) == 0x10;
        //flags.H = (((result-1) & 0xF) + (1 & 0xF)) > 0xF;
    }

    private void RrImpl(bool carry, bool arth)
    {
        var val = InByte();
        (val, flags.C) = Byte.ror(val, (carry && flags.C) || (arth && (val & 0x80) == 0x80));
        WriteByte(val);

        flags.Z = current.Prefix && val == 0;
        flags.H = false;
        flags.N = false;
    }

    private void RlImpl(bool carry)
    {
        var val = InByte();
        (val, flags.C) = Byte.rol(val, carry && flags.C);
        WriteByte(val);

        flags.Z = current.Prefix && val == 0;
        flags.H = false;
        flags.N = false;
    }

    private void SlaImpl()
    {
        var val = InByte();
        (val, flags.C) = Byte.rol(val, false);
        WriteByte(val);

        flags.Z = current.Prefix && val == 0;
        flags.H = false;
        flags.N = false;
    }

    private void SraImpl()
    {
        var val = InByte();
        (val, flags.C) = Byte.ror(val, (val & 0x80) == 0x80);
        WriteByte(val);

        flags.Z = current.Prefix && val == 0;
        flags.H = false;
        flags.N = false;
    }

    private void SrlImpl()
    {
        var val = InByte();
        (val, flags.C) = Byte.ror(val, false);
        WriteByte(val);

        flags.Z = current.Prefix && val == 0;
        flags.H = false;
        flags.N = false;
    }

    private void SwapImpl(bool carry)
    {
        var val = InByte();
        val = Byte.swap(val);
        WriteByte(val);

        flags.Z = val == 0;
        flags.C = false;
        flags.H = false;
        flags.N = false;
    }

    private void RstImpl()
    {
        PushWord(PC);

        // Could be done with maffs innit, oh well
        byte n = current.OPCode switch
        {
            0xC7 => 0x00,
            0xD7 => 0x10,
            0xE7 => 0x20,
            0xF7 => 0x30,
            0xCF => 0x08,
            0xDF => 0x18,
            0xEF => 0x28,
            0xFF => 0x38,
            
            _ => throw new InvalidOperationException(),
        };

        PC = Byte.combine(0x00, n);

        jumped = true;
    }

    private void CallImpl()
    {
        var cond = GetCond();

        if (cond)
        {
            PushWord(PC);
            PC = InWord();
            jumped = true;
        }
    }
}
