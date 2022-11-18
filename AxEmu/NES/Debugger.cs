using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxEmu.NES
{
    public class Debugger
    {
        public System system;

        public Debugger(System system)
        {
            this.system = system;
        }

        // Events
        public delegate void SystemEvent(System system);
        public event SystemEvent? Instruction;

        public virtual void OnInstruction()
        {
            Instruction?.Invoke(system);
        }
    }
}
