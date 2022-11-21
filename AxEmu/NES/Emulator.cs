using System.Reflection;

namespace AxEmu.NES
{
    public class Emulator
    {
        // Components
        internal Cart cart = new();
        internal CPU cpu;
        internal PPU ppu;
        internal APU apu;
        internal MemoryBus cpuBus;

        // Inputs
        public JoyPad joyPad1;
        public JoyPad joyPad2;

        // Helpers
        public Debugger debug;

        // Events
        public delegate void FrameEvent(byte[] bitmap);
        public event FrameEvent? FrameCompleted;

        // Control
        private ManualResetEvent? CycleWaitEvent;
        private bool running;
        public bool Running => running;

        public void Stop()
        {
            running = false;
        }

        public void SetCycleWaitEvent(ManualResetEvent evt)
        {
            CycleWaitEvent = evt;
        }

        protected virtual void OnFrameCompleted(byte[] bitmap)
        {
            FrameCompleted?.Invoke(bitmap);
        }

        public Emulator(string ROMFileLocation)
            : this()
        {
            LoadROM(ROMFileLocation);
        }

        public Emulator()
        {
            cpuBus = new CPUMemoryBus(this);
            cpu = new CPU(cpuBus);
            ppu = new PPU(this);
            apu = new APU(this);
            joyPad1 = new JoyPad(this);
            joyPad2 = new JoyPad(this);
            debug = new Debugger(this);
        }

        public Emulator(MemoryBus memory)
        {
            cpuBus = memory;
            cpu = new CPU(memory);
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

            ppu.FrameCompleted += OnFrameCompleted;
        }

        public string GetInstr()
        {
            var nextInstr = cpu.bus.Read(cpu.pc);
            return Debug.GetOpcodeName(this, nextInstr);
        }

        public void Run(bool consoleDebug = false, bool waitForKey = false)
        {
            running = true;

            while (running)
            {
                // TODO: Move to debugger
                if (consoleDebug)
                {
                    System.Console.WriteLine($"{cpu.ToSmallString()} | {GetInstr()}");
                }

                // TODO: Move to debugger
                if (waitForKey)
                {
                    var key = System.Console.ReadKey(true);

                    if (key.Key == ConsoleKey.P)
                        System.Console.WriteLine(Debug.PPUState(this));

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