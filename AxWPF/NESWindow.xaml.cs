using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace AxWPF
{
    /// <summary>
    /// Interaction logic for NESWindow.xaml
    /// </summary>
    public partial class NESWindow : Window
    {
        readonly WriteableBitmap bitmap;
        private readonly AxEmu.NES.System nes;
        private readonly Thread emuThread;
        int frames = 0;

        private ManualResetEvent frameWaitEvent = new(false);
        private bool stopOnNextFrame = true;
        private bool logging = false;
        private bool logFromNextKeypress = true;
        private StreamWriter? logWriteStream;

        public NESWindow()
        {
            InitializeComponent();

            //nes = new("D:\\Test\\NES\\zelda.nes");
            //nes = new("D:\\Test\\NES\\mario.nes");
            nes = new("D:\\Test\\NES\\nes-test-roms-master\\other\\nestest.nes");
            //nes = new("D:\\Test\\NES\\nes-test-roms-master\\other\\genie.nes");

            // Technically this can be a much lower bit image, but we might want ot apply some effects n stuff
            bitmap = new(
                (int)AxEmu.NES.PPU.RenderWidth, 
                (int)AxEmu.NES.PPU.RenderHeight, 
                96, 96, PixelFormats.Bgr24, null);

            image.Source = bitmap;

            emuThread = new Thread(() => nes.Run());
            emuThread.Start();

            nes.ppu.FrameCompleted += DispatchFrame;

            nes.SetCycleWaitEvent(frameWaitEvent);

            var fpsTimer = new Timer((t) =>
            {
                Application.Current.Dispatcher.BeginInvoke(() => {
                    Title = $"NES - FPS: {frames}";
                    frames = 0;
                });
            });
            fpsTimer.Change(0, 1000);
        }

        public void DispatchFrame(byte[] frame)
        {
            frames++;

            Application.Current.Dispatcher.BeginInvoke(
               () =>
               {
                   if (stopOnNextFrame)
                       frameWaitEvent.Reset();

                   stopOnNextFrame = false;
               }
            );

            Application.Current.Dispatcher.BeginInvoke(
                () => bitmap.WritePixels
                (
                    new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
                    frame,
                    3 * bitmap.PixelWidth,
                    0
                )
            );
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            stopOnNextFrame = false;
            frameWaitEvent.Set();
        }

        private void RunFrame_Click(object sender, RoutedEventArgs e)
        {
            stopOnNextFrame = true;
            frameWaitEvent.Set();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            frameWaitEvent.Reset();
        }

        private void StartLog()
        {
            StopLog();
            logFromNextKeypress = false;
            logging = true;

            logWriteStream = new StreamWriter("D:\\Test\\logs\\log.txt");

            nes.debug.Instruction += LogState;
        }
        private void StopLog()
        {
            nes.debug.Instruction -= LogState;
            logging = false;
            logWriteStream?.Close();
        }

        private void LogState(AxEmu.NES.System system)
        {
            try
            {
                logWriteStream?.WriteLine($"{system.cpu.ToSmallString()} | {system.GetInstr()}");
                logWriteStream?.Flush();
            } catch { }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch(e.Key)
            {
                case System.Windows.Input.Key.Up:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressUp());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Down:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressDown());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Left:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressLeft());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Right:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressRight());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.J:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressStart());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.S:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressSelect());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.F:    
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressA());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.D:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.PressB());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.L:
                    if (logging)
                        StartLog();
                    else
                        StopLog();
                    break;
                default:
                    break;
            }

            if (e.Handled && logFromNextKeypress)
                StartLog();
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.Up:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseUp());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Down:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseDown());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Left:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseLeft());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Right:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseRight());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.J:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseStart());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.S:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseSelect());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.F:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseA());
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.D:
                    Application.Current.Dispatcher.BeginInvoke(() => nes.joyPad1.ReleaseB());
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        }

        private void LogFromNextKeypress_Clicked(object sender, RoutedEventArgs e)
        {
            logFromNextKeypress = true;
        }
    }
}
