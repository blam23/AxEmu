namespace AxEmu.GBC.MBC;

[MBC(CartType = 0x01)]
[MBC(CartType = 0x02)]
[MBC(CartType = 0x03)]
internal class MBC1 : IMBC
{
    Cart cart = new();
    public byte CartType { get; set; }

    private bool battery = false;

    public void Initialise(Emulator system)
    {
        cart = system.cart;

        battery = CartType == 0x03;

        if (battery)
            cart.LoadRAM();
    }

    public void Shutdown()
    {
        if (battery)
            cart.SaveRAM();
    }

    private int ROMBank = 1;
    private int RAMBank = 0;
    private bool RAMEnable = false;

    public byte Read(ushort addr)
    {
        // Fixed ROM bank 0
        if (addr < 0x4000)
        {
            if (addr >= cart.rom.Length)
                return 0;

            return cart.rom[addr];
        }

        // Swappable ROM bank 01-7F
        if (addr < 0x8000)
        {
            var romAddr = (addr - 0x4000) + (ROMBank * 0x4000);

            return cart.rom[romAddr];
        }

        // RAM banks 00-03
        if (addr >= 0xA000 && addr < 0xC000)
        {
            if (!RAMEnable)
                return 0xFF;

            var ramAddr = (addr - 0xA000) + (RAMBank * 0x2000);
            return cart.ram[ramAddr];
        }

        throw new InvalidOperationException();
    }

    public void Write(ushort addr, byte value)
    {
        if (addr < 0x2000)
            SetRAMEnable(value);

        else if (addr < 0x4000)
            SetROMBank(value);

        else if (addr < 0x6000)
            SetRAMBank(value);

        else if (addr >= 0xA000 && addr < 0xC000)
        {
            addr -= 0xA000;

            if (addr >= cart.ram.Length)
                return;

            if (RAMEnable)
                cart.ram[addr] = value;
        }
    }

    private void SetROMBank(byte value)
    {
        var bank = value & 0x1F;

        if (bank == 0x00) bank = 0x01;
        if (bank == 0x20) bank = 0x21;
        if (bank == 0x40) bank = 0x41;
        if (bank == 0x60) bank = 0x61;

        if (bank > cart.rom.Length / 0x4000)
            bank = cart.rom.Length / 0x4000;

        ROMBank = bank;
    }

    private void SetRAMBank(byte value)
    {
        var bank = value & 0x03;

        if (bank > cart.ram.Length / 0x2000)
            return;

        RAMBank = bank;

        if (battery)
            cart.SaveRAM();
    }

    private void SetRAMEnable(byte value)
    {
        RAMEnable = (value & 0x0F) == 0x0A;
    }
}
