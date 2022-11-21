using System;
using System.Collections.Generic;
using System.Drawing;
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

        public Emulator system;
        private ILogger? logger;

        public Debugger(Emulator system)
        {
            this.system = system;
        }

        // Events
        public delegate void SystemEvent(Emulator system);
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

        private byte[] GetPatternTable(ushort offset)
        {
            var mem = system.ppu.mem;
            byte[] bitmap = new byte[128 * 128 * 3];

            for (byte tileY = 0; tileY < 16; tileY++)
            {
                for (byte tileX = 0; tileX < 16; tileX++)
                {
                    for (byte y = 0; y < 8; y++)
                    {
                        ushort location = (ushort)(offset + (tileX * 16) + (tileY * 256) + y);
                        byte firstPlaneByte = mem.Read(location);
                        byte secondPlaneByte = mem.Read((ushort)(location + 8));

                        for (byte x = 0; x < 8; x++)
                        {
                            var firstPlaneBit = (byte)((firstPlaneByte >> (byte)(7 - x)) & 1);
                            var secondPlaneBit = (byte)((secondPlaneByte >> (byte)(7 - x)) & 1);

                            Color pixelColour;
                            if (firstPlaneBit == 0 && secondPlaneBit == 0)
                            {
                                pixelColour = system.ppu.lookupBGPalette(0);
                            }
                            else
                            {
                                var paletteIndex = firstPlaneBit + (secondPlaneBit * 2);
                                pixelColour = system.ppu.lookupBGPalette((byte)paletteIndex);
                            }

                            var px = tileX * 8 + x;
                            var py = tileY * 8 + y;

                            // Store in our BGR buffer
                            bitmap[(px + (py * 128)) * 3 + 0] = pixelColour.B;
                            bitmap[(px + (py * 128)) * 3 + 1] = pixelColour.G;
                            bitmap[(px + (py * 128)) * 3 + 2] = pixelColour.R;
                        }
                    }
                }
            }

            return bitmap;
        }

        public byte[] GetPatternTableLeft()
        {
            return GetPatternTable(0x0000);
        }

        public byte[] GetPatternTableRight()
        {
            return GetPatternTable(0x1000);
        }
    }
}
