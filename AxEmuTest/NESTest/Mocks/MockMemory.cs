using AxEmu.NES;

namespace AxEmuTest.NESTest.Mocks
{
    internal class MockMemory : IMemory
    {
        private Dictionary<ushort, byte> data = new();
        public Dictionary<ushort, byte> Written = new();

        public void TestSetResponse(ushort address, byte value)
        {
            data[address] = value;
        }

        public byte Read(ushort address)
        {
            return data[address];
        }

        public ushort ReadWord(ushort address)
        {
            return (ushort)((data[address] << 8) + data[(ushort)(address+1)]);
        }

        public void Write(ushort address, byte value)
        {
            Written[address] = value;
        }

        public ushort ReadWordWrapped(ushort address)
        {
            return ReadWord(address);
        }
    }
}
