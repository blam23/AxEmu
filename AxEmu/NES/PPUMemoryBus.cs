namespace AxEmu.NES
{
    internal class PPUMemoryBus : MemoryBus
    {
        private readonly Emulator system;

        internal readonly byte[,] nametables   = new byte[4,0x400]; // Nametable data
        internal readonly byte[]  nameTableMap = new byte[4];       // Lookup for mirroring
        internal readonly byte[]  chrRAM       = new byte[0x2000];  // Only used if no chrrom pages
        internal readonly byte[]  palette      = new byte[0x20];    // Palette data

        public PPUMemoryBus(Emulator system)
        {
            this.system = system;
        }


        private ushort GetPaletteAddress(ushort address)
        {
            address &= 0x001F;
            return address switch
            {
                0x0010 => 0x0000,
                0x0014 => 0x0004,
                0x0018 => 0x0008,
                0x001C => 0x000C,
                _ => address
            };
        }

        public void UpdateMirroring()
        {
            switch(system.Mirroring)
            {
                case Mirroring.Horizontal:
                    nameTableMap[0] = 0;
                    nameTableMap[1] = 0;
                    nameTableMap[2] = 1;
                    nameTableMap[3] = 1;
                    break;
                case Mirroring.Vertical:
                    nameTableMap[0] = 0;
                    nameTableMap[1] = 1;
                    nameTableMap[2] = 0;
                    nameTableMap[3] = 1;
                    break;
                case Mirroring.OneScreenLower:
                    nameTableMap[0] = 0;
                    nameTableMap[1] = 0;
                    nameTableMap[2] = 0;
                    nameTableMap[3] = 0;
                    break;
                case Mirroring.OneScreenUpper:
                    nameTableMap[0] = 1;
                    nameTableMap[1] = 1;
                    nameTableMap[2] = 1;
                    nameTableMap[3] = 1;
                    break;
            }
        }

        public override byte Read(ushort address)
        {
            if (address < 0x2000)
            {
                if (system.cart.chrRomSize == 0)
                    return chrRAM[address];

                return system.mapper.ReadChrRom(address);
            }
            else if (address < 0x3F00)
            {
                switch (address & 0xfc00)
                {
                    case 0x2000: return nametables[nameTableMap[0],address & 0x3FF];
                    case 0x2400: return nametables[nameTableMap[1],address & 0x3FF];
                    case 0x2800: return nametables[nameTableMap[2],address & 0x3FF];
                    default:     return nametables[nameTableMap[3],address & 0x3FF];
                }
            }
            else if (address < 0x3FFF)
            {
                address = GetPaletteAddress(address);
                return palette[address];
            }    

            return 0;
        }

        public override void Write(ushort address, byte value)
        {
            // Special case where no chrrom
            if (address < 0x2000 && system.cart.chrRomSize == 0)
            {
                chrRAM[address] = value;
                return;
            }

            // Nametable data
            if (address >= 0x2000 && address < 0x3F00)
            {
                switch (address & 0xfc00)
                {
                    case 0x2000: nametables[nameTableMap[0], address & 0x3FF] = value; break;
                    case 0x2400: nametables[nameTableMap[1], address & 0x3FF] = value; break;
                    case 0x2800: nametables[nameTableMap[2], address & 0x3FF] = value; break;
                    default:     nametables[nameTableMap[3], address & 0x3FF] = value; break;
                }
                return;
            }

            // Palette data
            if (address < 0x3FFF)
            {
                address = GetPaletteAddress(address);
                palette[address] = value;
                return;
            }

            system.mapper.WriteChrRom(address, value);
        }
    }
}
