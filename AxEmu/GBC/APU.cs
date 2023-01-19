namespace AxEmu.GBC;

internal class Envelope
{
    public APU apu;

    public bool Enabled;
    public byte Initial;
    public bool Decrease;
    public byte Sweep;
    public byte CurrentVolume;
    public int Ticks;

    public Envelope(APU apu)
    {
        this.apu = apu;
    }

    public void Trigger()
    {
        CurrentVolume = Initial;
        Ticks = 0;
        Enabled = true;
    }

    public void Clock()
    {
        if (!Enabled)
            return;

        if (CurrentVolume == 0 && Decrease || CurrentVolume == 15 && !Decrease)
        {
            Enabled = false;
            return;
        }

        Ticks++;
        if (Ticks < (Sweep * (apu.sampleRate / 64)))
            return;

        Ticks = 0;

        if (Decrease)
            CurrentVolume--;
        else
            CurrentVolume++;
    }
}

internal class SC1
{
    APU apu;

    public bool Enabled;
    private Envelope envelope;
    private int ticks = 0;
    private int dutyStep = 2;
    private byte sample = 0;
    private int dutyType = 0;

    public byte Sample => sample;

    public SC1(APU apu)
    {
        this.apu = apu;
        envelope = new(apu);

        apu.system.bus.RegisterIOProperties(GetType(), this);
    }

    [IO(Address = 0xFF10)] public byte NR10
    {
        set
        {
            
        }
    }

    private byte _nr11 = 0;
    [IO(Address = 0xFF11)] public byte NR11
    { 
        get { return _nr11; }
        set
        {
            _nr11 = value;
            dutyType = (value & 0xC0) >> 6;
        }
    }

    private byte _nr12 = 0;
    [IO(Address = 0xFF12)]
    public byte NR12
    {
        set
        {
            _nr12 = value;
            envelope.Initial  = (byte)(value >> 4);
            envelope.Sweep    = (byte)(value & 0x07);
            envelope.Decrease = Byte.get(value, 3);
        }
        get
        {
            return _nr12;
        }
    }
    [IO(Address = 0xFF13)] public byte NR13 { get; set; } = 0;

    private byte _nr14 = 0;
    [IO(Address = 0xFF14)]
    public byte NR14
    {
        set
        {
            _nr14 = value;

            if ((value & 0x80) != 0)
            {
                envelope.Trigger();
                Enabled = true;
            }
        }
        get
        {
            return _nr14;
        }
    }

    public void Clock()
    {
        if (!Enabled)
            return;

        envelope.Clock();

        var raw = (ushort)(((_nr14 & 0x7) << 8) | NR13);
        var freq = (ushort)(131072 / (2048 - raw));

        ticks++;
        if (ticks >= apu.sampleRate / (freq * 8))
        {
            dutyStep = (dutyStep + 1) % 8;
            ticks = 0;
        }

        if (APU.DutyCycles[dutyType, dutyStep])
        {
            // TODO: & 0xFF?
            sample = (byte)((envelope.CurrentVolume * 4) & 0xFF);
        }
        else
        {
            sample = 0x0;
        }
    }
}

internal class APU
{
    internal readonly Emulator system;

    public byte Left { get; set; }
    public byte Right { get; set; }
    public bool Play { get; set; }

    public SC1 SC1;

    internal static bool[,] DutyCycles = new bool[4,8]
    {
        { false, false, false, false, false, false, false, true  },
        { true,  false, false, false, false, false, false, true  },
        { true,  false, false, false, false, true,  true,  true  },
        { false, true,  true,  true,  true,  true,  true,  false },
    };

    public APU(Emulator system)
    {
        this.system = system;
        SC1 = new(this);

        this.system.bus.RegisterIOProperties(GetType(), this);
    }

    internal int sampleRate = 0;
    internal int ticksPerSample = 0;
    public void SetSampleRate(int rate)
    {
        sampleRate = rate;
        ticksPerSample = (4 * 1024 * 1024) / rate;
    }

    int ticks = 0;
    public bool Clock()
    {
        ticks++;

        SC1.Clock();

        if (ticks >= ticksPerSample)
        {
            ticks = 0;
            Left  = SC1.Sample;
            Right = SC1.Sample;
            return true;
        }

        return false;
    }
}