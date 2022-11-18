using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AxEmu.NES
{
    public class JoyPad
    {
        internal readonly System system;

        // Controller state
        private bool up;
        private bool down;
        private bool left;
        private bool right;
        private bool start;
        private bool select;
        private bool a;
        private bool b;

        public JoyPad(System system)
        {
            this.system = system;
        }

        internal byte StateByte
        {
            get {
                byte status = 0;
                if (a) status |= 0x01;
                if (b) status |= 0x02;
                if (select) status |= 0x04;
                if (start) status |= 0x08;
                if (up) status |= 0x10;
                if (down) status |= 0x20;
                if (left) status |= 0x40;
                if (right) status |= 0x80;

                return status;
            }
        }

        public void PressDown() { down = true; }
        public void PressUp() { up = true; }
        public void PressLeft() { left = true; }
        public void PressRight() { right = true; }
        public void PressStart() { start = true; }
        public void PressSelect() { select = true; }
        public void PressA() { a = true; }
        public void PressB() { b = true; }

        public void ReleaseDown() { down = false; }
        public void ReleaseUp() { up = false; }
        public void ReleaseLeft() { left = false; }
        public void ReleaseRight() { right = false; }
        public void ReleaseStart() { start = false; }
        public void ReleaseSelect() { select = false; }
        public void ReleaseA() { a = false; }
        public void ReleaseB() { b = false; }

        private byte currentStateReadBit;

        internal byte Read()
        {
            byte ret = currentStateReadBit;
            currentStateReadBit >>= 1;
            return (byte)(ret & 0x1);
        }

        internal void Poll(byte value)
        {
            if ((value & 0x1) == 0x1)
                currentStateReadBit = StateByte;
        }
    }
}
