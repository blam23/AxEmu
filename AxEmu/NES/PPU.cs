using System.Drawing;

namespace AxEmu.NES
{
    public class PPU
    {
        private System system;

        internal enum SpriteSize
        {
            s8x8,
            s8x16,
        }

        // Constants
        public const uint ClocksPerLine = 341;
        public const uint LinesPerFrame = 262;
        public const uint VBlankLine    = 241;
        public const uint AttrTableAddr = 0x3c0;

        public const uint RenderWidth = 255;
        public const uint RenderHeight = 240;

        // Frame Info
        internal ulong clock;
        internal bool  oddEvenFlag       = false;
        internal bool  vblank            = false;
        internal bool  dontVBlank        = false;
        internal bool  renderingEnabled  = false;
        internal ulong x = 0;
        internal ulong y = 0;
        internal ulong frame = 0;

        // Control Info
        internal ushort     BaseTableAddr     = 0x2000;
        internal ushort     VRAMAddrInc       = 0x1;
        internal ushort     SpriteTableAddr   = 0x0;
        internal ushort     BackTableAddr     = 0x0;
        internal SpriteSize CurrentSpriteSize = SpriteSize.s8x8;
        internal bool       WriteEXTPins      = false;
        internal bool       NMIOnVBlank       = false;

        // Data
        internal readonly byte[] OAM  = new byte[0x100];
        internal readonly byte[] VRAM = new byte[0x2000];
        internal byte[] VRAMPage0000  = new byte[0x1000];
        internal byte[] VRAMPage1000  = new byte[0x1000];
        internal byte[] Bgr24Bitmap   = new byte[RenderWidth * RenderHeight * 3];

        // Events
        public delegate void PPUFrameEvent(byte[] bitmap);
        public event PPUFrameEvent? FrameCompleted;

        protected virtual void OnFrameCompleted()
        {
            FrameCompleted?.Invoke(Bgr24Bitmap);
        }

        // Registers
        bool writeAddrUpper = true;
        internal ushort Addr = 0;

        public PPU(System system)
        {
            this.system = system;
        }

        public void Init()
        {
            VRAMPage0000 = system.cart.chrRomPages[0];
            VRAMPage1000 = system.cart.chrRomPages[^1];
        }

        public void Tick(ulong cycles)
        {
            for (ulong i = 0; i < cycles; i++)
            {
                x = clock % ClocksPerLine;
                y = clock / ClocksPerLine;

                renderPixel();
                clock++;

                if (y == VBlankLine && x == 1)
                {
                    vblank = true;
                    frame++;
                    OnFrameCompleted();

                    if (NMIOnVBlank)
                    {
                        system.cpu.SetNMI();
                    }
                }

                if (y == VBlankLine && x == 3)
                    dontVBlank = false;

                if (y == 261 && x == 1)
                {
                    vblank = false;
                }

                if (x == 339 && y == 261 && renderingEnabled && !oddEvenFlag)
                    clock = 0;

                if (x == 340 && y == 261)
                    clock = 0;

                oddEvenFlag = !oddEvenFlag;
            }
        }

        private byte StatusByte()
        {
            byte status = 0;

            if (vblank)
                status |= 0x80;

            vblank = false;
            writeAddrUpper = true;

            if (y == 241 && (x >= 1 || x <= 3))
                dontVBlank = true;

            return status;
        }

        private void WriteCtrl(byte value)
        {
            BaseTableAddr = (value & 0x3) switch
            {
                0x0 => 0x2000,
                0x1 => 0x2400,
                0x2 => 0x2800,
                0x3 => 0x2C00,
                _   => 0x2000
            };

            VRAMAddrInc       = (value & 0x04) == 0x04 ? (ushort)0x0020   : (ushort)0x0001;
            SpriteTableAddr   = (value & 0x08) == 0x08 ? (ushort)0x1000   : (ushort)0x0000;
            BackTableAddr     = (value & 0x10) == 0x10 ? (ushort)0x1000   : (ushort)0x0000;
            CurrentSpriteSize = (value & 0x20) == 0x20 ? SpriteSize.s8x16 : SpriteSize.s8x8;
            WriteEXTPins      = (value & 0x40) == 0x40;
            NMIOnVBlank       = (value & 0x80) == 0x80;

            //Console.WriteLine(Debug.PPUState(system));
        }

        private void WriteMask(byte value)
        {

        }

        private void WriteAddr(byte value)
        {
            if (writeAddrUpper)
                Addr = (ushort)(value << 8);
            else
                Addr |= value;

            writeAddrUpper = !writeAddrUpper;
        }

        private byte ReadAddrValue()
        {
            if (Addr >= 0x4000 || Addr < 0x2000)
                return 0; // TODO: Fix this

            var ret = VRAM[Addr - 0x2000];
            Addr += VRAMAddrInc;
            return ret;
        }

        private void WriteAddrValue(byte value)
        {
            Console.WriteLine($"VRAM[{Addr - 0x2000:X4}] = {value:X2}");

            if (Addr >= 0x4000 || Addr < 0x2000)
                return; // TODO: Fix this

            VRAM[Addr - 0x2000] = value;
            Addr += VRAMAddrInc;
        }

        internal byte Read(ushort address)
        {
            if (address == 0x2002)
                return StatusByte();
            if (address == 0x2007)
                return ReadAddrValue();

            return 0;
        }

        internal void Write(ushort address, byte value)
        {
            if (address == 0x2000)
                WriteCtrl(value);
            if (address == 0x2001)
                WriteMask(value);
            if (address == 0x2006)
                WriteAddr(value);
            if (address == 0x2007)
                WriteAddrValue(value);
        }

        private Color lookupBGPalette(byte paletteIndex)
        {
            var p = VRAM[0x1f00 + paletteIndex];
            return lookupColour(p);
        }

        private Color lookupColour(byte colour)
        {
            if (colour >= 0x40)
                return Color.FromArgb(101, 101, 101);
                //throw new Exception($"Invalid colour: {colour:X2}");

            // https://www.nesdev.org/w/images/default/5/59/Savtool-swatches.png
            return colour switch {
                0x00 => Color.FromArgb(101, 101, 101),
                0x01 => Color.FromArgb(0, 45, 105),
                0x02 => Color.FromArgb(19, 31, 127),
                0x03 => Color.FromArgb(60, 19, 124),
                0x04 => Color.FromArgb(96, 11, 98),
                0x05 => Color.FromArgb(115, 10, 55),
                0x06 => Color.FromArgb(113, 15, 7),
                0x07 => Color.FromArgb(90, 26, 0),
                0x08 => Color.FromArgb(52, 40, 0),
                0x09 => Color.FromArgb(11, 52, 0),
                0x0A => Color.FromArgb(0, 60, 0),
                0x0B => Color.FromArgb(0, 61, 16),
                0x0C => Color.FromArgb(0, 56, 64),
                0x0D => Color.FromArgb(0, 0, 0),
                0x0E => Color.FromArgb(0, 0, 0),
                0x0F => Color.FromArgb(0, 0, 0),

                0x10 => Color.FromArgb(174, 174, 174),
                0x11 => Color.FromArgb(15, 99, 179),
                0x12 => Color.FromArgb(64, 81, 208),
                0x13 => Color.FromArgb(120, 65, 204),
                0x14 => Color.FromArgb(167, 54, 169),
                0x15 => Color.FromArgb(192, 52, 112),
                0x16 => Color.FromArgb(189, 60, 48),
                0x17 => Color.FromArgb(159, 74, 0),
                0x18 => Color.FromArgb(109, 92, 0),
                0x19 => Color.FromArgb(54, 109, 0),
                0x1A => Color.FromArgb(7 , 119, 4),
                0x1B => Color.FromArgb(0, 121, 61),
                0x1C => Color.FromArgb(0, 114, 125),
                0x1D => Color.FromArgb(78, 78, 78),
                0x1E => Color.FromArgb(0, 0, 0),
                0x1F => Color.FromArgb(0, 0, 0),

                0x20 => Color.FromArgb(254, 254, 255),
                0x21 => Color.FromArgb(93, 179, 255),
                0x22 => Color.FromArgb(143, 161, 255),
                0x23 => Color.FromArgb(200, 144, 255),
                0x24 => Color.FromArgb(247, 133, 250),
                0x25 => Color.FromArgb(255, 131, 192),
                0x26 => Color.FromArgb(255, 139, 127),
                0x27 => Color.FromArgb(239, 154, 73),
                0x28 => Color.FromArgb(189, 172, 44),
                0x29 => Color.FromArgb(133, 188, 47),
                0x2A => Color.FromArgb(85, 199, 83),
                0x2B => Color.FromArgb(60, 201, 140),
                0x2C => Color.FromArgb(62, 194, 205),
                0x2D => Color.FromArgb(78, 78, 78),
                0x2E => Color.FromArgb(0, 0, 0),
                0x2F => Color.FromArgb(0, 0, 0),

                0x30 => Color.FromArgb(254, 254, 255), // here
                0x31 => Color.FromArgb(188, 223, 255),
                0x32 => Color.FromArgb(42, 42, 42),
                0x33 => Color.FromArgb(232, 209, 255),
                0x34 => Color.FromArgb(251, 205, 253),
                0x35 => Color.FromArgb(255, 204, 229),
                0x36 => Color.FromArgb(251, 205, 253),
                0x37 => Color.FromArgb(84, 67, 75),
                0x38 => Color.FromArgb(255, 204, 229),
                0x39 => Color.FromArgb(122, 118, 90),
                0x3A => Color.FromArgb(185, 232, 184),
                0x3B => Color.FromArgb(174, 232, 208),
                0x3C => Color.FromArgb(175, 229, 234),
                0x3D => Color.FromArgb(133, 188, 47),
                0x3E => Color.FromArgb(0, 0, 0),
                0x3F => Color.FromArgb(0, 0, 0),

                _ => throw new Exception($"Invalid colour: {colour:X2}"),
            };
        }

        internal void renderPixel()
        {
            if (x >= RenderWidth || y >= RenderHeight)
                return;

            // Apply scrolling
            var scrollX = 0ul;
            var scrollY = 0ul;
            var ox = (x + scrollX) % RenderWidth;
            var oy = (y + scrollY) % RenderHeight;

            // Lookup tile data
            var tileX = x / 8;
            var tileY = y / 8;
            var ntL = BaseTableAddr - 0x2000ul + (tileY * 32) + tileX;
            var nametableEntry = VRAM[ntL];

            // Lookup attr data
            var attrX = tileX / 4;
            var attrY = tileY / 4;
            var attr = VRAM[AttrTableAddr + (attrY * 8) + attrX];

            // Lookup quadrant data
            var quadX = (attrX % 4) / 2;
            var quadY = (attrY % 4) / 2;

            var colourBits = (attr >> (byte)((quadX * 2) + (quadY * 4))) & 0x3;

            var firstEntry = nametableEntry * 0x10 + (int)y % 8;
            var secondEntry = nametableEntry * 0x10 + (int)y % 8 + 8;
            var firstPlaneByte  = VRAMPage0000[firstEntry];
            var secondPlaneByte = VRAMPage0000[secondEntry];

            var firstPlaneBit  = (byte)(firstPlaneByte >> (byte)(7 - x % 8) & 1);
            var secondPlaneBit = (byte)(firstPlaneByte >> (byte)(7 - x % 8) & 1);

            var paletteIndex = firstPlaneBit + (secondPlaneBit * 2) + (colourBits * 4);
            var pixelColour = lookupBGPalette((byte)paletteIndex);

            // Store in our BGR buffer
            Bgr24Bitmap[(ox + oy * RenderWidth) * 3 + 0] = pixelColour.B; // (byte)((pixelColour.B + frame) % 0xFF);
            Bgr24Bitmap[(ox + oy * RenderWidth) * 3 + 1] = pixelColour.G;
            Bgr24Bitmap[(ox + oy * RenderWidth) * 3 + 2] = pixelColour.R;
        }
    }
}