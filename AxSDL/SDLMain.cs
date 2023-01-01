using AxEmu;
using Silk.NET.Maths;
using Silk.NET.SDL;

namespace AxSDL;

internal unsafe class SDLMain
    : IDisposable 
{
    // Setup SDL
    internal static readonly Sdl SDL;

    static SDLMain()
    {
        SDL = Sdl.GetApi();

        var err = SDL.Init(Sdl.InitEverything);
        if (err < 0)
            throw new Exception($"Unable to load SDL, error: {err}"); 
    }

    private SDLSurfaceWindow window;
    private Joystick* joystick;

    private Surface* pixelSurface;
    private Surface* windowSurface;
    private Renderer* renderer;
    private Rectangle<int> emulatorRect;
    private Rectangle<int> pixelRect;

    private readonly int width;
    private readonly int height;
    private readonly int scale;
    private readonly IEmulator emulator;

    private List<SDLWindow> extraWindows = new();

    private uint audio;

    private bool running = true;

    private readonly Dictionary<Scancode, Action<IEmulator>> keyDown = new()
    {
        { Scancode.ScancodeW,         (emu) => emu.Controller1.PressUp() },
        { Scancode.ScancodeA,         (emu) => emu.Controller1.PressLeft() },
        { Scancode.ScancodeS,         (emu) => emu.Controller1.PressDown() },
        { Scancode.ScancodeD,         (emu) => emu.Controller1.PressRight() },
        { Scancode.ScancodeReturn,    (emu) => emu.Controller1.PressStart() },
        { Scancode.ScancodeBackspace, (emu) => emu.Controller1.PressSelect() },
        { Scancode.ScancodeK,         (emu) => emu.Controller1.PressA() },
        { Scancode.ScancodeL,         (emu) => emu.Controller1.PressB() },
    };
    private readonly Dictionary<Scancode, Action<IEmulator>> keyUp = new()
    {
        { Scancode.ScancodeW,         (emu) => emu.Controller1.ReleaseUp() },
        { Scancode.ScancodeA,         (emu) => emu.Controller1.ReleaseLeft() },
        { Scancode.ScancodeS,         (emu) => emu.Controller1.ReleaseDown() },
        { Scancode.ScancodeD,         (emu) => emu.Controller1.ReleaseRight() },
        { Scancode.ScancodeReturn,    (emu) => emu.Controller1.ReleaseStart() },
        { Scancode.ScancodeBackspace, (emu) => emu.Controller1.ReleaseSelect() },
        { Scancode.ScancodeK,         (emu) => emu.Controller1.ReleaseA() },
        { Scancode.ScancodeL,         (emu) => emu.Controller1.ReleaseB() },
    };

    public SDLSurfaceWindow AddWindow(int width, int height, int scale, string title)
    {
        var wnd = new SDLSurfaceWindow(width, height, scale, title);
        extraWindows.Add(wnd);
        return wnd;
    }

    public void AddKeyUpCall(Scancode code, Action<IEmulator> func)
    {
        keyUp[code] = func;
    }

    public SDLMain(IEmulator emulator, int scale)
    {
        this.emulator = emulator;
        this.scale = scale;

        width = emulator.GetScreenWidth();
        height = emulator.GetScreenHeight();

        window = new SDLSurfaceWindow(width, height, scale, "AxEmu");
        renderer = SDL.CreateRenderer(window.handle, -1, (uint)(RendererFlags.Accelerated));

        // TODO: Pull from IEmulator
        var settings = new AudioSpec
        {
            Freq = 44100,
            Format = 0x08, // 8-bit unsigned https://wiki.libsdl.org/SDL2/SDL_AudioFormat
            Channels = 1,
            Samples = 512,
            Callback = PfnAudioCallback.From(AudioTick),
            Userdata = null
        };

        AudioSpec obtained;
        audio = SDL.OpenAudioDevice((byte*)0, 0, &settings, &obtained, 0);
        //SDL.PauseAudioDevice(audio, 0); // Play audio

        var frameTimer = new Timer((e) =>
        {
            window.SetTitle($"FPS: {videoFrames}, APS: {audioFrames}");

            audioFrames = 0;
            videoFrames = 0;
        });
        frameTimer.Change(0, 1000);

        if (renderer == default(Renderer*))
            throw new Exception("Unable to create SDL renderer.");

        var controllers = SDL.NumJoysticks();
        Console.WriteLine($"Controllers: {controllers}");
        if (controllers > 0)
        {
            joystick = SDL.JoystickOpen(0);
            Console.WriteLine($"Got a joystick: {joystick->ToString()}!");
        }
    }

    int audioFrames = 0;
    ulong videoFrames = 0;
    byte volume = 10;

    private void AudioTick(void* UserData, byte* buffer, int length)
    {
        audioFrames++;
    }

    internal static void @throw(Func<int> sdlCall)
    {
        if (sdlCall() < 0)
            throw new Exception("SDL call failed");
    }

    public void SetPixels(byte[] data)
    {
        window.SetPixels(data);
        videoFrames++;
    }

    public void AddDebugWindow()
    {

    }

    public void Run()
    {
        ulong clock = 0;
        while (running)
        {
            // Check event queue
            Event evt;
            while (SDL.PollEvent(&evt) == 1)
                HandleEvent(evt);

            //DrawDebug(clock);

            window.BlitSurface();

            clock++;
            clock %= 100;

        }
    }

    private void HandleEvent(Event evt)
    {
        var type = (EventType)evt.Type;
        switch (type)
        {
            case EventType.Quit:
                running = false;
                break;

            case EventType.Keydown:
                KeyDown(evt.Key);
                break;

            case EventType.Keyup:
                KeyUp(evt.Key);
                break;

            case EventType.Joybuttondown:
                JoyDown(evt.Jbutton);
                break;

            case EventType.Joybuttonup:
                JoyUp(evt.Jbutton);
                break;

            case EventType.Joyhatmotion:
                JoyHat(evt.Jhat);
                break;

            default:
                break;
        }
    }

    private static readonly byte JHAT_UP    = 0x01;
    private static readonly byte JHAT_RIGHT = 0x02;
    private static readonly byte JHAT_DOWN  = 0x04;
    private static readonly byte JHAT_LEFT  = 0x08;
    private void JoyHat(JoyHatEvent jhat)
    {
        var val = jhat.Value;

        if ((val & JHAT_UP) == JHAT_UP)
            emulator.Controller1.PressUp();
        else
            emulator.Controller1.ReleaseUp();

        if ((val & JHAT_RIGHT) == JHAT_RIGHT)
            emulator.Controller1.PressRight();
        else
            emulator.Controller1.ReleaseRight();

        if ((val & JHAT_DOWN) == JHAT_DOWN)
            emulator.Controller1.PressDown();
        else
            emulator.Controller1.ReleaseDown();

        if ((val & JHAT_LEFT) == JHAT_LEFT)
            emulator.Controller1.PressLeft();
        else
            emulator.Controller1.ReleaseLeft();
    }

    private static readonly byte JBUTTON_A     = 0x00;
    private static readonly byte JBUTTON_B     = 0x01;
    private static readonly byte JBUTTON_START = 0x0B;
    private static readonly byte JBUTTON_SEL   = 0x0A;
    private void JoyDown(JoyButtonEvent button)
    {
        if (button.Button == JBUTTON_A)
            emulator.Controller1.PressA();

        if (button.Button == JBUTTON_B)
            emulator.Controller1.PressB();

        if (button.Button == JBUTTON_START)
            emulator.Controller1.PressStart();

        if (button.Button == JBUTTON_SEL)
            emulator.Controller1.PressStart();
    }
    private void JoyUp(JoyButtonEvent button)
    {
        if (button.Button == JBUTTON_A)
            emulator.Controller1.ReleaseA();

        if (button.Button == JBUTTON_B)
            emulator.Controller1.ReleaseB();

        if (button.Button == JBUTTON_START)
            emulator.Controller1.ReleaseStart();

        if (button.Button == JBUTTON_SEL)
            emulator.Controller1.ReleaseStart();
    }

    private void KeyDown(KeyboardEvent kbe)
    {
        var scancode = kbe.Keysym.Scancode;

        Console.WriteLine(scancode);

        if (scancode == Scancode.ScancodeEscape)
            running = false;

        if (keyDown.TryGetValue(scancode, out var kda))
            kda(emulator);
    }

    private void KeyUp(KeyboardEvent kbe)
    {
        if (keyUp.TryGetValue(kbe.Keysym.Scancode, out var kda))
            kda(emulator);
    }

    private bool disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            if (joystick != default(Joystick*))
            {
                SDL.JoystickClose(joystick);
                joystick = default;
            }

            window.Dispose();

            SDL.Quit();
        }

        disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        foreach (var w in extraWindows)
            w.Dispose();
        GC.SuppressFinalize(this);
    }

}
