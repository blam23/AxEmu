using Silk.NET.Maths;
using Silk.NET.SDL;

namespace AxSDL;

internal unsafe class SDLSurfaceWindow : SDLWindow
{
    private Surface* windowSurface;
    private Surface* pixelSurface;

    private Rectangle<int> windowRect;
    private Rectangle<int> pixelRect;

    public SDLSurfaceWindow(int width, int height, int scale, string title) : base(width * scale, height * scale, title)
    {
        pixelSurface = SDLMain.SDL.CreateRGBSurfaceWithFormat(0, width, height, 0, (uint)PixelFormatEnum.Bgr24);
        windowSurface = SDLMain.SDL.GetWindowSurface(handle);
        windowRect = new Rectangle<int>(0, 0, width * scale, height * scale);
        pixelRect = new Rectangle<int>(0, 0, width, height);
    }

    public void SetPixels(byte[] data)
    {
        SDLMain.SDL.Memcpy(pixelSurface->Pixels, ref data[0], (nuint)data.Length);
    }

    public void BlitSurface()
    {
        SDLMain.@throw(() => SDLMain.SDL.LowerBlitScaled(pixelSurface, ref pixelRect, windowSurface, ref windowRect));
        SDLMain.SDL.UpdateWindowSurface(handle);
    }
}
