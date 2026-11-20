# Tests

These are pure C# tests for the parts that do not need SimHub or Windows:
flag priority, `Flag_Name` parsing, the option strings sent to the display and
the previous-lap `DeltaTracker` interpolation.

```bash
dotnet run --project tests
```

Expected output:

```text
cydflags: flag priority/parse/option tests ok
ALL TESTS PASSED
```

The test project targets `net8.0` and has no external package dependencies. It
compiles `src/CydFlags/FlagModel.cs` and `src/CydFlags/DeltaTracker.cs`
directly; `GameReaderCommonStub.cs` supplies the three SimHub members the delta
tracker reads, so no SDK is needed.

The SimHub-dependent classes (`TimingBuilder`, `RaceLogicBuilder`,
`CydFlagsPlugin`) cannot be unit-tested without the SimHub SDK and a running
SimHub; they are verified by compiling the plugin and by driving the display.
