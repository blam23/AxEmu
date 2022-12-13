namespace AxEmu.GBC;

internal class MemoryBus
{
    private Emulator system;

    private byte[] VRAM = new byte[0x2000]; // 8KB Video RAM
    private byte[] WRAM = new byte[0x2000]; // 8KB Work  RAM
    private byte[] HRAM = new byte[0x0100]; // High Ram + I/O Registers
    private byte[]  OAM = new byte[0x00A0]; // Object Attribute Memory

    public MemoryBus(Emulator system)
    {
        this.system = system;
    }

    public void Write(ushort addr, byte data)
    {
        // ROM
        if (addr < 0x8000)
            return;
        else if (addr < 0xA000)
            VRAM[addr - 0x8000] = data;
        else if (addr < 0xC000)
            system.cart.ram[addr - 0xA000] = data;
        else if (addr < 0xE000)
            WRAM[addr - 0xC000] = data;
        else if (addr < 0xFE00) // Echo RAM
            WRAM[addr - 0xFE00] = data;
        else if (addr < 0xFEA0)
            OAM[addr - 0xFE00] = data;
        else if (addr < 0xFF00) // Unusable memory
            return;
        else
            HRAM[addr - 0xFF00] = data;
    }

    public byte Read(ushort addr)
    {
        if (addr < 0x8000)
            return system.cart.rom[addr];

        if (addr < 0xA000)
            return VRAM[addr - 0x8000];

        if (addr < 0xC000)
            return system.cart.ram[addr - 0xA000];

        // TODO: Swappable RAM
        if (addr < 0xE000)
            return WRAM[addr - 0xC000];

        // Echo RAM
        if (addr < 0xFE00)
            return WRAM[addr - 0xE000];

        if (addr < 0xFEA0)
            return OAM[addr - 0xFE00];

        // Unusable memory
        if (addr < 0xFF00)
            return 0;

        return HRAM[addr - 0xFF00];
    }

    public ushort ReadWord(ushort addr)
    {
        ushort r = Read((ushort)(addr + 1));
        r = (ushort)(r << 8);
        r += Read(addr);
        return r;
    }
}
