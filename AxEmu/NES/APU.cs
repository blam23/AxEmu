using AxEmu.NES.Audio;

namespace AxEmu.NES
{
    public class APU
    {
        private Emulator system;

        internal IChannel[] channels;
        internal PWM pulse1;

        // Frame data
        uint frameClockCounter;

        public ulong AudioData
        {
            get
            {
                ulong data = 0;
                foreach (var c in channels)
                {
                    data += c.GetSample();
                }
                data /= (ulong)channels.Length;

                return data;
            }
        }

        public APU(Emulator system)
        {
            this.system = system;

            pulse1 = new PWM();
            channels = new IChannel[]
            {
                pulse1
            };
        }

        internal byte Read(ushort address)
        {
            return 0;
        }

        internal void Write(ushort address, byte value)
        {
            switch (address)
            {
                case 0x4000: // PWM1 - Sequence
                    var seq = (value & 0xC0) >> 6;
                    switch (seq)
                    {
                        case 0x00: pulse1.sequencer.sequence = 0b00000001; break; // 1/8
                        case 0x01: pulse1.sequencer.sequence = 0b00000011; break; // 1/4
                        case 0x02: pulse1.sequencer.sequence = 0b00001111; break; // 1/2
                        case 0x03: pulse1.sequencer.sequence = 0b11111100; break; // 3/4
                    }
                    break;
                case 0x4001:
                    break;
                case 0x4002: // PWM1 - Reload Lower
                    pulse1.sequencer.reload = (ushort)((pulse1.sequencer.reload & 0xFF00) | value);
                    break;
                case 0x4003: // PWM1 - Reload Higher
                    pulse1.sequencer.reload = (ushort)((value & 0x07) << 8 | pulse1.sequencer.reload & 0x00FF);
                    pulse1.sequencer.timer = pulse1.sequencer.reload;
                    break;
                case 0x4015: // PWM1 - Enable
                    pulse1.enable = (value & 0x1) == 0x1;
                    break;

                default:
                    break;
            }
        }

        internal void Clock()
        {
            var quarterFrame = false;
            var halfFrame = false;

            frameClockCounter++;

            // TODO: 5 step

            // 4 step
            if (frameClockCounter == 3729
                || frameClockCounter == 7457
                || frameClockCounter == 11186
                || frameClockCounter == 14916)
            {
                quarterFrame = true;
            }

            if (frameClockCounter == 7457)
            {
                halfFrame = true;
            }

            if (frameClockCounter == 14916)
            {
                frameClockCounter = 0;
            }

            // Quarter beats adjust envelope
            if (quarterFrame)
            {
            }

            // Half beats adjust note length & sweepers
            if (halfFrame)
            {
            }

            pulse1.Clock();
        }
    }
}