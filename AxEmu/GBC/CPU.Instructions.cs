using System;

namespace AxEmu.GBC;

internal partial class CPU
{
    internal enum Data
    {
        None,    // None
        A,       // Use A Register
        B,       // Use B Register
        C,       // Use C Register
        D,       // Use D Register
        E,       // Use E Register
        H,       // Use H Register
        L,       // Use L Register
        AF,      // Use AF combined Register
        BC,      // Use BC combined Register
        DE,      // Use DE combined Register
        HL,      // Use HL combined Register
        SP,      // Use Stack Pointer
        Imm,     // Byte after instruction
        Ind_HL,  // Indirect using HL reg
        Inc_HL,  // Indirect using HL reg (and increment HL reg)
        Dec_HL,  // Indirect using HL reg (and decrement HL reg)
        Ind_BC,  // Indirect using BC reg
        Ind_DE,  // Indirect using DE reg
        Ind_C,   // Indirect using ((0xFF00) | C reg)
        Ind_Imm, // Indirect using ((0xFF00) | Imm)
        Ind_Abs, // Indirect using (Abs)
        Abs,     // Word after instruction

        // Specific bit targets, i.e. for SETing or RESing bits
        Bit0, Bit1, Bit2, Bit3,
        Bit4, Bit5, Bit6, Bit7,

        SP_Plus_Imm, // Only used by instr 0xF8
    }

    internal enum Conditional
    {
        None,
        Z, NZ,
        C, NC,
        Always
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal class InstructionAttribute : Attribute
    {
        public byte OPCode { get; set; }
        public string Name { get; set; } = "";
        public int Cycles { get; set; }

        public Data Input { get; set; } = Data.None;
        public Data Output { get; set; } = Data.None;
        public Conditional Condition { get; set; } = Conditional.None;

        public bool Prefix { get; set; } = false;
    }

    internal class Instruction
    {
        public bool Prefix;
        public byte OPCode;
        public string Name = "";
        public int Cycles;
        public ushort Size;
        public Data Input  = Data.None;
        public Data Output = Data.None;
        public Conditional Condition = Conditional.None;

        public Action action;

        public Instruction(CPU cpu, InstructionAttribute attr, System.Reflection.MethodInfo method)
        {
            Prefix    = attr.Prefix;
            OPCode    = attr.OPCode;
            Name      = PrettyName(attr, method.Name);
            Size      = CalcSize(attr);
            Cycles    = attr.Cycles;
            Input     = attr.Input;
            Output    = attr.Output;
            Condition = attr.Condition;

            action = (Action)Delegate.CreateDelegate(typeof(Action), cpu, method);
        }

        private static byte GetDataLen(Data data)
        {
            return data switch
            {
                Data.Imm => 1,
                Data.Ind_Imm => 1,
                Data.SP_Plus_Imm => 1,
                Data.Abs => 2,
                Data.Ind_Abs => 2,
                _ => 0,
            }; ;
        }

        private static ushort CalcSize(InstructionAttribute attr)
        {
            var inSize  = GetDataLen(attr.Input);
            var outSize = GetDataLen(attr.Output);

            return (ushort)(1 + inSize + outSize);
        }

        // TODO: Yikes
        private static string PrettyName(InstructionAttribute attr, string methodName)
        {
            var ret = attr.Name != "" ? attr.Name : methodName;
            var outputStr = attr.Output == Data.None ? "" : attr.Output.ToString();
            var paramStr = attr.Input == Data.None ? "" : attr.Input.ToString();

            if (outputStr.StartsWith("Ind_"))
                outputStr = string.Concat("(", outputStr.AsSpan(4), ")");

            if (paramStr.StartsWith("Ind_"))
                paramStr = string.Concat("(", paramStr.AsSpan(4), ")");

            if (outputStr != "" && paramStr != "")
                return $"{ret} {outputStr}, {paramStr}";

            if (outputStr != "")
                return $"{ret} {outputStr}";

            if (paramStr != "")
                return $"{ret} {paramStr}";

            return ret;
        }
    }

    internal readonly Instruction[] instructions       = new Instruction[0x100];
    internal readonly Instruction[] prefixInstructions = new Instruction[0x100];

    private void LoadInstructions()
    {
        foreach (var method in GetType().GetMethods())
        {
            var attrs = method.GetCustomAttributes(typeof(InstructionAttribute), true);
            foreach (var a in attrs)
            {
                if (a is InstructionAttribute instr)
                {
                    var tbl = instr.Prefix ? prefixInstructions : instructions;
                    var tblName = instr.Prefix ? "prefix " : "";

                    if (tbl[instr.OPCode] != null)
                        throw new InvalidDataException($"Duplicate {tblName}opcode: '{instr.OPCode:X2}' registered twice.");

                    tbl[instr.OPCode] = new Instruction(this, instr, method);
                    Console.WriteLine($"Registering {tblName}opcode: {instr.OPCode:X2} -> {tbl[instr.OPCode].Name}");
                }
            }
        }
    }

    #region Instructions

    //
    // Misc
    //
    [Instruction(OPCode = 0x00, Cycles = 4)]
    [Instruction(OPCode = 0xDD, Cycles = 4)]
    public void NOP() { }

    [Instruction(OPCode = 0x76, Cycles = 4)]
    public void HALT()
    {
        if (IME)
            halted = true;
    }

    [Instruction(OPCode = 0x10, Cycles = 4)]
    public void STOP()
    {
        // If a button is being held
        if ((bus.Read(0xFF00) & 0x0F) != 0x0F)
        {
            // And there's no interrupts pending
            if ((IE & IF) == 0)
            {
                halted = true;
                PC++;
            }
        }
        else
        {
            if ((IE & IF) != 0)
            {
                PC++;
            }

            stopped = true;
            bus.Write(0xFF04, 0x00); // Reset DIV
        }
    }

    [Instruction(OPCode = 0xF3, Cycles = 4)]
    public void DI() => IME = false;

    [Instruction(OPCode = 0xFB, Cycles = 4)]
    public void EI() => IME = true;

    //
    // Load
    //
    [Instruction(OPCode = 0x08, Cycles = 20, Output = Data.Ind_Abs, Input = Data.SP)]

    [Instruction(OPCode = 0x01, Cycles = 12, Output = Data.BC,      Input = Data.Abs)]
    [Instruction(OPCode = 0x11, Cycles = 12, Output = Data.DE,      Input = Data.Abs)]
    [Instruction(OPCode = 0x21, Cycles = 12, Output = Data.HL,      Input = Data.Abs)]
    [Instruction(OPCode = 0x31, Cycles = 12, Output = Data.SP,      Input = Data.Abs)]

    [Instruction(OPCode = 0x02, Cycles = 8,  Output = Data.Ind_BC,  Input = Data.A)]
    [Instruction(OPCode = 0x12, Cycles = 8,  Output = Data.Ind_DE,  Input = Data.A)]
    [Instruction(OPCode = 0x22, Cycles = 8,  Output = Data.Inc_HL,  Input = Data.A)]
    [Instruction(OPCode = 0x32, Cycles = 8,  Output = Data.Dec_HL,  Input = Data.A)]

    [Instruction(OPCode = 0x06, Cycles = 8,  Output = Data.B,       Input = Data.Imm)]
    [Instruction(OPCode = 0x16, Cycles = 8,  Output = Data.D,       Input = Data.Imm)]
    [Instruction(OPCode = 0x26, Cycles = 8,  Output = Data.H,       Input = Data.Imm)]
    [Instruction(OPCode = 0x36, Cycles = 12, Output = Data.Ind_HL,  Input = Data.Imm)]

    [Instruction(OPCode = 0x0A, Cycles = 8,  Output = Data.A,       Input = Data.Ind_BC)]
    [Instruction(OPCode = 0x1A, Cycles = 8,  Output = Data.A,       Input = Data.Ind_DE)]
    [Instruction(OPCode = 0x2A, Cycles = 8,  Output = Data.A,       Input = Data.Inc_HL)]
    [Instruction(OPCode = 0x3A, Cycles = 8,  Output = Data.A,       Input = Data.Dec_HL)]

    [Instruction(OPCode = 0x0E, Cycles = 8,  Output = Data.C,       Input = Data.Imm)]
    [Instruction(OPCode = 0x1E, Cycles = 8,  Output = Data.E,       Input = Data.Imm)]
    [Instruction(OPCode = 0x2E, Cycles = 8,  Output = Data.L,       Input = Data.Imm)]
    [Instruction(OPCode = 0x3E, Cycles = 8,  Output = Data.A,       Input = Data.Imm)]

    [Instruction(OPCode = 0x40, Cycles = 4,  Output = Data.B,       Input = Data.B)]
    [Instruction(OPCode = 0x50, Cycles = 4,  Output = Data.D,       Input = Data.B)]
    [Instruction(OPCode = 0x60, Cycles = 4,  Output = Data.H,       Input = Data.B)]
    [Instruction(OPCode = 0x70, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.B)]

    [Instruction(OPCode = 0x41, Cycles = 4,  Output = Data.B,       Input = Data.C)]
    [Instruction(OPCode = 0x51, Cycles = 4,  Output = Data.D,       Input = Data.C)]
    [Instruction(OPCode = 0x61, Cycles = 4,  Output = Data.H,       Input = Data.C)]
    [Instruction(OPCode = 0x71, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.C)]

    [Instruction(OPCode = 0x42, Cycles = 4,  Output = Data.B,       Input = Data.D)]
    [Instruction(OPCode = 0x52, Cycles = 4,  Output = Data.D,       Input = Data.D)]
    [Instruction(OPCode = 0x62, Cycles = 4,  Output = Data.H,       Input = Data.D)]
    [Instruction(OPCode = 0x72, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.D)]

    [Instruction(OPCode = 0x43, Cycles = 4,  Output = Data.B,       Input = Data.E)]
    [Instruction(OPCode = 0x53, Cycles = 4,  Output = Data.D,       Input = Data.E)]
    [Instruction(OPCode = 0x63, Cycles = 4,  Output = Data.H,       Input = Data.E)]
    [Instruction(OPCode = 0x73, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.E)]

    [Instruction(OPCode = 0x44, Cycles = 4,  Output = Data.B,       Input = Data.H)]
    [Instruction(OPCode = 0x54, Cycles = 4,  Output = Data.D,       Input = Data.H)]
    [Instruction(OPCode = 0x64, Cycles = 4,  Output = Data.H,       Input = Data.H)]
    [Instruction(OPCode = 0x74, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.H)]

    [Instruction(OPCode = 0x45, Cycles = 4,  Output = Data.B,       Input = Data.L)]
    [Instruction(OPCode = 0x55, Cycles = 4,  Output = Data.D,       Input = Data.L)]
    [Instruction(OPCode = 0x65, Cycles = 4,  Output = Data.H,       Input = Data.L)]
    [Instruction(OPCode = 0x75, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.L)]

    [Instruction(OPCode = 0x46, Cycles = 8,  Output = Data.B,       Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x56, Cycles = 8,  Output = Data.D,       Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x66, Cycles = 8,  Output = Data.H,       Input = Data.Ind_HL)]

    [Instruction(OPCode = 0x47, Cycles = 4,  Output = Data.B,       Input = Data.A)]
    [Instruction(OPCode = 0x57, Cycles = 4,  Output = Data.D,       Input = Data.A)]
    [Instruction(OPCode = 0x67, Cycles = 4,  Output = Data.H,       Input = Data.A)]
    [Instruction(OPCode = 0x77, Cycles = 8,  Output = Data.Ind_HL,  Input = Data.A)]

    [Instruction(OPCode = 0x48, Cycles = 4,  Output = Data.C,       Input = Data.B)]
    [Instruction(OPCode = 0x58, Cycles = 4,  Output = Data.E,       Input = Data.B)]
    [Instruction(OPCode = 0x68, Cycles = 4,  Output = Data.L,       Input = Data.B)]
    [Instruction(OPCode = 0x78, Cycles = 4,  Output = Data.A,       Input = Data.B)]

    [Instruction(OPCode = 0x49, Cycles = 4,  Output = Data.C,       Input = Data.C)]
    [Instruction(OPCode = 0x59, Cycles = 4,  Output = Data.E,       Input = Data.C)]
    [Instruction(OPCode = 0x69, Cycles = 4,  Output = Data.L,       Input = Data.C)]
    [Instruction(OPCode = 0x79, Cycles = 4,  Output = Data.A,       Input = Data.C)]

    [Instruction(OPCode = 0x4A, Cycles = 4,  Output = Data.C,       Input = Data.D)]
    [Instruction(OPCode = 0x5A, Cycles = 4,  Output = Data.E,       Input = Data.D)]
    [Instruction(OPCode = 0x6A, Cycles = 4,  Output = Data.L,       Input = Data.D)]
    [Instruction(OPCode = 0x7A, Cycles = 4,  Output = Data.A,       Input = Data.D)]

    [Instruction(OPCode = 0x4B, Cycles = 4,  Output = Data.C,       Input = Data.E)]
    [Instruction(OPCode = 0x5B, Cycles = 4,  Output = Data.E,       Input = Data.E)]
    [Instruction(OPCode = 0x6B, Cycles = 4,  Output = Data.L,       Input = Data.E)]
    [Instruction(OPCode = 0x7B, Cycles = 4,  Output = Data.A,       Input = Data.E)]

    [Instruction(OPCode = 0x4C, Cycles = 4,  Output = Data.C,       Input = Data.H)]
    [Instruction(OPCode = 0x5C, Cycles = 4,  Output = Data.E,       Input = Data.H)]
    [Instruction(OPCode = 0x6C, Cycles = 4,  Output = Data.L,       Input = Data.H)]
    [Instruction(OPCode = 0x7C, Cycles = 4,  Output = Data.A,       Input = Data.H)]

    [Instruction(OPCode = 0x4D, Cycles = 4,  Output = Data.C,       Input = Data.L)]
    [Instruction(OPCode = 0x5D, Cycles = 4,  Output = Data.E,       Input = Data.L)]
    [Instruction(OPCode = 0x6D, Cycles = 4,  Output = Data.L,       Input = Data.L)]
    [Instruction(OPCode = 0x7D, Cycles = 4,  Output = Data.A,       Input = Data.L)]
    
    [Instruction(OPCode = 0x4E, Cycles = 8,  Output = Data.C,       Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x5E, Cycles = 8,  Output = Data.E,       Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x6E, Cycles = 8,  Output = Data.L,       Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x7E, Cycles = 8,  Output = Data.A,       Input = Data.Ind_HL)]

    [Instruction(OPCode = 0x4F, Cycles = 4,  Output = Data.C,       Input = Data.A)]
    [Instruction(OPCode = 0x5F, Cycles = 4,  Output = Data.E,       Input = Data.A)]
    [Instruction(OPCode = 0x6F, Cycles = 4,  Output = Data.L,       Input = Data.A)]
    [Instruction(OPCode = 0x7F, Cycles = 4,  Output = Data.A,       Input = Data.A)]

    [Instruction(OPCode = 0xE0, Cycles = 12, Output = Data.Ind_Imm, Input = Data.A)]
    [Instruction(OPCode = 0xF0, Cycles = 12, Output = Data.A,       Input = Data.Ind_Imm)]

    [Instruction(OPCode = 0xEA, Cycles = 16, Output = Data.Ind_Abs, Input = Data.A)]
    [Instruction(OPCode = 0xFA, Cycles = 16, Output = Data.A,       Input = Data.Ind_Abs)]

    [Instruction(OPCode = 0xE2, Cycles = 8,  Output = Data.Ind_C,   Input = Data.A)]
    [Instruction(OPCode = 0xF2, Cycles = 8,  Output = Data.A,       Input = Data.Ind_C)]

    [Instruction(OPCode = 0xF9, Cycles = 8,  Output = Data.SP,      Input = Data.HL)]
    [Instruction(OPCode = 0xF8, Cycles = 16, Output = Data.HL,      Input = Data.SP_Plus_Imm)]
    public void LD() => Write();

    //
    // Jumps & Subroutines
    //

    // Note Cycles is 12 here because jumping always adds 4, so total will be 16
    [Instruction(OPCode = 0xE9, Cycles = 12, Input = Data.HL)]
    [Instruction(OPCode = 0xC3, Cycles = 12, Input = Data.Abs)]
    public void JMP() => JmpImpl();

    [Instruction(OPCode = 0xC9, Cycles = 12, Condition = Conditional.Always)]
    [Instruction(OPCode = 0xC0, Cycles = 12, Condition = Conditional.NZ)]
    [Instruction(OPCode = 0xD0, Cycles = 12, Condition = Conditional.NC)]
    [Instruction(OPCode = 0xC8, Cycles = 12, Condition = Conditional.Z)]
    [Instruction(OPCode = 0xD8, Cycles = 12, Condition = Conditional.C)]
    public void RET() => RetImpl();

    [Instruction(OPCode = 0xD9, Cycles = 8, Condition = Conditional.Always)]
    public void RETI() { RetImpl(); IME = true; }

    [Instruction(OPCode = 0xC7, Name = "RST 00", Cycles = 12)]
    [Instruction(OPCode = 0xCF, Name = "RST 08", Cycles = 12)]
    [Instruction(OPCode = 0xD7, Name = "RST 10", Cycles = 12)]
    [Instruction(OPCode = 0xDF, Name = "RST 18", Cycles = 12)]
    [Instruction(OPCode = 0xE7, Name = "RST 20", Cycles = 12)]
    [Instruction(OPCode = 0xEF, Name = "RST 28", Cycles = 12)]
    [Instruction(OPCode = 0xF7, Name = "RST 30", Cycles = 12)]
    [Instruction(OPCode = 0xFF, Name = "RST 38", Cycles = 12)]
    public void RST() => RstImpl();

    [Instruction(OPCode = 0xCD, Cycles = 20, Condition = Conditional.Always, Input = Data.Abs)]
    [Instruction(OPCode = 0xCC, Cycles =  8, Condition = Conditional.Z,      Input = Data.Abs)]
    [Instruction(OPCode = 0xDC, Cycles =  8, Condition = Conditional.C,      Input = Data.Abs)]
    [Instruction(OPCode = 0xC4, Cycles =  8, Condition = Conditional.NZ,     Input = Data.Abs)]
    [Instruction(OPCode = 0xD4, Cycles =  8, Condition = Conditional.NC,     Input = Data.Abs)]
    public void CALL() => CallImpl();

    [Instruction(OPCode = 0xCA, Name = "JMP Z",  Cycles = 12, Condition = Conditional.Z,  Input = Data.Abs)]
    [Instruction(OPCode = 0xC2, Name = "JMP NZ", Cycles = 12, Condition = Conditional.NZ, Input = Data.Abs)]
    [Instruction(OPCode = 0xDA, Name = "JMP C",  Cycles = 12, Condition = Conditional.C,  Input = Data.Abs)]
    [Instruction(OPCode = 0xD2, Name = "JMP NC", Cycles = 12, Condition = Conditional.NC, Input = Data.Abs)]
    [Instruction(OPCode = 0x28, Name = "JR Z",   Cycles = 8,  Condition = Conditional.Z,  Input = Data.Imm)]
    [Instruction(OPCode = 0x20, Name = "JR NZ",  Cycles = 8,  Condition = Conditional.NZ, Input = Data.Imm)]
    [Instruction(OPCode = 0x38, Name = "JR C",   Cycles = 8,  Condition = Conditional.C,  Input = Data.Imm)]
    [Instruction(OPCode = 0x30, Name = "JR NC",  Cycles = 8,  Condition = Conditional.NC, Input = Data.Imm)]

    [Instruction(OPCode = 0x18, Name = "JR",     Cycles = 8,  Condition = Conditional.Always, Input = Data.Imm)]
    public void JR() => JmpCondImpl();

    [Instruction(OPCode = 0x37, Cycles = 4)]
    public void SCF() { flags.C = true; flags.H = false; flags.N = false; }

    [Instruction(OPCode = 0x3F, Cycles = 4)]
    public void CCF() { flags.C = !flags.C; flags.H = false; flags.N = false; }

    //
    // Stack
    //

    [Instruction(OPCode = 0xC1, Cycles = 12, Output = Data.BC)]
    [Instruction(OPCode = 0xD1, Cycles = 12, Output = Data.DE)]
    [Instruction(OPCode = 0xE1, Cycles = 12, Output = Data.HL)]
    [Instruction(OPCode = 0xF1, Cycles = 12, Output = Data.AF)]
    public void POP() => WriteWord(PopWord());

    [Instruction(OPCode = 0xC5, Cycles = 16, Input = Data.BC)]
    [Instruction(OPCode = 0xD5, Cycles = 16, Input = Data.DE)]
    [Instruction(OPCode = 0xE5, Cycles = 16, Input = Data.HL)]
    [Instruction(OPCode = 0xF5, Cycles = 16, Input = Data.AF)]
    public void PUSH() => PushWord(InWord());

    //
    // Arithmetic
    //
    [Instruction(OPCode = 0x27, Cycles = 4, Output = Data.A)]
    public void DAA() => DaaImpl();

    [Instruction(OPCode = 0x09, Name = "Add", Cycles = 8, Output = Data.HL, Input = Data.BC)]
    [Instruction(OPCode = 0x19, Name = "Add", Cycles = 8, Output = Data.HL, Input = Data.DE)]
    [Instruction(OPCode = 0x29, Name = "Add", Cycles = 8, Output = Data.HL, Input = Data.HL)]
    [Instruction(OPCode = 0x39, Name = "Add", Cycles = 8, Output = Data.HL, Input = Data.SP)]
    public void ADD_16() => Add16Impl();

    [Instruction(OPCode = 0xE8, Name = "Add", Cycles = 16, Output = Data.SP, Input = Data.Imm)]
    public void ADD_SP_Imm() => AddSPImmImpl();

    [Instruction(OPCode = 0x80, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x81, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x82, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x83, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x84, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x85, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x86, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x87, Cycles = 4, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xC6, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void ADD() => AddImpl(false);

    [Instruction(OPCode = 0x88, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x89, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x8A, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x8B, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x8C, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x8D, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x8E, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x8F, Cycles = 4, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xCE, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void ADC() => AddImpl(true);

    [Instruction(OPCode = 0x90, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x91, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x92, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x93, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x94, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x95, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x96, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x97, Cycles = 4, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xD6, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void SUB() => SubImpl(false);

    [Instruction(OPCode = 0x98, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x99, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x9A, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x9B, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x9C, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x9D, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x9E, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x9F, Cycles = 4, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xDE, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void SBC() => SubImpl(true);

    [Instruction(OPCode = 0xB8, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0xB9, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0xBA, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0xBB, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0xBC, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0xBD, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0xBE, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0xBF, Cycles = 4, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xFE, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void CP() => CpImpl();

    [Instruction(OPCode = 0xA8, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0xA9, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0xAA, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0xAB, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0xAC, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0xAD, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0xAE, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0xAF, Cycles = 4, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xEE, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void XOR() => XorImpl();

    [Instruction(OPCode = 0xB0, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0xB1, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0xB2, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0xB3, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0xB4, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0xB5, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0xB6, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0xB7, Cycles = 8, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xF6, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void OR() => OrImpl();

    [Instruction(OPCode = 0xA0, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0xA1, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0xA2, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0xA3, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0xA4, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0xA5, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0xA6, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0xA7, Cycles = 8, Output = Data.A, Input = Data.A)]
    [Instruction(OPCode = 0xE6, Cycles = 8, Output = Data.A, Input = Data.Imm)]
    public void AND() => AndImpl();

    [Instruction(OPCode = 0x05, Cycles = 4,  Input = Data.B)]
    [Instruction(OPCode = 0x15, Cycles = 4,  Input = Data.D)]
    [Instruction(OPCode = 0x25, Cycles = 4,  Input = Data.H)]
    [Instruction(OPCode = 0x35, Cycles = 12, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x0D, Cycles = 4,  Input = Data.C)]
    [Instruction(OPCode = 0x1D, Cycles = 4,  Input = Data.E)]
    [Instruction(OPCode = 0x2D, Cycles = 4,  Input = Data.L)]
    [Instruction(OPCode = 0x3D, Cycles = 4,  Input = Data.A)]
    [Instruction(OPCode = 0x0B, Cycles = 8,  Input = Data.BC)]
    [Instruction(OPCode = 0x1B, Cycles = 8,  Input = Data.DE)]
    [Instruction(OPCode = 0x2B, Cycles = 8,  Input = Data.HL)]
    [Instruction(OPCode = 0x3B, Cycles = 8,  Input = Data.SP)]
    public void DEC() => DecImpl();

    [Instruction(OPCode = 0x04, Cycles = 4,  Input = Data.B)]
    [Instruction(OPCode = 0x14, Cycles = 4,  Input = Data.D)]
    [Instruction(OPCode = 0x24, Cycles = 4,  Input = Data.H)]
    [Instruction(OPCode = 0x34, Cycles = 12, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x0C, Cycles = 4,  Input = Data.C)]
    [Instruction(OPCode = 0x1C, Cycles = 4,  Input = Data.E)]
    [Instruction(OPCode = 0x2C, Cycles = 4,  Input = Data.L)]
    [Instruction(OPCode = 0x3C, Cycles = 4,  Input = Data.A)]
    [Instruction(OPCode = 0x03, Cycles = 8,  Input = Data.BC)]
    [Instruction(OPCode = 0x13, Cycles = 8,  Input = Data.DE)]
    [Instruction(OPCode = 0x23, Cycles = 8,  Input = Data.HL)]
    [Instruction(OPCode = 0x33, Cycles = 8,  Input = Data.SP)]
    public void INC() => IncImpl();

    [Instruction(Prefix = true, OPCode = 0x08, Cycles = 4, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x09, Cycles = 4, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x0A, Cycles = 4, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x0B, Cycles = 4, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x0C, Cycles = 4, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x0D, Cycles = 4, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x0E, Cycles = 12, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x0F, Cycles = 4, Input = Data.A, Output = Data.A)]
    [Instruction(OPCode = 0x0F, Cycles = 4, Input = Data.A, Output = Data.A)]
    public void RRC() => RrImpl(false, false, true);

    [Instruction(Prefix = true, OPCode = 0x18, Cycles = 4, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x19, Cycles = 4, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x1A, Cycles = 4, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x1B, Cycles = 4, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x1C, Cycles = 4, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x1D, Cycles = 4, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x1E, Cycles = 12, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x1F, Cycles = 4, Input = Data.A, Output = Data.A)]
    [Instruction(               OPCode = 0x1F, Cycles = 4, Input = Data.A, Output = Data.A)]
    public void RR() => RrImpl(true, false, false);

    [Instruction(Prefix = true, OPCode = 0x00, Cycles = 4, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x01, Cycles = 4, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x02, Cycles = 4, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x03, Cycles = 4, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x04, Cycles = 4, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x05, Cycles = 4, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x06, Cycles = 12, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x07, Cycles = 4, Input = Data.A, Output = Data.A)]
    [Instruction(               OPCode = 0x07, Cycles = 4, Input = Data.A, Output = Data.A)]
    public void RLC() => RlImpl(false, true);

    [Instruction(Prefix = true, OPCode = 0x10, Cycles = 4, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x11, Cycles = 4, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x12, Cycles = 4, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x13, Cycles = 4, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x14, Cycles = 4, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x15, Cycles = 4, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x16, Cycles = 12, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x17, Cycles = 4, Input = Data.A, Output = Data.A)]
    [Instruction(OPCode = 0x17, Cycles = 4, Input = Data.A, Output = Data.A)]
    public void RL() => RlImpl(true);

    [Instruction(Prefix = true, OPCode = 0x20, Cycles = 8, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x21, Cycles = 8, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x22, Cycles = 8, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x23, Cycles = 8, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x24, Cycles = 8, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x25, Cycles = 8, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x26, Cycles = 16, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x27, Cycles = 8, Input = Data.A, Output = Data.A)]
    public void SLA() => SlaImpl();

    [Instruction(Prefix = true, OPCode = 0x28, Cycles = 8, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x29, Cycles = 8, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x2A, Cycles = 8, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x2B, Cycles = 8, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x2C, Cycles = 8, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x2D, Cycles = 8, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x2E, Cycles = 16, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x2F, Cycles = 8, Input = Data.A, Output = Data.A)]
    public void SRA() => SraImpl();

    [Instruction(Prefix = true, OPCode = 0x38, Cycles = 8, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x39, Cycles = 8, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x3A, Cycles = 8, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x3B, Cycles = 8, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x3C, Cycles = 8, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x3D, Cycles = 8, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x3E, Cycles = 16, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x3F, Cycles = 8, Input = Data.A, Output = Data.A)]
    public void SRL() => SrlImpl();

    [Instruction(Prefix = true, OPCode = 0x30, Cycles = 4, Input = Data.B, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x31, Cycles = 4, Input = Data.C, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x32, Cycles = 4, Input = Data.D, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x33, Cycles = 4, Input = Data.E, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x34, Cycles = 4, Input = Data.H, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x35, Cycles = 4, Input = Data.L, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x36, Cycles = 12, Input = Data.Ind_HL, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x37, Cycles = 4, Input = Data.A, Output = Data.A)]
    public void SWAP() => SwapImpl(false);

    [Instruction(OPCode = 0x2F, Cycles = 8, Output = Data.A)]
    public void CPL() => CplImpl();

    //
    // Bit
    //

    [Instruction(Prefix = true, OPCode = 0x40, Cycles = 4, Input = Data.Bit0, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x48, Cycles = 4, Input = Data.Bit1, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x50, Cycles = 4, Input = Data.Bit2, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x58, Cycles = 4, Input = Data.Bit3, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x60, Cycles = 4, Input = Data.Bit4, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x68, Cycles = 4, Input = Data.Bit5, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x70, Cycles = 4, Input = Data.Bit6, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x78, Cycles = 4, Input = Data.Bit7, Output = Data.B)]

    [Instruction(Prefix = true, OPCode = 0x41, Cycles = 4, Input = Data.Bit0, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x49, Cycles = 4, Input = Data.Bit1, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x51, Cycles = 4, Input = Data.Bit2, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x59, Cycles = 4, Input = Data.Bit3, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x61, Cycles = 4, Input = Data.Bit4, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x69, Cycles = 4, Input = Data.Bit5, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x71, Cycles = 4, Input = Data.Bit6, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x79, Cycles = 4, Input = Data.Bit7, Output = Data.C)]

    [Instruction(Prefix = true, OPCode = 0x42, Cycles = 4, Input = Data.Bit0, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x4A, Cycles = 4, Input = Data.Bit1, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x52, Cycles = 4, Input = Data.Bit2, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x5A, Cycles = 4, Input = Data.Bit3, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x62, Cycles = 4, Input = Data.Bit4, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x6A, Cycles = 4, Input = Data.Bit5, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x72, Cycles = 4, Input = Data.Bit6, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x7A, Cycles = 4, Input = Data.Bit7, Output = Data.D)]

    [Instruction(Prefix = true, OPCode = 0x43, Cycles = 4, Input = Data.Bit0, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x4B, Cycles = 4, Input = Data.Bit1, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x53, Cycles = 4, Input = Data.Bit2, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x5B, Cycles = 4, Input = Data.Bit3, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x63, Cycles = 4, Input = Data.Bit4, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x6B, Cycles = 4, Input = Data.Bit5, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x73, Cycles = 4, Input = Data.Bit6, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x7B, Cycles = 4, Input = Data.Bit7, Output = Data.E)]

    [Instruction(Prefix = true, OPCode = 0x44, Cycles = 4, Input = Data.Bit0, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x4C, Cycles = 4, Input = Data.Bit1, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x54, Cycles = 4, Input = Data.Bit2, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x5C, Cycles = 4, Input = Data.Bit3, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x64, Cycles = 4, Input = Data.Bit4, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x6C, Cycles = 4, Input = Data.Bit5, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x74, Cycles = 4, Input = Data.Bit6, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x7C, Cycles = 4, Input = Data.Bit7, Output = Data.H)]

    [Instruction(Prefix = true, OPCode = 0x45, Cycles = 4, Input = Data.Bit0, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x4D, Cycles = 4, Input = Data.Bit1, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x55, Cycles = 4, Input = Data.Bit2, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x5D, Cycles = 4, Input = Data.Bit3, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x65, Cycles = 4, Input = Data.Bit4, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x6D, Cycles = 4, Input = Data.Bit5, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x75, Cycles = 4, Input = Data.Bit6, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x7D, Cycles = 4, Input = Data.Bit7, Output = Data.L)]

    [Instruction(Prefix = true, OPCode = 0x46, Cycles = 12, Input = Data.Bit0, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x4E, Cycles = 12, Input = Data.Bit1, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x56, Cycles = 12, Input = Data.Bit2, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x5E, Cycles = 12, Input = Data.Bit3, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x66, Cycles = 12, Input = Data.Bit4, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x6E, Cycles = 12, Input = Data.Bit5, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x76, Cycles = 12, Input = Data.Bit6, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x7E, Cycles = 12, Input = Data.Bit7, Output = Data.Ind_HL)]

    [Instruction(Prefix = true, OPCode = 0x47, Cycles = 4, Input = Data.Bit0, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x4F, Cycles = 4, Input = Data.Bit1, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x57, Cycles = 4, Input = Data.Bit2, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x5F, Cycles = 4, Input = Data.Bit3, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x67, Cycles = 4, Input = Data.Bit4, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x6F, Cycles = 4, Input = Data.Bit5, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x77, Cycles = 4, Input = Data.Bit6, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x7F, Cycles = 4, Input = Data.Bit7, Output = Data.A)]
    public void BIT() => BitImpl();

    [Instruction(Prefix = true, OPCode = 0x80, Cycles = 4, Input = Data.Bit0, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x88, Cycles = 4, Input = Data.Bit1, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x90, Cycles = 4, Input = Data.Bit2, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0x98, Cycles = 4, Input = Data.Bit3, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xA0, Cycles = 4, Input = Data.Bit4, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xA8, Cycles = 4, Input = Data.Bit5, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xB0, Cycles = 4, Input = Data.Bit6, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xB8, Cycles = 4, Input = Data.Bit7, Output = Data.B)]

    [Instruction(Prefix = true, OPCode = 0x81, Cycles = 4, Input = Data.Bit0, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x89, Cycles = 4, Input = Data.Bit1, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x91, Cycles = 4, Input = Data.Bit2, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0x99, Cycles = 4, Input = Data.Bit3, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xA1, Cycles = 4, Input = Data.Bit4, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xA9, Cycles = 4, Input = Data.Bit5, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xB1, Cycles = 4, Input = Data.Bit6, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xB9, Cycles = 4, Input = Data.Bit7, Output = Data.C)]

    [Instruction(Prefix = true, OPCode = 0x82, Cycles = 4, Input = Data.Bit0, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x8A, Cycles = 4, Input = Data.Bit1, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x92, Cycles = 4, Input = Data.Bit2, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0x9A, Cycles = 4, Input = Data.Bit3, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xA2, Cycles = 4, Input = Data.Bit4, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xAA, Cycles = 4, Input = Data.Bit5, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xB2, Cycles = 4, Input = Data.Bit6, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xBA, Cycles = 4, Input = Data.Bit7, Output = Data.D)]

    [Instruction(Prefix = true, OPCode = 0x83, Cycles = 4, Input = Data.Bit0, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x8B, Cycles = 4, Input = Data.Bit1, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x93, Cycles = 4, Input = Data.Bit2, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0x9B, Cycles = 4, Input = Data.Bit3, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xA3, Cycles = 4, Input = Data.Bit4, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xAB, Cycles = 4, Input = Data.Bit5, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xB3, Cycles = 4, Input = Data.Bit6, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xBB, Cycles = 4, Input = Data.Bit7, Output = Data.E)]

    [Instruction(Prefix = true, OPCode = 0x84, Cycles = 4, Input = Data.Bit0, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x8C, Cycles = 4, Input = Data.Bit1, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x94, Cycles = 4, Input = Data.Bit2, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0x9C, Cycles = 4, Input = Data.Bit3, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xA4, Cycles = 4, Input = Data.Bit4, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xAC, Cycles = 4, Input = Data.Bit5, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xB4, Cycles = 4, Input = Data.Bit6, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xBC, Cycles = 4, Input = Data.Bit7, Output = Data.H)]

    [Instruction(Prefix = true, OPCode = 0x85, Cycles = 4, Input = Data.Bit0, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x8D, Cycles = 4, Input = Data.Bit1, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x95, Cycles = 4, Input = Data.Bit2, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0x9D, Cycles = 4, Input = Data.Bit3, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xA5, Cycles = 4, Input = Data.Bit4, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xAD, Cycles = 4, Input = Data.Bit5, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xB5, Cycles = 4, Input = Data.Bit6, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xBD, Cycles = 4, Input = Data.Bit7, Output = Data.L)]

    [Instruction(Prefix = true, OPCode = 0x86, Cycles = 12, Input = Data.Bit0, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x8E, Cycles = 12, Input = Data.Bit1, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x96, Cycles = 12, Input = Data.Bit2, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0x9E, Cycles = 12, Input = Data.Bit3, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xA6, Cycles = 12, Input = Data.Bit4, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xAE, Cycles = 12, Input = Data.Bit5, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xB6, Cycles = 12, Input = Data.Bit6, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xBE, Cycles = 12, Input = Data.Bit7, Output = Data.Ind_HL)]

    [Instruction(Prefix = true, OPCode = 0x87, Cycles = 4, Input = Data.Bit0, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x8F, Cycles = 4, Input = Data.Bit1, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x97, Cycles = 4, Input = Data.Bit2, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0x9F, Cycles = 4, Input = Data.Bit3, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xA7, Cycles = 4, Input = Data.Bit4, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xAF, Cycles = 4, Input = Data.Bit5, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xB7, Cycles = 4, Input = Data.Bit6, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xBF, Cycles = 4, Input = Data.Bit7, Output = Data.A)]
    public void RES() => ResBitImpl();

    [Instruction(Prefix = true, OPCode = 0xC0, Cycles = 4, Input = Data.Bit0, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xC8, Cycles = 4, Input = Data.Bit1, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xD0, Cycles = 4, Input = Data.Bit2, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xD8, Cycles = 4, Input = Data.Bit3, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xE0, Cycles = 4, Input = Data.Bit4, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xE8, Cycles = 4, Input = Data.Bit5, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xF0, Cycles = 4, Input = Data.Bit6, Output = Data.B)]
    [Instruction(Prefix = true, OPCode = 0xF8, Cycles = 4, Input = Data.Bit7, Output = Data.B)]

    [Instruction(Prefix = true, OPCode = 0xC1, Cycles = 4, Input = Data.Bit0, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xC9, Cycles = 4, Input = Data.Bit1, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xD1, Cycles = 4, Input = Data.Bit2, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xD9, Cycles = 4, Input = Data.Bit3, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xE1, Cycles = 4, Input = Data.Bit4, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xE9, Cycles = 4, Input = Data.Bit5, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xF1, Cycles = 4, Input = Data.Bit6, Output = Data.C)]
    [Instruction(Prefix = true, OPCode = 0xF9, Cycles = 4, Input = Data.Bit7, Output = Data.C)]

    [Instruction(Prefix = true, OPCode = 0xC2, Cycles = 4, Input = Data.Bit0, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xCA, Cycles = 4, Input = Data.Bit1, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xD2, Cycles = 4, Input = Data.Bit2, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xDA, Cycles = 4, Input = Data.Bit3, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xE2, Cycles = 4, Input = Data.Bit4, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xEA, Cycles = 4, Input = Data.Bit5, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xF2, Cycles = 4, Input = Data.Bit6, Output = Data.D)]
    [Instruction(Prefix = true, OPCode = 0xFA, Cycles = 4, Input = Data.Bit7, Output = Data.D)]

    [Instruction(Prefix = true, OPCode = 0xC3, Cycles = 4, Input = Data.Bit0, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xCB, Cycles = 4, Input = Data.Bit1, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xD3, Cycles = 4, Input = Data.Bit2, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xDB, Cycles = 4, Input = Data.Bit3, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xE3, Cycles = 4, Input = Data.Bit4, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xEB, Cycles = 4, Input = Data.Bit5, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xF3, Cycles = 4, Input = Data.Bit6, Output = Data.E)]
    [Instruction(Prefix = true, OPCode = 0xFB, Cycles = 4, Input = Data.Bit7, Output = Data.E)]

    [Instruction(Prefix = true, OPCode = 0xC4, Cycles = 4, Input = Data.Bit0, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xCC, Cycles = 4, Input = Data.Bit1, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xD4, Cycles = 4, Input = Data.Bit2, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xDC, Cycles = 4, Input = Data.Bit3, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xE4, Cycles = 4, Input = Data.Bit4, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xEC, Cycles = 4, Input = Data.Bit5, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xF4, Cycles = 4, Input = Data.Bit6, Output = Data.H)]
    [Instruction(Prefix = true, OPCode = 0xFC, Cycles = 4, Input = Data.Bit7, Output = Data.H)]

    [Instruction(Prefix = true, OPCode = 0xC5, Cycles = 4, Input = Data.Bit0, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xCD, Cycles = 4, Input = Data.Bit1, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xD5, Cycles = 4, Input = Data.Bit2, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xDD, Cycles = 4, Input = Data.Bit3, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xE5, Cycles = 4, Input = Data.Bit4, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xED, Cycles = 4, Input = Data.Bit5, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xF5, Cycles = 4, Input = Data.Bit6, Output = Data.L)]
    [Instruction(Prefix = true, OPCode = 0xFD, Cycles = 4, Input = Data.Bit7, Output = Data.L)]

    [Instruction(Prefix = true, OPCode = 0xC6, Cycles = 12, Input = Data.Bit0, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xCE, Cycles = 12, Input = Data.Bit1, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xD6, Cycles = 12, Input = Data.Bit2, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xDE, Cycles = 12, Input = Data.Bit3, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xE6, Cycles = 12, Input = Data.Bit4, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xEE, Cycles = 12, Input = Data.Bit5, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xF6, Cycles = 12, Input = Data.Bit6, Output = Data.Ind_HL)]
    [Instruction(Prefix = true, OPCode = 0xFE, Cycles = 12, Input = Data.Bit7, Output = Data.Ind_HL)]

    [Instruction(Prefix = true, OPCode = 0xC7, Cycles = 4, Input = Data.Bit0, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xCF, Cycles = 4, Input = Data.Bit1, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xD7, Cycles = 4, Input = Data.Bit2, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xDF, Cycles = 4, Input = Data.Bit3, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xE7, Cycles = 4, Input = Data.Bit4, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xEF, Cycles = 4, Input = Data.Bit5, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xF7, Cycles = 4, Input = Data.Bit6, Output = Data.A)]
    [Instruction(Prefix = true, OPCode = 0xFF, Cycles = 4, Input = Data.Bit7, Output = Data.A)]
    public void SET() => SetBitImpl();

    #endregion
}
