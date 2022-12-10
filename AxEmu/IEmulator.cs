namespace AxEmu
{
    public delegate void FrameEvent(byte[] bitmap);

    public interface IEmulator
    {
        void Clock();
        void Reset();
        int GetScreenWidth();
        int GetScreenHeight();

        event FrameEvent? FrameCompleted;

        IController Controller1 { get; }
        IController Controller2 { get; }
    }
}
