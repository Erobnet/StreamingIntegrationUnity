using Unity.Entities;

namespace GameProject
{
    public struct TimerComponent : IComponentData
    {
        public double ExpireTime;

        public bool TimeIsUp(double timeElapsedTime)
        {
            return timeElapsedTime > ExpireTime;
        }

        public void SetTimer(double timeElapsedTime, double durationInSeconds)
        {
            ExpireTime = timeElapsedTime + durationInSeconds;
        }

        public bool Tick(double timeElapsedTime, double durationInSeconds)
        {
            if ( TimeIsUp(timeElapsedTime) )
            {
                SetTimer(ExpireTime, durationInSeconds);
                return true;
            }
            return false;
        }
    }
}