namespace AxEmu.NES
{
    internal class CPUMemoryBus : MemoryBus
    {
        // https://www.nesdev.org/wiki/CPU_memory_map

        private readonly byte[] internalRAM = new byte[0x800];
        private readonly Emulator system;
        private IMapper mapper;

        public CPUMemoryBus(Emulator system)
        {
            this.system = system;
        }

        private byte ReadRAM(ushort address) => internalRAM[address & 0x7ff];
        private void WriteRAM(ushort address, byte value) => internalRAM[address & 0x7ff] = value;

        public override byte Read(ushort address)
        {
            if (address < 0x2000)
                return ReadRAM(address);

            if (address < 0x4000)
                return system.ppu.Read(address);

            if (address == 0x4016)
                return system.joyPad1.Read();

            if (address == 0x4017)
                return system.joyPad2.Read();

            if (address < 0x4020)
                return system.apu.Read(address);

            if (address >= 0x8000 && address <= 0xBFFF)
                return system.cart.prgRomPages[0][address - 0x8000];

            if (address >= 0xc000)
                return system.cart.prgRomPages[^1][address - 0xc000];

            //    return mapper.Read(address);
            return 0;
        }

        public override void Write(ushort address, byte value)
        {
            if (address < 0x2000)
                WriteRAM(address, value);
            else if (address < 0x4000)
                system.ppu.Write(address, value);
            else if (address == 0x4014)
                system.ppu.OAMDMA(value);
            else if (address == 0x4016)
                system.joyPad1.Poll(value);
            else if (address == 0x4017)
                system.joyPad2.Poll(value);
            else if (address < 0x4020)
                system.apu.Write(address, value);
            //else
            //    return mapper.Write(address, value);
        }
    }
}