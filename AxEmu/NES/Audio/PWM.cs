namespace AxEmu.NES.Audio
{
    internal class PWM : IChannel
    {
        public bool enable = true;
        //private byte duty = 0;
        //private bool loop = false;
        //private byte constantVolume = 0;
        //private byte envelope = 0;
        //private bool sweepEnabled = false;
        //private byte period = 0;
        //private bool negate = false;
        //private byte shift = 0;
        //private ulong timer = 0;
        //private ushort lengthCounterLoad = 0;

        public Sequencer sequencer;

        public PWM()
        {
            sequencer = new Sequencer(
                // Shift right 1 bit (with wrapping)
                (s) => ((s & 1) << 7) | ((s & 0xFE) >> 1)
            );
        }

        public void Clock()
        {
            sequencer.Clock(enable);
        }

        public ulong GetSample()
        {
            return (ulong)(sequencer.output * 10);
        }
    }
}
