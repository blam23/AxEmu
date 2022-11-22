namespace AxEmu.NES.Mappers
{
    //[Mapper(MapperNumber = 1)]
    internal class MMC1 : IMapper
    {
        private Emulator? system;

        private readonly byte[] ram = new byte[0x2000];

        // Current banks
        private int firstRomPage = 0;
        private int secondRomPage = 0;

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


        }

        public byte Read(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
                return ram[address - 0x6000];

            if (address >= 0x8000 && address <= 0xBFFF)
                return system.cart.prgRomPages[firstRomPage][address - 0x8000];

            if (address >= 0xc000)
                return system.cart.prgRomPages[secondRomPage][address - 0xc000];

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

            }
        }
    }
}
