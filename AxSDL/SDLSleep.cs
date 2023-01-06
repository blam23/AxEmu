using AxEmu;

namespace AxSDL;

internal class SDLSleep : ISleep
{
    public void Sleep(int ms)
    {
        SDLMain.Sleep(ms);
    }
}
