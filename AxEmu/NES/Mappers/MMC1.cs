using System.Diagnostics;

namespace AxEmu.NES.Mappers
{
    [Mapper(MapperNumber = 1)]
    internal class MMC1 : IMapper
    {
        private Emulator? system;

        private readonly byte[] ram = new byte[0x2000];

        // ChrROM control
        private byte chrPage1     = 0x0;
        private byte chrPage2     = 0x0;
        private byte chrPageChunk = 0x0;

        // PrgROM control
        private byte prgPage1     = 0x0;
        private byte prgPage2     = 0x0;
        private byte prgPageChunk = 0x0;

        // Registers
        private byte load      = 0;
        private byte loadCount = 0;
        private byte control   = 0;

        public MMC1()
        {
        }

        public void Init(Emulator system)
        {
            this.system = system;

            control  = 0x1C;
            prgPage2 = (byte)(system.cart.prgRomSize - 1);
        }

        public byte Read(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
                return ram[address - 0x6000];

            if (address >= 0x8000)
            {
                if ((control & 0b01000) != 0)
                {
                    // 2 chunk, 16k mode

                    if (address < 0xC000)
                        return system.cart.prgRom[prgPage1 * 0x4000 + (address & 0x3FFF)];
                    else
                        return system.cart.prgRom[prgPage2 * 0x4000 + (address & 0x3FFF)];
                }
                else
                {
                    // 1 chunk, 32k mode
                    return system.cart.prgRom[prgPageChunk * 0x8000 + (address & 0x7FFF)];
                }
            }

            return 0;
        }

        public byte ReadChrRom(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address < 0x2000)
            {
                if ((control & 0b10000) != 0)
                {
                    // 2 chunk, 4k mode
                    if (address < 0x1000)
                        return system.cart.chrRom[chrPage1 * 0x1000 + (address & 0x0FFF)];
                    else
                        return system.cart.chrRom[chrPage2 * 0x1000 + (address & 0x0FFF)];
                }
                else
                {
                    // 1 chunk, 8k mode
                    return system.cart.chrRom[chrPageChunk * 0x2000 + (address & 0x1FFF)];
                }
            }

            return 0;
        }

        public void Write(ushort address, byte value)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to write to uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
            {
                ram[address - 0x6000] = value;
                return;
            }

            if (address >= 8000)
            {
                // Clear if value high bit is set
                if ((value & 0x80) != 0)
                {
                    load = 0;
                    loadCount = 0;
                    control |= 0x0C;
                }
                else
                {
                    // Shift in value
                    load >>= 1;
                    load |= (byte)((value & 0x01) << 4);
                    loadCount++;

                    if (loadCount == 5)
                    {
                        var target = (address >> 13) & 0x3;

                        switch(target)
                        {
                            case 0:
                                // Control Setup
                                control = (byte)(load & 0x1F);

                                switch(control & 0x3)
                                {
                                    case 0: system.Mirroring = Mirroring.OneScreenLower; break;
                                    case 1: system.Mirroring = Mirroring.OneScreenUpper; break;
                                    case 2: system.Mirroring = Mirroring.Vertical;       break;
                                    case 3: system.Mirroring = Mirroring.Horizontal;     break;
                                }

                                break;
                            case 1:
                                // ChrRom High / Chunk setup
                                if ((control & 0b10000) != 0)
                                    chrPage1 = (byte)(load & 0x1F);
                                else
                                    chrPageChunk = (byte)(load & 0x1E);

                                break;
                            case 2:
                                // ChrRom Hi setup
                                if ((control & 0b10000) != 0)
                                    chrPage2 = (byte)(load & 0x1F);

                                break;
                            case 3:

                                var prgMode = (control >> 2) & 0x3;

                                switch(prgMode)
                                {
                                    case 0:
                                    case 1:
                                        // 32k mode
                                        prgPageChunk = (byte)((load & 0x0E) >> 1);
                                        break;
                                    case 2:
                                        // First page fixed, second page set to load
                                        prgPage1 = 0;
                                        prgPage2 = (byte)(load & 0x0F);
                                        break;
                                    case 3:
                                        // First page load, second page set to last page
                                        prgPage1 = (byte)(load & 0x0F);
                                        prgPage2 = (byte)(system.cart.prgRomSize - 1);
                                        break;
                                    default:
                                        throw new UnreachableException();
                                }

                                break;
                        }

                        load = 0;
                        loadCount = 0;
                    }
                }
            }
        }

        public void WriteChrRom(ushort address, byte value)
        {
            return;
        }

        public bool IsIRQSet() { return false; }

        public void Scanline() { }
    }
}
