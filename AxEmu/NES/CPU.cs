namespace AxEmu.NES
{
    internal class CPU
    {
        // https://www.nesdev.org/wiki/CPU
        public MemoryBus bus;

        // Interrupt vector addresses
        public const ushort NMI_VECTOR   = 0xFFFA; // NMI vector word address
        public const ushort RESET_VECTOR = 0xFFFC; // Reset vector word address
        public const ushort IRQ_VECTOR   = 0xFFFE; // IRQ vector word address

        internal struct StatusRegister
        {
            internal bool Carry            = false;
            internal bool Zero             = false;
            internal bool InterruptDisable = true;
            internal bool Decimal          = false;
            internal bool Overflow         = false;
            internal bool Negative         = false;
            internal bool Break            = false;

            public StatusRegister()
            {
            }

            public void Set(byte inp, bool setBreak = false)
            {
                Carry            = (inp & 0x1) == 0x1;
                Zero             = (inp & 0x2) == 0x2;
                InterruptDisable = (inp & 0x4) == 0x4;
                Decimal          = (inp & 0x8) == 0x8;
                Overflow         = (inp & 0x40) == 0x40;
                Negative         = (inp & 0x80) == 0x80;

                if (setBreak)
                    Break = (inp & 0x10) == 0x10;
            }

            public byte AsByte(bool overrideBreakWithTrue = false)
            {
                byte r = 0;

                r |= (byte)(Carry ? 0x1 : 0);
                r |= (byte)(Zero ? 0x2 : 0);
                r |= (byte)(InterruptDisable ? 0x4 : 0);
                r |= (byte)(Decimal ? 0x8 : 0);
                r |= (byte)(Break || overrideBreakWithTrue ? 0x10 : 0);
                r |= 0x20;
                r |= (byte)(Overflow ? 0x40 : 0);
                r |= (byte)(Negative ? 0x80 : 0);

                return r;
            }


            public override string ToString()
            {
                return $@"
    Flags:
        carry    : {Carry}
        zero     : {Zero}
        id       : {InterruptDisable}
        decimal  : {Decimal}
        break    : {Break}
        overflow : {Overflow}
        negative : {Negative}
";
            }

            internal string ToSmallString()
            {
                return string.Format
                (
                    "{0}{1}-- {2}{3}{4}{5}",
                    Negative ? 'N' : '-',
                    Overflow ? 'V' : '-',
                    Decimal ? 'D' : '-',
                    InterruptDisable ? 'I' : '-',
                    Zero ? 'Z' : '-',
                    Carry ? 'C' : '-'
                );
            }
        }

        // Registers
        internal byte a;    // Accumulator
        internal byte x;    // Index X
        internal byte y;    // Index Y
        internal byte sp;   // stack pointer
        internal ushort pc; // program counter
        internal StatusRegister status; // p

        // Clock
        internal ulong totalClock = 0;
        internal ulong cycles = 0;
        internal ulong clock = 0;
        internal ulong instrs = 0;

        // Interrupts
        private bool NMISet = false;

        private void SetInitialState()
        {
            a = 0;
            y = 0;
            x = 0;
            sp = 0xFD;
            status = new();

            // pc set on Init
        }

        internal void Init()
        {
            SetInitialState();
            pc = bus.ReadWord(RESET_VECTOR);
        }

        public CPU(MemoryBus bus)
        {
            this.bus = bus;
        }

        public override string ToString()
        {
            return
                $@"
CPU:
    a  : 0x{a:X}
    x  : 0x{x:X}
    y  : 0x{y:X}
    s  : 0x{sp:X}
    pc : 0x{pc:X}
    {status}
";
        }

        public string ToSmallString()
        {
            return $"{pc:X4} | a: {a:X2} | x: {x:X2} | y: {y:X2} | s: {sp:X2} | {status.ToSmallString()}";
        }

        public void Clock()
        {
            CheckInterrupts();

            byte op = bus.Read(pc);

            if (opCodeActions.TryGetValue(op, out var opAction))
            {
                opAction(this);
                totalClock += clock;
                cycles = clock;
                clock = 0;
            }
            else
            {
                throw new NotImplementedException($"Opcode not supported: {op:X2}");
            }

            instrs++;
        }

        public void CheckInterrupts()
        {
            if (NMISet)
                NMI();
        }

        internal void SetNMI()
        {
            NMISet = true;
        }

        private void NMI()
        {
            NMISet = false;

            PushWord(pc);
            PushStatus();

            pc = bus.ReadWord(NMI_VECTOR);
        }

        #region Op Codes

        // http://www.oxyron.de/html/opcodes02.html
        internal enum Mode
        {
            None,
            IMP,
            IMM,
            ZP,
            ZPX,
            ZPY,
            INDX,
            INDY,
            ABS,
            ABSX,
            ABSY,
            IND,
            REL
        }

        internal readonly Dictionary<ushort, Action<CPU>> opCodeActions = new()
        {
            //
            // MOVE
            //

            // LDA
            { 0xA9, (c) => c.a = c.Load(Mode.IMM) },
            { 0xA5, (c) => c.a = c.Load(Mode.ZP) },
            { 0xB5, (c) => c.a = c.Load(Mode.ZPX) },
            { 0xA1, (c) => c.a = c.Load(Mode.INDX) },
            { 0xB1, (c) => c.a = c.Load(Mode.INDY) },
            { 0xAD, (c) => c.a = c.Load(Mode.ABS) },
            { 0xBD, (c) => c.a = c.Load(Mode.ABSX) },
            { 0xB9, (c) => c.a = c.Load(Mode.ABSY) },

            // LDX
            { 0xA2, (c) => c.x = c.Load(Mode.IMM) },
            { 0xA6, (c) => c.x = c.Load(Mode.ZP) },
            { 0xB6, (c) => c.x = c.Load(Mode.ZPY) },
            { 0xAE, (c) => c.x = c.Load(Mode.ABS) },
            { 0xBE, (c) => c.x = c.Load(Mode.ABSY) },

            // LDY
            { 0xA0, (c) => c.y = c.Load(Mode.IMM) },
            { 0xA4, (c) => c.y = c.Load(Mode.ZP) },
            { 0xB4, (c) => c.y = c.Load(Mode.ZPX) },
            { 0xAC, (c) => c.y = c.Load(Mode.ABS) },
            { 0xBC, (c) => c.y = c.Load(Mode.ABSX) },

            // STA
            { 0x85, (c) => c.Store(c.a, Mode.ZP) },
            { 0x95, (c) => c.Store(c.a, Mode.ZPX) },
            { 0x81, (c) => c.Store(c.a, Mode.INDX) },
            { 0x91, (c) => c.Store(c.a, Mode.INDY) },
            { 0x8D, (c) => c.Store(c.a, Mode.ABS) },
            { 0x9D, (c) => c.Store(c.a, Mode.ABSX) },
            { 0x99, (c) => c.Store(c.a, Mode.ABSY) },

            // STX
            { 0x86, (c) => c.Store(c.x, Mode.ZP) },
            { 0x96, (c) => c.Store(c.x, Mode.ZPY) },
            { 0x8E, (c) => c.Store(c.x, Mode.ABS) },

            // STY
            { 0x84, (c) => c.Store(c.y, Mode.ZP) },
            { 0x94, (c) => c.Store(c.y, Mode.ZPX) },
            { 0x8C, (c) => c.Store(c.y, Mode.ABS) },

            // Transfers
            { 0xAA, (c) => c.TAX() },
            { 0x8A, (c) => c.TXA() },
            { 0xA8, (c) => c.TAY() },
            { 0x98, (c) => c.TYA() },
            { 0xBA, (c) => c.TSX() },
            { 0x9A, (c) => c.TXS() },

            // Stack
            { 0x68, (c) => c.PLA() },
            { 0x48, (c) => c.PHA() },
            { 0x28, (c) => c.PLP() },
            { 0x08, (c) => c.PHP() },

            //
            // JUMP/FLAG
            //

            // Branch
            { 0x10, (c) => c.Branch(! c.status.Negative) },
            { 0x30, (c) => c.Branch(c.status.Negative) },
            { 0x50, (c) => c.Branch(! c.status.Overflow) },
            { 0x70, (c) => c.Branch(c.status.Overflow) },
            { 0x90, (c) => c.Branch(! c.status.Carry) },
            { 0xB0, (c) => c.Branch(c.status.Carry) },
            { 0xD0, (c) => c.Branch(! c.status.Zero) },
            { 0xF0, (c) => c.Branch(c.status.Zero) },

            // Interrupts
            { 0x00, (c) => c.BRK() },
            { 0x20, (c) => c.JSR() },
            { 0x40, (c) => c.RTI() },
            { 0x60, (c) => c.RTS() },

            // Jumps
            { 0x4C, (c) => c.Jump(Mode.ABS) },
            { 0x6C, (c) => c.Jump(Mode.IND) },

            // Flag Stuff
            { 0xEA, (c) => c.NOP() },
            { 0x24, (c) => c.TestBit(Mode.ZP) },
            { 0x2C, (c) => c.TestBit(Mode.ABS) },
            { 0x18, (c) => { c.NOP(); c.status.Carry = false; } },            // CLC
            { 0x38, (c) => { c.NOP(); c.status.Carry = true; } },             // SEC
            { 0xD8, (c) => { c.NOP(); c.status.Decimal = false; } },          // CLD
            { 0xF8, (c) => { c.NOP(); c.status.Decimal = true; } },           // SED
            { 0x58, (c) => { c.NOP(); c.status.InterruptDisable = false; } }, // CLI
            { 0x78, (c) => { c.NOP(); c.status.InterruptDisable = true; } },  // SEI
            { 0xB8, (c) => { c.NOP(); c.status.Overflow = false; } },         // CLV

            //
            // Logic & Math
            //

            // ORA
            { 0x09, (c) => c.a = c.Or(c.a, Mode.IMM) },
            { 0x05, (c) => c.a = c.Or(c.a, Mode.ZP) },
            { 0x15, (c) => c.a = c.Or(c.a, Mode.ZPX) },
            { 0x01, (c) => c.a = c.Or(c.a, Mode.INDX) },
            { 0x11, (c) => c.a = c.Or(c.a, Mode.INDY) },
            { 0x0D, (c) => c.a = c.Or(c.a, Mode.ABS) },
            { 0x1D, (c) => c.a = c.Or(c.a, Mode.ABSX) },
            { 0x19, (c) => c.a = c.Or(c.a, Mode.ABSY) },

            // AND
            { 0x29, (c) => c.a = c.And(c.a, Mode.IMM) },
            { 0x25, (c) => c.a = c.And(c.a, Mode.ZP) },
            { 0x35, (c) => c.a = c.And(c.a, Mode.ZPX) },
            { 0x21, (c) => c.a = c.And(c.a, Mode.INDX) },
            { 0x31, (c) => c.a = c.And(c.a, Mode.INDY) },
            { 0x2D, (c) => c.a = c.And(c.a, Mode.ABS) },
            { 0x3D, (c) => c.a = c.And(c.a, Mode.ABSX) },
            { 0x39, (c) => c.a = c.And(c.a, Mode.ABSY) },

            // EOR
            { 0x49, (c) => c.a = c.Xor(c.a, Mode.IMM) },
            { 0x45, (c) => c.a = c.Xor(c.a, Mode.ZP) },
            { 0x55, (c) => c.a = c.Xor(c.a, Mode.ZPX) },
            { 0x41, (c) => c.a = c.Xor(c.a, Mode.INDX) },
            { 0x51, (c) => c.a = c.Xor(c.a, Mode.INDY) },
            { 0x4D, (c) => c.a = c.Xor(c.a, Mode.ABS) },
            { 0x5D, (c) => c.a = c.Xor(c.a, Mode.ABSX) },
            { 0x59, (c) => c.a = c.Xor(c.a, Mode.ABSY) },

            // ADC
            { 0x69, (c) => c.a = c.Add(c.a, Mode.IMM) },
            { 0x65, (c) => c.a = c.Add(c.a, Mode.ZP) },
            { 0x75, (c) => c.a = c.Add(c.a, Mode.ZPX) },
            { 0x61, (c) => c.a = c.Add(c.a, Mode.INDX) },
            { 0x71, (c) => c.a = c.Add(c.a, Mode.INDY) },
            { 0x6D, (c) => c.a = c.Add(c.a, Mode.ABS) },
            { 0x7D, (c) => c.a = c.Add(c.a, Mode.ABSX) },
            { 0x79, (c) => c.a = c.Add(c.a, Mode.ABSY) },

            // SBC
            { 0xE9, (c) => c.a = c.Sub(c.a, Mode.IMM) },
            { 0xE5, (c) => c.a = c.Sub(c.a, Mode.ZP) },
            { 0xF5, (c) => c.a = c.Sub(c.a, Mode.ZPX) },
            { 0xE1, (c) => c.a = c.Sub(c.a, Mode.INDX) },
            { 0xF1, (c) => c.a = c.Sub(c.a, Mode.INDY) },
            { 0xED, (c) => c.a = c.Sub(c.a, Mode.ABS) },
            { 0xFD, (c) => c.a = c.Sub(c.a, Mode.ABSX) },
            { 0xF9, (c) => c.a = c.Sub(c.a, Mode.ABSY) },

            // CMP
            { 0xC9, (c) => c.Cmp(c.a, Mode.IMM) },
            { 0xC5, (c) => c.Cmp(c.a, Mode.ZP) },
            { 0xD5, (c) => c.Cmp(c.a, Mode.ZPX) },
            { 0xC1, (c) => c.Cmp(c.a, Mode.INDX) },
            { 0xD1, (c) => c.Cmp(c.a, Mode.INDY) },
            { 0xCD, (c) => c.Cmp(c.a, Mode.ABS) },
            { 0xDD, (c) => c.Cmp(c.a, Mode.ABSX) },
            { 0xD9, (c) => c.Cmp(c.a, Mode.ABSY) },

            // CPX
            { 0xE0, (c) => c.Cmp(c.x, Mode.IMM) },
            { 0xE4, (c) => c.Cmp(c.x, Mode.ZP) },
            { 0xEC, (c) => c.Cmp(c.x, Mode.ABS) },

            // CPY
            { 0xC0, (c) => c.Cmp(c.y, Mode.IMM) },
            { 0xC4, (c) => c.Cmp(c.y, Mode.ZP) },
            { 0xCC, (c) => c.Cmp(c.y, Mode.ABS) },

            // DEC
            { 0xC6, (c) => c.DecAddr(Mode.ZP) },
            { 0xD6, (c) => c.DecAddr(Mode.ZPX) },
            { 0xCE, (c) => c.DecAddr(Mode.ABS) },
            { 0xDE, (c) => c.DecAddr(Mode.ABSX) },

            // DEX, DEY
            { 0xCA, (c) => c.x = c.Dec(c.x) },
            { 0x88, (c) => c.y = c.Dec(c.y) },

            // INC
            { 0xE6, (c) => c.IncAddr(Mode.ZP) },
            { 0xF6, (c) => c.IncAddr(Mode.ZPX) },
            { 0xEE, (c) => c.IncAddr(Mode.ABS) },
            { 0xFE, (c) => c.IncAddr(Mode.ABSX) },

            // INX, INY
            { 0xE8, (c) => c.x = c.Inc(c.x) },
            { 0xC8, (c) => c.y = c.Inc(c.y) },

            // ASL
            { 0x0A, (c) => c.a = c.Asl(c.a) },
            { 0x06, (c) => c.AslAddr(Mode.ZP) },
            { 0x16, (c) => c.AslAddr(Mode.ZPX) },
            { 0x0E, (c) => c.AslAddr(Mode.ABS) },
            { 0x1E, (c) => c.AslAddr(Mode.ABSX) },

            // ROL
            { 0x2A, (c) => c.a = c.Rol(c.a) },
            { 0x26, (c) => c.RolAddr(Mode.ZP) },
            { 0x36, (c) => c.RolAddr(Mode.ZPX) },
            { 0x2E, (c) => c.RolAddr(Mode.ABS) },
            { 0x3E, (c) => c.RolAddr(Mode.ABSX) },

            // LSR
            { 0x4A, (c) => c.a = c.Lsr(c.a) },
            { 0x46, (c) => c.LsrAddr(Mode.ZP) },
            { 0x56, (c) => c.LsrAddr(Mode.ZPX) },
            { 0x4E, (c) => c.LsrAddr(Mode.ABS) },
            { 0x5E, (c) => c.LsrAddr(Mode.ABSX) },

            // ROR
            { 0x6A, (c) => c.a = c.Ror(c.a) },
            { 0x66, (c) => c.RorAddr(Mode.ZP) },
            { 0x76, (c) => c.RorAddr(Mode.ZPX) },
            { 0x6E, (c) => c.RorAddr(Mode.ABS) },
            { 0x7E, (c) => c.RorAddr(Mode.ABSX) },
        };

        internal ushort GetAddress(Mode mode, bool watchPageBoundary)
        {
            ushort addr = 0;
            int page = 0;
            int newpage = 0;

            ushort argument = (ushort)(pc + 1);

            switch (mode)
            {
                case Mode.IMM:
                    pc += 2;
                    return argument;
                case Mode.ZP:
                    pc += 2;
                    addr = bus.Read(argument);
                    return addr;
                case Mode.ZPX:
                    pc += 2;
                    return (ushort)((bus.Read(argument) + x) & 0xFF);
                case Mode.ZPY:
                    pc += 2;
                    return (ushort)((bus.Read(argument) + y) & 0xFF);
                case Mode.ABS:
                    pc += 3;
                    return bus.ReadWord(argument);
                case Mode.ABSX:
                    pc += 3;
                    addr = bus.ReadWord(argument);

                    page = addr >> 8;
                    addr += x;
                    newpage = addr >> 8;

                    if (watchPageBoundary && page != newpage)
                        clock++;

                    return addr;
                case Mode.ABSY:
                    pc += 3;

                    addr = bus.ReadWord(argument);

                    page = addr >> 8;
                    addr += y;
                    newpage = addr >> 8;

                    if (watchPageBoundary && page != newpage)
                        clock++;

                    return addr;
                case Mode.INDX:
                    pc += 2;

                    addr = bus.Read(argument);
                    addr += x;
                    addr &= 0xFF;

                    return bus.ReadWordWrapped(addr);
                case Mode.INDY:
                    pc += 2;

                    addr = bus.Read(argument);
                    addr = bus.ReadWordWrapped(addr);

                    if (watchPageBoundary && ((addr & 0xFF00) != ((addr + y) & 0xFF00)))
                        clock++;

                    return (ushort)((addr + y) & 0xFFFF);
                case Mode.IMP:
                case Mode.IND:
                case Mode.REL:
                    throw new NotImplementedException("TODO!");
                default:
                    throw new Exception("Unknown Address Mode");
            }
        }

        internal byte ReadNext(Mode mode, bool watchPageBoundary = false)
        {
            return bus.Read(GetAddress(mode, watchPageBoundary));
        }

        internal void WriteNext(Mode mode, byte value, bool watchPageBoundary = false)
        {
            bus.Write(GetAddress(mode, watchPageBoundary), value);
        }

        private void SetNegativeAndZero(byte value)
        {
            status.Negative = (value & 0x80) == 0x80;
            status.Zero     = value == 0;
        }

        private void AddArthClockTime(Mode mode)
        {
            // https://www.nesdev.org/wiki/6502_cycle_times
            clock += mode switch
            {
                Mode.IMM => 2,
                Mode.ZP => 3,
                Mode.ZPX or Mode.ABS or Mode.ABSX or Mode.ABSY => 4,
                Mode.INDY => 5,
                Mode.INDX => 6,
                _ => throw new Exception("Invalid opcode"),
            };
        }

        private void AddShiftClockTime(Mode mode)
        {
            // DEC, ROL, ROR, LSR, ASL
            clock += mode switch
            {
                Mode.IMM => 2,
                Mode.ZP => 5,
                Mode.ZPX or Mode.ABS => 6,
                Mode.ABSX => 7,
                _ => throw new Exception("Invalid opcode"),
            };
        }

        private byte Or(byte value, Mode mode)
        {
            AddArthClockTime(mode);

            byte ret = (byte)(value | ReadNext(mode, true));
            SetNegativeAndZero(ret);
            return ret;
        }

        private byte Xor(byte value, Mode mode)
        {
            AddArthClockTime(mode);

            byte ret = (byte)(value ^ ReadNext(mode));
            SetNegativeAndZero(ret);
            return ret;
        }

        private byte And(byte value, Mode mode)
        {
            AddArthClockTime(mode);

            byte ret = (byte)(value & ReadNext(mode));
            SetNegativeAndZero(ret);
            return ret;
        }

        private byte AddCarry(byte input, byte operand)
        {
            int result = (sbyte)input + (sbyte)operand + (sbyte)(status.Carry ? 1 : 0);

            status.Overflow = result < -128 || result > 127;
            status.Carry = (input + operand + (status.Carry ? 1 : 0)) > 0xFF;

            byte ret = (byte)(result);
            SetNegativeAndZero(ret);

            return ret;
        }

        private byte Add(byte value, Mode mode)
        {
            AddArthClockTime(mode);
            return AddCarry(value, ReadNext(mode));
        }

        // CBBD | a: 40 | x: AA | y: 72 | s: FB | ---- -I-C | SBC #$3F
        // CBBF | a: FF | x: AA | y: 72 | s: FB | N--- -I-- | JSR $F94C

        // should be a = 01, negative = false, carry = true

        private byte Sub(byte value, Mode mode)
        {
            AddArthClockTime(mode);
            return AddCarry(value, (byte)~ReadNext(mode));
        }

        private void Cmp(byte value, Mode mode)
        {
            AddArthClockTime(mode);

            long res = value - ReadNext(mode);
            status.Negative = (res & 0x80) == 0x80;
            status.Zero = res == 0;
            status.Carry = res >= 0;
        }

        private byte Dec(byte value)
        {
            AddShiftClockTime(Mode.IMM);

            pc++;
            byte ret = (byte)(value - 1);
            SetNegativeAndZero(ret);
            return ret;
        }

        private void DecAddr(Mode mode)
        {
            AddShiftClockTime(mode);

            var addr = GetAddress(mode, false);

            var value = bus.Read(addr);
            value--;
            SetNegativeAndZero(value);

            bus.Write(addr, value);
        }
        private byte Inc(byte value)
        {
            AddShiftClockTime(Mode.IMM);

            pc++;
            byte ret = (byte)(value + 1);
            SetNegativeAndZero(ret);
            return ret;
        }

        private void IncAddr(Mode mode)
        {
            AddShiftClockTime(mode);

            var addr = GetAddress(mode, false);

            var value = bus.Read(addr);
            value++;
            SetNegativeAndZero(value);

            bus.Write(addr, value);
        }

        private byte Asl(byte value)
        {
            AddShiftClockTime(Mode.IMM);

            pc++;
            status.Carry = (value & 0x80) == 0x80;
            byte ret = (byte)(value << 1);
            SetNegativeAndZero(ret);
            return ret;
        }

        private void AslAddr(Mode mode)
        {
            AddShiftClockTime(mode);

            var addr = GetAddress(mode, false);

            var value = bus.Read(addr);
            status.Carry = (value & 0x80) == 0x80;
            value = (byte)(value << 1);
            SetNegativeAndZero(value);

            bus.Write(addr, value);
        }

        private byte Lsr(byte value)
        {
            AddShiftClockTime(Mode.IMM);

            pc++;
            status.Carry = (value & 0x1) == 0x1;
            byte ret = (byte)(value >> 1);
            SetNegativeAndZero(ret);
            return ret;
        }

        private void LsrAddr(Mode mode)
        {
            AddShiftClockTime(mode);

            var addr = GetAddress(mode, false);
            var value = bus.Read(addr);
            status.Carry = (value & 0x1) == 0x1;
            value = (byte)(value >> 1);
            SetNegativeAndZero(value);

            bus.Write(addr, value);
        }

        private byte Rol(byte value)
        {
            bool prevCarry = status.Carry;
            AddShiftClockTime(Mode.IMM);

            pc++;
            status.Carry = (value & 0x80) == 0x80;
            byte ret = (byte)((value << 1) + (prevCarry ? 1 : 0));
            SetNegativeAndZero(ret);
            return ret;
        }

        private void RolAddr(Mode mode)
        {
            bool prevCarry = status.Carry;
            AddShiftClockTime(mode);

            var addr = GetAddress(mode, false);

            var value = bus.Read(addr);
            status.Carry = (value & 0x80) == 0x80;
            value = (byte)((value << 1) + (prevCarry ? 1 : 0));
            SetNegativeAndZero(value);

            bus.Write(addr, value);
        }

        private byte Ror(byte value)
        {
            bool prevCarry = status.Carry;
            AddShiftClockTime(Mode.IMM);

            pc++;
            status.Carry = (value & 0x1) == 0x1;
            byte ret = (byte)((value >> 1) + (prevCarry ? 0x80 : 0));
            SetNegativeAndZero(ret);
            return ret;
        }

        private void RorAddr(Mode mode)
        {
            bool prevCarry = status.Carry;
            AddShiftClockTime(mode);

            var addr = GetAddress(mode, false);
            var value = bus.Read(addr);
            status.Carry = (value & 0x1) == 0x1;
            value = (byte)((value >> 1) + (prevCarry ? 0x80 : 0));
            SetNegativeAndZero(value);

            bus.Write(addr, value);
        }

        private void NOP()
        {
            pc++;
            clock += 2;
        }

        private void TestBit(Mode mode)
        {
            ushort addr = 0;
            ushort argument = (ushort)(pc + 1);
            byte value;

            switch (mode)
            {
                case Mode.ZP:
                    clock += 3;
                    addr = bus.Read(argument);
                    value = bus.Read(addr);
                    pc += 2;
                    break;
                case Mode.ABS:
                    clock += 4;
                    addr = bus.ReadWord(argument);
                    value = bus.Read(addr);
                    pc += 3;
                    break;
                default:
                    throw new Exception("Invalid BIT");
            }

            status.Zero     = (value & a)    == 0;
            status.Negative = (value & 0x80) == 0x80;
            status.Overflow = (value & 0x40) == 0x40;
        }

        private void Jump(Mode mode)
        {
            ushort addr = 0;
            ushort argument = (ushort)(pc + 1);

            switch(mode)
            {
                case Mode.ABS:
                    clock += 3;
                    addr = bus.ReadWord(argument);
                    pc = addr;
                    break;
                case Mode.IND:
                    clock += 5;
                    addr = bus.ReadWord(argument);
                   
                    var oldPC = pc;
                    pc = bus.ReadWordWrapped(addr);

                    if ((oldPC & 0xFF00) != (pc & 0xFF00)) 
                        clock += 2;

                    break;
                default:
                    throw new Exception("Invalid JMP");
            }
        }

        private void BRK()
        {
            clock += 7;

            PushWord(pc);
            PushStatus();
            pc = bus.ReadWord(0xFFFE);
            status.InterruptDisable = true;
        }

        private void RTI()
        {
            clock += 6;

            PullStatus();
            pc = PullWord();
        }

        private void RTS()
        {
            clock += 6;

            pc = PullWord();
            pc++;
        }

        private void JSR()
        {
            clock += 6;

            PushWord((ushort)(pc + 2));
            pc = bus.ReadWord((ushort)(pc + 1));
        }

        private void Branch(bool cond)
        {
            clock += 2;

            if (cond)
            {
                clock++;

                var argument = (ushort)(pc + 1);
                var address = (sbyte)(bus.Read(argument));
                pc += 2;
                var page = pc >> 8;
                pc = (ushort)(pc + address);
                var npage = pc >> 8;

                if (page != npage)
                    clock++;
            }
            else
            {
                pc += 2;
            }
        }

        internal void TAX()
        {
            pc += 1; 
            clock += 2;

            x = a;
            SetNegativeAndZero(a);
        }

        internal void TXA()
        {
            pc += 1;
            clock += 2;

            a = x;
            SetNegativeAndZero(a);
        }

        internal void TAY()
        {
            pc += 1;
            clock += 2;

            y = a;
            SetNegativeAndZero(a);
        }

        internal void TYA()
        {
            pc += 1;
            clock += 2;


            a = y;
            SetNegativeAndZero(a);
        }

        internal void TSX()
        {
            pc += 1;
            clock += 2;

            x = sp;
            SetNegativeAndZero(x);
        }

        internal void TXS()
        {
            pc += 1;
            clock += 2;

            sp = x;
        }

        internal void PLA()
        {
            pc += 1;
            clock += 4;

            sp++;
            a = bus.Read((ushort)(sp + 0x100));

            SetNegativeAndZero(a);
        }

        internal void PHA()
        {
            pc += 1;
            clock += 3;

            bus.Write((ushort)(sp + 0x100), a);
            sp--;
        }

        internal void PLP()
        {
            pc += 1;
            clock += 4;

            PullStatus();
            status.Break = false;
        }

        private void PullStatus()
        {
            status.Set(Pull());
        }

        private byte Pull()
        {
            sp++;
            var value = bus.Read((ushort)(sp + 0x100));

            return value;
        }

        private ushort PullWord()
        {
            return (ushort)(Pull() | (Pull() << 8));
        }

        internal void PHP()
        {
            pc += 1;
            clock += 3;

            PushStatus();
        }

        private void PushStatus()
        {
            Push(status.AsByte(true));
        }

        private void PushWord(ushort value)
        {
            Push((byte)(value >> 8));
            Push((byte)(value & 0xFF));
        }

        private void Push(byte value)
        {
            bus.Write((ushort)(sp + 0x100), value);
            sp--;
        }

        internal void Store(byte value, Mode mode)
        {
            clock += mode switch
            {
                Mode.ZP or Mode.ZPY => 3,
                Mode.ABS or Mode.ZPX => 4,
                Mode.ABSX or Mode.ABSY => 5,
                Mode.INDX or Mode.INDY => 6,
                _ => throw new Exception("Invalid Write"),
            };

            WriteNext(mode, value);
        }

        internal byte Load(Mode mode)
        {
            clock += mode switch
            {
                Mode.IMM => 2,
                Mode.ZP => 3,
                Mode.ZPX or Mode.ZPY or Mode.ABS or Mode.ABSX or Mode.ABSY => 4,
                Mode.INDX => 6,
                Mode.INDY => 5,
                _ => throw new Exception("Invalid Load"),
            };

            if (mode == Mode.INDX) 
            {
                System.Console.WriteLine("AYY");
            }

            var value = ReadNext(mode, true);
            SetNegativeAndZero(value);
            return value;
        }
        #endregion
    }
}