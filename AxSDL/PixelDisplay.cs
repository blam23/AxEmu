using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;

namespace AxEmu.NES
{
    internal unsafe class PixelDisplay : IDisposable
    {
        // Setup SDL
        private static readonly Sdl SDL;
        static PixelDisplay()
        {
            SDL = Sdl.GetApi();
            var err = SDL.Init(Sdl.InitEverything);
            if (err < 0)
            {
                throw new Exception($"Unable to load SDL, error: {err}");
            }
        }

        private Window*        window;
        private Surface*       pixelSurface;
        private Surface*       windowSurface;
        private Renderer*      renderer;
        private Rectangle<int> windowRect;
        private Rectangle<int> pixelRect;
        public PixelDisplay(string title, int pixelWidth, int pixelHeight, int windowWidth, int windowHeight, PixelFormatEnum pixelFormat)
        {
            window        = SDL.CreateWindow(title, 300, 300, windowWidth, windowHeight, (uint)(WindowFlags.Shown));
            renderer      = SDL.CreateRenderer(window, -1, (uint)(RendererFlags.Accelerated));
            pixelSurface  = SDL.CreateRGBSurfaceWithFormat(0, pixelWidth, pixelHeight, 0, (uint)pixelFormat);
            windowRect    = new Rectangle<int>(0, 0, windowWidth, windowHeight);
            pixelRect     = new Rectangle<int>(0, 0, pixelWidth, pixelHeight);
            windowSurface = SDL.GetWindowSurface(window);

            if(renderer == default(Renderer*))
                throw new Exception("Unable to create SDL renderer.");
        }

        private static void @throw(Func<int> sdlCall)
        {
            if (sdlCall() < 0)
            {
                throw new Exception("SDL call failed");
            }
        }

        public void SetPixels(byte[] data)
        {
            SDL.Memcpy(pixelSurface->Pixels, ref data[0], (nuint)data.Length);
            @throw(() => SDL.LowerBlitScaled(pixelSurface, ref pixelRect, windowSurface, ref windowRect));
            SDL.UpdateWindowSurface(window);
        }

        public void Run()
        {
            int x = 0;
            while(true)
            {
                // Check event queue
                Event evt;
                while(SDL.PollEvent(&evt) == 1)
                {
                    var type = (EventType)evt.Type;
                    Console.WriteLine($"{type}");
                    switch (type)
                    {
                        case EventType.Quit:
                            return;

                        case EventType.Mousemotion:
                            Console.WriteLine($"{evt.Motion.X}, {evt.Motion.Y}");
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (window != default(Window*))
                    {
                        SDL.DestroyWindow(window);
                        window = default;
                        SDL.Quit();
                    }
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private bool disposed;
    }
}
