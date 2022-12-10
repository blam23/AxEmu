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
    }

    internal enum Conditional
    {
        None,
        Z, NZ,
        C, NC
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
    }

    internal class Instruction
    {
        public string Name = "";
        public int Cycles;
        public ushort Size;
        public Data Param = Data.None;
        public Data Output = Data.None;
        public Conditional Condition = Conditional.None;

        public Action action;

        public Instruction(CPU cpu, InstructionAttribute attr, System.Reflection.MethodInfo method)
        {
            Name      = PrettyName(attr, method.Name);
            Size      = CalcSize(attr);
            Cycles    = attr.Cycles;
            Param     = attr.Input;
            Output    = attr.Output;
            Condition = attr.Condition;

            action = (Action)Delegate.CreateDelegate(typeof(Action), cpu, method);
        }

        private static ushort CalcSize(InstructionAttribute attr)
        {
            return attr.Input switch
            {
                Data.Imm => 2,
                Data.Ind_Imm => 2,
                Data.Abs => 3,
                _ => 1,
            };
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

    internal readonly Instruction[] instructions = new Instruction[0x100];
    internal readonly Instruction[] prefix_instr = new Instruction[0x100];

    private void LoadInstructions()
    {
        foreach (var method in GetType().GetMethods())
        {
            var attrs = method.GetCustomAttributes(typeof(InstructionAttribute), true);
            foreach (var a in attrs)
            {
                if (a is InstructionAttribute instr)
                {
                    instructions[instr.OPCode] = new Instruction(this, instr, method);
                    Console.WriteLine($"Registering OPCode: {instr.OPCode:X2} -> {instructions[instr.OPCode].Name}");
                }
            }
        }
    }

    #region Instructions

    //
    // Misc
    //
    [Instruction(OPCode = 0x00, Cycles = 4)]
    public void NOP() { }

    [Instruction(OPCode = 0x76, Cycles = 4)]
    public void HALT() 
    { 
        // TODO: HALT
    }

    [Instruction(OPCode = 0xF3, Cycles = 4)]
    public void DI() => IME = false;

    [Instruction(OPCode = 0xFB, Cycles = 4)]
    public void EI() => IME = true;

    //
    // Load
    //
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
    public void LD() => Write();

    //
    // Jumps & Subroutines
    //

    // Note Cycles is 12 here because jumping always adds 4, so total will be 16
    [Instruction(OPCode = 0xC3, Cycles = 12, Input = Data.Abs)]
    public void JMP() => JmpImpl();

    [Instruction(OPCode = 0xCA, Name = "JMP Z",  Cycles = 12, Condition = Conditional.Z,  Input = Data.Abs)]
    [Instruction(OPCode = 0xC2, Name = "JMP NZ", Cycles = 12, Condition = Conditional.NZ, Input = Data.Abs)]
    [Instruction(OPCode = 0xDA, Name = "JMP C",  Cycles = 12, Condition = Conditional.C,  Input = Data.Abs)]
    [Instruction(OPCode = 0xD2, Name = "JMP NC", Cycles = 12, Condition = Conditional.NC, Input = Data.Abs)]
    [Instruction(OPCode = 0x28, Name = "JR Z",   Cycles = 8,  Condition = Conditional.Z,  Input = Data.Imm)]
    [Instruction(OPCode = 0x20, Name = "JR NZ",  Cycles = 8,  Condition = Conditional.NZ, Input = Data.Imm)]
    [Instruction(OPCode = 0x38, Name = "JR C",   Cycles = 8,  Condition = Conditional.C,  Input = Data.Imm)]
    [Instruction(OPCode = 0x30, Name = "JR NC",  Cycles = 8,  Condition = Conditional.NC, Input = Data.Imm)]
    public void JR() => JmpCondImpl();

    //
    // Stack
    //

    //
    // Arithmetic (8-bit)
    //
    [Instruction(OPCode = 0x80, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x81, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x82, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x83, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x84, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x85, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x86, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x87, Cycles = 4, Output = Data.A, Input = Data.A)]
    public void ADD() => AddImpl(false);

    [Instruction(OPCode = 0x88, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x89, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x8A, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x8B, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x8C, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x8D, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x8E, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x8F, Cycles = 4, Output = Data.A, Input = Data.A)]
    public void ADC() => AddImpl(true);

    [Instruction(OPCode = 0x90, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x91, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x92, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x93, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x94, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x95, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x96, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x97, Cycles = 4, Output = Data.A, Input = Data.A)]
    public void SUB() => SubImpl(false);

    [Instruction(OPCode = 0x98, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0x99, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0x9A, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0x9B, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0x9C, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0x9D, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0x9E, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0x9F, Cycles = 4, Output = Data.A, Input = Data.A)]
    public void SBC() => SubImpl(true);

    [Instruction(OPCode = 0xA8, Cycles = 4, Output = Data.A, Input = Data.B)]
    [Instruction(OPCode = 0xA9, Cycles = 4, Output = Data.A, Input = Data.C)]
    [Instruction(OPCode = 0xAA, Cycles = 4, Output = Data.A, Input = Data.D)]
    [Instruction(OPCode = 0xAB, Cycles = 4, Output = Data.A, Input = Data.E)]
    [Instruction(OPCode = 0xAC, Cycles = 4, Output = Data.A, Input = Data.H)]
    [Instruction(OPCode = 0xAD, Cycles = 4, Output = Data.A, Input = Data.L)]
    [Instruction(OPCode = 0xAE, Cycles = 8, Output = Data.A, Input = Data.Ind_HL)]
    [Instruction(OPCode = 0xAF, Cycles = 4, Output = Data.A, Input = Data.A)]
    public void XOR() => XorImpl();

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

    //
    // Arithmetic (16-bit)
    //

    //
    // Bit
    //

    //
    // Bitshift
    //

    #endregion
}
