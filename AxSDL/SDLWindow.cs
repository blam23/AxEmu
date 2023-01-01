using Silk.NET.Input;
using Silk.NET.SDL;

namespace AxSDL;

internal unsafe class SDLWindow 
    : IDisposable
{
    public Window* handle;

    private readonly int width;
    private readonly int height;

    public SDLWindow(int width, int height, string title)
    {
        this.width = width;
        this.height = height;

        handle = SDLMain.SDL.CreateWindow(title, width, height, width, height, (uint)(WindowFlags.Opengl));
    }

    public void SetTitle(string title) => SDLMain.SDL.SetWindowTitle(handle, title);

    private bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            if (handle != default(Window*))
            {
                SDLMain.SDL.DestroyWindow(handle);
                handle = default;
            }
        }

        disposed = true;
    }
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
