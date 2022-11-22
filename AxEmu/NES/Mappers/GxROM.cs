namespace AxEmu.NES.Mappers
{
    [Mapper(MapperNumber = 66)]
    internal class GxROM : IMapper
    {
        private Emulator? system;

        private readonly byte[] ram = new byte[0x2000];

        // Current bank
        private int prgPage = 0;
        private int chrPage = 0;

        public GxROM()
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

            if (address >= 0x8000 && address <= 0xFFFF)
                return system.cart.prgRom[prgPage * 0x8000 + (address & 0x7FFF)];

            return 0;
        }

        public byte ReadChrRom(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address < 0x2000)
                return system.cart.chrRom[chrPage * 0x2000 + address];

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
                chrPage = value & 0x03;
                prgPage = (value & 0x30) >> 4;
            }
        }

        public void WriteChrRom(ushort address, byte value)
        {
            return;
        }
    }
}
