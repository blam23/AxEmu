using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace AxEmu.NES
{
    internal class Display
    {
        IWindow window;
        Emulator system;

        public Display(Emulator system)
        {
            this.system = system;

            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(256, 240);
            options.WindowBorder = WindowBorder.Fixed;
            options.Title = "AxNES";

            window = Window.Create(options);

            //Assign events.
            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;

            //Run the window.
            window.Run();
        }

        private void OnLoad()
        {
            //Set-up input context.
            IInputContext input = window.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += KeyDown;
            }
        }

        private void OnRender(double obj)
        {
            //Here all rendering should be done.
        }

        private void OnUpdate(double obj)
        {
            //Here all updates to the program should be done.
        }

        private void KeyDown(IKeyboard kb, Key key, int raw)
        {
            //Check to close the window on escape.
            if (key == Key.Escape)
            {
                Close();
            }
        }

        public void Close()
        {
            window.Close();
        }
    }
}
