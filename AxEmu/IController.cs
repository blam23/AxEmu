namespace AxEmu
{
    public interface IController
    {
        void PressDown();
        void PressUp();
        void PressLeft();
        void PressRight();
        void PressStart();
        void PressSelect();
        void PressA();
        void PressB();
        void ReleaseDown();
        void ReleaseUp();
        void ReleaseLeft();
        void ReleaseRight();
        void ReleaseStart();
        void ReleaseSelect();
        void ReleaseA();
        void ReleaseB();
    }
}
