using AxEmu.NES;
using AxSDL;
using System.Diagnostics;

var running = true;

var nes = new Emulator();
var display = new SDLEmulatorWindow(nes, 2);

// Load NES
nes.LoadROM("D:\\Test\\NES\\mario.nes");

nes.FrameCompleted += display.SetPixels;

var dispThread = new Thread(() =>
{
    while (running)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 89342; i++)
        {
            nes.Clock();
        }
        sw.Stop();
        var elapsed = sw.ElapsedMilliseconds;
        if (elapsed < 16)
            Thread.Sleep((int)(16 - elapsed));
    }
});
dispThread.Start();

display.Run();

running = false;
