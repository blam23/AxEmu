using System.Diagnostics;
using System.Net;

namespace AxEmu.NES.Mappers
{
    [Mapper(MapperNumber = 4)]
    internal class MMC3 : IMapper
    {
        private Emulator? system;

        // Static RAM
        private readonly byte[] ram = new byte[0x2000];

        // Memory indexing
        private bool    prgPageMode   = false;
        private bool    chrInverted   = false;
        internal uint[] prgPageLookup = new uint[4];
        internal uint[] chrPageLookup = new uint[8];

        // Registers
        private byte[] registers      = new byte[8];
        private byte   targetRegister = 0;

        // IRQ
        private bool   irqActive  = false;
        private bool   irqEnable  = false;
        private ushort irqCounter = 0;
        private ushort irqReload  = 0;

        public MMC3()
        {
        }

        public void Init(Emulator system)
        {
            this.system = system;

            system.Mirroring = Mirroring.Horizontal;
            prgPageLookup[0] = 0 * 0x2000;
            prgPageLookup[1] = 1 * 0x2000;
            prgPageLookup[2] = (uint)(((system.cart.prgRomSize * 2) - 2) * 0x2000);
            prgPageLookup[3] = (uint)(((system.cart.prgRomSize * 2) - 1) * 0x2000);
        }

        public uint GetPgrPage(ushort address)
        {
            if (address >= 0x8000 && address < 0xA000)
                return prgPageLookup[0];

            if (address >= 0xA000 && address < 0xC000)
                return prgPageLookup[1];

            if (address >= 0xC000 && address < 0xE000)
                return prgPageLookup[2];

            if (address >= 0xE000)
                return prgPageLookup[3];

            throw new UnreachableException();
        }

        public uint GetChrPage(ushort address)
        {
            if (address < 0x0400)
                return chrPageLookup[0];

            if (address >= 0x0400 && address < 0x0800)
                return chrPageLookup[1];

            if (address >= 0x0800 && address < 0x0C00)
                return chrPageLookup[2];

            if (address >= 0x0C00 && address < 0x1000)
                return chrPageLookup[3];

            if (address >= 0x1000 && address < 0x1400)
                return chrPageLookup[4];

            if (address >= 0x1400 && address < 0x1800)
                return chrPageLookup[5];

            if (address >= 0x1800 && address < 0x1C00)
                return chrPageLookup[6];

            if (address >= 0x1C00 && address < 0x2000)
                return chrPageLookup[7];

            throw new UnreachableException();
        }

        public byte Read(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
                return ram[address - 0x6000];

            if (address >= 0x8000)
                return system.cart.prgRom[GetPgrPage(address) + (address & 0x1FFF)];

            return 0;
        }

        public byte ReadChrRom(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address < 0x2000)
                return system.cart.chrRom[GetChrPage(address) + (address & 0x03FF)];

            return 0;
        }

        public void Write(ushort address, byte value)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to write to uninitialised mapper");

            var addressEven = (address & 0x0001) == 0;

            if (address >= 0x6000 && address < 0x8000)
            {
                ram[address - 0x6000] = value;
                return;
            }

            if (address >= 0x8000 && address < 0xA000)
            {
                if (addressEven)
                {
                    targetRegister = (byte)(value & 0x07);
                    prgPageMode    = (value & 0x40) == 0x40;
                    chrInverted    = (value & 0x80) == 0x80;
                }
                else
                {
                    registers[targetRegister] = value;

                    if (chrInverted)
                    {
                        chrPageLookup[0] = (ushort)(registers[2] * 0x400);
                        chrPageLookup[1] = (ushort)(registers[3] * 0x400);
                        chrPageLookup[2] = (ushort)(registers[4] * 0x400);
                        chrPageLookup[3] = (ushort)(registers[5] * 0x400);
                        chrPageLookup[4] = (ushort)((registers[0] & 0xFE) * 0x400);
                        chrPageLookup[5] = (ushort)(registers[0] * 0x400 + 0x400);
                        chrPageLookup[6] = (ushort)((registers[1] & 0xFE) * 0x400);
                        chrPageLookup[7] = (ushort)(registers[1] * 0x400 + 0x400);
                    }
                    else
                    {
                        chrPageLookup[0] = (ushort)((registers[0] & 0xFE) * 0x400);
                        chrPageLookup[1] = (ushort)(registers[0] * 0x400 + 0x400);
                        chrPageLookup[2] = (ushort)((registers[1] & 0xFE) * 0x400);
                        chrPageLookup[3] = (ushort)(registers[1] * 0x400 + 0x400);
                        chrPageLookup[4] = (ushort)(registers[2] * 0x400);
                        chrPageLookup[5] = (ushort)(registers[3] * 0x400);
                        chrPageLookup[6] = (ushort)(registers[4] * 0x400);
                        chrPageLookup[7] = (ushort)(registers[5] * 0x400);
                    }

                    if (prgPageMode)
                    {
                        prgPageLookup[2] = (ushort)((registers[6] & 0x3F) * 0x2000);
                        prgPageLookup[0] = (ushort)(((system.cart.prgRomSize * 2) - 2) * 0x2000);
                    }
                    else
                    {
                        prgPageLookup[0] = (ushort)((registers[6] & 0x3F) * 0x2000);
                        prgPageLookup[2] = (ushort)(((system.cart.prgRomSize * 2) - 2) * 0x2000);
                    }

                    prgPageLookup[1] = (ushort)((registers[7] & 0x3F) * 0x2000);
                    prgPageLookup[3] = (ushort)(((system.cart.prgRomSize * 2) - 1) * 0x2000);

                    using(Debug.ConsoleColour.Error())
                    {
                        Console.WriteLine($"<!> Changing MMC3 Lookups, r[{targetRegister:X2}] = {value:X2};");
                        system.debug.DumpMMC3Lookups();
                    }
                }
            }

            if (address >= 0xA000 && address < 0xC000)
            {
                if (addressEven)
                {
                    if ((value & 0x1) == 0x1)
                        system.Mirroring = Mirroring.Horizontal;
                    else
                        system.Mirroring = Mirroring.Vertical;
                }
            }

            if (address >= 0xC000 && address < 0xE000)
            {
                if (addressEven)
                    irqReload = value;
                else
                    irqCounter = 0;
            }

            if (address >= 0xE000)
            {
                if (addressEven)
                {
                    irqEnable = false;
                    irqActive = false;
                }
                else
                    irqEnable = true;
            }
        }

        public void WriteChrRom(ushort address, byte value)
        {
            return;
        }

        public bool IsIRQSet() { return irqActive; }

        public void Scanline()
        {
            if (irqCounter == 0)
                irqCounter = irqReload;
            else
                irqCounter--;

            if (irqCounter == 0 && irqEnable)
                irqActive = true;
        }
    }
}
