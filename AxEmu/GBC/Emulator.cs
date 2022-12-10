namespace AxEmu.GBC;

public class Emulator : IEmulator
{
    public int GetScreenWidth() => 160;
    public int GetScreenHeight() => 144;

    public IController Controller1 => throw new NotImplementedException();
    public IController Controller2 => throw new NotImplementedException();

    public event FrameEvent? FrameCompleted;

    // Components
    internal CPU cpu;
    internal MemoryBus bus;
    internal Cart cart;
    public Debugger debug;

    // Data
    private byte[] bootROM = Array.Empty<byte>();

    public Emulator(string? bootROMFile = null)
    {
        cart  = new();
        debug = new(this);
        bus   = new(this);
        cpu   = new(bus);

        if (bootROMFile != null)
            LoadBootROM(bootROMFile);
    }

    private void LoadBootROM(string bootROMFile)
    {
        // just throw the exception if we fail to open >:)
        bootROM = File.ReadAllBytes(bootROMFile);
    }

    public void Clock()
    {
        cpu.Clock();
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
    }
}
