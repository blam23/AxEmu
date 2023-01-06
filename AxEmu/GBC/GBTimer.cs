namespace AxEmu.GBC;

internal class GBTimer
{
    readonly Emulator system;

    public GBTimer(Emulator system)
    {
        this.system = system;
        system.bus.RegisterIOProperties(GetType(), this);
    }

    private int clock = 0;

    //
    // DIV
    //
    private const int DivClocks = 256;
    private byte div = 0xAC;
    [IO(Address = 0xFF04)]
    public byte DIV
    {
        get => div;
        set => div = 0;
    }

    //
    // Timer
    //
    private bool timerEnable = false;
    private int  timerClocks = 0x400;
    private byte tct         = 0x00;

    [IO(Address = 0xFF05)] public byte TIMA { get; set; } // Counter
    [IO(Address = 0xFF06)] public byte TMA { get; set; }  // Modulo


    private static readonly byte TACAlwaysSetMask = 0xF8;
    [IO(Address = 0xFF07)]
    public byte TAC
    { 
        get
        {
            return (byte)(((timerEnable ? 0x04 : 0x00) + tct) | TACAlwaysSetMask);
        }
        set
        {
            timerEnable = (value & 0x04) == 0x04;
            tct         = (byte)(value & 0x03);

            timerClocks = tct switch
            {
                0b00 => 0x400,
                0b01 => 0x010,
                0b10 => 0x040,
                0b11 => 0x100,

                _ => throw new InvalidDataException()
            };
        }
    }


    public void Clock()
    {
        if (system.cpu.stopped)
            return;

        if (clock % DivClocks == 0)
        {
            div++;
        }

        if (timerEnable)
        {
            if (clock % timerClocks == 0)
            {
                TIMA++;

                if (TIMA == 0x00)
                {
                    TIMA = TMA;
                    system.cpu.RequestInterrupt(CPU.Interrupt.Timer);
                }
            }
        }

        clock++;
    }
}
