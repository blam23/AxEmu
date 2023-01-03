namespace AxEmu.GBC;

internal class JoyPad : IController
{
    internal Emulator system;

    internal byte state;
    internal byte select;

    public JoyPad(Emulator system)
    {
        this.system = system;
        state  = 0xFF;
        select = 0x00;

        system.bus.RegisterIOProperties(GetType(), this);
    }

    public enum Key : byte
    {
        Right  = 0b0000_0001,
        Left   = 0b0000_0010,
        Up     = 0b0000_0100,
        Down   = 0b0000_1000,
        A      = 0b0001_0000,
        B      = 0b0010_0000,
        Select = 0b0100_0000,
        Start  = 0b1000_0000,
    }

    private void press(Key key)
    {
        var k = (byte)~(byte)key;
        state &= k;
        system.cpu.RequestInterrupt(CPU.Interrupt.Joypad);

        Console.WriteLine($"Press -> State: {state:X2}, keyb: {(byte)key:X2}, k: {k:X2} ({key})");
    }

    private void release(Key key)
    {
        state |= (byte)key;

        Console.WriteLine($"Release -> State: {state:X2}");
    }

    [IO(Address = 0xFF00)]
    public byte JOYP
    {
        get
        {
            if ((select & 0b0001_0000) == 0x00)
                return (byte)(select | (state & 0x0F));
            if ((select & 0b0010_0000) == 0x00)
                return (byte)(select | (state >> 4));

            return select;
        }
        set
        {
            select = value;
        }
    }

    public void PressDown() => press(Key.Down);
    public void PressUp() => press(Key.Up);
    public void PressLeft() => press(Key.Left);
    public void PressRight() => press(Key.Right);
    public void PressStart() => press(Key.Start);
    public void PressSelect() => press(Key.Select);
    public void PressA() => press(Key.A);
    public void PressB() => press(Key.B);

    public void ReleaseDown() => release(Key.Down);
    public void ReleaseUp() => release(Key.Up);
    public void ReleaseLeft() => release(Key.Left);
    public void ReleaseRight() => release(Key.Right);
    public void ReleaseStart() => release(Key.Start);
    public void ReleaseSelect() => release(Key.Select);
    public void ReleaseA() => release(Key.A);
    public void ReleaseB() => release(Key.B);
}
