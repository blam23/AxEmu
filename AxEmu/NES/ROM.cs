namespace AxEmu.NES
{
    internal class ROM
    {
        public enum State
        {
            Unloaded,
            Loaded,
            FailedToOpen,
            Invalid,
        }

        private readonly byte[] rawData = Array.Empty<byte>();
        private readonly State state = State.Unloaded;
        public State LoadState => state;

        private ushort mapperNumber;

        // Sizes
        private ulong prgRomSize; // in KB
        private ulong chrRomSize; // in KB

        // Flags 6
        private bool mirroring;
        private bool batteryBackedRam;
        private bool trainer;
        private bool ignoreMirroring;

        // Flags 7
        private bool vsUnisystem;
        private bool NES2Format;

        private bool validate()
        {
            // 16 byte header
            if (rawData.Length < 16)
                return false;

            // 'NES<EOF>'
            if (rawData[0] != 'N' || rawData[1] != 'E' || rawData[2] != 'S' || rawData[3] != 0x1A)
                return false;

            // ROM Sizes
            prgRomSize = rawData[4] * 16ul;
            chrRomSize = rawData[5] * 8ul;

            // Flags 6
            var flags6 = rawData[6];
            mirroring =        (flags6 & 0x1) == 0x1;
            batteryBackedRam = (flags6 & 0x2) == 0x2;
            trainer =          (flags6 & 0x4) == 0x4;
            ignoreMirroring =  (flags6 & 0x8) == 0x8;
            mapperNumber = (ushort)(flags6 & 0x0F);

            // Flags 7
            var flags7 = rawData[7];
            vsUnisystem = (flags7 & 0x1) == 0x1;
            NES2Format =  (flags7 & 0x4) == 0x0 && (flags7 & 0x8) == 0x8;
            mapperNumber |= (ushort)(flags7 & 0xF0);

            return true;
        }

        public ROM(string ROMFileLocation)
        {
            try
            {
                rawData = File.ReadAllBytes(ROMFileLocation);
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

        public ROM()
        {
        }
    }
}