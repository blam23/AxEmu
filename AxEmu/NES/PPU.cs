namespace AxEmu.NES
{
    internal class PPU
    {
        private System system;

        internal ulong clock;
        bool frameOddEven = false;
        bool vblank = false;

        public const uint ClocksPerLine = 341;
        public const uint LinesPerFrame = 262;
        public const uint VBlankLine = 241;

        public PPU(System system)
        {
            this.system = system;
        }

        public void Tick(ulong cycles)
        {
            clock += cycles;

            var clockInFrame = clock % (LinesPerFrame * ClocksPerLine);

            if (clockInFrame >= (VBlankLine * ClocksPerLine))
                vblank = true;

            if (clockInFrame <= (VBlankLine * ClocksPerLine))
                vblank = false;

            if (clockInFrame == 0)
                NextFrame();
        }

        private void NextFrame()
        {
            frameOddEven = !frameOddEven;
        }

        private byte StatusByte()
        {
            byte status = 0;
            
            if (vblank)
                status |= 0x80;

            return status;
        }

        internal byte Read(ushort address)
        {
            if (address == 0x2002)
                return StatusByte();

            return 0;
            //throw new NotImplementedException();
        }

        internal void Write(ushort address, byte value)
        {
            //throw new NotImplementedException();
        }
    }
}