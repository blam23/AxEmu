using System.Diagnostics;
using System.Drawing;
using static AxEmu.GBC.CPU;

namespace AxEmu.GBC;

internal struct OAMEntry
{
    public byte x;
    public byte y;
    public byte tile;

    public byte palette;
    public byte dmaP;
    public bool vramBank;
    public bool flipX;
    public bool flipY;
    public bool bgPrio;

    public OAMEntry(Span<byte> data)
    {
        y = data[0];
        x = data[1];
        tile = data[2];

        var flags = data[3];
        palette   = (byte)(flags & 0x7);
        vramBank  = Byte.get(flags, 3);
        dmaP      = Byte.get(flags, 4) ? (byte)1 : (byte)0;
        flipX     = Byte.get(flags, 5);
        flipY     = Byte.get(flags, 6);
        bgPrio    = Byte.get(flags, 7);
    }
}

internal class FIFO
{
    private PPU ppu;

    internal enum Mode
    {
        Tile,
        DataLow,
        DataHigh,
        Sleep,
        Push
    }

    public FIFO(PPU ppu)
    {
        this.ppu = ppu;
    }

    private Queue<Color> fifo = new();

    internal byte pushedX;
    private byte fetchX;
    private byte mapX;
    private byte mapY;
    private byte tileY;
    private byte lineX;
    private byte fifoX;

    private Mode mode;

    internal byte currentTile;
    private byte tileHi;
    private byte tileLo;
    private bool inWindow = false;

    private byte[] fetchData = new byte[3];

    private List<OAMEntry> sprites = new();
    private List<byte> spriteData = new();

    Random rng = new Random();

    private void adjustTile()
    {
        currentTile += 128;
    }

    private void getTile()
    {
        var tileMap = Byte.get(ppu.LCDC, 3) ? 0x9C00 : 0x9800;

        ushort addr = (ushort)(tileMap + (mapX / 8) + ((mapY / 8) * 32));

        currentTile = ppu.bus.Read(addr);

        if (!Byte.get(ppu.LCDC, 4))
            adjustTile();
    }

    private void getWindow()
    {
        var wy = ppu.WY;
        var wx = ppu.WX;

        if (fetchX + 7 >= wx && fetchX+7 < wx + PPU.RenderWidth + 14
            && ppu.LY >= wy && ppu.LY < wy + PPU.RenderHeight)
        {
            var ty = ppu.windowLine / 8;

            var winMap = Byte.get(ppu.LCDC, 6) ? 0x9C00 : 0x9800;
            ushort addr = (ushort)((winMap + (fetchX + 7 - wx) / 8) + ty * 32);
            currentTile = ppu.bus.Read(addr);

            if (!Byte.get(ppu.LCDC, 4))
                adjustTile();

            inWindow = true;
        }
    }

    private void getSprites()
    {
        foreach(var sprite in ppu.spritesOnLine)
        {
            var sx = (sprite.x - 8) + (ppu.SCX % 8);

            if ((sx >= fetchX && sx < fetchX + 8)
                || ((sx + 8) >= fetchX && (sx + 8) < fetchX + 8))
            {
                sprites.Add(sprite);
            }
        }
    }

    private void getSpriteData()
    {
        var cy = ppu.LY;
        byte height = (byte)(Byte.get(ppu.LCDC, 2) ? 16 : 8);

        foreach (OAMEntry sprite in sprites)
        {
            var ty = (cy + 16 - sprite.y) * 2;

            if (sprite.flipY)
                ty = (height * 2) - 2 - ty;

            var tile = sprite.tile;
            if (height == 16)
                tile &= 0xFE; // remove last bit

            var addr = (ushort)(0x8000 + (tile * 16) + ty);
            spriteData.Add(ppu.bus.Read(addr));
            spriteData.Add(ppu.bus.Read(++addr));
        }
    }


    private void getTileLow()
    {
        var map = Byte.get(ppu.LCDC, 4) ? 0x8000 : 0x8800;
        ushort addr = (ushort)(map + (currentTile * 16) + tileY);
        tileLo = ppu.bus.Read(addr);
    }

    private void getTileHigh()
    {
        var map = Byte.get(ppu.LCDC, 4) ? 0x8000 : 0x8800;
        ushort addr = (ushort)(map + (currentTile * 16) + tileY + 1);
        tileHi = ppu.bus.Read(addr);
    }

    // One pixel per clock
    // Pauses unless it has > 8 pixels
    private void push()
    {
        if (fifo.Count <= 8)
            return;

        var p = fifo.Dequeue();

        if (lineX > (ppu.SCX % 8))
        {
            if (ppu.LY != 0x90)
            {
                var comb = pushedX + (ppu.LY * PPU.RenderWidth);

                ppu.Bgr24Bitmap[comb * 3 + 0] = p.B;
                ppu.Bgr24Bitmap[comb * 3 + 1] = p.G;
                ppu.Bgr24Bitmap[comb * 3 + 2] = p.R;
            }

            pushedX++;

            if (ppu.dbgSlowMode)
            {
                ppu.OnFrameCompleted();
            }
        }

        lineX++;
    }

    private bool add()
    {
        // Full
        if (fifo.Count > 8)
            return false;

        var x = inWindow
            ? fetchX - (8 - (ppu.SCX % 8))
            : fetchX;

        for (var i = 0; i < 8; i++)
        {
            var bit = 7 - i;
            
            byte hi = (byte)(Byte.get(tileHi, bit) ? 1 : 0);
            byte lo = (byte)(Byte.get(tileLo, bit) ? 1 : 0);
            var colour = ppu.dmgPalette[hi << 1 | lo];

            if (!Byte.get(ppu.LCDC, 0))
                colour = 0;

            if (Byte.get(ppu.LCDC, 1))
            {
                var (spriteSet, spriteC) = fetchSpritePixel(bit, colour);
                if (spriteSet)
                    colour = spriteC;
            }

            var pixelColour = ppu.rgbLookup(colour);
            if (x >= 0)
            {
                fifo.Enqueue(pixelColour);
                fifoX++;
            }
        }

        return true;
    }

    private (bool, byte) fetchSpritePixel(int bit, byte bgColour)
    {
        for (int i = 0; i < sprites.Count; i++)
        {
            OAMEntry sprite = sprites[i];
            var x = sprite.x - 8 + (ppu.SCX % 8);

            if (x + 8 < fifoX)
                continue;

            var offset = fifoX - x;

            if (offset < 0 || offset > 7)
                continue;

            var sbit = sprite.flipX ? offset : (7 - offset);

            byte lo = (byte)(Byte.get(spriteData[i * 2], sbit)       ? 1 : 0);//(byte)((spriteData[i * 2] & (1 << bit)) >> bit);
            byte hi = (byte)(Byte.get(spriteData[(i * 2) + 1], sbit) ? 1 : 0);//(byte)((spriteData[(i * 2) + 1] & (1 << bit)) >> bit);
            var idx = hi << 1 | lo;

            // transparent
            if (hi == 0 && lo == 0)
                continue;

            if (!sprite.bgPrio || bgColour == 0)
            {
                return (true, ppu.paletteOAM[sprite.dmaP][idx]);
            }
        }

        return (false, 0);
    }

    // 3 clocks to fetch 8 pixels
    // pauses in 4th clock unless space in data
    private void fetch()
    {
        switch (mode)
        {
            case Mode.Tile:

                sprites.Clear();
                spriteData.Clear();
                inWindow = false;

                if (Byte.get(ppu.LCDC, 0))
                    getTile();

                if (Byte.get(ppu.LCDC, 1) && ppu.spritesOnLine.Count > 0)
                    getSprites();

                if (ppu.showWindow)
                    getWindow();

                mode = Mode.DataLow;
                fetchX += 8;
                break;

            case Mode.DataLow:
                getTileLow();
                getSpriteData(); // should just get low sprite data, YOLO
                mode = Mode.DataHigh;
                break;

            case Mode.DataHigh:
                getTileHigh();
                mode = Mode.Sleep;
                break;

            case Mode.Sleep:
                mode = Mode.Push;
                break;

            case Mode.Push:
                if (add())
                    mode = Mode.Tile;
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    public void Reset()
    {
        fifo = new();
        mapX = 0;
        mapY = 0;
        fetchX = 0;
        tileY = 0;
        lineX = 0;
        fifoX = 0;
        pushedX = 0;
        mode = Mode.Tile;
        fetchData = new byte[3];
    }

    public void Clock()
    {
        mapX = (byte)(fetchX + ppu.SCX);
        mapY = (byte)(ppu.LY + ppu.SCY);
        tileY = (byte)((mapY % 8) * 2);

        if ((ppu.lineDot % 2) == 0)
        {
            fetch();
        }

        push();
    }
}

internal class PPU
{
    internal enum Mode
    {
        HorizontalBlank,
        VerticalBlank,
        OAMScan,
        Drawing,
    }

    #region IO
    // https://gbdev.io/pandocs/LCDC.html#ff40--lcdc-lcd-control
    // Bit  Name                          Usage notes
    // 7    LCD and PPU enable            0=Off, 1=On
    // 6    Window tile map area          0=9800-9BFF, 1=9C00-9FFF
    // 5    Window enable                 0=Off, 1=On
    // 4    BG and Window tile data area  0=8800-97FF, 1=8000-8FFF
    // 3    BG tile map area              0=9800-9BFF, 1=9C00-9FFF
    // 2    OBJ size                      0=8x8, 1=8x16
    // 1    OBJ enable                    0=Off, 1=On
    // 0    BG and Window enable/priority 0=Off, 1=On
    [IO(Address = 0xFF40)] public byte LCDC { get; set; } = 0x91;

    //
    // https://gbdev.io/pandocs/Scrolling.html
    //
    [IO(Address = 0xFF42)] public byte SCY { get; set; } = 0;
    [IO(Address = 0xFF43)] public byte SCX { get; set; } = 0;
    [IO(Address = 0xFF44)] public byte LY  { get; set; } = 0;
    [IO(Address = 0xFF45)] public byte LYC { get; set; } = 0;
    [IO(Address = 0xFF4A)] public byte WY  { get; set; } = 0;
    [IO(Address = 0xFF4B)] public byte WX  { get; set; } = 7;

    // PPU STAT interrupts
    private bool LYCompareInterrupt = false;
    private bool OAMInterrupt       = false;
    private bool VBlankInterrupt    = false;
    private bool HBlankInterrupt    = false;

    private byte getLCDStatus()
    {
        byte ret = mode switch {
            Mode.HorizontalBlank => 0,
            Mode.VerticalBlank   => 1,
            Mode.OAMScan         => 2,
            Mode.Drawing         => 3,

            _ => throw new InvalidOperationException()
        };

        if (LY == LYC)          ret |= 0x04;
        if (HBlankInterrupt)    ret |= 0x08;
        if (VBlankInterrupt)    ret |= 0x10;
        if (OAMInterrupt)       ret |= 0x20;
        if (LYCompareInterrupt) ret |= 0x40;

        // Always set
        ret |= 0x80;

        return ret;
    }

    private void setLCDStatus(byte value)
    {
        HBlankInterrupt    = (value & 0x08) == 0x08;
        VBlankInterrupt    = (value & 0x10) == 0x10;
        OAMInterrupt       = (value & 0x20) == 0x20;
        LYCompareInterrupt = (value & 0x40) == 0x40;
    }

    
    internal byte[] dmgPalette = new byte[] { 0, 1, 2, 3 };
    internal byte[][] paletteOAM = new byte[][] { new byte[] { 0, 1, 2, 3 }, new byte[] { 0, 1, 2, 3 } };
    private void setBGPalette(byte value)
    {
        dmgPalette[3] = (byte)((value & 0xC0) >> 6);
        dmgPalette[2] = (byte)((value & 0x30) >> 4);
        dmgPalette[1] = (byte)((value & 0x0C) >> 2);
        dmgPalette[0] = (byte)(value & 0x03);
    }

    private void setOAMPalette(int palette, byte value)
    {
        paletteOAM[palette][3] = (byte)((value & 0xC0) >> 6);
        paletteOAM[palette][2] = (byte)((value & 0x30) >> 4);
        paletteOAM[palette][1] = (byte)((value & 0x0C) >> 2);
        paletteOAM[palette][0] = (byte)(value & 0x03);
    }

    [IO(Address = 0xFF41, Type=IOType.Read)]
    public static byte GetLCDStatus(Emulator system) => system.ppu.getLCDStatus();

    [IO(Address = 0xFF41, Type=IOType.Write)]
    public static void SetLCDStatus(Emulator system, byte value) => system.ppu.setLCDStatus(value);

    [IO(Address = 0xFF47, Type = IOType.Write)]
    public static void SetBGPalette(Emulator system, byte value) => system.ppu.setBGPalette(value);

    [IO(Address = 0xFF48, Type = IOType.Write)]
    public static void SetOAMPalette1(Emulator system, byte value) => system.ppu.setOAMPalette(0, value);

    [IO(Address = 0xFF49, Type = IOType.Write)]
    public static void SetOAMPalette2(Emulator system, byte value) => system.ppu.setOAMPalette(1, value);

    #endregion

    internal readonly MemoryBus bus;
    private readonly Emulator system;

    internal const int OneFrameInDots = 70224;
    internal const int LinesPerFrame = 154;
    internal const int DotsPerLine = 456;
    public const uint RenderWidth = 160;
    public const uint RenderHeight = 144;

    // Debug
    internal bool dbgSlowMode = false;
    internal byte dbgCurrentTile => fifo.currentTile;
    internal bool dbgFixLY = false;
    internal byte[] DbgSpriteBmp = new byte[RenderWidth * RenderHeight * 3];

    // Events
    public delegate void PPUFrameEvent(byte[] bitmap);
    public event PPUFrameEvent? FrameCompleted;
    internal virtual void OnFrameCompleted() => FrameCompleted?.Invoke(Bgr24Bitmap);

    internal byte[] Bgr24Bitmap = new byte[RenderWidth * RenderHeight * 3];

    // Status
    internal int dot = 0;
    internal int lineDot = 0;
    internal int scanline = 0;

    internal bool showWindow => Byte.get(LCDC, 5) && WX >= 0 && WX <= RenderWidth && WY >= 0 && WY <= RenderHeight;
    internal int windowLine = 0;

    internal int x = 0;
    internal Mode mode = Mode.OAMScan;
    internal bool vramBlocked = false;
    internal FIFO fifo;

    public bool VRAMAccessible() => !vramBlocked;

    public PPU(Emulator system)
    {
        this.system = system;
        bus = system.bus;
        bus.RegisterIOProperties(GetType(), this);

        fifo = new FIFO(this);

        frameTimer.Start();
    }

    private void ChangeMode(Mode mode)
    {
        this.mode = mode;

        // VBlank interrupt
        if (mode == Mode.VerticalBlank) { system.cpu.RequestInterrupt(Interrupt.VBlank); }

        // STAT interrupts
        if (mode == Mode.OAMScan         && OAMInterrupt)    { system.cpu.RequestInterrupt(Interrupt.Stat); }
        if (mode == Mode.VerticalBlank   && VBlankInterrupt) { system.cpu.RequestInterrupt(Interrupt.Stat); }
        if (mode == Mode.HorizontalBlank && HBlankInterrupt) { system.cpu.RequestInterrupt(Interrupt.Stat); }
    }

    private void NextLine()
    {
        if (showWindow && LY >= WY)
            windowLine++;

        LY++;

        if (LY == LYC && LYCompareInterrupt)
        {
            if (LYCompareInterrupt) { system.cpu.RequestInterrupt(Interrupt.Stat); Console.WriteLine($"LYC interrupt on line {LY}"); }
        }

        lineDot = 0;
    }

    private void HBlankClock()
    {
        if (lineDot >= DotsPerLine)
        {
            NextLine();

            if (LY >= RenderHeight)
                ChangeMode(Mode.VerticalBlank);
            else
                ChangeMode(Mode.OAMScan);
        }
    }

    Stopwatch frameTimer = new Stopwatch();

    private void VBlankClock()
    {
        if (lineDot >= DotsPerLine)
        {
            NextLine();


            if (LY >= LinesPerFrame)
            {
                if (system.LimitFrames)
                {
                    var elapsed = frameTimer.ElapsedMilliseconds;
                    if (elapsed < 13)
                    {
                        Thread.Sleep((int)(13 - elapsed));
                    }
                }

                OnFrameCompleted();

                if (dot != OneFrameInDots)
                    Console.WriteLine($"Invalid dots in frame: {dot}.");

                ChangeMode(Mode.OAMScan);
                LY = 0;
                dot = 0;
                lineDot = 0;
                windowLine = 0;
                fifo.Reset();
                frameTimer.Restart();
            }
        }
    }


    internal List<OAMEntry> spritesOnLine = new();
    private void OAMScanClock()
    {
        if (lineDot == 1)
        {
            spritesOnLine.Clear();
            getSprites();
        }

        if (lineDot >= 80)
        {
            ChangeMode(Mode.Drawing);
            fifo.Reset();
        }
    }

    private void getSprites()
    {
        var y = LY;
        byte height = (byte)(Byte.get(LCDC, 2) ? 16 : 8);
        var OAM = system.bus.OAM;

        for (int i = 0; i < 0x28; i++)
        {
            var sprite = new OAMEntry(OAM.AsSpan(i*4,4));

            if (sprite.x == 0)
                continue;

            if (sprite.y <= y + 16 && sprite.y + height > y + 16)
            {
                spritesOnLine.Add(sprite);

                if (spritesOnLine.Count > 9)
                    break;
            }
        }

        spritesOnLine = spritesOnLine.OrderBy((e) => e.x).ToList();
    }

    private void DrawClock()
    {
        fifo.Clock();

        if (fifo.pushedX >= RenderWidth)
        {
            fifo.Reset();
            ChangeMode(Mode.HorizontalBlank);
        }
    }

    public void Clock()
    {
        lineDot++;
        dot++;

        if (dbgFixLY)
            LY = 0x90;

        switch (mode)
        {
            case Mode.HorizontalBlank:
                HBlankClock();
                break;
            case Mode.VerticalBlank:
                VBlankClock();
                break;
            case Mode.OAMScan:
                OAMScanClock();
                break;
            case Mode.Drawing:
                DrawClock();
                break;
            default:
                throw new InvalidOperationException();
        }

        if (dbgFixLY)
            LY = 0x90;
    }

    private static readonly Color[] blackAndWhite = new[]
    {
        Color.White,
        Color.LightGray,
        Color.DarkGray,
        Color.Black,
    };

    private static readonly Color[] highContrastGreen = new[]
    {
        Color.FromArgb(255, 147, 153,   4),
        Color.FromArgb(255,  77, 125,  44),
        Color.FromArgb(255,  41,  99,  65),
        Color.FromArgb(255,  12,  59,  30),
    };

    private static readonly Color[] lighterGreen = new[]
    {
        Color.FromArgb(255, 147, 153,   4),
        Color.FromArgb(255,  77, 125,  44),
        Color.FromArgb(255,  41,  99,  65),
        Color.FromArgb(255,  12,  59,  30),
    };

    internal Color rgbLookup(byte c)
    {
        return c switch
        {
            0 => highContrastGreen[0],
            1 => highContrastGreen[1],
            2 => highContrastGreen[2],
            3 => highContrastGreen[3],

            _ => Color.DodgerBlue,
        };
    }
}
