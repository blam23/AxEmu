using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxEmu.NES
{
    public abstract class MemoryBus
    {
        public abstract byte Read(ushort address);
        public abstract void Write(ushort address, byte value);

        public ushort ReadWord(ushort address)
        {
            ushort r = Read((ushort)(address + 1));
            r = (ushort)(r << 8);
            r += Read(address);
            return r;
        }

        public ushort ReadWordWrapped(ushort address)
        {
            ushort high = (ushort)((address & 0xFF) == 0xFF ? address - 0xFF : address + 1);
            return (ushort)(Read(address) | Read(high) << 8);
        }
    }
}
