namespace AxEmu.NES
{
    internal class PPUMemoryBus : MemoryBus
    {
        private readonly Emulator system;

        internal readonly byte[] VRAM = new byte[0x2000];
        internal byte[] patternTable0 = new byte[0x1000];
        internal byte[] patternTable1 = new byte[0x1000];

        public PPUMemoryBus(Emulator system)
        {
            this.system = system;
        }

        internal void Init()
        {
            patternTable0 = system.cart.chrRomPages[0];
            patternTable1 = system.cart.chrRomPages[^1];
        }

        private static readonly Dictionary<ushort, ushort> addressMirrors = new()
        {
            { 0x3F10, 0x3F00 },
            { 0x3F14, 0x3F04 },
            { 0x3F18, 0x3F08 },
            { 0x3F1C, 0x3F0C },
        };

        private static ushort MirrorAddress(ushort address)
        {
            // Nametable mirroring
            if (address > 0x2000 && address < 0x3EFF)
            {

            }

            // Palette Mirroring
            if (addressMirrors.TryGetValue(address, out var fixedAddress))
                return fixedAddress;

            return address;
        }

        public override byte Read(ushort address)
        {
            address = MirrorAddress(address);

            if (address < 0x1000)
            {
                return patternTable0[address];
            }
            else if (address < 0x2000)
            {
                return patternTable1[address - 0x1000];
            }
            else if (address < 0x4000)
            {
                return VRAM[address - 0x2000];
            }

            return 0;
        }

        public override void Write(ushort address, byte value)
        {
            address = MirrorAddress(address);

            if (address < 0x1000)
            {
                patternTable0[address] = value;
            }
            else if (address < 0x2000)
            {
                patternTable1[address - 0x1000] = value;
            }
            else if (address < 0x4000)
            {
                VRAM[address - 0x2000] = value;
            }
        }
    }
}
