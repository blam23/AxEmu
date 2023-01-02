using System.Reflection;

namespace AxEmu.GBC;

internal class MemoryBus
{
    private Emulator system;

    // Memory chips
    internal byte[] VRAM = new byte[0x2000]; // 8KB Video RAM
    internal byte[] WRAM = new byte[0x2000]; // 8KB Work  RAM
    internal byte[] HRAM = new byte[0x0100]; // High Ram + I/O Registers
    internal byte[]  OAM = new byte[0x00A0]; // Object Attribute Memory

    private readonly Action<Emulator, byte>[] io_writers = new Action<Emulator, byte>[0x100];
    private readonly Func<Emulator, byte>[]   io_readers = new Func<Emulator, byte>[0x100];

    public void RegisterIOProperties(Type caller, object instance)
    {
        foreach (var prop in caller.GetProperties())
        {
            var attrs = prop.GetCustomAttributes(typeof(IOAttribute), true);
            foreach (var a in attrs)
            {
                if (a is IOAttribute io)
                {
                    var addr = io.Address - 0xFF00;

                    if (prop.CanWrite)
                    {
                        if (io_writers[addr] != null)
                            throw new InvalidDataException($"Duplicate IO write prop for address: {io.Address:X4}.");

                        io_writers[addr] = (e, v) => prop.SetMethod?.Invoke(instance, new object[] { v });
                    }

                    if (prop.CanRead)
                    {
                        if (io_readers[addr] != null)
                            throw new InvalidDataException($"Duplicate IO write prop for address: {io.Address:X4}.");

                        io_readers[addr] = (e) => (prop.GetMethod == null) ? (byte)0 : (byte)(prop.GetMethod.Invoke(instance, null) ?? 0);
                    }
                }
            }
        }
    }

    private void RegisterStaticIOMethods()
    {
        foreach (var module in Assembly.GetExecutingAssembly().GetModules())
        {
            foreach(var type in module.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    var attrs = method.GetCustomAttributes(typeof(IOAttribute), true);
                    foreach (var a in attrs)
                    {
                        if (a is IOAttribute io)
                        {
                            var addr = io.Address - 0xFF00;

                            switch(io.Type)
                            {
                                case IOType.Read:
                                    if (io_readers[addr] != null)
                                        throw new InvalidDataException($"Duplicate IO read method for address: {io.Address:X4}.");

                                    io_readers[addr] = (Func<Emulator, byte>)Delegate.CreateDelegate(typeof(Func<Emulator, byte>), method);
                                    break;
                                    case IOType.Write:
                                    if (io_writers[addr] != null)
                                        throw new InvalidDataException($"Duplicate IO write method for address: {io.Address:X4}.");

                                    io_writers[addr] = (Action<Emulator, byte>)Delegate.CreateDelegate(typeof(Action<Emulator, byte>), method);
                                    break;
                                default:
                                    throw new InvalidOperationException();
                            }
                        }
                    }
                }
            }
        }
    }

    public MemoryBus(Emulator system)
    {
        this.system = system;

        RegisterStaticIOMethods();
    }

    public void Write(ushort addr, byte data)
    {
        // ROM
        if (addr < 0x8000)
            return;
        else if (addr < 0xA000)
        {
            if (system.ppu.VRAMAccessible())
                VRAM[addr - 0x8000] = data;
        }
        else if (addr < 0xC000)
            system.cart.ram[addr - 0xA000] = data;
        else if (addr < 0xE000)
            WRAM[addr - 0xC000] = data;
        else if (addr < 0xFE00) // Echo RAM
            WRAM[(addr - 0xC000) % 0x2000] = data;
        else if (addr < 0xFEA0)
        {
            if (system.dma.TransferActive)
                return;

            OAM[addr - 0xFE00] = data;
        }
        else if (addr < 0xFF00) // Unusable memory
            return;
        else
        {
            var action = io_writers[addr - 0xFF00];

            if (action != null)
                action(system, data);
            else
                HRAM[addr - 0xFF00] = data;
        }
    }

    Random test = new Random(123);

    public byte Read(ushort addr)
    {
        if (addr < 0x8000)
        {
            if (addr >= system.cart.rom.Length)
                return 0;

            return system.cart.rom[addr];
        }

        if (addr < 0xA000)
        {
            if (system.ppu.VRAMAccessible())
                //return (byte)(test.Next() & 0xFF);
                return VRAM[addr - 0x8000];
            else
                return 0xFF;
        }

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

        var action = io_readers[addr - 0xFF00];

        if (action != null)
            return action(system);

        return HRAM[addr - 0xFF00];
    }

    public ushort ReadWord(ushort addr)
    {
        ushort r = Read((ushort)(addr + 1));
        r = (ushort)(r << 8);
        r += Read(addr);
        return r;
    }

    public void WriteWord(ushort addr, ushort val)
    {
        Write(addr,   Byte.lower(val));
        Write(++addr, Byte.upper(val));
    }
}
