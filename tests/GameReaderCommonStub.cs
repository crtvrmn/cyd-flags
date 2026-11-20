using System;

// Minimal stand-in so the pure DeltaTracker tests can compile without the
// SimHub SDK. Only the three members DeltaTracker reads are needed.
namespace GameReaderCommon
{
    public class StatusDataBase
    {
        public TimeSpan? CurrentLapTime;
        public int CurrentLap;
        public double TrackPositionPercent;
    }
}
