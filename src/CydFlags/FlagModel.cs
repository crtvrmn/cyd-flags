using System;

namespace CydFlags
{
    /// <summary>Flags the display knows how to render.</summary>
    public enum FlagKind
    {
        None = 0,
        Green,
        Yellow,
        Blue,
        White,
        Checkered,
        Red,
        Black,
        Orange
    }

    /// <summary>
    /// The raw boolean flag properties SimHub exposes on StatusDataBase.
    /// A plain struct keeps the priority logic testable without SimHub.
    /// </summary>
    public struct FlagBits
    {
        public bool Green;
        public bool Yellow;
        public bool Blue;
        public bool White;
        public bool Checkered;
        public bool Red;
        public bool Black;
        public bool Orange;
    }

    public static class FlagModel
    {
        /// <summary>
        /// Collapse SimHub's independent booleans into the one flag that matters
        /// most to the driver. Games occasionally report more than one flag at
        /// once (for example yellow + blue, or checkered + orange at race end).
        /// Priority, highest first: red, black, checkered, orange, yellow, blue,
        /// white, green.
        /// </summary>
        public static FlagKind Select(FlagBits flags)
        {
            if (flags.Red) return FlagKind.Red;
            if (flags.Black) return FlagKind.Black;
            if (flags.Checkered) return FlagKind.Checkered;
            if (flags.Orange) return FlagKind.Orange;
            if (flags.Yellow) return FlagKind.Yellow;
            if (flags.Blue) return FlagKind.Blue;
            if (flags.White) return FlagKind.White;
            if (flags.Green) return FlagKind.Green;
            return FlagKind.None;
        }

        /// <summary>
        /// Last-resort parser for SimHub's Flag_Name string. It is only used when
        /// none of the boolean properties are set, which happens on some older
        /// game plugins.
        /// </summary>
        public static FlagKind ParseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return FlagKind.None;

            string n = name.ToLowerInvariant();

            // Checkered is spelled several ways, and some games call it
            // "black and white".
            if (n.Contains("checker") || n.Contains("chequer") || n.Contains("finish")
                || n.Contains("black and white") || n.Contains("black & white"))
                return FlagKind.Checkered;

            if (n.Contains("red")) return FlagKind.Red;
            if (n.Contains("black")) return FlagKind.Black;
            if (n.Contains("orange") || n.Contains("meatball")) return FlagKind.Orange;
            if (n.Contains("yellow")) return FlagKind.Yellow;
            if (n.Contains("blue")) return FlagKind.Blue;
            if (n.Contains("white")) return FlagKind.White;
            if (n.Contains("green")) return FlagKind.Green;

            if (n.Contains("none") || n.Contains("clear") || n.Contains("no flag"))
                return FlagKind.None;

            return FlagKind.None;
        }

        /// <summary>The option string declared by the ESPHome select entity.</summary>
        public static string ToOption(FlagKind kind)
        {
            switch (kind)
            {
                case FlagKind.Green: return "GREEN";
                case FlagKind.Yellow: return "YELLOW";
                case FlagKind.Blue: return "BLUE";
                case FlagKind.White: return "WHITE";
                case FlagKind.Checkered: return "CHECKERED";
                case FlagKind.Red: return "RED";
                case FlagKind.Black: return "BLACK";
                case FlagKind.Orange: return "ORANGE";
                default: return "NONE";
            }
        }

        /// <summary>Human readable name, used in the SimHub settings panel.</summary>
        public static string Describe(FlagKind kind)
        {
            switch (kind)
            {
                case FlagKind.Green: return "Green";
                case FlagKind.Yellow: return "Yellow";
                case FlagKind.Blue: return "Blue";
                case FlagKind.White: return "White";
                case FlagKind.Checkered: return "Checkered";
                case FlagKind.Red: return "Red";
                case FlagKind.Black: return "Black";
                case FlagKind.Orange: return "Orange";
                default: return "No flag";
            }
        }
    }
}
