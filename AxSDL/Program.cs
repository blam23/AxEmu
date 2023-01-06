using AxEmu;
using AxSDL;
using static System.Net.Mime.MediaTypeNames;
using System;

var running = true;
var runNES = false;
var runGBC = true;

IEmulator emu;

AxEmu.GBC.Emulator? gbc = null;

if (runNES)
{
    var nes = new AxEmu.NES.Emulator();
    emu = nes;

    // Load NES
    nes.LoadROM("D:\\Test\\NES\\mario.nes");
}
else if (runGBC)
{
    gbc = new AxEmu.GBC.Emulator();
    emu = gbc;

    gbc.LoadROM(@"D:\Test\GBC\pokeblue.gb");
    //gbc.LoadROM(@"D:\Test\GBC\kirby.gb");
    //gbc.LoadROM(@"D:\Test\GBC\tetris.gb");
    //gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\bully\bully.gb");
    //gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\blargg\cpu_instrs\cpu_instrs.gb");
    //gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\mooneye-test-suite\acceptance\ppu\hblank_ly_scx_timing-GS.gb");
    //gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\blargg\instr_timing\instr_timing.gb");
    //gbc.LoadROM(@"D:\Test\GBC\game-boy-test-roms-v5.1\dmg-acid2\dmg-acid2.gb");
    //gbc.LoadROM("D:\\Test\\GBC\\zelda.gb");
}
else
{
    throw new Exception("No Emulator selected");
}

var display = new SDLMain(emu, 4);
emu.FrameCompleted += (b) => { display.SetPixels(b); SDLGBCDebug.Update(); };

bool printCPU = false;

if (runGBC && gbc is not null)
{
    SDLGBCDebug.Init(gbc, display);
    gbc.SetSleep(new SDLSleep());
    display.AddKeyUpCall(Silk.NET.SDL.Scancode.ScancodeF1, (e) => { (e as AxEmu.GBC.Emulator)?.debug.ToggleSlowMode(); });
    display.AddKeyUpCall(Silk.NET.SDL.Scancode.ScancodeF2, (e) => { printCPU = !printCPU; });
    display.AddKeyUpCall(Silk.NET.SDL.Scancode.ScancodeSpace, (e) => { gbc.LimitFrames = !gbc.LimitFrames; });
}

var dispThread = new Thread(() =>
{
    //var sw = Stopwatch.StartNew();
    //long targetMS = (long)(1000 / emu.FramesPerSecond);
    while (running)
    {
        for (var i = 0; i < emu.CyclesPerFrame; i++)
        {
            if (printCPU && gbc is not null && gbc.CpuRanLastClock)
                Console.WriteLine(gbc.debug.CPUStatus());

            emu.Clock();
        }


        //var elapsed = sw.ElapsedMilliseconds;
        //if (elapsed < targetMS)
        //Thread.Sleep((int)(targetMS - elapsed));

        //sw.Reset();
    }
});

dispThread.Start();
display.Run();

running = false;
emu.Shutdown();

display.Dispose();
