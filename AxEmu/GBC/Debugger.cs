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
}
