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

        internal struct Sprite
        {
            public byte x;
            public byte y;
            public bool isSpriteZero;
            public byte attr;
            public byte idx;

            public Sprite(byte x, byte y, byte attr, byte idx)
            {
                this.x = x;
                this.y = y;
                this.attr = attr;
                this.idx = idx;
            }
        }

        // Register data
        // https://www.nesdev.org/wiki/PPU_scrolling#PPU_internal_registers
        internal struct Registers
        {
            public ushort v; // Current VRAM Address
            public ushort t; // Temporary VRAM Address (top left onscreen tile)
            public byte x; // Fine scroll (3 bit)
            public bool w; // First or second write toggle
        }
        internal Registers reg;

        // Constants
        public const uint ClocksPerLine = 341;
        public const uint LinesPerFrame = 262;
        public const uint VBlankLine    = 241;
        public const uint AttrTableAddr = 0x3c0;
        public const uint RenderWidth   = 256;
        public const uint RenderHeight  = 240;

        // Frame Info
        internal ulong frame = 0;
        internal ulong clock;
        internal bool  oddEvenFlag       = false;
        internal bool  vblank            = false;
        internal bool  dontVBlank        = false;
        internal bool  renderingEnabled  = false;
        internal bool  spriteZeroHit     = false;

        // Pixel data
        internal ulong scanX       = 0;
        internal ulong scanline    = 0;
        internal ulong scrollX     = 0;
        internal ulong scrollY     = 0;

        // Sprite data
        internal List<Sprite> currentSprites = new(); // sprite data for current scanline

        // Control Info
        internal ushort     OAMAddress        = 0x0;
        internal ushort     VRAMAddrInc       = 0x1;
        internal ushort     SpriteTableAddr   = 0x0;
        internal ushort     BackTableAddr     = 0x0;
        internal SpriteSize CurrentSpriteSize = SpriteSize.s8x8;
        internal bool       WriteEXTPins      = false;
        internal bool       NMIOnVBlank       = false;

        // Mask info
        internal bool Greyscale              = false;
        internal bool ShowBackgroundLeftmost = false;
        internal bool ShowSpritesLeftmost    = false;
        internal bool ShowBackground         = false;
        internal bool ShowSprites            = false;
        internal bool EmphasizeRed           = false;
        internal bool EmphasizeGreen         = false;
        internal bool EmphasizeBlue          = false;

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
                scanX    = clock % ClocksPerLine;
                scanline = clock / ClocksPerLine;

                if(scanX >= 257 && scanX <= 320 
                    && ((scanline > 1 && scanline < 239) || scanline == 261))
                {
                    OAMAddress = 0;
                }

                if(renderingEnabled && !vblank && scanX == 256
                    && (scanline < 240 || scanline == 261))
                {
                    //incrementY();
                }

                // Copy over horizontal bits
                if (renderingEnabled && scanX == 257)
                {
                    ushort horiBits = (ushort)(reg.t & 0x041f);
                    reg.v &= 0xfbe0;
                    reg.v |= horiBits;
                }

                // Copy over vertical bits
                if (renderingEnabled && (scanX >= 280 && scanX < 304) && scanline == LinesPerFrame)
                {
                    ushort vertiBits = (ushort)(reg.t & 0x7be0);
                    reg.v &= 0x041f;
                    reg.v |= vertiBits;
                }

                // Calculate scroll offsets
                scrollX = (ushort)((ushort)((reg.v & 0x1f) << 3) + reg.x);
                scrollY = (ushort)((((reg.v >> 5) & 0x1f) << 3) + (reg.v >> 12));

                // Start of new line - setup sprite data.
                if (scanX == 320 && scanline >= 0 && scanline < RenderHeight)
                    SetupSpritesOnNextLine();

                bool solidBG = false;
                if (ShowBackground)
                    solidBG = renderPixelBackground();

                if (ShowSprites)
                    renderPixelSprite(solidBG);

                clock++;

                if (scanline == VBlankLine && scanX == 1)
                {
                    vblank = true;
                }

                if (scanline == 261 && scanX == 1)
                { 
                    frame++;
                    OnFrameCompleted();

                    if (NMIOnVBlank)
                    {
                        system.cpu.SetNMI();
                    }
                }

                if (scanline == VBlankLine && scanX == 3)
                    dontVBlank = false;

                if (scanline == 261 && scanX == 1)
                {
                    vblank = false;
                    spriteZeroHit = false;
                }

                if (scanX == 339 && scanline == 261 && renderingEnabled && !oddEvenFlag)
                    clock = 0;

                if (scanX == 340 && scanline == 261)
                    oddEvenFlag = !oddEvenFlag;
            }
        }

        private void incrementXCoarse()
        {
            // https://www.nesdev.org/wiki/PPU_scrolling#Coarse_X_increment
            //
            // if ((v & 0x001F) == 31) // if coarse X == 31
            //   v &= ~0x001F          // coarse X = 0
            //   v ^= 0x0400           // switch horizontal nametable
            // else
            //   v += 1                // increment coarse X
            //

            // Make sure we get our bitwise logic correct
            // Don't want our ands to think we're signed, etc.
            ulong v = reg.v;

            if ((v & 0x001f) == 0x001f)
            {
                // if v is at 0x1f (31) we wrap
                v &= 0xFFE0;
                v ^= 0x0400; // switch hori nametable
            }
            else
                v++;

            reg.v = (ushort)v;
        }

        private void incrementY()
        {
            // https://www.nesdev.org/wiki/PPU_scrolling#Y_increment
            //
            // if ((v & 0x7000) != 0x7000)        // if fine Y < 7
            //   v += 0x1000                      // increment fine Y
            // else
            //   v &= ~0x7000                     // fine Y = 0
            //   int y = (v & 0x03E0) >> 5        // let y = coarse Y
            //   if (y == 29)
            //     y = 0                          // coarse Y = 0
            //     v ^= 0x0800                    // switch vertical nametable
            //   else if (y == 31)
            //     y = 0                          // coarse Y = 0, nametable not switched
            //   else
            //     y += 1                         // increment coarse Y
            //   v = (v & ~0x03E0) | (y << 5)     // put coarse Y back into v
            //

            // Make sure we get our bitwise logic correct
            // Don't want our ands to think we're signed, etc.
            ulong orv = reg.v;

            if ((orv & 0x7000) != 0x7000) // cy < 7
                orv += 0x1000;            // inc cy
            else
            {
                orv &= 0x8FFF;
                ushort y = (ushort)((orv & 0x03E0) >> 5);
                if (y == 0x1D)
                {
                    y = 0;           // wrap y
                    orv ^= 0x0800; // switch vertical nametable
                }
                else if (y == 0x1F)
                    y = 0;
                else
                    y++;

                orv = (orv & 0xFC1F) | (ushort)(y << 5);
            }

            reg.v = (ushort)orv;
        }

        private void incrementV()
        {
            if ((ShowBackground || ShowSprites) && (scanline < 240 || scanline == 261))
            {
                //incrementXCoarse();
                //incrementY();
            }
            else
            {
                reg.v += VRAMAddrInc;
            }
        }

        private byte ReadVRAM(ushort addr)
        {
            return VRAM[addr - 0x2000];
        }

        private byte ReadVRAM(ulong addr)
        {
            return VRAM[addr - 0x2000];
        }

        private void WriteVRAM(ushort addr, byte value)
        {
            VRAM[addr - 0x2000] = value;
        }

        private byte StatusByte()
        {
            byte status = 0;

            if (vblank)
                status |= 0x80;

            if (spriteZeroHit)
                status |= 0x40;

            vblank = false;
            reg.w = false;

            if (scanline == 241 && (scanX >= 1 || scanX <= 3))
                dontVBlank = true;

            return status;
        }

        private void WriteCtrl(byte value)
        {
            // Set Base Table Addr value in t register
            var gh = (value & 0x3);
            reg.t &= 0xF3FF;
            reg.t |= (ushort)(gh << 0xA);

            VRAMAddrInc       = (value & 0x04) == 0x04 ? (ushort)0x0020   : (ushort)0x0001;
            SpriteTableAddr   = (value & 0x08) == 0x08 ? (ushort)0x1000   : (ushort)0x0000;
            BackTableAddr     = (value & 0x10) == 0x10 ? (ushort)0x1000   : (ushort)0x0000;
            CurrentSpriteSize = (value & 0x20) == 0x20 ? SpriteSize.s8x16 : SpriteSize.s8x8;
            WriteEXTPins      = (value & 0x40) == 0x40;
            NMIOnVBlank       = (value & 0x80) == 0x80;
        }

        private static readonly Dictionary<ushort, ushort> addressMirrors = new()
        {
            { 0x3F10, 0x3F00 },
            { 0x3F14, 0x3F04 },
            { 0x3F18, 0x3F08 },
            { 0x3F1C, 0x3F0C },
        };

        private static ushort MirrorAddress(ushort address)
        {
            if (addressMirrors.TryGetValue(address, out var fixedAddress))
                return fixedAddress;

            return address;
        }

        private void WriteMask(byte value)
        {
            Greyscale              = (value & 0x01) == 0x01;
            ShowBackgroundLeftmost = (value & 0x02) == 0x02;
            ShowSpritesLeftmost    = (value & 0x04) == 0x04;
            ShowBackground         = (value & 0x08) == 0x08;
            ShowSprites            = (value & 0x10) == 0x10;
            EmphasizeRed           = (value & 0x20) == 0x20;
            EmphasizeGreen         = (value & 0x40) == 0x40;
            EmphasizeBlue          = (value & 0x80) == 0x80;

            renderingEnabled = ShowBackground || ShowSprites;
        }

        private void WriteAddr(byte value)
        {
            if (reg.w == false)
            {
                var lower = value & 0x003F;
                reg.t &= 0x00FF;
                reg.t |= (ushort)(lower << 8);
                reg.w = true;
            }
            else
            {
                reg.t &= 0xFF00;
                reg.t |= value;

                // Here we actually update the VRAM address (v) with the buffer reg (t)
                reg.v = reg.t;

                reg.w = false;
            }
        }

        private void WriteScroll(byte value)
        {
            // Here we split the given scroll byte into the upper 5 and lower 3 bits.
            // Upper5 corresponds to ABCDE
            // Lower3 corresponds to FGH
            // https://www.nesdev.org/wiki/PPU_scrolling#$2005_first_write_(w_is_0)

            var low3 = (ushort)(value & 0x7);
            var upper5 = (ushort)(value >> 3);

            if (reg.w == false)
            {
                reg.t &= 0xFFE0;
                reg.t |= upper5;

                reg.x = (byte)low3;

                reg.w = true;
            }
            else
            {
                reg.t &= 0x0C1F;
                reg.t |= (ushort)(low3 << 12);
                reg.t |= (ushort)(upper5 << 5);

                reg.w = false;
            }
        }

        private byte readBuffer = 0;
        private byte BufferedRead(byte newValue)
        {
            var ret = readBuffer;
            readBuffer = newValue;
            return ret;
        }

        private byte ReadAddrValue()
        {
            byte ret = 0;

            var address = MirrorAddress(reg.v);

            if (address < 0x1000)
            {
                ret = BufferedRead(VRAMPage0000[address]);
            }
            else if (address < 0x2000)
            {
                ret = BufferedRead(VRAMPage1000[address - 0x1000]);
            }
            else if (address < 0x3EFF)
            {
                ret = BufferedRead(ReadVRAM(address));
            }
            else if (address < 0x4000)
            {
                ret = ReadVRAM(address);
            }

            incrementV();
            return ret;
        }

        private void WriteAddrValue(byte value)
        {
            var address = MirrorAddress(reg.v);

            if (address < 0x1000)
            {
                VRAMPage0000[address] = value;
            }
            else if (address < 0x2000)
            {
                VRAMPage1000[address - 0x1000] = value;
            }
            else if (address < 0x4000)
            {
                WriteVRAM(address, value);
            }

            incrementV();
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
            else if (address == 0x2001)
                WriteMask(value);
            else if (address == 0x2003)
                OAMAddress = value;
            else if (address == 0x2004)
                WriteOAM(value);
            else if (address == 0x2005)
                WriteScroll(value);
            else if (address == 0x2006)
                WriteAddr(value);
            else if (address == 0x2007)
                WriteAddrValue(value);
        }

        private void WriteOAM(byte value)
        {
            // FIXME: Remove
            OAMAddress %= (ushort)OAM.Length;

            OAM[OAMAddress] = value;
            OAMAddress++;
        }

        private Color lookupBGPalette(byte paletteIndex)
        {
            var p = ReadVRAM((ushort)(0x3f00 + paletteIndex));
            return lookupColour(p);
        }

        private Color lookupSpritePalette(byte paletteIndex)
        {
            ushort addr = paletteIndex switch
            {
                0x0 => 0x3f00,
                0x4 => 0x3f04,
                0x8 => 0x3f08,
                0xc => 0x3f0c,
                _   => (ushort)(0x3f10 + paletteIndex),
            };

            var p = ReadVRAM(addr);
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

        private void SetupSpritesOnNextLine()
        {
            var line = scanline + 1;

            currentSprites.Clear();
            uint height = CurrentSpriteSize == SpriteSize.s8x16 ? 16u : 8u;

            for (int i = 0; i < OAM.Length; i+=4)
            {
                var y = (byte)(OAM[i] + 1);

                if (y > 0xef)
                    continue;

                if (line >= y && line < y + height)
                {
                    currentSprites.Add(new Sprite {
                        isSpriteZero = i == 0,
                        y = y,
                        idx = OAM[i + 1],
                        attr = OAM[i + 2],
                        x = OAM[i + 3],
                    });
                }

                if (currentSprites.Count == 8)
                    return;
            }
        }

        private bool ignoreSprite(Sprite sprite)
        {
            return sprite.y == 0
                || sprite.y >  RenderHeight - 1
                ||        scanX <  sprite.x
                ||        scanX >= (ulong)(sprite.x + 8)
                ||        scanX <  8 && !ShowSpritesLeftmost;
        }

        internal void renderPixelSprite(bool solidBG)
        {
            if (scanX >= RenderWidth || scanline >= RenderHeight)
                return;

            bool tallSprites = CurrentSpriteSize == SpriteSize.s8x16;

            foreach (var sprite in currentSprites)
            {
                if (ignoreSprite(sprite))
                    continue;

                // Figure out tile index
                ushort tile = sprite.idx;
                if (tallSprites)
                    tile = (byte)(tile & ~0x1);
                tile *= 16;

                // Get data from sprite attrs
                var palette  = sprite.attr & 0x3;
                var front    = (sprite.attr & 0x20) == 0;
                var flipX    = (sprite.attr & 0x40) == 0x40;
                var flipY    = (sprite.attr & 0x80) == 0x80;

                // Figure out which pixel in the sprite we're rendering
                byte pixelX = (byte)(scanX - sprite.x);
                byte pixelY = (byte)(scanline - sprite.y);

                // Find out what table addr to use
                ushort table = tallSprites ?
                               (ushort)((sprite.idx & 1) * 1000) :
                               SpriteTableAddr;

                // If we're using tall sprites,
                //  make sure we're rendering the bottom half correctly.
                if (tallSprites)
                {
                    if (pixelY >= 8)
                    {
                        pixelY -= 8;
                        if (!flipY)
                            tile += 16;
                        flipY = false;
                    }
                    if (flipY) tile += 16;
                }

                // Flip around sprite if required
                pixelX = flipX ? (byte)(7 - pixelX) : pixelX;
                pixelY = flipY ? (byte)(7 - pixelY) : pixelY;

                // Calc colour address
                ushort address = (ushort)(table + tile + pixelY);

                var tbl = address >= 0x1000 ? VRAMPage1000 : VRAMPage0000;
                var upperBit = (tbl[(address + 8) % 0x1000] & (0x80u >> pixelX)) >> (7 - pixelX);
                var lowerBit = (tbl[address % 0x1000] & (0x80ul >> pixelX)) >> (7 - pixelX);

                byte colour = (byte)((upperBit << 1) | lowerBit);
                
                if (colour > 0)
                {
                    // Check if spriteZeroHit should be set
                    if(sprite.isSpriteZero
                        && (scanX > 8 || ShowSpritesLeftmost)
                        && (!solidBG)
                        && (!spriteZeroHit)
                        && (scanX != 255)
                      )
                    {
                        spriteZeroHit = true;
                    }

                    // Check if we should draw our sprite pixel
                    if (ShowBackground && (front || !solidBG))
                    {
                        var paletteIndex = palette * 4 + colour;
                        var pixelColour = lookupSpritePalette((byte)paletteIndex);

                        // Store in our BGR buffer
                        Bgr24Bitmap[(scanX + scanline * RenderWidth) * 3 + 0] = pixelColour.B;
                        Bgr24Bitmap[(scanX + scanline * RenderWidth) * 3 + 1] = pixelColour.G;
                        Bgr24Bitmap[(scanX + scanline * RenderWidth) * 3 + 2] = pixelColour.R;

                        // Don't need to continue - we want the first sprite to be on top
                        return;
                    }
                }
            }
        }

        internal bool renderPixelBackground()
        {
            if (scanX >= RenderWidth || scanline >= RenderHeight)
                return false;

            // Apply scrolling
            var ox = scanX + scrollX;
            ulong oy = scanline;

            var baseIndex = (ushort)((reg.v >> 10) & 0x3);
            ushort bta = baseIndex switch
            {
                0 => 0x2000,
                1 => 0x2400,
                2 => 0x2800,
                3 => 0x2c00,
                _ => 0x0000
            };



            // Wrap around and move to the next tileset
            if (ox >= RenderWidth)
            {
                // TODO: Add vertical mirroring support
                bta ^= 0x400;
                ox -= RenderWidth;
            }
            if (oy >= RenderHeight)
            {
                // TODO: Add vertical mirroring support
                //bta ^= 0x400;
                oy -= RenderHeight;
            }

            // Lookup tile data
            var tileX = ox / 8;
            var tileY = oy / 8;
            var ntL = bta + tileY * 32 + tileX;
            var nametableEntry = ReadVRAM(ntL);

            // Lookup attr data
            var attrX = tileX / 4;
            var attrY = tileY / 4;
            var attr = ReadVRAM(bta + AttrTableAddr + attrY * 8 + attrX);

            // Lookup quadrant data
            var quadX = (tileX % 4) / 2;
            var quadY = (tileY % 4) / 2;

            var colourBits = (attr >> (byte)((quadX * 2) + (quadY * 4))) & 0x3;

            var entry = nametableEntry * 0x10 + ((int)oy % 8);

            var tbl = BackTableAddr == 0x0000 ? VRAMPage0000: VRAMPage1000;
            byte firstPlaneByte = tbl[entry];
            byte secondPlaneByte = tbl[entry + 8];

            var firstPlaneBit  = (byte)((firstPlaneByte >> (byte)(7 - (ox % 8))) & 1);
            var secondPlaneBit = (byte)((secondPlaneByte >> (byte)(7 - (ox % 8))) & 1);

            Color pixelColour;
            bool solid = false;
            if (firstPlaneBit == 0 && secondPlaneBit == 0)
            {
                pixelColour = lookupBGPalette(0);
            }
            else
            {
                var paletteIndex = firstPlaneBit + (secondPlaneBit * 2) + (colourBits * 4);
                pixelColour = lookupBGPalette((byte)paletteIndex);
                solid = true;
            }

            // Store in our BGR buffer
            Bgr24Bitmap[(scanX + scanline * RenderWidth) * 3 + 0] = pixelColour.B;
            Bgr24Bitmap[(scanX + scanline * RenderWidth) * 3 + 1] = pixelColour.G;
            Bgr24Bitmap[(scanX + scanline * RenderWidth) * 3 + 2] = pixelColour.R;

            return solid;
        }


        public void OAMDMA(byte value)
        {
            // TODO: CPU cycles

            var from = (ushort)(value << 8);
            for(uint i = 0; i <= 0xFF; i++)
            {
                // FIXME: Remove
                OAMAddress %= (ushort)OAM.Length;

                OAM[OAMAddress] = system.memory.Read(from);
                OAMAddress++;
                from++;
            }
        }
    }
}