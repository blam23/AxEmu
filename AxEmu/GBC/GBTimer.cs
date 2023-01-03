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
    private int  timerClocks = 1024;
    private byte tct         = 0x00;

    private byte tima;
    [IO(Address = 0xFF05)]
    public byte TIMA
    {
        get => tima;
    }

    [IO(Address = 0xFF06)] public byte TMA { get; set; }

    [IO(Address = 0xFF07)]
    public byte TAC
    { 
        get
        {
            return (byte)((timerEnable ? 0x04 : 0x00) + tct);
        }
        set
        {
            timerEnable = (value & 0x04) == 0x04;
            tct         = (byte)(value & 0x03);

            timerClocks = tct switch
            {
                0b00 => 1024,
                0b01 => 16,
                0b10 => 64,
                0b11 => 256,

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
                tima++;

                if (tima == 0x00)
                {
                    tima = TMA;
                    system.cpu.RequestInterrupt(CPU.Interrupt.Timer);
                }
            }
        }

        clock++;

        clock %= 2046;
    }
}
