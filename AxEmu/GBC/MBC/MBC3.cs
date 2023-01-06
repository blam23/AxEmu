namespace AxEmu.GBC.MBC;

[MBC(CartType = 0x011)]
[MBC(CartType = 0x012)]
[MBC(CartType = 0x013)]
internal class MBC3 : IMBC
{
    Cart cart = new();
    public byte CartType { get; set; }

    private bool battery = false;
    private bool timer   = false;
    private string saveFile = "";


    public void Initialise(Emulator system)
    {
        cart     = system.cart;

        battery = CartType == 0x0F || CartType == 0x10 || CartType == 0x13;
        timer   = CartType == 0x0F || CartType == 0x10;

        if (battery)
            cart.LoadRAM();
    }

    public void Shutdown()
    {
        if (battery)
            cart.SaveRAM();
    }

    private int  ROMBank        = 1;
    private int  RAMBank        = 0;
    private bool RAMEnable      = false;
    private bool ClockMode      = false;
    private bool latchCanEnable = false;
    private bool latchEnabled   = false;

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

            if (ClockMode)
                return ReadClock(addr);
            else
            {
                var ramAddr = (addr - 0xA000) + (RAMBank * 0x2000);
                return cart.ram[ramAddr];
            }
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

        else if (addr < 0x8000)
            UpdateLatch(value);

        else if (addr >= 0xA000 && addr < 0xC000)
        {
            if (!RAMEnable)
                return;

            if (ClockMode)
            {
                WriteClock(addr, value);
            }
            else
            {
                var ramAddr = (addr - 0xA000) + (RAMBank * 0x2000);
                if (ramAddr >= cart.ram.Length)
                    return;

                cart.ram[ramAddr] = value;
            }
        }
    }

    private void SetROMBank(byte value)
    {
        var bank = value & 0x7F;

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
        if (value <= 0x03)
        {
            var bank = value & 0x03;

            if (bank > cart.ram.Length / 0x2000)
                return;

            var oldbank = RAMBank;
            RAMBank = bank;
        }
        else
        {
            if (value >= 0x08 && value <= 0x0C)
                ClockMode = true;
        }
    }


    private void SetRAMEnable(byte value)
    {
        RAMEnable = (value & 0x0F) == 0x0A;
    }

    //
    // Clock
    //
    // TODO: This
    //
    private byte ReadClock(ushort addr)
    {
        byte value = 0x00;
        Console.WriteLine($"Clock Read: ${addr:X4} -> {value:X2}");
        return value;
    }

    private void WriteClock(ushort addr, byte value)
    {
        Console.WriteLine($"Clock Write: ${addr:X4} <- {value:X2}");
    }

    private void UpdateLatch(byte value)
    {
        if (latchCanEnable && value == 0x01)
        {
            latchCanEnable = false;
            SetLatch();
            latchEnabled = true;
        }
        if (!latchCanEnable && value == 0x00)
        {
            latchEnabled = false;
            latchCanEnable = true;
        }
    }

    private void SetLatch()
    {
        Console.WriteLine($"Latch Clock");
    }
}
