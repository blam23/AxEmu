using System.Reflection;

namespace AxEmu.NES
{
    public class System
    {
        // Components
        public Cart cart = new();
        public CPU cpu;
        public PPU ppu;
        public APU apu;
        public JoyPad joyPad1;
        public JoyPad joyPad2;
        public IMemory memory;

        // Helpers
        public Debugger debug;

        // Events
        public delegate void FrameEvent(byte[] bitmap);
        public event FrameEvent FrameCompleted;

        // Control
        private ManualResetEvent? CycleWaitEvent;

        public void SetCycleWaitEvent(ManualResetEvent evt)
        {
            CycleWaitEvent = evt;
        }

        protected virtual void OnFrameCompleted(byte[] bitmap)
        {
            FrameCompleted?.Invoke(bitmap);
        }

        public System(string ROMFileLocation)
            : this()
        {
            LoadROM(ROMFileLocation);
        }

        public System()
        {
            memory = new MemoryMapper(this);
            cpu = new CPU(this);
            ppu = new PPU(this);
            apu = new APU(this);
            joyPad1 = new JoyPad(this);
            joyPad2 = new JoyPad(this);
            debug = new Debugger(this);
        }

        public System(IMemory memory)
        {
            this.memory = memory;
            cpu = new CPU(this);
            ppu = new PPU(this);
            apu = new APU(this);
            joyPad1 = new JoyPad(this);
            joyPad2 = new JoyPad(this);
            debug = new Debugger(this);
        }

        private void Reset()
        {
            // TODO: Signal stop & wait for any running crap to end
        }

        public void LoadROM(string ROMFileLocation)
        {
            Reset();
            cart = new(ROMFileLocation);

            if (cart.LoadState == Cart.State.FailedToOpen)
                throw new FileLoadException("Unable to open ROM file");

            if (cart.LoadState == Cart.State.Invalid)
                throw new InvalidDataException("Unable to parse ROM file");

            if (cart.LoadState != Cart.State.Loaded)
                throw new Exception("Error loading ROM file");

            cpu.Init();
            ppu.Init();

            ppu.FrameCompleted += (frame) => OnFrameCompleted(frame);
        }

        public string GetInstr()
        {
            var nextInstr = memory.Read(cpu.pc);
            return Debug.GetOpcodeName(this, nextInstr);
        }

        public void Run(bool consoleDebug = false, bool waitForKey = false)
        {
            while (true)
            {
                // TODO: Move to debugger
                if (consoleDebug)
                {
                    Console.WriteLine($"{cpu.ToSmallString()} | {GetInstr()}");
                }

                // TODO: Move to debugger
                if (waitForKey)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.P)
                        Console.WriteLine(Debug.PPUState(this));

                    if (key.Key == ConsoleKey.Escape)
                        break;
                }

                // Wait for our cycle event if one is set (such as a debugger)
                CycleWaitEvent?.WaitOne();

                cpu.CheckInterrupts();
                cpu.Iterate();
                ppu.Tick(cpu.lastClock * 3);

                //debug.OnInstruction();
            }
        }
    }
}