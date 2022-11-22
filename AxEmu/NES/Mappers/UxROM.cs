namespace AxEmu.NES.Mappers
{
    [Mapper(MapperNumber = 2)]
    internal class UxROM : IMapper
    {
        private Emulator? system;

        private readonly byte[] ram = new byte[0x2000];

        // Current bank
        private int page1   = 0;
        private int page2   = 0;

        public UxROM()
        {
        }

        public void Init(Emulator system)
        {
            this.system = system;
            page2 = system.cart.prgRomSize - 1;
        }

        public byte Read(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
                return ram[address - 0x6000];

            if (address >= 0x8000 && address <= 0xBFFF)
                return system.cart.prgRom[page1 * 0x4000 + (address & 0x3FFF)];

            if (address >= 0xC000)
                return system.cart.prgRom[page2 * 0x4000 + (address & 0x3FFF)];

            return 0;
        }

        public byte ReadChrRom(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address < 0x2000)
                return system.cart.chrRom[address];

            return 0;
        }

        public void Write(ushort address, byte value)
        {
            if (address >= 0x6000 && address < 0x8000)
            {
                ram[address - 0x6000] = value;
                return;
            }

            if (address >= 8000)
            {
                page1 = value & 0x0F;
            }
        }

        public void WriteChrRom(ushort address, byte value)
        {
            return;
        }
    }
}
