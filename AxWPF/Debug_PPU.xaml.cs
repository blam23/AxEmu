using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AxWPF
{
    /// <summary>
    /// Interaction logic for Debug_PPU.xaml
    /// </summary>
    public partial class Debug_PPU : Window
    {
        readonly WriteableBitmap bmpLeft;
        readonly WriteableBitmap bmpRight;


        Timer patternTableUpdateTimer;
        public AxEmu.NES.Emulator? nes;

        public Debug_PPU()
        {
            InitializeComponent();

            patternTableUpdateTimer = new Timer((x) => UpdatePatternTables());
            patternTableUpdateTimer.Change(0, 100);

            bmpLeft  = new WriteableBitmap(128,128, 96, 96, PixelFormats.Bgr24, null);
            bmpRight = new WriteableBitmap(128,128, 96, 96, PixelFormats.Bgr24, null);

            PTL.Source = bmpLeft;
            PTR.Source = bmpRight;
        }

        public void SetNes(AxEmu.NES.Emulator system)
        {
            nes = system;
            UpdatePatternTables();
        }

        public void ClearNes()
        {
            nes = null;
        }

        public void UpdatePatternTables()
        {
            if (nes == null || nes.Unloaded())
                return;

            Application.Current.Dispatcher.BeginInvoke(
                () =>
                {
                    var fullRect = new Int32Rect(0, 0, 128, 128);
                    byte[] ptl = nes.debug.GetPatternTableLeft();
                    byte[] ptr = nes.debug.GetPatternTableRight();

                    bmpLeft.WritePixels
                    (
                        fullRect,
                        ptl,
                        3 * 128,
                        0
                    );

                    bmpRight.WritePixels
                    (
                        fullRect,
                        ptr,
                        3 * 128,
                        0
                    );
                }
            );
        }
    }
}
