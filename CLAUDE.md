# NSMB (DS) → PC port — project rules for Claude

Goal: port the **entire single-player campaign** of New Super Mario Bros. (DS) to PC, using this Unity repo (fork of NSMB-MarioVsLuigi) as the base.
Full plan, architecture decisions and milestones: **`docs/NSMBDS_PLAN.md`** (read it first). Milestone → commit log: **`docs/NSMBDS_MILESTONES.md`**.
Do not start a milestone until the user says so. Ask open questions before implementing.

## Hard rules
1. **Nothing derived from the ROM is ever committed**: ROMs, extracted assets, disassembly/decompiler output, analysis dumps. Gitignore (rules placed *after* the existing `!/Assets/**/*.meta` line, covering `.meta` files too):
   `/Assets/NSMBDS/Extracted/`, `/Assets/NSMBDS/Extracted.meta`, `/Roms/`, `*.nds`, `/RomAnalysis/`, tool `bin/` + `obj/`.
2. Committed code never references extracted assets by GUID; use a generated index (path/name lookup).
3. **Commit and push only tested milestones** (branch `full-game-test`, remote `origin`). Tag `nsmbds-mN` and add the commit hash to `docs/NSMBDS_MILESTONES.md` so there is always a known-good fallback.
4. **Stage explicit paths only.** Unrelated Unity-generated changes (deleted `Assets/Photon/Quantum/PackageResources/*.unitypackage.meta`, modified `Assets/QuantumUser/View/Generated/QPrototypeFireSnake.cs`) must not go into milestone commits.
5. No license file for now (experiment). No ported code or data from GPL projects (NSMB Editor is GPL-3: use it only as a format reference, write own parsers).
6. Ported game logic is derived from Nintendo's code: keep the project non-distributed, no builds containing assets.

## Decisions (from the user's review)
- New **MonoBehaviour layer** in `Assets/NSMBDS/` with its own asmdefs. Quantum/Photon multiplayer and the Mario-vs-Luigi scripts/scenes are left alone for now and **deleted once everything is ported**. Online multiplayer is out of scope.
- Scope: whole single-player campaign. No Vs / wireless / Mario vs Luigi. Mini-games assumed out of scope unless the user says otherwise.
- Physics: **float math, fixed 60 Hz step**, "feels right" rather than bit-exact. All tunables (speeds, gravity, timers, hitboxes) are exposed in the inspector/ScriptableObjects so behaviour is easy to experiment with. Exact glitches are what an emulator is for. **Mario's movement is a plain C# port of the base project's `MarioPlayerSystem`** (user's decision after play-testing M4), with its values in units per second; other actors use DS values converted from px/frame.
- **Extractor**: standalone .NET 10 CLI in `Tools/NsmbExtractor/`, **zero NuGet dependencies** (own PNG writer etc.), works on a fresh clone: `dotnet run --project Tools/NsmbExtractor -- extract Roms/<rom>.nds`. Must support USA (`A2DE`) and Europe (`A2DP`) ROMs: detect by game code, per-region table offsets validated at runtime, refuse instead of emitting garbage. Includes `info`, `dump-fs`, `dump-overlays`, `census` (sprite/entrance/tile-behaviour usage), `verify <usa> <eu>` (reports the regional level differences, see below).
- **Level data source is USA.** Level data differs slightly between regions (see plan section 8). EU ROMs stay fully accepted and extract their own data. The EU-only area `F11_3` is left out for now. `NSMB_EU_DUMP.nds` is the unmodified EU reference; `NSMB_EU.nds` is an edited copy that `check` refuses.
- **Assets live in the Unity Editor** like the MvL assets: extractor writes Unity-importable files into `Assets/NSMBDS/Extracted/`; an Editor importer (`NSMB DS ▸ Import`) generates Tile assets, atlases, palettes and one scene/prefab per level area (Tilemaps + GameObjects for entrances, sprites, zones, views). A runtime-supplied-assets build (asset bundles/Addressables from disk) comes later, only if there is demand.
- Levels use real tile graphics from M3 on; a colour/label overlay by tile behaviour is only an optional inspection toggle. ? blocks and bricks are plain solids until M5. Debug level select = small in-game world/level/area list.
- Behaviours come from the **USA overlays' disassembly**, hand-ported into small MonoBehaviours (one `NsmbActor` subclass per actor, shared helpers for physics/tile queries). **No emulation at runtime.** Enemy/tile implementation order is driven by the extractor's census.
- Verification: play side-by-side with DeSmuME, spot-measure (jump height, run speed, timings). Full frame-trace comparison only for tricky actors.
- Format knowledge comes from NSMB Editor's source, not the user's ROMs: **M1 must verify the assumptions against both real ROMs first and report differences** before M2 builds on them.
- Reference: the NSMB-Decomp/nsmb project (CC0, USA only, early stage) is useful for symbol names (`config/A2DE/arm9/symbols.txt`), struct layouts and the Ghidra header `nsmb.h`, not as a source of behaviour.

## Milestones (details in the plan)
Phase 1: M0 foundations · M1 ROM/FS/compression · M2 tilesets+levels+census · M3 Editor import + debug level select · M4 player, tile collision, pipes/doors/area transitions.
Phase 2: M5 tile logic & items · M6 actor framework + first enemies · M7 level mechanics · M8 remaining actors/bosses.
Phase 3: M9 audio (SDAT) · M10 menus, world map, save, game flow · M11 polish.
Work on one milestone at a time; the user tests, then you commit, tag, push and log it.

## Environment (user's PC, Windows)
- Unity 6000.3.10f1 (see `ProjectSettings/ProjectVersion.txt`), .NET 10 SDK.
- ROMs: copy USA + EU from `C:\Users\jupah\Documents\Nds-hacking` into `Roms/` (gitignored).
- Ghidra: `C:\Users\jupah\Tools`. DeSmuME: `C:\Users\jupah\Documents\Nds-hacking`. Newer versions may be downloaded if that works without asking the user.
- Git remotes: `origin` = jupahe64/NSMB-MarioVsLuigi (fork), `upstream` = ipodtouch0218/NSMB-MarioVsLuigi. Working branch: `full-game-test`.
