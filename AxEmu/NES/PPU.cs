namespace AxEmu.NES
{
    internal class PPU
    {
        private System system;

        public PPU(System system)
        {
            this.system = system;
        }

        internal byte read(ushort address)
        {
            throw new NotImplementedException();
        }

        internal void write(ushort address, byte value)
        {
            throw new NotImplementedException();
        }
    }
}