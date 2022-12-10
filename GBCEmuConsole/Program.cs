using AxEmu.GBC;

Emulator gbc = new();

gbc.LoadROM("D:\\Test\\GBC\\tetris.gb");

var readKey = false;
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
    }

    gbc.Clock();

    i++;
}
