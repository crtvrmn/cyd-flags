using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using GameReaderCommon;

namespace CydFlags
{
    /// <summary>
    /// Builds the payload for the third display page: the player's current lap,
    /// live delta to the personal best, and the three sector times.
    ///
    ///     lap~lapcolor~delta~deltacolor~s1~s1color~s2~s2color~s3~s3color
    ///
    /// Sector colors compare the current lap's sector with the player's best
    /// lap sector: green = faster, red = slower, '-' = not available yet.
    /// </summary>
    internal static class RaceLogicBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly PropertyInfo SectorDataProperty =
            typeof(SectorTimes).GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo GetSectorTimeMethod =
            typeof(SectorTimes).GetMethod("GetSectorTime", new[] { typeof(int) });

        public static string Build(StatusDataBase data)
        {
            if (data == null) return string.Empty;

            Opponent player = FindPlayer(data.Opponents);
            SectorTimes current = player != null ? player.CurrentLapSectorTimes : null;
            SectorTimes best = player != null ? player.BestLapSectorTimes : null;

            double lap = Seconds(data.CurrentLapTime);
            double bestLap = Seconds(data.BestLapTime);

            double s1 = SectorSeconds(current, 0);
            double s2 = SectorSeconds(current, 1);
            double s3 = SectorSeconds(current, 2);

            // Some games fill StatusDataBase but not the player opponent's
            // sector array. At least the first two current sectors are exposed
            // there, so use them as a fallback instead of showing nothing.
            if (s1 <= 0) s1 = Seconds(data.Sector1Time);
            if (s2 <= 0) s2 = Seconds(data.Sector2Time);
            // Newer SimHub builds expose Sector3Time; the SDK this plugin is
            // built against does not, so read it via reflection.
            if (s3 <= 0) s3 = PropertySeconds(data, "Sector3Time");
            // SimHub does not expose a live S3 value in most versions: the
            // current-lap sector array only contains completed sectors. While
            // S3 is running, the lap time is larger than S1 + S2, and the
            // remainder is exactly the live S3. In S1/S2 the condition is
            // false (S2 is still running, so lap == S1 + S2).
            if (s3 <= 0 && s1 > 0.5 && s2 > 0.5 && lap > s1 + s2 + 0.05)
                s3 = lap - s1 - s2;

            double b1 = SectorSeconds(best, 0);
            double b2 = SectorSeconds(best, 1);
            double b3 = SectorSeconds(best, 2);
            if (b1 <= 0) b1 = Seconds(data.Sector1BestLapTime);
            if (b2 <= 0) b2 = Seconds(data.Sector2BestLapTime);
            if (b3 <= 0) b3 = Seconds(data.Sector3BestLapTime);

            double delta;
            string deltaColor = "-";
            if (!TryDelta(player, data, lap, bestLap, out delta))
            {
                delta = 0;
            }
            else
            {
                if (delta < -0.0005) deltaColor = "G";
                else if (delta > 0.0005) deltaColor = "R";
            }

            return string.Join("~", new[]
            {
                FormatLap(lap),
                "-",
                deltaColor == "-" ? "--.---" : FormatDelta(delta),
                deltaColor,
                FormatSector(s1),
                SectorColor(s1, b1),
                FormatSector(s2),
                SectorColor(s2, b2),
                FormatSector(s3),
                SectorColor(s3, b3)
            });
        }

        private static double PropertySeconds(object target, string propertyName)
        {
            try
            {
                PropertyInfo property = target.GetType().GetProperty(
                    propertyName, BindingFlags.Public | BindingFlags.Instance);
                return property == null ? 0 : Seconds(property.GetValue(target, null));
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool TryDelta(Opponent player, StatusDataBase data, double lap,
            double bestLap, out double delta)
        {
            delta = 0;

            // SimHub computes this live for the player; it is much better than
            // subtracting two absolute lap times at the start of a lap.
            if (player != null && TrySeconds(player.DeltaToBest, out delta) && IsFinite(delta))
                return true;

            if (TrySeconds(data.DeltaToSessionBest, out delta) && IsFinite(delta))
                return true;

            // Last-resort fallback for games that expose neither live delta.
            // Do not show it in the first seconds of a lap, where lap - best
            // would always look absurdly negative.
            if (lap >= 10 && bestLap > 0)
            {
                delta = lap - bestLap;
                return true;
            }

            delta = 0;
            return false;
        }

        private static Opponent FindPlayer(object opponents)
        {
            IEnumerable items = opponents as IEnumerable;
            if (items == null) return null;

            foreach (object item in items)
            {
                Opponent opponent = item as Opponent;
                if (opponent != null && opponent.IsPlayer) return opponent;
            }
            return null;
        }

        private static double SectorSeconds(SectorTimes times, int index)
        {
            if (times == null) return 0;

            try
            {
                if (SectorDataProperty != null)
                {
                    Array values = SectorDataProperty.GetValue(times, null) as Array;
                    if (values != null && index >= 0 && index < values.Length)
                        return Seconds(values.GetValue(index));
                }

                // Older SimHub versions may not expose Data publicly; the
                // GetSectorTime helper takes a 1-based sector number.
                if (GetSectorTimeMethod != null)
                {
                    object value = GetSectorTimeMethod.Invoke(times, new object[] { index + 1 });
                    return Seconds(value);
                }
            }
            catch (Exception)
            {
                // Reflection against a game-specific SectorTimes implementation
                // must never break the display.
            }

            return 0;
        }

        private static string SectorColor(double current, double best)
        {
            if (current <= 0 || best <= 0) return "-";
            if (current < best - 0.0005) return "G";
            if (current > best + 0.0005) return "R";
            return "-";
        }

        private static string FormatLap(double seconds)
        {
            if (seconds <= 0) return "--:--.---";
            int minutes = (int)(seconds / 60.0);
            double rest = seconds - minutes * 60.0;
            return minutes.ToString(Inv) + ":" + rest.ToString("00.000", Inv);
        }

        private static string FormatSector(double seconds)
        {
            if (seconds <= 0) return "--.---";
            return seconds.ToString("00.000", Inv);
        }

        private static string FormatDelta(double seconds)
        {
            if (seconds < 0) return "-" + Math.Abs(seconds).ToString("0.000", Inv);
            return "+" + seconds.ToString("0.000", Inv);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Seconds(object value)
        {
            double seconds;
            return TrySeconds(value, out seconds) && IsFinite(seconds) ? seconds : 0;
        }

        private static bool TrySeconds(object value, out double seconds)
        {
            seconds = 0;
            if (value == null) return false;

            if (value is TimeSpan)
            {
                seconds = ((TimeSpan)value).TotalSeconds;
                return true;
            }

            if (value is double || value is float || value is int || value is long || value is decimal)
            {
                seconds = Convert.ToDouble(value, Inv);
                return true;
            }

            string text = value.ToString();
            return double.TryParse(text, NumberStyles.Float, Inv, out seconds);
        }
    }
}
