namespace AxEmu.NES
{
    internal class MemoryMapper : IMemory
    {
        // https://www.nesdev.org/wiki/CPU_memory_map

        private readonly byte[] internalRAM = new byte[0x800];
        private readonly System system;

        public MemoryMapper(System system)
        {
            this.system = system;
        }

        public byte Read(ushort address)
        {
            if (address < 0x2000)
                return internalRAM[address & 0x7ff];

            if (address < 0x4000)
                return system.ppu.read(address);

            if (address < 0x4020)
                return system.apu.read(address);

            throw new NotImplementedException("Need to map ROM n shit");
        }
        public ushort ReadWord(ushort address)
        {
            ushort r = Read((ushort)(address + 1));
            r = (ushort)(r << 8);
            r += Read(address);
            return r;
        }

        public void Write(ushort address, byte value)
        {
            if (address < 0x2000)
                internalRAM[address & 0x7ff] = value;

            else if (address < 0x4000)
                system.ppu.write(address, value);

            else if (address < 0x4020)
                system.apu.write(address, value);

            else
                throw new NotImplementedException("Need to map ROM n shit");
        }
    }
}