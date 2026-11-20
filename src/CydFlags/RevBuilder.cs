using System;
using System.Globalization;
using System.Text;
using GameReaderCommon;

namespace CydFlags
{
    /// <summary>
    /// Builds the rev-limiter / tachometer page payload:
    ///
    ///     gear~speed_kmh~rpm~max_rpm~redline~rpm_percent~blink
    ///
    /// All numeric fields are integers in invariant culture. A field that is
    /// not available is "-" (the gear falls back to "-" as well). blink is "1"
    /// when the engine is at/over the redline - either the live RPM reached
    /// Redline, or the game set CarSettings_RPMRedLineReached - otherwise "0".
    ///
    /// rpm_percent is RPM as a percentage of MaxRpm. Some games only expose a
    /// redline instead of a maximum; in that case the percentage is relative to
    /// Redline, and the firmware still shows a useful bar. The value is clamped
    /// to 0..100 so the bar cannot overflow. The page is only offered when at
    /// least the gear, the speed or the RPM is known.
    /// </summary>
    internal static class RevBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Build(StatusDataBase data)
        {
            if (data == null) return string.Empty;

            double speed = Finite(data.SpeedKmh) ? data.SpeedKmh : double.NaN;
            double rpm = Finite(data.Rpms) ? data.Rpms : double.NaN;

            double maxRpm = Finite(data.MaxRpm) && data.MaxRpm > 0 ? data.MaxRpm : 0;
            double redline = Finite(data.Redline) && data.Redline > 0 ? data.Redline : 0;
            if (redline <= 0 && Finite(data.CarSettings_RedLineRPM) && data.CarSettings_RedLineRPM > 0)
                redline = data.CarSettings_RedLineRPM;
            if (redline <= 0) redline = maxRpm;

            double percent = double.NaN;
            if (rpm > 0)
            {
                if (maxRpm > 0) percent = rpm / maxRpm * 100.0;
                else if (redline > 0) percent = rpm / redline * 100.0;
            }
            if (Finite(percent)) percent = Math.Max(0.0, Math.Min(100.0, percent));

            bool blink = false;
            if (rpm > 0 && redline > 0 && rpm >= redline - 0.5) blink = true;
            if (Finite(data.CarSettings_RPMRedLineReached) && data.CarSettings_RPMRedLineReached > 0)
                blink = true;

            string gear = CleanGear(data.Gear);

            // Do not offer an all-empty page: the firmware uses a non-empty
            // payload to decide whether REV is available at all.
            if (gear == "-" && !Finite(speed) && !Finite(rpm)) return string.Empty;

            return string.Join("~", new[]
            {
                gear,
                Int(speed),
                Int(rpm),
                Int(maxRpm),
                Int(redline),
                Int(percent),
                blink ? "1" : "0"
            });
        }

        private static string CleanGear(string gear)
        {
            if (string.IsNullOrEmpty(gear)) return "-";

            var sb = new StringBuilder(2);
            foreach (char c in gear.Trim())
            {
                char upper = char.ToUpperInvariant(c);
                if ((upper >= '0' && upper <= '9') || upper == 'N' || upper == 'R'
                    || upper == '-' || upper == '+')
                {
                    sb.Append(upper);
                    if (sb.Length == 2) break;
                }
            }
            return sb.Length == 0 ? "-" : sb.ToString();
        }

        private static string Int(double value)
        {
            if (!Finite(value)) return "-";
            return ((long)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(Inv);
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
