using System.Reflection;

namespace AxEmu.NES
{
    public class System
    {
        // Components
        internal Cart cart = new();
        internal CPU cpu;
        internal PPU ppu;
        internal APU apu;
        internal IMemory memory;

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
        }

        public System(IMemory memory)
        {
            this.memory = memory;
            cpu = new CPU(this);
            ppu = new PPU(this);
            apu = new APU(this);
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
        }

        public void Run(bool consoleDebug = false, bool waitForKey = false)
        {
            while (true)
            {
                if (consoleDebug)
                {
                    var nextInstr = memory.Read(cpu.pc);
                    var prettyInstr = Debug.OpCodeNames[nextInstr](this);
                    Console.WriteLine($"{cpu.ToSmallString()} | {prettyInstr}");

                    if (waitForKey)
                    {
                        var key = Console.ReadKey(true);

                        if (key.Key == ConsoleKey.Escape)
                            break;
                    }
                }

                cpu.Iterate();
                ppu.Tick(cpu.lastClock * 3);
            }
        }
    }
}