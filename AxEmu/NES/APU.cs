﻿namespace AxEmu.NES
{
    internal class APU
    {
        private System system;

        public APU(System system)
        {
            this.system = system;
        }
        internal byte read(ushort address)
        {
            return 0;
            //throw new NotImplementedException();
        }

        internal void write(ushort address, byte value)
        {
            //throw new NotImplementedException();
        }
    }
}