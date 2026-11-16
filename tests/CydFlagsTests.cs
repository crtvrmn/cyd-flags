using System;
using CydFlags;

/// <summary>
/// Pure logic tests for the CYD Flags plugin. No SimHub assembly needed: this
/// only exercises FlagModel (priority selection, name parsing, option strings).
/// </summary>
internal static class CydFlagsTests
{
    public static int Run()
    {
        int fail = 0;

        // Every single flag on its own maps to itself.
        fail += Expect(FlagModel.Select(new FlagBits { Green = true }), FlagKind.Green, "green alone");
        fail += Expect(FlagModel.Select(new FlagBits { Yellow = true }), FlagKind.Yellow, "yellow alone");
        fail += Expect(FlagModel.Select(new FlagBits { Blue = true }), FlagKind.Blue, "blue alone");
        fail += Expect(FlagModel.Select(new FlagBits { White = true }), FlagKind.White, "white alone");
        fail += Expect(FlagModel.Select(new FlagBits { Checkered = true }), FlagKind.Checkered, "checkered alone");
        fail += Expect(FlagModel.Select(new FlagBits { Red = true }), FlagKind.Red, "red alone");
        fail += Expect(FlagModel.Select(new FlagBits { Black = true }), FlagKind.Black, "black alone");
        fail += Expect(FlagModel.Select(new FlagBits { Orange = true }), FlagKind.Orange, "orange alone");
        fail += Expect(FlagModel.Select(new FlagBits()), FlagKind.None, "no flag bits");

        // Mixed flags: the most important one wins.
        fail += Expect(FlagModel.Select(new FlagBits { Yellow = true, Blue = true }), FlagKind.Yellow, "yellow beats blue");
        fail += Expect(FlagModel.Select(new FlagBits { Checkered = true, Orange = true }), FlagKind.Checkered, "checkered beats orange");
        fail += Expect(FlagModel.Select(new FlagBits { Red = true, Yellow = true }), FlagKind.Red, "red beats yellow");
        fail += Expect(FlagModel.Select(new FlagBits { Black = true, Checkered = true }), FlagKind.Black, "black beats checkered");
        fail += Expect(FlagModel.Select(new FlagBits { White = true, Green = true }), FlagKind.White, "white beats green");
        fail += Expect(FlagModel.Select(new FlagBits { Green = true, Yellow = true }), FlagKind.Yellow, "yellow beats green");

        // SimHub Flag_Name fallback parsing.
        fail += Expect(FlagModel.ParseName("Yellow"), FlagKind.Yellow, "name Yellow");
        fail += Expect(FlagModel.ParseName("YELLOW FLAG"), FlagKind.Yellow, "name YELLOW FLAG");
        fail += Expect(FlagModel.ParseName("blue"), FlagKind.Blue, "name blue");
        fail += Expect(FlagModel.ParseName("Checkered"), FlagKind.Checkered, "name Checkered");
        fail += Expect(FlagModel.ParseName("chequered flag"), FlagKind.Checkered, "name chequered");
        fail += Expect(FlagModel.ParseName("Finish"), FlagKind.Checkered, "name Finish");
        fail += Expect(FlagModel.ParseName("black and white"), FlagKind.Checkered, "name black and white");
        fail += Expect(FlagModel.ParseName("Black"), FlagKind.Black, "name Black");
        fail += Expect(FlagModel.ParseName("black flag"), FlagKind.Black, "name black flag");
        fail += Expect(FlagModel.ParseName("Orange"), FlagKind.Orange, "name Orange");
        fail += Expect(FlagModel.ParseName("meatball"), FlagKind.Orange, "name meatball");
        fail += Expect(FlagModel.ParseName("Red"), FlagKind.Red, "name Red");
        fail += Expect(FlagModel.ParseName("White"), FlagKind.White, "name White");
        fail += Expect(FlagModel.ParseName("Green"), FlagKind.Green, "name Green");
        fail += Expect(FlagModel.ParseName("None"), FlagKind.None, "name None");
        fail += Expect(FlagModel.ParseName("clear"), FlagKind.None, "name clear");
        fail += Expect(FlagModel.ParseName(""), FlagKind.None, "empty name");
        fail += Expect(FlagModel.ParseName(null), FlagKind.None, "null name");

        // Select option strings must match the firmware's declared options.
        fail += Option(FlagKind.None, "NONE");
        fail += Option(FlagKind.Green, "GREEN");
        fail += Option(FlagKind.Yellow, "YELLOW");
        fail += Option(FlagKind.Blue, "BLUE");
        fail += Option(FlagKind.White, "WHITE");
        fail += Option(FlagKind.Checkered, "CHECKERED");
        fail += Option(FlagKind.Red, "RED");
        fail += Option(FlagKind.Black, "BLACK");
        fail += Option(FlagKind.Orange, "ORANGE");

        // Describe is used by the settings panel, just check it is non-empty and
        // that "no flag" reads sensibly.
        foreach (FlagKind kind in Enum.GetValues(typeof(FlagKind)))
        {
            if (string.IsNullOrEmpty(FlagModel.Describe(kind)))
            {
                fail++;
                Console.WriteLine("FAIL: Describe(" + kind + ") was empty");
            }
        }

        Console.WriteLine("cydflags: flag priority/parse/option tests " + (fail == 0 ? "ok" : fail + " failure(s)"));
        return fail;
    }

    private static int Expect(FlagKind actual, FlagKind expected, string what)
    {
        if (actual == expected) return 0;
        Console.WriteLine("FAIL: " + what + ": expected " + expected + ", got " + actual);
        return 1;
    }

    private static int Option(FlagKind kind, string expected)
    {
        string actual = FlagModel.ToOption(kind);
        if (actual == expected) return 0;
        Console.WriteLine("FAIL: ToOption(" + kind + "): expected " + expected + ", got " + actual);
        return 1;
    }
}
