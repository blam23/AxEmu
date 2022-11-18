using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxEmu.NES
{
    public interface IMemory
    {
        byte Read(ushort address);
        ushort ReadWord(ushort address);

        ushort ReadWordWrapped(ushort address);

        void Write(ushort address, byte value);
    }
}
