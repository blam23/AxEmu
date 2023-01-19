namespace AxEmu
{
    public delegate void FrameEvent(byte[] bitmap);

    public interface IEmulator
    {
        bool Clock();
        void Reset();
        int GetScreenWidth();
        int GetScreenHeight();

        void Shutdown();

        event FrameEvent? FrameCompleted;

        IController Controller1 { get; }
        IController Controller2 { get; }

        int CyclesPerFrame { get; }
        double FramesPerSecond { get; }

        //
        // Audio
        //
        (byte left, byte right) APUState();
        void SetAudioSampleRate(int sampleRate);
    }
}
