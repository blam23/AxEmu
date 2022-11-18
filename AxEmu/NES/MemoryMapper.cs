﻿namespace AxEmu.NES
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
                return system.ppu.Read(address);

            if (address < 0x4020)
                return system.apu.read(address);

            if (address >= 0x8000 && address < 0xBFFF)
                return system.cart.prgRomPages[0][address - 0x8000];

            if (address >= 0xc000)
                return system.cart.prgRomPages[^1][address - 0xc000];

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
                system.ppu.Write(address, value);

            else if (address < 0x4020)
                system.apu.write(address, value);

            else
                Console.WriteLine($"<!> Unknown write to: {address:X4} -> {value:X2}");
                //throw new NotImplementedException("Need to map ROM n shit");
        }
    }
}