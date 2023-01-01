using AxEmu.GBC;
using Silk.NET.SDL;

namespace AxSDL;

internal class SDLGBCDebug
{
    private static SDLGBCDebug? instance;

    SDLSurfaceWindow tileDisplay; // disposed by SDLMain
    SDLSurfaceWindow oamDisplay; // disposed by SDLMain
    Emulator system;
    SDLMain main;

    public SDLGBCDebug(Emulator system, SDLMain main)
    {
        this.system = system;
        this.main = main;
        tileDisplay = main.AddWindow(16 * 9, 24 * 9, 3, "Tiles");
    }

    private void update()
    {
        tileDisplay.SetPixels(system.debug.RenderTileMap());
        tileDisplay.BlitSurface();
    }

    public static void Update()
    {
        instance?.update();
    }

    public static void Init(Emulator system, SDLMain main)
    {
        instance ??= new SDLGBCDebug(system, main);
    }
}
