using static AxEmu.NES.CPU;

namespace AxEmu.NES
{
    internal class Debug
    {
        private static byte NextByte(System system)
        {
            return system.memory.Read((ushort)(system.cpu.pc + 1));
        }

        private static ushort NextWord(System system)
        {
            return system.memory.ReadWord((ushort)(system.cpu.pc + 1));
        }

        private static Dictionary<string, string> prettyAddrs = new()
        {
            { "$2000", "PPU_CTRL" },
            { "$2001", "PPU_MASK" },
            { "$2002", "PPU_STATUS" },
            { "$2003", "PPU_OAM_ADDR" },
            { "$2005", "PPU_SCROLL" },
            { "$2006", "PPU_ADDRESS" },
            { "$2007", "PPU_DATA" },

            { "$4015", "APU_STATUS" },
            { "$4017", "APU_FRAME_COUNTER" },

        };

        private static string PrettyAddress(string addr)
        {
            if (prettyAddrs.TryGetValue(addr, out var pretty))
                return $"{addr} ({pretty})";

            return addr;
        }

        private static string CalcName(System system, string instr, Mode addrMode = Mode.None)
        {
            var addr = addrMode switch
            {
                Mode.IMM => $"#${NextByte(system):X2}",
                Mode.REL => $"${(2+system.cpu.pc+(sbyte)NextByte(system)):X4}",
                Mode.ZP => $"${NextByte(system):X4}",
                Mode.ZPX => $"${NextByte(system):X4}, x({system.cpu.x:X2})",
                Mode.ZPY => $"${NextByte(system):X4}, y({system.cpu.y:X2})",
                Mode.INDX => $"$({NextByte(system):X4}, x({system.cpu.x:X2}))",
                Mode.INDY => $"$({NextByte(system):X4}, y({system.cpu.y:X2}))",
                Mode.ABS => $"${NextWord(system):X4}",
                Mode.ABSX => $"${NextWord(system):X4}, x({system.cpu.x:X2})",
                Mode.ABSY => $"${NextWord(system):X4}, y({system.cpu.y:X2})",
                _ => ""
            };

            addr = PrettyAddress(addr);

            return $"{instr} {addr}";
        }

        public static readonly Dictionary<ushort, Func<System, string>> OpCodeNames = new()
        {
            //
            // MOVE
            //

            // LDA
            { 0xA9, (c) => CalcName(c, "LDA", Mode.IMM) },
            { 0xA5, (c) => CalcName(c, "LDA", Mode.ZP) },
            { 0xB5, (c) => CalcName(c, "LDA", Mode.ZPX) },
            { 0xA1, (c) => CalcName(c, "LDA", Mode.INDX) },
            { 0xB1, (c) => CalcName(c, "LDA", Mode.INDY) },
            { 0xAD, (c) => CalcName(c, "LDA", Mode.ABS) },
            { 0xBD, (c) => CalcName(c, "LDA", Mode.ABSX) },
            { 0xB9, (c) => CalcName(c, "LDA", Mode.ABSY) },

            // LDX
            { 0xA2, (c) => CalcName(c, "LDX", Mode.IMM) },
            { 0xA6, (c) => CalcName(c, "LDX", Mode.ZP) },
            { 0xB6, (c) => CalcName(c, "LDX", Mode.ZPY) },
            { 0xAE, (c) => CalcName(c, "LDX", Mode.ABS) },
            { 0xBE, (c) => CalcName(c, "LDX", Mode.ABSY) },

            // LDY
            { 0xA0, (c) => CalcName(c, "LDY", Mode.IMM) },
            { 0xA4, (c) => CalcName(c, "LDY", Mode.ZP) },
            { 0xB4, (c) => CalcName(c, "LDY", Mode.ZPX) },
            { 0xAC, (c) => CalcName(c, "LDY", Mode.ABS) },
            { 0xBC, (c) => CalcName(c, "LDY", Mode.ABSX) },

            // STA
            { 0x85, (c) => CalcName(c, "STA", Mode.ZP) },
            { 0x95, (c) => CalcName(c, "STA", Mode.ZPX) },
            { 0x81, (c) => CalcName(c, "STA", Mode.INDX) },
            { 0x91, (c) => CalcName(c, "STA", Mode.INDY) },
            { 0x8D, (c) => CalcName(c, "STA", Mode.ABS) },
            { 0x9D, (c) => CalcName(c, "STA", Mode.ABSX) },
            { 0x99, (c) => CalcName(c, "STA", Mode.ABSY) },

            // STX
            { 0x86, (c) => CalcName(c, "STX", Mode.ZP) },
            { 0x96, (c) => CalcName(c, "STX", Mode.ZPY) },
            { 0x8E, (c) => CalcName(c, "STX", Mode.ABS) },

            // STY
            { 0x84, (c) => CalcName(c, "STY", Mode.ZP) },
            { 0x94, (c) => CalcName(c, "STY", Mode.ZPX) },
            { 0x8C, (c) => CalcName(c, "STY", Mode.ABS) },

            // Transfers
            { 0xAA, (c) => CalcName(c, "TAX") },
            { 0x8A, (c) => CalcName(c, "TXA") },
            { 0xA8, (c) => CalcName(c, "TAY") },
            { 0x98, (c) => CalcName(c, "TYA") },
            { 0xBA, (c) => CalcName(c, "TSX") },
            { 0x9A, (c) => CalcName(c, "TXS") },

            // Stack
            { 0x68, (c) => CalcName(c, "PLA") },
            { 0x48, (c) => CalcName(c, "PHA") },
            { 0x28, (c) => CalcName(c, "PLP") },
            { 0x08, (c) => CalcName(c, "PHP") },

            //
            // JUMP/FLAG
            //

            // Branch
            { 0x10, (c) => CalcName(c, "BPL", Mode.REL) },
            { 0x30, (c) => CalcName(c, "BMI", Mode.REL) },
            { 0x50, (c) => CalcName(c, "BVC", Mode.REL) },
            { 0x70, (c) => CalcName(c, "BVS", Mode.REL) },
            { 0x90, (c) => CalcName(c, "BCC", Mode.REL) },
            { 0xB0, (c) => CalcName(c, "BCS", Mode.REL) },
            { 0xD0, (c) => CalcName(c, "BNE", Mode.REL) },
            { 0xF0, (c) => CalcName(c, "BEQ", Mode.REL) },

            // Interrupts
            { 0x00, (c) => CalcName(c, "BRK") },
            { 0x20, (c) => CalcName(c, "JSR", Mode.ABS) },
            { 0x40, (c) => CalcName(c, "RTI") },
            { 0x60, (c) => CalcName(c, "RTS") },

            // Jumps
            { 0x4C, (c) => CalcName(c, "JMP", Mode.ABS) },
            { 0x6C, (c) => CalcName(c, "JMP", Mode.IND) },

            // Flag Stuff
            { 0xEA, (c) => CalcName(c, "NOP") },
            { 0x24, (c) => CalcName(c, "BIT", Mode.ZP) },
            { 0x2C, (c) => CalcName(c, "BIT", Mode.ABS) },
            { 0x18, (c) => CalcName(c, "CLC") },
            { 0x38, (c) => CalcName(c, "SEC") },
            { 0xD8, (c) => CalcName(c, "CLD") },
            { 0xF8, (c) => CalcName(c, "SED") },
            { 0x58, (c) => CalcName(c, "CLI") },
            { 0x78, (c) => CalcName(c, "SEI") },
            { 0xB8, (c) => CalcName(c, "CLV") },

            //
            // Logic & Math
            //

            // ORA
            { 0x09, (c) => CalcName(c, "ORA", Mode.IMM) },
            { 0x05, (c) => CalcName(c, "ORA", Mode.ZP) },
            { 0x15, (c) => CalcName(c, "ORA", Mode.ZPX) },
            { 0x01, (c) => CalcName(c, "ORA", Mode.INDX) },
            { 0x11, (c) => CalcName(c, "ORA", Mode.INDY) },
            { 0x0D, (c) => CalcName(c, "ORA", Mode.ABS) },
            { 0x1D, (c) => CalcName(c, "ORA", Mode.ABSX) },
            { 0x19, (c) => CalcName(c, "ORA", Mode.ABSY) },

            // AND
            { 0x29, (c) => CalcName(c, "AND", Mode.IMM) },
            { 0x25, (c) => CalcName(c, "AND", Mode.ZP) },
            { 0x35, (c) => CalcName(c, "AND", Mode.ZPX) },
            { 0x21, (c) => CalcName(c, "AND", Mode.INDX) },
            { 0x31, (c) => CalcName(c, "AND", Mode.INDY) },
            { 0x2D, (c) => CalcName(c, "AND", Mode.ABS) },
            { 0x3D, (c) => CalcName(c, "AND", Mode.ABSX) },
            { 0x39, (c) => CalcName(c, "AND", Mode.ABSY) },

            // EOR
            { 0x49, (c) => CalcName(c, "EOR", Mode.IMM) },
            { 0x45, (c) => CalcName(c, "EOR", Mode.ZP) },
            { 0x55, (c) => CalcName(c, "EOR", Mode.ZPX) },
            { 0x41, (c) => CalcName(c, "EOR", Mode.INDX) },
            { 0x51, (c) => CalcName(c, "EOR", Mode.INDY) },
            { 0x4D, (c) => CalcName(c, "EOR", Mode.ABS) },
            { 0x5D, (c) => CalcName(c, "EOR", Mode.ABSX) },
            { 0x59, (c) => CalcName(c, "EOR", Mode.ABSY) },

            // ADC
            { 0x69, (c) => CalcName(c, "ADC", Mode.IMM) },
            { 0x65, (c) => CalcName(c, "ADC", Mode.ZP) },
            { 0x75, (c) => CalcName(c, "ADC", Mode.ZPX) },
            { 0x61, (c) => CalcName(c, "ADC", Mode.INDX) },
            { 0x71, (c) => CalcName(c, "ADC", Mode.INDY) },
            { 0x6D, (c) => CalcName(c, "ADC", Mode.ABS) },
            { 0x7D, (c) => CalcName(c, "ADC", Mode.ABSX) },
            { 0x79, (c) => CalcName(c, "ADC", Mode.ABSY) },

            // SBC
            { 0xE9, (c) => CalcName(c, "SBC", Mode.IMM) },
            { 0xE5, (c) => CalcName(c, "SBC", Mode.ZP) },
            { 0xF5, (c) => CalcName(c, "SBC", Mode.ZPX) },
            { 0xE1, (c) => CalcName(c, "SBC", Mode.INDX) },
            { 0xF1, (c) => CalcName(c, "SBC", Mode.INDY) },
            { 0xED, (c) => CalcName(c, "SBC", Mode.ABS) },
            { 0xFD, (c) => CalcName(c, "SBC", Mode.ABSX) },
            { 0xF9, (c) => CalcName(c, "SBC", Mode.ABSY) },

            // CMP
            { 0xC9, (c) => CalcName(c, "CMP", Mode.IMM) },
            { 0xC5, (c) => CalcName(c, "CMP", Mode.ZP) },
            { 0xD5, (c) => CalcName(c, "CMP", Mode.ZPX) },
            { 0xC1, (c) => CalcName(c, "CMP", Mode.INDX) },
            { 0xD1, (c) => CalcName(c, "CMP", Mode.INDY) },
            { 0xCD, (c) => CalcName(c, "CMP", Mode.ABS) },
            { 0xDD, (c) => CalcName(c, "CMP", Mode.ABSX) },
            { 0xD9, (c) => CalcName(c, "CMP", Mode.ABSY) },

            // CPX
            { 0xE0, (c) => CalcName(c, "CPX", Mode.IMM) },
            { 0xE4, (c) => CalcName(c, "CPX", Mode.ZP) },
            { 0xEC, (c) => CalcName(c, "CPX", Mode.ABS) },

            // CPY
            { 0xC0, (c) => CalcName(c, "CPY", Mode.IMM) },
            { 0xC4, (c) => CalcName(c, "CPY", Mode.ZP) },
            { 0xCC, (c) => CalcName(c, "CPY", Mode.ABS) },

            // DEC
            { 0xC6, (c) => CalcName(c, "DEC", Mode.ZP) },
            { 0xD6, (c) => CalcName(c, "DEC", Mode.ZPX) },
            { 0xCE, (c) => CalcName(c, "DEC", Mode.ABS) },
            { 0xDE, (c) => CalcName(c, "DEC", Mode.ABSX) },

            // DEX, DEY
            { 0xCA, (c) => CalcName(c, "DEX") },
            { 0x88, (c) => CalcName(c, "DEY") },

            // INC
            { 0xE6, (c) => CalcName(c, "INC", Mode.ZP) },
            { 0xF6, (c) => CalcName(c, "INC", Mode.ZPX) },
            { 0xEE, (c) => CalcName(c, "INC", Mode.ABS) },
            { 0xFE, (c) => CalcName(c, "INC", Mode.ABSX) },

            // INX, INY
            { 0xE8, (c) => CalcName(c, "INX", Mode.INDX) },
            { 0xC8, (c) => CalcName(c, "INY", Mode.INDY) },

            // ASL
            { 0x0A, (c) => CalcName(c, "ASL") },
            { 0x06, (c) => CalcName(c, "ASL", Mode.ZP) },
            { 0x16, (c) => CalcName(c, "ASL", Mode.ZPX) },
            { 0x0E, (c) => CalcName(c, "ASL", Mode.ABS) },
            { 0x1E, (c) => CalcName(c, "ASL", Mode.ABSX) },

            // ROL
            { 0x2A, (c) => CalcName(c, "ROL") },
            { 0x26, (c) => CalcName(c, "ROL", Mode.ZP) },
            { 0x36, (c) => CalcName(c, "ROL", Mode.ZPX) },
            { 0x2E, (c) => CalcName(c, "ROL", Mode.ABS) },
            { 0x3E, (c) => CalcName(c, "ROL", Mode.ABSX) },

            // LSR
            { 0x4A, (c) => CalcName(c, "LSR") },
            { 0x46, (c) => CalcName(c, "LSR", Mode.ZP) },
            { 0x56, (c) => CalcName(c, "LSR", Mode.ZPX) },
            { 0x4E, (c) => CalcName(c, "LSR", Mode.ABS) },
            { 0x5E, (c) => CalcName(c, "LSR", Mode.ABSX) },

            // ROR
            { 0x6A, (c) => CalcName(c, "ROR") },
            { 0x66, (c) => CalcName(c, "ROR", Mode.ZP) },
            { 0x76, (c) => CalcName(c, "ROR", Mode.ZPX) },
            { 0x6E, (c) => CalcName(c, "ROR", Mode.ABS) },
            { 0x7E, (c) => CalcName(c, "ROR", Mode.ABSX) },
        };
    }
}
