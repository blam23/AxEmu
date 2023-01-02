using System.Drawing;
using System.Text;

namespace AxEmu.GBC;

public class Debugger
{
    public Emulator system;

    public Debugger(Emulator system)
    {
        this.system = system;
    }

    public string FlagString()
    {
        var flags = system.cpu.flags;
        return $"{(flags.Z ? 'Z' : '-')}{(flags.N ? 'N' : '-')}{(flags.H ? 'H' : '-')}{(flags.C ? 'C' : '-')}";
    }

    public string PCStr()
    {
        // TODO: Mapping, etc.
        return $"{system.cpu.PC:X4}";
    }

    public string InstructionStr()
    {
        // TODO: Mapping, etc.
        var inst = system.bus.Read(system.cpu.PC);
        var instData = system.cpu.instructions[inst];

        if (instData == null)
        {
            return $"{inst:X2} (UNKNOWN)";
        }

        var name = instData.Name;
        name = name.Replace("Abs", $"${system.bus.ReadWord((ushort)(system.cpu.PC + 1)):X4}");
        name = name.Replace("Imm", $"0x{system.bus.Read((ushort)(system.cpu.PC + 1)):X2}");

        return name;
    }

    public string CPUStatus()
    {
        var cpu = system.cpu;

        return $"{PCStr()} | {FlagString()} | A:{cpu.A:X2} B:{cpu.B:X2} C:{cpu.C:X2} D:{cpu.D:X2} E:{cpu.E:X2} H:{cpu.H:X2} L:{cpu.L:X2} SP:{cpu.SP:X4} | {InstructionStr()}";    
    }

    public void SetupGBDoctorMode()
    {
        system.ppu.dbgFixLY = true; 
    }

    public string CPUStatusGBDoctor()
    {
        var cpu = system.cpu;

        var pcmem1 = system.bus.Read(system.cpu.PC);
        var pcmem2 = system.bus.Read((ushort)(system.cpu.PC+1));
        var pcmem3 = system.bus.Read((ushort)(system.cpu.PC+2));
        var pcmem4 = system.bus.Read((ushort)(system.cpu.PC+3));

        return $"A:{cpu.A:X2} F:{cpu.flags.AsByte:X2} B:{cpu.B:X2} C:{cpu.C:X2} D:{cpu.D:X2} E:{cpu.E:X2} H:{cpu.H:X2} L:{cpu.L:X2} SP:{cpu.SP:X4} PC:{cpu.PC:X4} PCMEM:{pcmem1:X2},{pcmem2:X2},{pcmem3:X2},{pcmem4:X2}";
    }

    public string DumpMemory(int start, int end, int stride = 16, ushort highlight = ushort.MaxValue)
    {
        start = int.Clamp(start, 0, ushort.MaxValue-1);
        end   = int.Clamp(end, 1, ushort.MaxValue);

        StringBuilder str = new();
        for (int i = start; i <= end; i += stride)
        {
            var x = i == highlight ? " >" : "  ";

            str.Append($"\n{x} {i:X4} | ");
            for (ushort j = 0; j < stride; j++)
            {
                if (i + j > 0xFFFF)
                    break;

                str.Append($"{system.bus.Read((ushort)(j + i)):X2} ");
            }

            str.Append("| ");

            for (ushort j = 0; j < stride; j++)
            {
                if (i + j > 0xFFFF)
                    break;

                var asC = (char)system.bus.Read((ushort)(j + i));
                if (!char.IsAsciiLetterOrDigit(asC) || char.IsPunctuation(asC))
                    asC = '.';

                str.Append($"{asC}");
            }
        }

        return str.ToString();
    }

    public string SurroundingMemory()
    {
        return DumpMemory(system.cpu.PC - 0x100, system.cpu.PC + 0x100, 16, system.cpu.PC);
    }

    private Color lookupDbgPalette(byte value)
    {
        return value switch
        {
            0x0 => Color.Red,
            0x1 => Color.DarkRed,
            0x2 => Color.Violet,
            0x3 => Color.DarkViolet,

            _ => Color.DodgerBlue
        };
    }

    private void RenderTile(byte[] bitmap, int offset, int tileNum, int x, int y)
    {
        var mem = system.bus;

        var onTile = tileNum == system.ppu.dbgCurrentTile;

        Func<byte, Color> lookup = onTile ? lookupDbgPalette : system.ppu.lookupBGPalette;

        for (int ty = 0; ty < 16; ty+= 2)
        {
            var addr = offset + (tileNum * 16) + ty;
            var tileLo = mem.Read((ushort)(addr));
            var tileHi = mem.Read((ushort)(addr + 1));

            for (var bit = 7; bit >= 0; bit--)
            {
                byte hi = (byte)((tileLo & (1 << bit)) >> bit);
                byte lo = (byte)((tileHi & (1 << bit)) >> bit);

                var pixelColour = lookup((byte)(hi << 1 | lo));

                var px = (x * 9) + (7 - bit);
                var py = (y * 9) + (ty / 2);

                bitmap[(px + (py * 16 * 9)) * 3 + 0] = pixelColour.B;
                bitmap[(px + (py * 16 * 9)) * 3 + 1] = pixelColour.G;
                bitmap[(px + (py * 16 * 9)) * 3 + 2] = pixelColour.R;
            }
        }
    }

    public void SetSlowRender(bool value)
    {
        system.ppu.dbgSlowMode = value;
    }
    public void ToggleSlowMode()
    {
        SetSlowRender(!system.ppu.dbgSlowMode);
    }

    public byte[] RenderTileMap()
    {
        var bitmap = new byte[24 * 16 * 9 * 9 * 3];
        var offset = 0x8000;
        var tile = 0;

        for (byte tileY = 0; tileY < 24; tileY++)
        {
            for (byte tileX = 0; tileX < 16; tileX++)
            {
                RenderTile(bitmap, offset, tile, tileX, tileY);
                tile++;
            }
        }

        return bitmap;
    }
}
