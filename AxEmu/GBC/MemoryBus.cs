namespace AxEmu.GBC;

internal class MemoryBus
{
    private Emulator system;

    public MemoryBus(Emulator system)
    {
        this.system = system;
    }

    public void Write(ushort addr, byte data)
    {
    }

    public byte Read(ushort addr)
    {
        // TODO: Memory mapping
        if (addr < 0x8000)
            return system.cart.rom[addr];

        return 0;
    }

    public ushort ReadWord(ushort addr)
    {
        ushort r = Read((ushort)(addr + 1));
        r = (ushort)(r << 8);
        r += Read(addr);
        return r;
    }
}
