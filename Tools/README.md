# Tools

## NsmbExtractor

Standalone .NET 10 CLI, no NuGet dependencies. Run from the repository root (`global.json` pins the SDK):

```
dotnet run --project Tools/NsmbExtractor -- info Roms/<rom>.nds
dotnet run --project Tools/NsmbExtractor -- check Roms/<rom>.nds
dotnet run --project Tools/NsmbExtractor -- dump-fs Roms/<rom>.nds [outDir]
dotnet run --project Tools/NsmbExtractor -- dump-overlays Roms/<rom>.nds [outDir]
dotnet run --project Tools/NsmbExtractor -- compare-fs Roms/<a>.nds Roms/<b>.nds
dotnet test Tools/NsmbTools.sln
```

`check` validates every format assumption and fails on an inconsistent or edited ROM. Dumps default to `RomAnalysis/<rom name>/`.

Put ROMs in `Roms/` (gitignored). Supported game codes: `A2DE` (USA), `A2DP` (Europe). Anything else is refused.
Tests that need a ROM look for `Roms/NSMB_USA.nds`, `Roms/NSMB_EU_DUMP.nds` (unmodified) and `Roms/NSMB_EU.nds` (edited copy, must be refused) and skip when missing.

- `NsmbRom/` – parsing library (netstandard2.1).
- `NsmbExtractor/` – CLI.
- `NsmbRom.Tests/` – xUnit tests (NuGet is allowed here only).

## Reverse-engineering setup

Output goes to `RomAnalysis/` (gitignored). Nothing derived from a ROM is ever committed.

Ghidra 11.3 needs JDK 21+; set `JAVA_HOME` to a new enough JDK for the shell that launches it:

```
$env:JAVA_HOME = "C:\Program Files\Java\jdk-23"
```

Symbol names come from the CC0 decomp, cloned outside this repository: https://github.com/NSMB-Decomp/nsmb (`config/A2DE/arm9/symbols.txt`).
