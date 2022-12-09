using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;

namespace AxEmu.NES
{
    internal unsafe class SDLNESWindow : IDisposable
    {
        // Setup SDL
        private static readonly Sdl SDL;

        static SDLNESWindow()
        {
            SDL = Sdl.GetApi();

            var err = SDL.Init(Sdl.InitEverything);
            if (err < 0)
                throw new Exception($"Unable to load SDL, error: {err}");
        }

        private Window* window;
        private Joystick* joystick;

        private Surface* pixelSurface;
        private Surface* windowSurface;
        private Renderer* renderer;
        private Rectangle<int> emulatorRect;
        private Rectangle<int> pixelRect;

        private readonly int width = 256;
        private readonly int height = 240;
        private readonly int scale;
        private readonly Emulator nes;

        private Surface* dbgPatternTableLeft;
        private Surface* dbgPatternTableRight;
        private Surface* dbgNameTable;

        private uint audio;

        private bool running = true;

        private Dictionary<Scancode, Action<Emulator>> keyDown = new()
        {
            { Scancode.ScancodeW,         (nes) => nes.joyPad1.PressUp() },
            { Scancode.ScancodeA,         (nes) => nes.joyPad1.PressLeft() },
            { Scancode.ScancodeS,         (nes) => nes.joyPad1.PressDown() },
            { Scancode.ScancodeD,         (nes) => nes.joyPad1.PressRight() },
            { Scancode.ScancodeReturn,    (nes) => nes.joyPad1.PressStart() },
            { Scancode.ScancodeBackspace, (nes) => nes.joyPad1.PressSelect() },
            { Scancode.ScancodeK,         (nes) => nes.joyPad1.PressA() },
            { Scancode.ScancodeL,         (nes) => nes.joyPad1.PressB() },
        };
        private Dictionary<Scancode, Action<Emulator>> keyUp = new()
        {
            { Scancode.ScancodeW,         (nes) => nes.joyPad1.ReleaseUp() },
            { Scancode.ScancodeA,         (nes) => nes.joyPad1.ReleaseLeft() },
            { Scancode.ScancodeS,         (nes) => nes.joyPad1.ReleaseDown() },
            { Scancode.ScancodeD,         (nes) => nes.joyPad1.ReleaseRight() },
            { Scancode.ScancodeReturn,    (nes) => nes.joyPad1.ReleaseStart() },
            { Scancode.ScancodeBackspace, (nes) => nes.joyPad1.ReleaseSelect() },
            { Scancode.ScancodeK,         (nes) => nes.joyPad1.ReleaseA() },
            { Scancode.ScancodeL,         (nes) => nes.joyPad1.ReleaseB() },
        };

        public SDLNESWindow(Emulator nes, int scale)
        {
            this.nes = nes;
            this.scale = scale;

            window = SDL.CreateWindow("AxNES", width, height, (width * scale) + 20 + 256 + 256 + 20, height * scale + 400, (uint)(WindowFlags.Opengl));
            renderer = SDL.CreateRenderer(window, -1, (uint)(RendererFlags.Accelerated));
            pixelSurface = SDL.CreateRGBSurfaceWithFormat(0, width, height, 0, (uint)PixelFormatEnum.Bgr24);
            emulatorRect = new Rectangle<int>(10, 10, width * scale, height * scale);
            pixelRect = new Rectangle<int>(0, 0, width, height);
            windowSurface = SDL.GetWindowSurface(window);

            dbgPatternTableLeft  = SDL.CreateRGBSurfaceWithFormat(0, 128, 128, 0, (uint)PixelFormatEnum.Bgr24);
            dbgPatternTableRight = SDL.CreateRGBSurfaceWithFormat(0, 128, 128, 0, (uint)PixelFormatEnum.Bgr24);
            dbgNameTable         = SDL.CreateRGBSurfaceWithFormat(0, 256, 240, 0, (uint)PixelFormatEnum.Bgr24);

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
                SDL.SetWindowTitle(window, $"FPS: {audioFrames}, APS: {videoFrames}");
               
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

            for (var i = 0; i < length; i++)
            {
                var n = nes.GetSample();
                *buffer++ = (byte)n;
            }
        }

        private static void @throw(Func<int> sdlCall)
        {
            if (sdlCall() < 0)
                throw new Exception("SDL call failed");
        }

        public void SetPixels(byte[] data)
        {
            SDL.Memcpy(pixelSurface->Pixels, ref data[0], (nuint)data.Length);
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



                if (clock == 0)
                {
                    var rect128 = new Rectangle<int>(0, 0, 128, 128);
                    var leftOutRect = new Rectangle<int>((width * scale) + 20, 10, 256, 256);
                    var rightOutRect = new Rectangle<int>((width * scale) + 20 + 256, 10, 256, 256);

                    var left = nes.debug.GetPatternTableLeft();
                    SDL.Memcpy(dbgPatternTableLeft->Pixels, ref left[0], (nuint)left.Length);

                    var right = nes.debug.GetPatternTableRight();
                    SDL.Memcpy(dbgPatternTableRight->Pixels, ref right[0], (nuint)right.Length);

                    @throw(() => SDL.LowerBlitScaled(dbgPatternTableLeft, ref rect128, windowSurface, ref leftOutRect));
                    @throw(() => SDL.LowerBlitScaled(dbgPatternTableRight, ref rect128, windowSurface, ref rightOutRect));
                }

                if (clock == 50)
                {
                    var rect = new Rectangle<int>(0, 0, 256, 240);

                    var nameTbl1OutRect = new Rectangle<int>((width * scale) + 20, 256 + 20, 256, 240);
                    var nameTbl2OutRect = new Rectangle<int>((width * scale) + 20 + 256, 256 + 20, 256, 240);
                    var nameTbl3OutRect = new Rectangle<int>((width * scale) + 20, 256 + 20 + 240, 256, 240);
                    var nameTbl4OutRect = new Rectangle<int>((width * scale) + 20 + 256, 256 + 20 + 240, 256, 240);
                    var ntl1 = nes.debug.GetNameTable(0x2000);
                    var ntl2 = nes.debug.GetNameTable(0x2400);
                    var ntl3 = nes.debug.GetNameTable(0x2800);
                    var ntl4 = nes.debug.GetNameTable(0x2C00);

                    SDL.Memcpy(dbgNameTable->Pixels, ref ntl1[0], (nuint)ntl1.Length);
                    @throw(() => SDL.LowerBlitScaled(dbgNameTable, ref rect, windowSurface, ref nameTbl1OutRect));
                    SDL.Memcpy(dbgNameTable->Pixels, ref ntl2[0], (nuint)ntl2.Length);
                    @throw(() => SDL.LowerBlitScaled(dbgNameTable, ref rect, windowSurface, ref nameTbl2OutRect));
                    SDL.Memcpy(dbgNameTable->Pixels, ref ntl3[0], (nuint)ntl3.Length);
                    @throw(() => SDL.LowerBlitScaled(dbgNameTable, ref rect, windowSurface, ref nameTbl3OutRect));
                    SDL.Memcpy(dbgNameTable->Pixels, ref ntl4[0], (nuint)ntl4.Length);
                    @throw(() => SDL.LowerBlitScaled(dbgNameTable, ref rect, windowSurface, ref nameTbl4OutRect));
                }

                @throw(() => SDL.LowerBlitScaled(pixelSurface, ref pixelRect, windowSurface, ref emulatorRect));
                SDL.UpdateWindowSurface(window);

                clock++;
                clock %= 100;

                videoFrames++;
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
                nes.joyPad1.PressUp();
            else
                nes.joyPad1.ReleaseUp();

            if ((val & JHAT_RIGHT) == JHAT_RIGHT)
                nes.joyPad1.PressRight();
            else
                nes.joyPad1.ReleaseRight();

            if ((val & JHAT_DOWN) == JHAT_DOWN)
                nes.joyPad1.PressDown();
            else
                nes.joyPad1.ReleaseDown();

            if ((val & JHAT_LEFT) == JHAT_LEFT)
                nes.joyPad1.PressLeft();
            else
                nes.joyPad1.ReleaseLeft();
        }

        private static readonly byte JBUTTON_A     = 0x00;
        private static readonly byte JBUTTON_B     = 0x01;
        private static readonly byte JBUTTON_START = 0x0B;
        private static readonly byte JBUTTON_SEL   = 0x0A;
        private void JoyDown(JoyButtonEvent button)
        {
            if (button.Button == JBUTTON_A)
                nes.joyPad1.PressA();

            if (button.Button == JBUTTON_B)
                nes.joyPad1.PressB();

            if (button.Button == JBUTTON_START)
                nes.joyPad1.PressStart();

            if (button.Button == JBUTTON_SEL)
                nes.joyPad1.PressStart();
        }
        private void JoyUp(JoyButtonEvent button)
        {
            if (button.Button == JBUTTON_A)
                nes.joyPad1.ReleaseA();

            if (button.Button == JBUTTON_B)
                nes.joyPad1.ReleaseB();

            if (button.Button == JBUTTON_START)
                nes.joyPad1.ReleaseStart();

            if (button.Button == JBUTTON_SEL)
                nes.joyPad1.ReleaseStart();
        }

        private void KeyDown(KeyboardEvent kbe)
        {
            var scancode = kbe.Keysym.Scancode;

            Console.WriteLine(scancode);

            if (scancode == Scancode.ScancodeEscape)
                running = false;

            if (keyDown.TryGetValue(scancode, out var kda))
                kda(nes);
        }

        private void KeyUp(KeyboardEvent kbe)
        {
            if (keyUp.TryGetValue(kbe.Keysym.Scancode, out var kda))
                kda(nes);
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

                if (window != default(Window*))
                {
                    SDL.DestroyWindow(window);
                    window = default;
                    SDL.Quit();
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
}
