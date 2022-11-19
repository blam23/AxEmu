using System.Windows;

namespace AxWPF
{
    /// <summary>
    /// Interaction logic for WPFConsole.xaml
    /// </summary>
    public partial class WPFConsole : Window, AxEmu.NES.Debugger.ILogger
    {
        public WPFConsole()
        {
            InitializeComponent();
        }

        private void PushLine(string message)
        {
            textBlock.Text += message + "\n";
            scroll.ScrollToEnd();

            if (textBlock.Text.Length > 20000)
                textBlock.Text = textBlock.Text.Substring(10000, textBlock.Text.Length - 10000);
        }

        public void Log(string message) 
        {
            Application.Current.Dispatcher.BeginInvoke(
               () =>
               {
                   PushLine(message);
               }
            );
        }
    }
}
