using AxEmu.NES;

bool running = true;

// Initialise Display
var display = new PixelDisplay("AxNES", 256, 240, 256 * 2, 240 * 2, Silk.NET.SDL.PixelFormatEnum.Bgr24);
var dispThread = new Thread(display.Run);
dispThread.Start();

// Load NES
var nes = new Emulator();
nes.LoadROM("D:\\Test\\NES\\mario.nes");

nes.FrameCompleted += display.SetPixels;

while(running)
{
    nes.Clock();
}