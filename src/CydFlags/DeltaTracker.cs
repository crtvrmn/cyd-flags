using System;
using System.Collections.Generic;
using GameReaderCommon;

namespace CydFlags
{
    /// <summary>Result of one delta sample. Positive seconds mean slower than
    /// the previous lap at the same track position, negative means faster.</summary>
    internal struct DeltaResult
    {
        public readonly bool HasValue;
        public readonly double Seconds;

        public DeltaResult(bool hasValue, double seconds)
        {
            HasValue = hasValue;
            Seconds = seconds;
        }

        public static DeltaResult None
        {
            get { return new DeltaResult(false, 0.0); }
        }

        public static DeltaResult Of(double seconds)
        {
            return new DeltaResult(true, seconds);
        }
    }

    /// <summary>
    /// Builds a live delta to the previous lap from SimHub's normalized track
    /// position and current lap time. SimHub only exposes a delta to the
    /// session best, so this class records where on track the current lap time
    /// reached and compares it with the same position on the previous lap.
    ///
    /// The numeric Update overload is deliberately independent of SimHub and
    /// can be exercised with plain values. The StatusDataBase overload is the
    /// adapter used by the plugin.
    /// </summary>
    internal sealed class DeltaTracker
    {
        // Add a sample when the car moved at least 0.5 % of the lap or when
        // 0.2 s passed (the latter keeps pit stops / standing time covered).
        private const double MinPositionStep = 0.005;
        private const double MinTimeStep = 0.2;

        // A position jump of more than 5 % backwards is treated as a teleport
        // (reset, session change, garage) instead of normal track movement.
        private const double BackwardsJump = 0.05;

        // Two samples are enough for interpolation, but a lap that only covers
        // a small part of the track is not a useful reference.
        private const int MinSamplesForPreviousLap = 5;
        private const double FullLapStart = 0.02;
        private const double FullLapEnd = 0.90;

        private const int MaxSamples = 1024;
        private const double Epsilon = 1e-9;

        private struct Sample
        {
            public readonly double Position;
            public readonly double Time;

            public Sample(double position, double time)
            {
                Position = position;
                Time = time;
            }
        }

        private List<Sample> _current = new List<Sample>();
        private List<Sample> _previous;

        private int _lap = int.MinValue;
        private bool _haveLap;
        // Some games bump CurrentLap a frame before TrackPositionPercent wraps
        // from ~1.0 back to ~0.0. Wait for the position to come back to the
        // start of the lap before recording samples, otherwise the new lap
        // would begin with a bogus sample at the end of the track.
        private bool _waitForLapStart;

        // Last position/time actually observed (not necessarily sampled), used
        // for backwards-jump and same-lap reset detection.
        private double _lastObservedPosition = double.NaN;
        private double _lastObservedTime;

        // Last sample in _current, used for the sampling thresholds.
        private double _lastSamplePosition = double.NaN;
        private double _lastSampleTime;

        /// <summary>Read the three SimHub values and update the tracker.</summary>
        public DeltaResult Update(StatusDataBase data)
        {
            if (data == null) return DeltaResult.None;

            TimeSpan? lapTime = data.CurrentLapTime;
            if (!lapTime.HasValue) return DeltaResult.None;

            return Update(data.TrackPositionPercent, lapTime.Value.TotalSeconds, data.CurrentLap);
        }

        /// <summary>
        /// Pure update path. Position is 0..1 around the lap, lapTimeSeconds is
        /// the current lap's elapsed time and currentLap is SimHub's lap
        /// counter (it only needs to increase when a new lap starts).
        /// </summary>
        public DeltaResult Update(double positionPercent, double lapTimeSeconds, int currentLap)
        {
            double position = positionPercent;
            if (double.IsNaN(position) || double.IsInfinity(position)
                || position < 0.0 || position > 1.0)
                return DeltaResult.None;

            double time = lapTimeSeconds;
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0.0)
                return DeltaResult.None;

            if (!_haveLap)
            {
                StartSessionLap(currentLap, position, time);
                return DeltaResult.None;
            }

            if (currentLap < _lap)
            {
                // Lap counter went backwards: session restart / new race.
                StartSessionLap(currentLap, position, time);
                return DeltaResult.None;
            }

            if (currentLap > _lap)
            {
                PromoteCurrentLap();
                StartNewLap(currentLap);
                return DeltaResult.None;
            }

            if (_waitForLapStart)
            {
                // More than half a lap cannot be the start of a new lap;
                // this is the old position seen before the wrap.
                if (position > 0.5) return DeltaResult.None;
                _waitForLapStart = false;
            }

            // Same lap but the lap timer jumped backwards by more than a
            // second: treat it as a session reset that kept the lap number.
            if (_lastObservedTime > 1.0 && time < _lastObservedTime - 1.0)
            {
                StartSessionLap(currentLap, position, time);
                return DeltaResult.None;
            }

            // Teleport / spin across the line / garage reset.
            if (!double.IsNaN(_lastObservedPosition)
                && position < _lastObservedPosition - BackwardsJump)
            {
                StartSessionLap(currentLap, position, time);
                return DeltaResult.None;
            }

            if (double.IsNaN(_lastObservedPosition) || position > _lastObservedPosition)
                _lastObservedPosition = position;
            _lastObservedTime = time;

            AddSample(position, time, false);

            return ComputeDelta(position, time);
        }

        /// <summary>Forget both laps (game stop, leaving a race session, ...).</summary>
        public void Reset()
        {
            _current.Clear();
            _previous = null;
            _lap = int.MinValue;
            _haveLap = false;
            _waitForLapStart = false;
            _lastObservedPosition = double.NaN;
            _lastObservedTime = 0.0;
            _lastSamplePosition = double.NaN;
            _lastSampleTime = 0.0;
        }

        private void StartSessionLap(int lap, double position, double time)
        {
            Reset();

            _lap = lap;
            _haveLap = true;
            _lastObservedPosition = position;
            _lastObservedTime = time;

            SeedLap(position, time);
        }

        private void StartNewLap(int lap)
        {
            _lap = lap;
            _current.Clear();
            _lastSamplePosition = 0.0;
            _lastSampleTime = 0.0;
            _waitForLapStart = true;
            // Do not trust the position/time of the update that reported the
            // new lap: some games increment CurrentLap one frame before
            // TrackPositionPercent wraps from ~1.0 back to ~0.0.
            _current.Add(new Sample(0.0, 0.0));
            _lastObservedPosition = 0.0;
            _lastObservedTime = 0.0;
        }

        private void SeedLap(double position, double time)
        {
            _current.Clear();
            _lastSamplePosition = double.NaN;
            _lastSampleTime = 0.0;

            if (position <= 0.05 && time <= 5.0)
            {
                // Lap starts near the line: pin the origin to (0, 0) so the
                // first real sample does not fake a delta of a few tenths.
                _current.Add(new Sample(0.0, 0.0));
                _lastSamplePosition = 0.0;
                _lastSampleTime = 0.0;
                if (position > 0.0 || time > 0.0)
                    AddSample(position, time, true);
            }
            else
            {
                // Joined mid-lap (session reset, garage). Keep the real values;
                // this lap is not used as a previous-lap reference unless it
                // still covers a full lap.
                _current.Add(new Sample(position, time));
                _lastSamplePosition = position;
                _lastSampleTime = time;
            }
        }

        private void PromoteCurrentLap()
        {
            if (_current.Count >= MinSamplesForPreviousLap
                && _current[0].Position <= FullLapStart
                && _current[_current.Count - 1].Position >= FullLapEnd)
            {
                _previous = _current;
            }
            else
            {
                _previous = null;
            }

            _current = new List<Sample>();
        }

        private void AddSample(double position, double time, bool force)
        {
            if (_current.Count == 0) return;
            if (time < _lastSampleTime) return;
            if (position < _lastSamplePosition) return;

            double positionStep = position - _lastSamplePosition;
            double timeStep = time - _lastSampleTime;
            bool due = positionStep >= MinPositionStep || timeStep >= MinTimeStep;
            if (!force && !due) return;

            if (_current.Count >= MaxSamples) Decimate();

            _current.Add(new Sample(position, time));
            _lastSamplePosition = position;
            _lastSampleTime = time;
        }

        private void Decimate()
        {
            var reduced = new List<Sample>((_current.Count / 2) + 1);
            for (int i = 0; i < _current.Count; i += 2)
                reduced.Add(_current[i]);

            Sample tail = _current[_current.Count - 1];
            if (reduced.Count == 0 || reduced[reduced.Count - 1].Position < tail.Position)
                reduced.Add(tail);

            _current = reduced;
            tail = _current[_current.Count - 1];
            _lastSamplePosition = tail.Position;
            _lastSampleTime = tail.Time;
        }

        private DeltaResult ComputeDelta(double position, double time)
        {
            if (_previous == null || _previous.Count < 2) return DeltaResult.None;

            int count = _previous.Count;
            double previousTime;

            if (position <= _previous[0].Position)
            {
                previousTime = _previous[0].Time;
            }
            else if (position > _previous[count - 1].Position + Epsilon)
            {
                // The previous lap has no sample for this part of the track
                // (incomplete data); do not invent one by extrapolating.
                return DeltaResult.None;
            }
            else
            {
                int lo = 1;
                int hi = count - 1;
                while (lo < hi)
                {
                    int mid = lo + ((hi - lo) / 2);
                    if (_previous[mid].Position < position) lo = mid + 1;
                    else hi = mid;
                }

                Sample a = _previous[lo - 1];
                Sample b = _previous[lo];
                double span = b.Position - a.Position;
                if (span <= Epsilon)
                {
                    previousTime = b.Time;
                }
                else
                {
                    double fraction = (position - a.Position) / span;
                    if (fraction < 0.0) fraction = 0.0;
                    if (fraction > 1.0) fraction = 1.0;
                    previousTime = a.Time + ((b.Time - a.Time) * fraction);
                }
            }

            double delta = time - previousTime;
            if (double.IsNaN(delta) || double.IsInfinity(delta)) return DeltaResult.None;
            return DeltaResult.Of(delta);
        }
    }
}
