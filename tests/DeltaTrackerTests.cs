using System;
using CydFlags;

/// <summary>
/// Pure logic tests for DeltaTracker. The numeric Update overload needs no
/// SimHub assembly; GameReaderCommonStub only exists so the source file can be
/// linked into this test project unchanged.
/// </summary>
internal static class DeltaTrackerTests
{
    public static int Run()
    {
        int fail = 0;
        DeltaTracker t = new DeltaTracker();

        // Lap 1: 60 s, sampled every 0.5 %.
        for (int i = 0; i <= 200; i++) t.Update(i / 200.0, i * 0.30, 1);
        fail += Expect(!t.Update(0.995, 59.85, 1).HasValue, "first lap has no delta");

        // Lap 2 at the same pace: the delta should hover around zero.
        t.Update(0.0, 0.0, 2);
        DeltaResult r = DeltaResult.None;
        for (int i = 1; i <= 100; i++) r = t.Update(i / 200.0, i * 0.30, 2);
        fail += Expect(r.HasValue && Math.Abs(r.Seconds) < 0.05, "same pace near zero");

        // A faster lap is negative; a slower lap is positive.
        t.Reset();
        for (int i = 0; i <= 200; i++) t.Update(i / 200.0, i * 0.30, 1);
        t.Update(0.0, 0.0, 2);
        for (int i = 1; i <= 200; i++) t.Update(i / 200.0, i * 0.29, 2);
        t.Update(0.0, 0.0, 3);
        r = t.Update(0.50, 28.0, 3);
        fail += Expect(r.HasValue && r.Seconds < -0.5 && r.Seconds > -1.5, "faster lap is negative");

        t.Reset();
        for (int i = 0; i <= 200; i++) t.Update(i / 200.0, i * 0.30, 1);
        t.Update(0.0, 0.0, 2);
        r = t.Update(0.50, 31.0, 2);
        fail += Expect(r.HasValue && r.Seconds > 0.5 && r.Seconds < 1.5, "slower lap is positive");

        // A backwards jump clears the comparison until the lap restarts.
        t.Update(0.60, 37.0, 2);
        t.Update(0.10, 6.0, 2);
        r = t.Update(0.12, 7.0, 2);
        fail += Expect(!r.HasValue, "backwards jump clears delta");

        // A lap counter that increments before the position wraps must not
        // record the old position as the start of the new lap.
        t.Reset();
        for (int i = 0; i <= 200; i++) t.Update(i / 200.0, i * 0.30, 1);
        t.Update(0.99, 59.70, 1);
        t.Update(0.995, 0.10, 2);
        fail += Expect(!t.Update(0.997, 0.20, 2).HasValue, "pre-wrap position ignored");
        r = t.Update(0.001, 0.30, 2);
        fail += Expect(r.HasValue, "delta resumes after wrap");

        // Missing SimHub lap time is ignored instead of throwing.
        var data = new GameReaderCommon.StatusDataBase
        {
            CurrentLap = 1,
            CurrentLapTime = null,
            TrackPositionPercent = 0.2
        };
        fail += Expect(!t.Update(data).HasValue, "null lap time ignored");

        return fail;
    }

    private static int Expect(bool ok, string what)
    {
        if (ok) return 0;
        Console.WriteLine("FAIL: " + what);
        return 1;
    }
}
