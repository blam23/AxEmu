using AxEmu.NES;

namespace AxEmuTest.NESTest.Mocks
{
    internal class MockMemory : MemoryBus
    {
        private Dictionary<ushort, byte> data = new();
        public Dictionary<ushort, byte> Written = new();

        public void TestSetResponse(ushort address, byte value)
        {
            data[address] = value;
        }

        public override byte Read(ushort address)
        {
            return data[address];
        }

        public override void Write(ushort address, byte value)
        {
            Written[address] = value;
        }

    }
}
