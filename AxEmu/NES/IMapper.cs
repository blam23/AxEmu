namespace AxEmu.NES
{
    public interface IMapper
    {
        byte Read(ushort address);
        void Write(ushort address, byte value);
        byte ReadChrRom(ushort address);
        void Init(Emulator system);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=false)]
    public class MapperAttribute : Attribute
    {
        public ushort MapperNumber { get; set; }
    }
}