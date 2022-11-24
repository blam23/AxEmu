namespace AxEmu.NES.Mappers
{
    [Mapper(MapperNumber=0)]
    internal class NROM : IMapper
    {
        private Emulator? system;

        private readonly byte[] ram = new byte[0x2000];
        private ushort mask = 0x3FFF;

        public NROM()
        {
        }

        public void Init(Emulator system)
        {
            this.system = system;
            mask = (ushort)(system.cart.prgRomSize > 1 ? 0x7FFF : 0x3FFF);
        }

        public byte Read(ushort address)
        {
            if (system == null)
                throw new InvalidOperationException("Attempted to read from uninitialised mapper");

            if (address >= 0x6000 && address < 0x8000)
                return ram[address - 0x6000];

            if (address >= 0x8000 && address <= 0xFFFF)
                return system.cart.prgRom[address & mask];

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
            // NROM has no mapping or ctrl

            if (address >= 0x6000 && address < 0x8000)
                ram[address - 0x6000] = value;
        }

        public void WriteChrRom(ushort address, byte value)
        {
            return;
        }

        public bool IsIRQSet() { return false; }

        public void Scanline() { }
    }
}
