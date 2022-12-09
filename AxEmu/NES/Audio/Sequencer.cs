namespace AxEmu.NES.Audio
{
    internal class Sequencer
    {
        public uint   sequence = 0;
        public ushort timer    = 0;
        public ushort reload   = 0;
        public byte   output   = 0;
        readonly Func<uint, uint> manipulator;

        public Sequencer(Func<uint, uint> manipulator)
        {
            this.manipulator = manipulator;
        }

        public void Clock(bool enable)
        {
            if (!enable) 
                return;
            
            timer--;
            if (timer == 0xFFFF)
            {
                timer = (ushort)(reload + 1);
                var ns = manipulator(sequence);
                output = (byte)(ns & 1);
            }
        }
    }
}
