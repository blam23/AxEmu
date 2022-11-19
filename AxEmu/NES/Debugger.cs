using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxEmu.NES
{
    public class Debugger
    {
        public interface ILogger
        {
            void Log(string message);
        }

        public System system;
        private ILogger? logger;

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

        public void SetLogger(ILogger logger)
        {
            this.logger = logger;
        }

        public void Log(string message)
        {
            logger?.Log(message);
        }

        public void Log(object o)
        {
            logger?.Log(o.ToString() ?? "<null>");
        }

        public void UnsetLogger()
        {
            this.logger = null;
        }
    }
}
