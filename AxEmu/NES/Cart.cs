using System.Collections.Generic;

namespace AxEmu.NES
{
    public partial class Cart
    {
        public enum State
        {
            Unloaded,
            Loaded,
            FailedToOpen,
            Invalid,
        }

        public enum Region
        {
            NTSC,
            PAL,
            Multi,
            Dendy
        }

        private readonly byte[] rom = Array.Empty<byte>();
        private readonly State state = State.Unloaded;
        public State LoadState => state;

        private const int PRG_PAGE_SIZE = 16 * 1024; 
        private const int CHR_PAGE_SIZE =  8 * 1024;

        // Cart data
        public ushort Mapper;
        internal int prgRomSize;
        internal int chrRomSize;
        internal byte[] prgRom;
        internal byte[] chrRom;
        internal Mirroring mirroring;
        internal bool batteryPresent;
        internal Region region;

        private bool validate()
        {
            // 16 byte header
            if (rom.Length < 16)
                return false;

            // 'NES<EOF>'
            if (rom[0] != 'N' || rom[1] != 'E' || rom[2] != 'S' || rom[3] != 0x1A)
                return false;

            // ROM Sizes
            prgRomSize = rom[4];
            chrRomSize = rom[5];

            // Flags 6
            var flags6 = rom[6];
            mirroring = (flags6 & 0x1) == 0x1 ? Mirroring.Vertical : Mirroring.Horizontal;
            batteryPresent = (flags6 & 0x2) == 0x2;
            Mapper = (ushort)(flags6 >> 4);

            // Flags 7
            var flags7 = rom[7];
            Mapper |= (ushort)(flags7 & 0xF0);

            // Region
            region = (rom[12] & 0x3) switch
            {
                0 => Region.NTSC,
                1 => Region.PAL,
                2 => Region.Multi,
                3 => Region.Dendy,
                _ => Region.NTSC,
            };

            // Setup pages
            var pos = 16; // Start at end of header

            // Copy ROM data into PRG buffer
            prgRom = new byte[PRG_PAGE_SIZE * prgRomSize];
            Array.Copy(rom, pos, prgRom, 0, PRG_PAGE_SIZE * prgRomSize);
            pos += PRG_PAGE_SIZE * prgRomSize;

            chrRom = new byte[CHR_PAGE_SIZE * chrRomSize];
            if (chrRomSize > 0)
            {
                Array.Copy(rom, pos, chrRom, 0, CHR_PAGE_SIZE * chrRomSize);
            }

            return true;

            //var trainer          = (flags6 & 0x4) == 0x4;
            //var ignoreMirroring  = (flags6 & 0x8) == 0x8;
            //var vsUnisystem   = (flags7 & 0x1) == 0x1;
            //var NES2Format    = (flags7 & 0x4) == 0x0 && (flags7 & 0x8) == 0x8;
        }

        public Cart(string ROMFileLocation)
        {
            try
            {
                rom = File.ReadAllBytes(ROMFileLocation);
            }
            catch 
            {
                state = State.FailedToOpen;
                return;
            }

            if (!validate())
            {
                state = State.Invalid;
                return;
            }

            state = State.Loaded;
        }

        public Cart()
        {
        }
    }
}