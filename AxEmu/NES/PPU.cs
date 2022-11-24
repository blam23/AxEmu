using System.Drawing;

namespace AxEmu.NES
{
    public class PPU
    {
        private Emulator system;

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
                isSpriteZero = false;
            }
        }

        internal struct VRegister
        {
            public byte coarseX;
            public byte coarseY;
            public byte ntlX;
            public byte ntlY;
            public byte fineY;

            private ushort full;

            public void Set(ushort addr)
            {
                full = addr;

                // this should be set according to bits in addr.
                coarseX = 0;
                coarseY = 0;
                ntlX = 0;
                ntlY = 0;
                fineY = 0;
            }

            public ushort Get() => full;

            internal void Increment(ushort amount)
            {
                full += amount;
            }
        }

        // Register data
        // https://www.nesdev.org/wiki/PPU_scrolling#PPU_internal_registers
        internal struct Registers
        {
            public VRegister v; // Current VRAM reg
            public VRegister t; // Temporary VRAM reg
            public byte fineX;      // Fine scroll (3 bit)
        }
        internal Registers reg;
        internal bool regLatch = false;

        // Constants
        public const uint ClocksPerLine = 341;
        public const uint LinesPerFrame = 262;
        public const uint VBlankLine    = 241;
        public const uint AttrTableAddr = 0x3c0;
        public const uint RenderWidth   = 256;
        public const uint RenderHeight  = 240;

        // Frame Info
        internal ulong frame = 0;
        internal bool  oddFrame          = false;
        internal bool  vblank            = false;
        internal bool  dontVBlank        = false;
        internal bool  spriteZeroHit     = false;
        internal bool  spriteOverflow    = false;

        // Pixel data
        internal ushort clock      = 0;
        internal int scanline      = 0;
        internal bool scrollingX   = true;
        internal byte scrollX      = 0;
        internal byte scrollY      = 0;

        // Sprite data
        internal List<Sprite> currentSprites = new(); // sprite data for current scanline

        // Control Info
        internal ushort     OAMAddress        = 0x0;
        internal ushort     BaseTableAddr     = 0x2000;
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

        internal bool Rendering => ShowBackground || ShowSprites;

        // Data
        internal PPUMemoryBus mem;
        internal byte[] Bgr24Bitmap   = new byte[RenderWidth * RenderHeight * 3];
        internal readonly byte[] OAM  = new byte[0x100];

        // Events
        public delegate void PPUFrameEvent(byte[] bitmap);
        public event PPUFrameEvent? FrameCompleted;

        protected virtual void OnFrameCompleted()
        {
            FrameCompleted?.Invoke(Bgr24Bitmap);
        }

        // Registers
        bool writeAddrUpper = true;
        internal ushort addr = 0;

        public PPU(Emulator system)
        {
            this.system = system;
            mem = new PPUMemoryBus(system);
        }

        public void Clock()
        {
            if(scanline >= -1 && scanline < 240)
            {
                //if (scanline == 0 && clock == 0 && oddFrame && Rendering)
                //    clock = 1;

                if (scanline == -1 && clock == 1)
                {
                    // New frame - so clear everything
                    vblank         = false;
                    spriteOverflow = false;
                    spriteZeroHit  = false;
                }

                if ((clock >= 2 && clock < 258))
                    IncrementX();

                if (scanline >= 0)
                {
                    var solid = renderPixelBackground();
                    renderPixelSprite(solid);
                }
            }

            if (clock == 256)
                IncrementY();

            if (clock == 257)
                TransferX();

            if (scanline == -1 && clock > 280 && clock < 305)
                TransferY();

            if (scanline == 241 && clock == 1)
            {
                vblank = true;

                if (NMIOnVBlank)
                    system.cpu.SetNMI();
            }

            if(Rendering && clock == 260 && scanline < 240)
                system.mapper.Scanline();

            if(clock == 320 && scanline >= 0 && scanline < RenderHeight)
                SetupSpritesOnNextLine();

            clock++;

            if(clock >= 341)
            {
                clock = 0;
                scanline++;

                if(scanline >= 261)
                {
                    scanline = -1;
                    oddFrame = !oddFrame;

                    OnFrameCompleted();
                }
            }    
        }

        private byte StatusByte()
        {
            byte status = 0;

            if (vblank)
                status |= 0x80;

            if (spriteZeroHit)
                status |= 0x40;

            vblank = false;
            writeAddrUpper = true;

            if (scanline == 241 && (clock >= 1 || clock <= 3))
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
        }

        private void WriteAddr(byte value)
        {
            if (!regLatch)
            {
                // Set high nibble
                var current = reg.t.Get();
                var tUpper = (ushort)((value & 0x3F) << 8 | (current & 0xFF));
                reg.t.Set(tUpper);
            }
            else
            {
                // Set low nibble
                var current = reg.t.Get();
                var tLower = (ushort)((current & 0xFF00) | value);
                reg.t.Set(tLower);

                reg.v = reg.t;
            }

            regLatch = !regLatch;
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
            byte ret;

            if (addr < 0x3EFF)
                ret = BufferedRead(mem.Read(reg.v.Get()));
            else
                ret = mem.Read(reg.v.Get());

            reg.v.Increment(VRAMAddrInc);
            return ret;
        }

        private void WriteAddrValue(byte value)
        {
            mem.Write(reg.v.Get(), value);
            reg.v.Increment(VRAMAddrInc);
        }

        private void WriteScroll(byte value)
        {
            if (!regLatch)
            {
                reg.fineX = (byte)(value & 0x07);
                reg.t.coarseX = (byte)(value >> 3);
            }
            else
            {
                reg.t.fineY = (byte)(value & 0x07);
                reg.t.coarseY = (byte)(value >> 3);
            }

            regLatch = !regLatch;
        }

        private void IncrementX()
        {
            if (!Rendering)
                return;

            if (reg.fineX < 7)
            {
                reg.fineX++;
            }
            else
            {
                if (reg.v.coarseX == 31)
                {
                    reg.v.coarseX = 0;
                    reg.v.ntlX = (byte)~reg.v.ntlX;
                }
                else
                {
                    reg.v.coarseX++;
                }
                reg.fineX = 0;
            }
        }

        private void IncrementY()
        {
            if (!Rendering)
                return;

            if (reg.v.fineY < 7)
            {
                reg.v.fineY++;
            }
            else
            {
                reg.v.fineY = 0;

                if (reg.v.coarseY == 29)
                {
                    reg.v.coarseY = 0;
                    reg.v.ntlY = (byte)~reg.v.ntlY;
                }
                else if (reg.v.coarseY == 31)
                {
                    reg.v.coarseY = 0;
                }
                else
                {
                    reg.v.coarseY++;
                }
            }
        }

        private void TransferX()
        {
            if (!Rendering)
                return;

            reg.v.ntlX = reg.t.ntlX;
            reg.v.coarseX = reg.t.coarseX;
        }

        private void TransferY()
        {
            if (!Rendering)
                return;

            reg.v.ntlY = reg.t.ntlY;
            reg.v.coarseY = reg.t.coarseY;
            reg.v.fineY = reg.t.fineY;
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
            // Wrap access - 0x2000 == 0x2008 == 0x2010 == 0x2018 == ... == 0x3FF8
            var instr = address & 0x000F;

            switch(instr)
            {
                case 0x0: WriteCtrl(value); break;
                case 0x1: WriteMask(value); break;
                case 0x2: WriteMask(value); break;
                case 0x3: WriteOAMAddr(value); break;
                case 0x4: WriteOAM(value); break;
                case 0x5: WriteScroll(value); break;
                case 0x6: WriteAddr(value); break;
                case 0x7: WriteAddrValue(value); break;

                default:
                    throw new Exception($"Unknown PPU instruction: {address}");
            }
        }

        private void WriteOAMAddr(byte value)
        {
            OAMAddress = value;
        }

        private void WriteOAM(byte value)
        {
            // FIXME: Remove
            OAMAddress %= (ushort)OAM.Length;

            OAM[OAMAddress] = value;
            OAMAddress++;
        }

        internal Color lookupBGPalette(byte paletteIndex)
        {
            var p = mem.Read((ushort)(0x3F00 + paletteIndex));
            return lookupColour(p);
        }

        internal Color lookupSpritePalette(byte paletteIndex)
        {
            ushort addr = paletteIndex switch
            {
                0x0 => 0x3F00,
                0x4 => 0x3F04,
                0x8 => 0x3F08,
                0xc => 0x3F0C,
                _ => (ushort)(0x3F10 + paletteIndex),
            };

            var p = mem.Read(addr);
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
                ||        clock <  sprite.x
                ||        clock >= (ulong)(sprite.x + 8)
                ||        clock <  8 && !ShowSpritesLeftmost;
        }

        internal void renderPixelSprite(bool solidBG)
        {
            if (clock >= RenderWidth || scanline >= RenderHeight)
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
                var palette  =  sprite.attr & 0x3;
                var front    = (sprite.attr & 0x20) == 0;
                var flipX    = (sprite.attr & 0x40) == 0x40;
                var flipY    = (sprite.attr & 0x80) == 0x80;

                // Figure out which pixel in the sprite we're rendering
                byte pixelX = (byte)(clock - sprite.x);
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

                //var tbl = address >= 0x1000 ? VRAMPage1000 : VRAMPage0000;
                //var upperBit = (tbl[(address + 8) % 0x1000] & (0x80u >> pixelX)) >> (7 - pixelX);
                //var lowerBit = (tbl[address % 0x1000] & (0x80ul >> pixelX)) >> (7 - pixelX);

                byte upperBit = (byte)((mem.Read((ushort)(address + 8)) & (0x80u >> pixelX)) >> (7 - pixelX));
                byte lowerBit = (byte)((mem.Read(address)               & (0x80u >> pixelX)) >> (7 - pixelX));
                byte colour = (byte)((upperBit << 1) | lowerBit);
                
                if (colour > 0)
                {
                    // Check if spriteZeroHit should be set
                    if(sprite.isSpriteZero
                        && (clock > 8 || ShowSpritesLeftmost)
                        && (!solidBG)
                        && (!spriteZeroHit)
                        && (clock != 255)
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
                        Bgr24Bitmap[(clock + scanline * RenderWidth) * 3 + 0] = pixelColour.B;
                        Bgr24Bitmap[(clock + scanline * RenderWidth) * 3 + 1] = pixelColour.G;
                        Bgr24Bitmap[(clock + scanline * RenderWidth) * 3 + 2] = pixelColour.R;

                        // Don't need to continue - we want the first sprite to be on top
                        return;
                    }
                }
            }
        }

        internal bool renderPixelBackground()
        {
            if (clock >= RenderWidth || scanline >= RenderHeight)
                return false;

            // Calculate pixel to render
            ulong ox = (ulong)((reg.v.coarseX << 3) | (reg.fineX));
            ulong oy = (ulong)((reg.v.coarseY << 3) | (reg.v.fineY));

            var bta = BaseTableAddr;

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
                bta ^= 0x800;
                oy -= RenderHeight;
            }

            // Lookup tile data
            var tileX = reg.v.coarseX;
            var tileY = reg.v.coarseY;
            var ntL = (ushort)(bta + tileY * 32 + tileX);
            var nametableEntry = mem.Read(ntL);

            // Lookup attr data
            var attrX = tileX / 4;
            var attrY = tileY / 4;
            var attr = mem.Read((ushort)(bta + AttrTableAddr + attrY * 8 + attrX));

            // Lookup quadrant data
            var quadX = (tileX % 4) / 2;
            var quadY = (tileY % 4) / 2;

            var colourBits = (attr >> (byte)((quadX * 2) + (quadY * 4))) & 0x3;

            ushort entry = (ushort)(BackTableAddr + nametableEntry * 0x10 + ((int)oy % 8));

            byte firstPlaneByte = mem.Read(entry);
            byte secondPlaneByte = mem.Read((ushort)(entry + 8));

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
            Bgr24Bitmap[(clock + scanline * RenderWidth) * 3 + 0] = pixelColour.B;
            Bgr24Bitmap[(clock + scanline * RenderWidth) * 3 + 1] = pixelColour.G;
            Bgr24Bitmap[(clock + scanline * RenderWidth) * 3 + 2] = pixelColour.R;

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

                OAM[OAMAddress] = system.cpu.bus.Read(from);
                OAMAddress++;
                from++;
            }
        }

        internal void UpdateMirroring()
        {
            mem.UpdateMirroring();
        }
    }
}