using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameReaderCommon;

namespace CydFlags
{
    /// <summary>
    /// Turns SimHub's relative-opponent data into the tiny CSV-ish payload the
    /// CYD firmware understands:
    ///
    ///     position~name~gap~gapcolor~lap~lapcolor;...
    ///
    /// Row 1 is the car ahead, row 2 the player, row 3 the car behind. Values
    /// are pre-formatted so the ESP32 does not need any floating point or time
    /// formatting code. Color codes: '-' none, 'B' blue/neutral, 'G' green, 'R' red, 'P' purple.
    /// The player row has an empty gap field (0.00 is meaningless for yourself).
    /// </summary>
    internal static class TimingBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly object TrendLock = new object();
        private static readonly System.Collections.Generic.Dictionary<string, double> LastGapByCar =
            new System.Collections.Generic.Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly System.Collections.Generic.Dictionary<string, string> LastTrendByCar =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Forget gap history when a race ends or the session changes.</summary>
        public static void ResetTrend()
        {
            lock (TrendLock)
            {
                LastGapByCar.Clear();
                LastTrendByCar.Clear();
            }
        }

        public static bool IsRaceSession(StatusDataBase data)
        {
            if (data == null) return false;
            string session = data.SessionTypeName;
            if (string.IsNullOrEmpty(session)) return false;

            // SimHub reports e.g. "Race", "Practice", "Qualifying", "Warmup",
            // "Hotlap" and "Time Attack". Only a race session gets the tower.
            if (session.IndexOf("race", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (session.IndexOf("practice", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (session.IndexOf("qualif", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (session.IndexOf("warmup", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (session.IndexOf("hotlap", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (session.IndexOf("time attack", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        public static string Build(StatusDataBase data, bool bestLapMode)
        {
            if (data == null) return string.Empty;

            Opponent ahead;
            Opponent behind;
            FindNeighbours(data, out ahead, out behind);

            string aheadColor = ahead != null ? GapColor(ahead, true) : "-";
            string behindColor = behind != null ? GapColor(behind, false) : "-";
            return BuildRows(data, ahead, behind, aheadColor, behindColor, bestLapMode);
        }

        /// <summary>
        /// Build the LAST and BEST variants from one frame. The gap trend is a
        /// rate of change, so it is updated once and both variants share the
        /// resulting colours; calling <see cref="Build"/> twice would advance
        /// the history twice and rob the second payload of its trend colour.
        /// </summary>
        public static void BuildBoth(StatusDataBase data, out string lastPayload, out string bestPayload)
        {
            lastPayload = string.Empty;
            bestPayload = string.Empty;
            if (data == null) return;

            Opponent ahead;
            Opponent behind;
            FindNeighbours(data, out ahead, out behind);

            string aheadColor = ahead != null ? GapColor(ahead, true) : "-";
            string behindColor = behind != null ? GapColor(behind, false) : "-";
            lastPayload = BuildRows(data, ahead, behind, aheadColor, behindColor, false);
            bestPayload = BuildRows(data, ahead, behind, aheadColor, behindColor, true);
        }

        private static void FindNeighbours(StatusDataBase data, out Opponent ahead, out Opponent behind)
        {
            ahead = null;
            behind = null;

            // Prefer the overall position. In a two-car race the ghost can
            // appear in BOTH on-track lists (or the "ahead" list may contain
            // the car behind), which made the same driver appear twice. The
            // position relative to the player is unambiguous.
            int playerPosition = data.Position;
            if (playerPosition > 0)
            {
                foreach (Opponent opponent in Enumerate(data.Opponents))
                {
                    if (opponent == null || opponent.IsPlayer || opponent.Position <= 0) continue;
                    if (opponent.Position < playerPosition)
                    {
                        if (ahead == null || opponent.Position > ahead.Position) ahead = opponent;
                    }
                    else if (opponent.Position > playerPosition)
                    {
                        if (behind == null || opponent.Position < behind.Position) behind = opponent;
                    }
                }
            }

            // Some games do not fill Position on every opponent; use the
            // on-track lists only to fill the missing side.
            if (ahead == null)
                ahead = First(data.OpponentsAheadOnTrack);
            if (behind == null)
                behind = First(data.OpponentsBehindOnTrack);

            // Never show the same car in both rows.
            if (ahead != null && behind != null && SameCar(ahead, behind))
            {
                if (behind.Position > playerPosition && behind.Position > 0)
                    ahead = null;
                else
                    behind = null;
            }

            if (ahead == null && behind == null) return;

            // A ghost that is listed as ahead while its position says behind
            // is still the car behind; keep the rows consistent.
            if (ahead != null && behind == null && playerPosition > 0 &&
                ahead.Position > playerPosition)
            {
                behind = ahead;
                ahead = null;
            }
            else if (behind != null && ahead == null && playerPosition > 0 &&
                     behind.Position < playerPosition)
            {
                ahead = behind;
                behind = null;
            }
        }

        private static bool SameCar(Opponent first, Opponent second)
        {
            if (ReferenceEquals(first, second)) return true;
            return first.Position == second.Position &&
                   string.Equals(Clean(first.Name), Clean(second.Name), StringComparison.Ordinal);
        }

        private static string BuildRows(StatusDataBase data, Opponent ahead, Opponent behind,
            string aheadColor, string behindColor, bool bestLapMode)
        {
            double playerLap = LapSeconds(data, bestLapMode);
            double best = BestLap(data, playerLap, bestLapMode);

            string payload = Row(ahead, true, best, playerLap, bestLapMode, aheadColor)
                + ";" + PlayerRow(data, playerLap, best)
                + ";" + Row(behind, false, best, playerLap, bestLapMode, behindColor);
            if (payload.Length > 250) payload = payload.Substring(0, 250);
            return payload;
        }

        private static Opponent First(object list)
        {
            IEnumerable items = list as IEnumerable;
            if (items == null) return null;

            foreach (object item in items)
            {
                Opponent opponent = item as Opponent;
                if (opponent != null && !opponent.IsPlayer) return opponent;
            }
            return null;
        }

        private static Opponent AtPosition(object list, int position)
        {
            IEnumerable items = list as IEnumerable;
            if (items == null) return null;

            foreach (object item in items)
            {
                Opponent opponent = item as Opponent;
                if (opponent == null || opponent.IsPlayer) continue;
                if (opponent.Position == position) return opponent;
            }
            return null;
        }

        private static string GapColor(Opponent opponent, bool isAhead)
        {
            object gap = opponent.RelativeGapToPlayer;
            double gapSeconds;
            if (!TrySeconds(gap, out gapSeconds) || Math.Abs(gapSeconds) < 0.0005)
                gap = opponent.GaptoPlayer;

            if (!TrySeconds(gap, out gapSeconds)) return "-";
            return GapCode(opponent, Math.Abs(gapSeconds), isAhead);
        }

        private static string Row(Opponent opponent, bool isAhead, double bestLap,
            double playerLap, bool bestLapMode, string gapColor)
        {
            if (opponent == null)
                return "0~---~~-~--:--.---~-";

            object gap = opponent.RelativeGapToPlayer;
            double gapSeconds;
            if (!TrySeconds(gap, out gapSeconds) || Math.Abs(gapSeconds) < 0.0005)
                gap = opponent.GaptoPlayer;

            bool gapValid = TrySeconds(gap, out gapSeconds);
            if (gapValid) gapSeconds = Math.Abs(gapSeconds);

            double lap = LapSeconds(opponent, bestLapMode);
            return string.Join("~", new[]
            {
                opponent.Position.ToString(Inv),
                Clean(opponent.Name),
                gapValid ? GapSymbol(gapColor) : string.Empty,
                gapValid ? gapColor : "-",
                FormatLap(lap),
                TimeCode(lap, bestLap, playerLap, false)
            });
        }

        private static string PlayerRow(StatusDataBase data, double playerLap, double bestLap)
        {
            string name = Clean(data.PlayerName);
            if (name == "---") name = "YOU";

            return string.Join("~", new[]
            {
                data.Position > 0 ? data.Position.ToString(Inv) : "?",
                name,
                string.Empty, // gap to yourself: no meaningless 0.00
                "-",
                FormatLap(playerLap),
                TimeCode(playerLap, bestLap, 0, true)
            });
        }

        private static string GapSymbol(string color)
        {
            if (color == "G") return "+";
            if (color == "R") return "-";
            return string.Empty;
        }

        private static string GapCode(Opponent opponent, double gapSeconds, bool isAhead)
        {
            if (gapSeconds <= 0) return "-";

            string key = (isAhead ? "A|" : "B|") + opponent.Position.ToString(Inv) + "|" + Clean(opponent.Name);
            double previous;
            bool known;
            string previousTrend;
            lock (TrendLock)
            {
                if (LastGapByCar.Count > 200) LastGapByCar.Clear();
                known = LastGapByCar.TryGetValue(key, out previous);
                LastGapByCar[key] = gapSeconds;
                if (!LastTrendByCar.TryGetValue(key, out previousTrend)) previousTrend = "B";
            }

            // Blue is the neutral state. Once the gap is closing/opening the
            // green/red disc stays visible until the direction actually
            // changes; a steady gap must not make the disc disappear.
            if (!known) return "B";

            bool closing = gapSeconds < previous - 0.02;
            bool opening = gapSeconds > previous + 0.02;
            string trend = previousTrend;
            if (closing)
                trend = isAhead ? "G" : "R";
            else if (opening)
                trend = isAhead ? "R" : "G";

            lock (TrendLock)
            {
                if (LastTrendByCar.Count > 200) LastTrendByCar.Clear();
                LastTrendByCar[key] = trend;
            }
            return trend;
        }

        private static string TimeCode(double value, double best, double player, bool isPlayer)
        {
            if (value <= 0) return "-";
            if (best > 0 && Math.Abs(value - best) <= 0.0005) return "P";
            if (!isPlayer && player > 0)
            {
                if (value < player - 0.0005) return "R";
                if (value > player + 0.0005) return "G";
            }
            return "-";
        }

        private static double LapSeconds(StatusDataBase data, bool bestLapMode)
        {
            return SecondsOf(bestLapMode ? data.BestLapTime : data.LastLapTime);
        }

        private static double LapSeconds(Opponent opponent, bool bestLapMode)
        {
            return SecondsOf(bestLapMode ? opponent.BestLapTime : opponent.LastLapTime);
        }

        private static double BestLap(StatusDataBase data, double playerLap, bool bestLapMode)
        {
            double best = playerLap;
            foreach (Opponent opponent in Enumerate(data.Opponents))
            {
                if (opponent == null || opponent.IsPlayer) continue;
                double value = LapSeconds(opponent, bestLapMode);
                if (value > 0 && (best <= 0 || value < best)) best = value;
            }
            return best;
        }

        private static IEnumerable<Opponent> Enumerate(object list)
        {
            IEnumerable items = list as IEnumerable;
            if (items == null) yield break;

            foreach (object item in items)
            {
                Opponent opponent = item as Opponent;
                if (opponent != null) yield return opponent;
            }
        }

        private static double SecondsOf(object value)
        {
            double seconds;
            return TrySeconds(value, out seconds) && seconds > 0 ? seconds : 0;
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return "---";

            // The CYD font is ASCII-only. Fold accents (René -> Rene) and
            // replace anything that still cannot be drawn, so ESPHome does not
            // log "Codepoint ... not found in font" for every redraw.
            value = FoldToAscii(value);

            var chars = new char[value.Length];
            int n = 0;
            foreach (char c in value.Trim())
            {
                if (c == '~' || c == ';') continue;
                if (!IsFontChar(c)) continue;
                chars[n++] = c;
            }

            string result = new string(chars, 0, n).Trim();
            if (result.Length == 0) return "---";
            if (result.Length > 11) result = result.Substring(0, 12);
            return result;
        }

        private static bool IsFontChar(char c)
        {
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= 'a' && c <= 'z') return true;
            if (c >= '0' && c <= '9') return true;
            const string punctuation = " .,:;!?()[]&+/-_'~";
            return punctuation.IndexOf(c) >= 0;
        }

        private static string FoldToAscii(string value)
        {
            // German umlauts are expanded before Unicode decomposition, so
            // "Müller" becomes "Mueller" and "MÜLLER" becomes "MUELLER".
            var expanded = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case 'ä': expanded.Append("ae"); break;
                    case 'ö': expanded.Append("oe"); break;
                    case 'ü': expanded.Append("ue"); break;
                    case 'Ä': expanded.Append("AE"); break;
                    case 'Ö': expanded.Append("OE"); break;
                    case 'Ü': expanded.Append("UE"); break;
                    case 'ß': expanded.Append("ss"); break;
                    case 'ẞ': expanded.Append("SS"); break;
                    default: expanded.Append(c); break;
                }
            }

            string decomposed = expanded.ToString().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                switch (c)
                {
                    case 'ß': sb.Append("ss"); break;
                    case 'ø': case 'Ø': sb.Append(c == 'ø' ? "o" : "O"); break;
                    case 'ł': case 'Ł': sb.Append(c == 'ł' ? "l" : "L"); break;
                    case 'đ': case 'Đ': sb.Append(c == 'đ' ? "d" : "D"); break;
                    case 'æ': sb.Append("ae"); break;
                    case 'Æ': sb.Append("AE"); break;
                    case 'œ': sb.Append("oe"); break;
                    case 'Œ': sb.Append("OE"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string FormatGap(object value)
        {
            double seconds;
            if (!TrySeconds(value, out seconds)) return "---";
            return Math.Abs(seconds).ToString("0.00", Inv);
        }

        private static string FormatLap(double seconds)
        {
            if (seconds <= 0) return "--:--.---";

            int minutes = (int)(seconds / 60.0);
            double rest = seconds - minutes * 60.0;
            return minutes.ToString(Inv) + ":" + rest.ToString("00.000", Inv);
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
