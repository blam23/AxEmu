using AxEmu.NES.Mappers;
using System.Drawing;

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
            var bitmap = new byte[128 * 128 * 3];

            for (byte tileY = 0; tileY < 16; tileY++)
            {
                for (byte tileX = 0; tileX < 16; tileX++)
                {
                    for (byte y = 0; y < 8; y++)
                    {
                        var location = (ushort)(offset + (tileX * 16) + (tileY * 256) + y);
                        var firstPlaneByte = mem.Read(location);
                        var secondPlaneByte = mem.Read((ushort)(location + 8));

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

        public byte[] GetNameTable(int offset)
        {
            var mem = system.ppu.mem;
            var bitmap = new byte[256 * 240 * 3];

            for (byte tileY = 0; tileY < 30; tileY++)
            {
                for (byte tileX = 0; tileX < 32; tileX++)
                {
                    ushort nametableEntry = mem.Read((ushort)(offset + tileX + (tileY * 32)));

                    var attrX   = tileX / 4;
                    var attrY   = tileY / 4;
                    var quadX   = (tileX % 4) / 2;
                    var quadY   = (tileY % 4) / 2;

                    var attr    = mem.Read((ushort)(offset + PPU.AttrTableAddr + attrY * 8 + attrX));
                    var palette = (attr >> (byte)((quadX * 2) + (quadY * 4))) & 0x3;

                    for (byte y = 0; y < 8; y++)
                    {
                        var entry = (ushort)(system.ppu.BackTableAddr + nametableEntry * 0x10 + y);
                        var firstPlaneByte = mem.Read(entry);
                        var secondPlaneByte = mem.Read((ushort)(entry + 8));

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
                                var paletteIndex = firstPlaneBit + (secondPlaneBit * 2) + (palette * 4);
                                pixelColour = system.ppu.lookupBGPalette((byte)paletteIndex);
                            }

                            var px = tileX * 8 + x;
                            var py = tileY * 8 + y;

                            // Store in our BGR buffer
                            bitmap[(px + (py * 256)) * 3 + 0] = pixelColour.B;
                            bitmap[(px + (py * 256)) * 3 + 1] = pixelColour.G;
                            bitmap[(px + (py * 256)) * 3 + 2] = pixelColour.R;
                        }
                    }
                }
            }

            return bitmap;
        }

        public byte ReadPRGRom(ushort addr)
        { 
            return system.cart.prgRom[addr];
        }

        public void DumpMMC3Lookups()
        {
            if (system.mapper is MMC3 mmc)
            {
                Console.WriteLine("MMC:");
                for (var i = 0; i < mmc.prgPageLookup.Length; i++)
                {
                    Console.WriteLine($"\tPRG[{i:X2}] = {mmc.prgPageLookup[i] / 0x2000:X2} | {mmc.prgPageLookup[i]:X6}");
                }
                Console.WriteLine($"");
                for (var i = 0; i < mmc.chrPageLookup.Length; i++)
                {
                    Console.WriteLine($"\tCHR[{i:X2}] = {mmc.chrPageLookup[i] / 0x0400:X2} | {mmc.chrPageLookup[i]:X6}");
                }
            }
        }

        public string CPUStatus()
        {
            var status = "";
            var cpu = system.cpu;


            if (system.mapper is MMC3 mmc)
            {
                var p = mmc.GetPgrPage(cpu.pc);
                var page = p / 0x2000;
                var romAddr = p + (cpu.pc & 0x1FFF);
                status += $"{page:X2}:{cpu.pc:X4} | {romAddr:X6} |";
            }
            else
            {
                status += $"{cpu.pc:X4} |";
            }

            status += $" a: {cpu.a:X2} | x: {cpu.x:X2} | y: {cpu.y:X2} | s: {cpu.sp:X2} | {cpu.status.ToSmallString()}";
            return status;
        }

 
    }
}
