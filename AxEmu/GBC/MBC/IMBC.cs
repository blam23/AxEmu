namespace AxEmu.GBC.MBC;

internal interface IMBC
{
    byte CartType { get; set; }

    void Initialise(Emulator system);
    void Shutdown();

    void Write(ushort addr, byte value);
    byte Read(ushort addr);
}
