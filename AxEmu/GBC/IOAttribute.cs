namespace AxEmu.GBC;

internal enum IOType
{
    Read,
    Write
}

internal class IOAttribute : Attribute
{
    public ushort Address { get; set; }
    public IOType Type { get; set; }
}
