using System.Collections.Generic;

namespace AxEmu.NES
{
    public class Cart
    {
        public enum State
        {
            Unloaded,
            Loaded,
            FailedToOpen,
            Invalid,
        }

        public enum Mirroring
        {
            Horizontal,
            Vertical,
            OneScreenLower,
            OneScreenUpper,
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
        private int prgRomSize;
        private int chrRomSize;
        internal readonly List<byte[]> prgRomPages = new();
        internal readonly List<byte[]> chrRomPages = new();
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
            var flags6     = rom[6];
            mirroring      = (flags6 & 0x1) == 0x1 ? Mirroring.Vertical : Mirroring.Horizontal;
            batteryPresent = (flags6 & 0x2) == 0x2;
            Mapper         = (ushort)(flags6 >> 4);

            // Flags 7
            var flags7 = rom[7];
            Mapper    |= (ushort)(flags7 & 0xF0);

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

            // Copy ROM data into PRG pages
            for (var i = 0; i < prgRomSize; i++)
            {
                var page = new byte[PRG_PAGE_SIZE];
                Array.Copy(rom, pos, page, 0, PRG_PAGE_SIZE);
                prgRomPages.Add(page);
                pos += PRG_PAGE_SIZE;
            }

            // Copy ROM data into CHR pages
            var halfSize = CHR_PAGE_SIZE / 2;
            for (var i = 0; i < chrRomSize; i++)
            {
                var pageA = new byte[CHR_PAGE_SIZE / 2];
                Array.Copy(rom, pos, pageA, 0, halfSize);
                chrRomPages.Add(pageA);
                pos += CHR_PAGE_SIZE / 2;

                var pageB = new byte[CHR_PAGE_SIZE / 2];
                Array.Copy(rom, pos, pageB, 0, halfSize);
                chrRomPages.Add(pageB);
                pos += CHR_PAGE_SIZE / 2;
            }

            if (chrRomSize == 0)
                chrRomPages.Add(new byte[CHR_PAGE_SIZE]);

            return true;

            // TODO: Shit

            //for (var i = 0; i < chrRomSize; i++)
            //{
            //    var firstPage = new byte[0x1000];
            //    Array.Copy(rom, pos, firstPage, 0, 0x1000);
            //    prgRomPages.Add(firstPage);

            //    var secondPage = new byte[0x1000];
            //    Array.Copy(rom, pos + 0x1000, secondPage, 0, 0x1000);
            //    prgRomPages.Add(secondPage);

            //    pos += CHR_PAGE_SIZE;
            //}


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