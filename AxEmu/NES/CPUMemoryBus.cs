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
            return address switch
            {
                < 0x2000 => ReadRAM(address),
                < 0x4000 => system.ppu.Read(address),
                0x4016 => system.joyPad1.Read(),
                0x4017 => system.joyPad2.Read(),
                < 0x4020 => system.apu.Read(address),
                _ => system.mapper.Read(address)
            };
        }

        public override void Write(ushort address, byte value)
        {
            switch (address)
            {
                case < 0x2000:
                    WriteRAM(address, value);
                    break;
                case < 0x4000:
                    system.ppu.Write(address, value);
                    break;
                case 0x4014:
                    system.ppu.OAMDMA(value);
                    break;
                case 0x4016:
                    system.joyPad1.Poll(value);
                    break;
                case 0x4017:
                    system.joyPad2.Poll(value);
                    break;
                case < 0x4020:
                    system.apu.Write(address, value);
                    break;
                default:
                    system.mapper.Write(address, value);
                    break;
            }
        }
    }
}