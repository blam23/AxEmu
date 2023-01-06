namespace AxEmu.GBC.MBC;

[MBC(CartType = 0)]
internal class NoMBC : IMBC
{
    Cart cart = new();
    public byte CartType { get; set; }

    public void Initialise(Emulator system)
    {
        cart = system.cart;
    }
    public void Shutdown()
    {
    }

    public byte Read(ushort addr)
    {
        if (addr < 0x8000)
        {
            if (addr >= cart.rom.Length)
                return 0;

            return cart.rom[addr];
        }

        if (addr >= 0xA000 && addr < 0xC000)
        {
            addr -= 0xA000;

            if (addr >= cart.ram.Length)
                return 0;

            return cart.ram[addr];
        }

        throw new InvalidOperationException();
    }

    public void Write(ushort addr, byte value)
    {
        if (addr >= 0xA000 && addr < 0xC000)
        {
            addr -= 0xA000;

            if (addr >= cart.ram.Length)
                return;

            cart.ram[addr] = value;
        }
    }
}
