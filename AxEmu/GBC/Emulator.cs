namespace AxEmu.GBC;

public class Emulator : IEmulator
{
    public int GetScreenWidth() => 160;
    public int GetScreenHeight() => 144;
    public int CyclesPerFrame => PPU.OneFrameInDots;
    public double FramesPerSecond => 59.7;

    private JoyPad jp1;
    public IController Controller1 => jp1;
    public IController Controller2 => throw new NotImplementedException();

    public event FrameEvent? FrameCompleted;
    protected virtual void OnFrameCompleted(byte[] bitmap) => FrameCompleted?.Invoke(bitmap);

    public bool CpuRanLastClock => !cpu.halted && !cpu.stopped;

    public bool LimitFrames { get; set; }

    // Components
    internal CPU cpu;
    internal PPU ppu;
    internal APU apu;
    internal DMA dma;
    internal MemoryBus bus;
    internal Cart cart;
    internal GBTimer timer;
    public Debugger debug;

    // Data
    private byte[] bootROM = Array.Empty<byte>();

    public Emulator(string? bootROMFile = null)
    {
        cart  = new();
        debug = new(this);
        bus   = new(this);
        cpu   = new(bus);
        ppu   = new(this);
        apu   = new(this);
        dma   = new(bus);
        timer = new(this);
        jp1   = new(this);
        if (bootROMFile != null)
            LoadBootROM(bootROMFile);

        ppu.FrameCompleted += OnFrameCompleted;
    }

    private void LoadBootROM(string bootROMFile)
    {
        // just throw the exception if we fail to open >:)
        bootROM = File.ReadAllBytes(bootROMFile);
    }

    public bool Clock()
    {
        cpu.Clock();
        ppu.Clock();
        timer.Clock();
        return apu.Clock();
    }

    public void Reset()
    {
        cpu.Reset();
    }

    public void LoadROM(string file)
    {
        Reset();

        cart = new(file);

        if (cart.LoadState == Cart.State.FailedToOpen)
            throw new FileLoadException("Unable to open ROM file");

        if (cart.LoadState == Cart.State.Invalid)
            throw new InvalidDataException("Unable to parse ROM file");

        if (cart.LoadState != Cart.State.Loaded)
            throw new Exception("Error loading ROM file");

        bus.SetMBC(cart.CreateMBC());
    }

    public void SetSleep(ISleep sleep) => sleeper = sleep;

    ISleep sleeper = new ThreadSleep();
    internal void Sleep(int ms)
    {
        sleeper.Sleep(ms);
    }

    public void Shutdown()
    {
        // Save any state for battery backed carts, etc.
        bus.Shutdown();
    }

    //
    // APU Stuff
    //

    public void SetAudioSampleRate(int sampleRate) => apu.SetSampleRate(sampleRate);

    public (byte left, byte right) APUState()
    {
        return (apu.Left, apu.Right);
    }
}
