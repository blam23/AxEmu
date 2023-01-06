using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxEmu
{
    public interface ISleep
    {
        void Sleep(int ms);
    }

    public class ThreadSleep : ISleep
    {
        public void Sleep(int ms) => Thread.Sleep(ms);
    }
}
