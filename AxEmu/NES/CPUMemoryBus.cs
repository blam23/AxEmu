using AxEmu.NES.Mappers;

namespace AxEmu.NES
{
    public class CPUMemoryBus : MemoryBus
    {
        // https://www.nesdev.org/wiki/CPU_memory_map

        private readonly byte[] internalRAM = new byte[0x800];
        private readonly Emulator system;

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

            return system.mapper.Read(address);
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
            else
                system.mapper.Write(address, value);
        }
    }
}