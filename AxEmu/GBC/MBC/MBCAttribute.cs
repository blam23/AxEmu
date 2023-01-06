namespace AxEmu.GBC.MBC;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal class MBCAttribute : Attribute
{
    public byte CartType { get; set; }
}
