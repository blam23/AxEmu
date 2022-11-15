namespace AxEmu.NES
{
    public class System
    {
        // Components
        internal ROM rom = new();
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
            rom = new(ROMFileLocation);

            if (rom.LoadState == ROM.State.FailedToOpen)
                throw new FileLoadException("Unable to open ROM file");

            if (rom.LoadState == ROM.State.Invalid)
                throw new InvalidDataException("Unable to parse ROM file");

            if (rom.LoadState != ROM.State.Loaded)
                throw new Exception("Error loading ROM file");

            cpu.Init();
        }

        public void Run(bool consoleDebug = false)
        {
            while (true)
            {
                if (consoleDebug)
                {
                    Console.Clear();
                    Console.WriteLine(cpu);
                    var key = Console.ReadKey();

                    if (key.Key == ConsoleKey.Escape)
                        break;
                }

                cpu.Iterate();
            }
        }
    }
}