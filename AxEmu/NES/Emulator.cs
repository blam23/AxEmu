using System.Diagnostics;
using System.Reflection;

namespace AxEmu.NES
{
    public class Emulator : IEmulator
    {
        // Components
        internal Cart cart = new();
        internal CPU cpu;
        internal PPU ppu;
        internal APU apu;
        internal MemoryBus cpuBus;
        internal IMapper mapper;

        // Inputs
        internal JoyPad joyPad1;
        internal JoyPad joyPad2;
        public IController Controller1 => joyPad1;
        public IController Controller2 => joyPad2;

        // Data
        private Mirroring mirroring = Mirroring.Horizontal;
        public Mirroring Mirroring 
        { 
            get { return mirroring; } 
            set { mirroring = value; ppu.UpdateMirroring(); } 
        }
        public bool Unloaded() => mapper == null;

        // Helpers
        public Debugger debug;
        public int GetScreenWidth() => 256;
        public int GetScreenHeight() => 240;

        public int CyclesPerFrame => 89342;
        public double FramesPerSecond => 60;

        // Events
        public event FrameEvent? FrameCompleted;

        // Control
        private ManualResetEvent? CycleWaitEvent;
        private bool running;
        public bool Running => running;

        public ushort GetSample() => apu.AudioData;

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

        public Emulator(MemoryBus? memory = null)
        {
            cpuBus = memory ?? new CPUMemoryBus(this);
            cpu = new CPU(cpuBus);
            ppu = new PPU(this);
            apu = new APU(this);

            joyPad1 = new JoyPad(this);
            joyPad2 = new JoyPad(this);

            debug = new Debugger(this);

            LoadMappers();
            mapper = CreateMapper(0);
            ppu.FrameCompleted += OnFrameCompleted;
        }

        internal Dictionary<ushort, Type> mappers = new();
        private void LoadMappers()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var attrs = type.GetCustomAttributes(typeof(MapperAttribute), true);
                foreach (var m in attrs)
                {
                    if (m is MapperAttribute mapper)
                    {
                        mappers[mapper.MapperNumber] = type;
                    }
                }
            }
        }

        private IMapper CreateMapper(ushort mapperNumber)
        {
            if(mappers.TryGetValue(mapperNumber, out var mapperType))
            {
                if (Activator.CreateInstance(mapperType) is IMapper mapper)
                    return mapper;

                throw new Exception($"Mapper '{mapperNumber}' is invalid!");
            }

            throw new Exception($"Mapper '{mapperNumber}' not loaded (likely not supported).");
        }


        public void Reset()
        {
            // TODO: Signal stop & wait for any running crap to end
        }

        public void LoadROM(string ROMFileLocation)
        {
            Reset();
            cart = new(ROMFileLocation);
            Mirroring = cart.mirroring;

            if (cart.LoadState == Cart.State.FailedToOpen)
                throw new FileLoadException("Unable to open ROM file");

            if (cart.LoadState == Cart.State.Invalid)
                throw new InvalidDataException("Unable to parse ROM file");

            if (cart.LoadState != Cart.State.Loaded)
                throw new Exception("Error loading ROM file");

            mapper = CreateMapper(cart.Mapper);
            mapper.Init(this);

            cpu.Init();
        }

        private ulong clock = 0;
        public bool Clock()
        {
            // Always clock PPU
            ppu.Clock();

            // Clock APU every 6 cycles
            if (clock % 6 == 0)
            {
                apu.Clock();
            }

            // Clock CPU every 3 cycles
            if (clock % 3 == 0)
            {
                if (mapper.IsIRQSet())
                    cpu.SetIRQ();

                cpu.Clock();
            }

            clock++;

            return false;
        }

        public void Run()
        {
            running = true;

            while (running)
            {
                // Wait for our cycle event if one is set (such as a debugger)
                CycleWaitEvent?.WaitOne();

                var sw = Stopwatch.StartNew();
                for (var i = 0; i < 89342; i++)
                {
                    Clock();
                }
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                if (elapsed < 16)
                    Thread.Sleep((int)(16 - elapsed));
            }
        }

        public void Shutdown()
        {
        }

        // TODO: THIS!
        public (byte left, byte right) APUState()
        {
            return (0, 0);
        }

        public void SetAudioSampleRate(int sampleRate)
        {
            throw new NotImplementedException();
        }
    }
}