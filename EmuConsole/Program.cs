
using AxEmu.NES;
using System.Diagnostics;

Emulator testNes = new();

testNes.LoadROM("D:\\Test\\NES\\tetris.nes");

bool readKey = true;
bool printCpu = true;

ulong i = 0;
while (true)
{
    if (i % 3 == 0)
    {
        if (printCpu)
            Console.WriteLine(AxEmu.NES.Debug.CPUState(testNes));

        if (readKey)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.P)
                Console.WriteLine(AxEmu.NES.Debug.PPUState(testNes));

            if (key.Key == ConsoleKey.Escape)
                break;
        }
    }

    testNes.Clock();

    i++;
}
