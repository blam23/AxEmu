using AxEmu.NES;

Emulator nes = new();

nes.LoadROM("D:\\Test\\NES\\mario-3.nes");

var readKey = false;
var printCpu = true;

ushort ReadHexValue()
{
    var num = "";

    while (true)
    {
        var key = Console.ReadKey(true);

        if (num.Length < 5)
        {
            if (key.KeyChar >= '0' 
                && key.KeyChar <= '9')
            {
                Console.Write(key.KeyChar);
                num += key.KeyChar;
            }

            if (key.KeyChar >= 'A'
                && key.KeyChar <= 'F')
            {
                Console.Write(key.KeyChar);
                num += key.KeyChar;
            }

            if (key.KeyChar >= 'a'
                && key.KeyChar <= 'f')
            {
                var upper = char.ToUpper(key.KeyChar);
                Console.Write(upper);
                num += upper;
            }
        }

        if (key.Key == ConsoleKey.Backspace 
            && num.Length > 0)
        {
            Console.CursorLeft -= 1;
            Console.Write(' ');
            num = num[..^1];
        }

        if (key.Key == ConsoleKey.Enter)
            break;
    }

    return num.Length == 0 ? (ushort)0 : ushort.Parse(num, System.Globalization.NumberStyles.HexNumber);
}

void DumpMemory(ulong stride = 16)
{
    Console.Write("\n\nPlease write mem range to dump, start: 0x");
    var start = ReadHexValue();
    Console.Write("\tend: 0x");
    var end = ReadHexValue();

    for (ulong i = start; i <= end; i+= stride)
    {

        Console.Write($"\n{i:X4} | ");
        for (ushort j = 0; j < stride; j++)
        {
            if (i + j > 0xFFFF)
                return;

            Console.Write($"{nes.debug.ReadPRGRom((ushort)(j + i)):X2} ");
        }

        Console.Write("| ");

        for (ushort j = 0; j < stride; j++)
        {
            if (i + j > 0xFFFF)
                return;

            var asC = (char)nes.debug.ReadPRGRom((ushort)(j + i));
            if (!char.IsAsciiLetterOrDigit(asC) || char.IsPunctuation(asC))
                asC = '.';

            Console.Write($"{asC}");
        }
    }

    Console.WriteLine();
}

ulong i = 0;
while (true)
{
    if (i % 3 == 0)
    {
        if (printCpu)
            Console.WriteLine(AxEmu.NES.Debug.Helpers.CPUState(nes));

        if (readKey)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.P)
                Console.WriteLine(AxEmu.NES.Debug.Helpers.PPUState(nes));

            if (key.Key == ConsoleKey.Escape)
                break;

            if (key.Key == ConsoleKey.M)
            {
                DumpMemory();
            }

            if (key.Key == ConsoleKey.N)
            {
                nes.debug.DumpMMC3Lookups();
            }
        }
    }

    nes.Clock();

    i++;
}
