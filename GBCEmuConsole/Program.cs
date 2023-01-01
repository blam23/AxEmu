using AxEmu.GBC;

Emulator gbc = new();

gbc.LoadROM("D:\\Test\\GBC\\tetris.gb");

var readKey = true;
var printCpu = true;

ulong i = 0;
while (true)
{
    if (printCpu)
        Console.WriteLine(gbc.debug.CPUStatus());

    if (readKey)
    {
        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.Escape)
            break;

        if(key.Key == ConsoleKey.M)
            Console.WriteLine(gbc.debug.SurroundingMemory());
    }

    try
    {
        gbc.Clock();
    }
    catch(Exception ex)
    {
        Console.WriteLine();
        using (_ = new AxEmu.ConsoleColour(ConsoleColor.DarkRed, ConsoleColor.White))
            Console.WriteLine($"\n <ERROR> {ex.Message}\n");

        Console.WriteLine(gbc.debug.SurroundingMemory());
        
        throw;
    }

    i++;
}
