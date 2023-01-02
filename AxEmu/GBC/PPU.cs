using System.Drawing;
using static AxEmu.GBC.CPU;

namespace AxEmu.GBC;

internal struct Pixel
{
    private byte colour;
    private byte palette;
    //private ushort spritePriority; // CGB only
    private bool bgAndWndPriority; // False == No, True == BG & Wnd colours 1-3 over obj
}

internal struct OAMEntry
{
    byte x;
    byte y;
    byte tile;

    byte palette;
    bool vramBank;
    bool flipX;
    bool flipY;
    bool behindBG;
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

    internal int pushedX;
    private int fetchX;
    private int mapX;
    private int mapY;
    private int tileY;
    private int lineX;
    private int fifoX;

    private Mode mode;

    internal byte currentTile;
    private byte tileHi;
    private byte tileLo;

    private byte[] fetchData = new byte[3];

    Random rng = new Random();

    private void getTile()
    {
        var tileMap = Byte.get(ppu.LCDC, 3) ? 0x9C00 : 0x9800;

        ushort addr = (ushort)(tileMap + (mapX / 8) + ((mapY / 8) * 32));

        currentTile = ppu.bus.Read(addr);

        if (!Byte.get(ppu.LCDC, 4))
            currentTile += 128;
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

                Thread.Sleep(1);
            }

        }

        lineX++;
    }

    private bool add()
    {
        // Full
        if (fifo.Count > 8)
            return false;

        var x = fetchX - (8 - (ppu.SCX % 8));

        for (var i = 0; i < 8; i++)
        {
            var bit = 7 - i;
            byte hi = (byte)((tileHi & (1 << bit)) >> bit);
            byte lo = (byte)((tileLo & (1 << bit)) >> bit);
            var pixelColour = ppu.lookupBGPalette(ppu.dmgPalette[hi << 1 | lo]);

            if (x >= 0)
            {
                fifo.Enqueue(pixelColour);
                fifoX++;
            }
        }

        return true;
    }

    // 3 clocks to fetch 8 pixels
    // pauses in 4th clock unless space in data
    private void fetch()
    {
        switch (mode)
        {
            case Mode.Tile:

                if (Byte.get(ppu.LCDC, 0))
                    getTile();

                mode = Mode.DataLow;
                fetchX += 8;
                break;

            case Mode.DataLow:
                getTileLow();
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
        mapX = fetchX + ppu.SCX;
        mapY = ppu.LY + ppu.SCY;
        tileY = (mapY % 8) * 2;

        if ((ppu.lineDot & 1) == 0)
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
    private void setPalette(byte value)
    {
        dmgPalette[3] = (byte)((value & 0xC0) >> 6);
        dmgPalette[2] = (byte)((value & 0x30) >> 4);
        dmgPalette[1] = (byte)((value & 0x0C) >> 2);
        dmgPalette[0] = (byte)(value & 0x03);
    }

    [IO(Address = 0xFF41, Type=IOType.Read)]
    public static byte GetLCDStatus(Emulator system) => system.ppu.getLCDStatus();

    [IO(Address = 0xFF41, Type=IOType.Write)]
    public static void SetLCDStatus(Emulator system, byte value) => system.ppu.setLCDStatus(value);

    [IO(Address = 0xFF47, Type = IOType.Write)]
    public static void SetPalette(Emulator system, byte value) => system.ppu.setPalette(value);

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

    // Events
    public delegate void PPUFrameEvent(byte[] bitmap);
    public event PPUFrameEvent? FrameCompleted;
    internal virtual void OnFrameCompleted() => FrameCompleted?.Invoke(Bgr24Bitmap);

    internal byte[] Bgr24Bitmap = new byte[RenderWidth * RenderHeight * 3];

    // Status
    internal int dot = 0;
    internal int lineDot = 0;
    internal int scanline = 0;
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
    }

    private void ChangeMode(Mode mode)
    {
        this.mode = mode;

        if (mode == Mode.OAMScan         && OAMInterrupt)    { system.cpu.RequestInterrupt(Interrupt.Stat); }
        if (mode == Mode.VerticalBlank   && VBlankInterrupt) { system.cpu.RequestInterrupt(Interrupt.VBlank); }
        if (mode == Mode.HorizontalBlank && HBlankInterrupt) { system.cpu.RequestInterrupt(Interrupt.Stat); }
    }

    private void NextLine()
    {
        LY++;

        if (LY == LYC && LYCompareInterrupt)
        {
            if (LYCompareInterrupt) { system.cpu.RequestInterrupt(Interrupt.Stat); }
            // TODO: Interrupt
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

    private void VBlankClock()
    {
        if (lineDot >= DotsPerLine)
        {
            NextLine();


            if (LY >= LinesPerFrame)
            {
                OnFrameCompleted();

                if (dot != OneFrameInDots)
                    Console.WriteLine("Invalid dots in frame: {dot}.");

                ChangeMode(Mode.OAMScan);
                LY = 0;
                dot = 0;
                lineDot = 0;
                fifo.Reset();
            }
        }
    }

    private void OAMScanClock()
    {
        if (lineDot >= 80)
        {
            ChangeMode(Mode.Drawing);
            fifo.Reset();
        }
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

    internal Color lookupBGPalette(byte c)
    {
        return c switch
        {
            0 => blackAndWhite[0],
            1 => blackAndWhite[1],
            2 => blackAndWhite[2],
            3 => blackAndWhite[3],

            _ => Color.DodgerBlue,
        };
    }
}
