namespace AxEmu.GBC;

internal class DMA
{
    private readonly MemoryBus bus;

    public DMA(MemoryBus bus)
    {
        this.bus = bus;
    }

    private bool active;
    private byte offset;
    private byte value;
    private byte delay;

    internal bool TransferActive => active;

    internal void Start(byte value)
    {
        this.value = value;
        active = true;
        offset = 0;
        delay = 2;
    }

    [IO(Address = 0xFF46, Type = IOType.Write)]
    public static void DMAWrite(Emulator system, byte value)
    {
        system.dma.Start(value);
    }

    internal void Clock()
    {
        if (!active)
            return;

        if (delay > 0)
        {
            delay--;
            return;
        }

        var data = bus.Read((ushort)((value * 0x0100) + offset));
        bus.OAM[offset] = data;

        offset++;

        active = offset <= 0x9F;
    }
}
