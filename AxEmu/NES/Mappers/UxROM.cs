namespace AxEmu.NES.Mappers
{
    [Mapper(MapperNumber = 2)]
    internal class UxROM : IMapper
    {
        private Emulator? system;

        private readonly byte[] ram = new byte[0x2000];

        // Current bank
        private int page = 0;

        public UxROM()
        {
        }

        public void Init(Emulator system)
        {
            this.system = system;
        }

        public byte Read(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
                return ram[address - 0x6000];

            if (address >= 0x8000 && address <= 0xBFFF)
                return system.cart.prgRomPages[page][address - 0x8000];

            if (address >= 0xC000)
                return system.cart.prgRomPages[^1][address - 0xc000];

            return 0;
        }

        public byte ReadChrRom(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address < 0x1000)
                return system.cart.chrRomPages[0][address];

            if (address < 0x2000)
                return system.cart.chrRomPages[^1][address - 0x1000];

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
                page = value & 0x0F;
            }
        }
    }
}
