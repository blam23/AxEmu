namespace AxEmu;

public class ConsoleColour : IDisposable
{
    readonly ConsoleColor bg;
    readonly ConsoleColor fg;

    private static void FixLine()
    {
        // Make sure bg colour takes up whole line
        Console.WriteLine(new string(' ', Console.BufferWidth - Console.CursorLeft));
        Console.CursorTop--;
    }

    public ConsoleColour(ConsoleColor Bg, ConsoleColor Fg)
    {
        bg = Console.BackgroundColor;
        fg = Console.ForegroundColor;

        Console.BackgroundColor = Bg;
        Console.ForegroundColor = Fg;

        FixLine();
    }

    public void Dispose()
    {
        Console.BackgroundColor = bg;
        Console.ForegroundColor = fg;

        FixLine();
        GC.SuppressFinalize(this);
    }

    public static ConsoleColour Error()
    {
        return new ConsoleColour(ConsoleColor.Red, ConsoleColor.White);
    }
}
