# NSMB (DS) → PC port on the NSMB-MarioVsLuigi codebase — Plan v1 (for review, nothing implemented yet)

Date: 2026-11-01. Status: **v3 – in progress, see `docs/NSMBDS_MILESTONES.md`; M1 format verification results are in section 8.**

## 1. What I found

### 1.1 Your fork (identical to upstream `ipodtouch0218/NSMB-MarioVsLuigi`)
- Unity 6000.3.10f1, URP 2D, Tilemap, Input System.
- **The whole game simulation runs on Photon Quantum** (deterministic ECS, fixed-point `FP`, `.qtn` code-gen, baked `Map` assets).
  - `MarioPlayerSystem` ≈ 3,000 lines, `PhysicsObjectSystem` ≈ 1,500 lines, polygon-based tile collision (`StageTile`, `VersusStageData`).
  - Levels are Unity scenes + a Quantum `Map` + `VersusStageData` (wrapping, versus spawn points, star spawns).
  - 1 tile = 0.5 world units, so 1 DS pixel = 1/32 unit. `MarioPlayerPhysicsInfo` already holds DS-style numbers (e.g. walk 0.9375 / 2.8125 / 4.21875 / 5.625; `OnePixelPerFrame = 1.875`).
- Enemies that exist today (Quantum, MvL-oriented): Goomba, Koopa, Bobomb, Piranha Plant, Bullet Bill, Boo, Fire Snake. Plus powerups, pipes, liquids, platforms, bump blocks.
- The repo already ships converted Mario/Luigi FBX models, sprites and sounds. I found no LICENSE file in the repo root — please confirm which license you want to treat the fork as (matters for GPL code, see 1.3).

### 1.2 Existing NSMB-DS decompilation: `NSMB-Decomp/nsmb` (CC0, C++, USA/A2DE only)
- Early stage: cannot build a ROM. `Goomba::onCreate` is an empty stub; `PlayerBase.cpp` ≈ 1.1k lines; `StageEntity` ≈ 2.8k lines; `Actor`, `Item`, `Coin` partial.
- **Not a source of behaviour, but very valuable as a map**: ≈ 2,066 named symbols for A2DE, struct layouts (`Actor`, `StageActor`, `PlayerBase`…), `ObjectID` enum, Ghidra type header (`nsmb.h`), overlay layout config (`config/A2DE`).
- EU/JP are not supported there → code analysis is done on USA; EU is used for data (see 3.4).

### 1.3 NSMB Editor 5 (`Dirbaio/NSMB-Editor`, GPL-3, C#)
- Not usable as code (GPL-3), but its source documents the formats precisely. I'll write the extractor clean-room from the format knowledge.
- Format facts I'll build on (all to be verified against your real ROMs in M1):
  - ROM game codes `A2DE` (US) / `A2DP` (EU). Overlays are BLZ-compressed; graphics/palettes are LZ77.
  - `course/Axx_y.bin` (14-block table: tileset ID, camera views, entrances, sprites, zones, paths …) + `Axx_y_bgdat.bin` (10-byte tile-object records that must be expanded using the tileset's object definitions, including slope objects) → a 512×256 Map16 grid.
  - Sprites: 12 bytes (type u16, x, y, 6 data bytes), **326 sprite types**. Entrances: 20 bytes, ~26 types (door, pipes in 4 directions, mini pipes, connected mini pipes, exits, slide/swim/ground-pound/jump/vine entrances).
  - Tilesets: 3 layers per level (common "Jyotyu" set, per-level tileset out of ≤76, "Nohara" sub-unit), Map16 = 4 × 8×8 tiles, 8bpp graphics, 2×256-colour RGB555 palettes, **one u32 behaviour word per Map16 tile** (this is what makes tiles solid / slopes / coins / ? blocks / pipes …).
  - File-ID tables live in overlay 0 at region-specific offsets; NSMBe lists offsets for US and EU for all of the level/tileset tables.
  - ≈ **100 levels / ≈ 190 areas** (worlds 1–8, secret worlds, Toad houses, Mario-vs-Luigi arenas, unused levels).

## 2. Architecture decisions (please confirm or change)

| # | Decision | Reason |
|---|----------|--------|
| D1 | The single-player port is a **new MonoBehaviour layer** (`Assets/NSMBDS/`, own asmdef) living next to the Quantum MvL game. Quantum stays untouched. | You asked for simple MonoBehaviours. Quantum needs deterministic systems, `.qtn` code-gen and editor-baked maps — wrong shape for ~100 levels loaded from a ROM at runtime. |
| D2 *(v2)* | Simulation runs at a **fixed 60 Hz step with float math** ("feels right", not bit-exact). All tunables (speeds, gravity, timers, hitboxes) are DS values converted from px/frame and exposed as inspector fields / ScriptableObjects. | Easy to experiment with behaviour; exact glitches are what an emulator is for. |
| D3 | **Extractor = standalone .NET CLI, zero NuGet dependencies**, in `Tools/NsmbExtractor/`. Core library `NsmbRom` is netstandard2.1 + a thin CLI. PNG output through a ~60-line own writer (`System.IO.Compression` is part of the BCL). | Works on a fresh clone: `dotnet run --project Tools/NsmbExtractor -- extract path/to/rom.nds`. |
| D4 *(v2)* | **Extracted assets live inside the Unity project, like the MvL assets do**: `Assets/NSMBDS/Extracted/` (Unity-importable files + generated Unity assets). Gitignored together with their `.meta` files (explicit rules placed *after* the existing `!/Assets/**/*.meta` rule): `/Assets/NSMBDS/Extracted/`, `/Assets/NSMBDS/Extracted.meta`, `/Roms/`, `*.nds`, `/RomAnalysis/`, tool `bin/` + `obj/`. Committed code never references extracted assets by GUID, only through a generated index (path/name lookup). | Levels and assets are available in the Editor, nothing from the ROM can slip into a commit, and a later "supply assets at runtime" build can load the same data through asset bundles/Addressables from disk. |
| D5 *(v2)* | Two stages. **(1) Extractor CLI** (pure .NET): ROM to Unity-native formats where possible (PNG, WAV/OGG, mesh formats Unity imports, JSON). **(2) Unity Editor importer** (`NSMB DS > Import`, Editor asmdef): launches the CLI if needed, then generates Tile assets, sprite atlases, palettes/materials and **one Unity scene (or prefab) per level area** with Tilemaps and GameObjects for entrances / sprites / zones / views, the same workflow as the MvL levels. | Level geometry and object placement are editable in the Unity Editor; actors are components you can tweak live. |
| D6 | **Behaviour source = disassembly of the USA overlays, ported by hand into small MonoBehaviours** (one `NsmbActor` subclass per actor, shared helpers for physics / tile queries). No emulation at runtime. | Matches your requirement. |
| D7 *(v2)* | Behaviour is checked by **side-by-side play against DeSmuME** and spot measurements (jump height, run speed, enemy timings), not full frame traces. Full trace comparison stays an optional tool for tricky actors. | "Feels right" is the bar; keeps effort proportional. |
| D8 | Existing MvL assets (Mario/Luigi models, UI) are only **placeholders**; the final visuals come from the ROM (own NSBMD/NSBTX/NSBCA converters, later milestone). | Fidelity + the "everything extracted from ROM" rule. |

## 3. Extractor design (`Tools/NsmbExtractor`)

### 3.1 Commands
- `extract <rom.nds> [--out NSMBDS-Extracted]` — everything supported so far; idempotent; writes `manifest.json`.
- `info <rom.nds>` — header, game code, region, overlay table, file count.
- `dump-fs <rom.nds> <dir>` / `dump-overlays <rom.nds> <dir>` — raw + decompressed files into `RomAnalysis/` for reverse engineering (dev only).
- `census <rom.nds>` — how often each of the 326 sprite types / each entrance type / each tile behaviour appears across all levels. **This decides the implementation order of enemies and tile logic.**
- `verify <usa.nds> <eu.nds>` — extracts both and diffs level data; any difference is a bug in a region table or a real regional change we must know about.

### 3.2 Layers
1. `Nds` — header, FNT/FAT, overlay tables, ARM9 data, banner. 2. `Compression` — BLZ (overlays), LZ77 (0x10), LZ11/RLE if needed. 3. `Nitro` — NARC, NCGR/NCLR/NSCR (with NSMB's raw variants). 4. `Nsmb` — overlay-0 tables, tilesets, Map16, tile objects (incl. slopes), levels, sprites, entrances, views, zones, paths, tile behaviours. 5. later: NSBMD/NSBTX/NSBCA/NSBTA, SDAT.

### 3.3 Output
`manifest.json`; `tilesets/<id>/{gfx.png, map16.json|bin, behaviors.bin, objects.json, palettes}`; `levels/A01_1.json` (header, entrances, views, zones, sprites, paths) + `A01_1.map16` (binary grid); `levels/index.json` (world / level / area, own naming); later `models/`, `audio/`, `ui/`.

### 3.4 USA + EU
- Game code detected from the header; table offsets chosen per region from a table, **validated at runtime** (all file IDs resolve, decompression succeeds, sizes sane). If validation fails the tool refuses instead of emitting garbage.
- Level/tileset data are expected to be region-identical (checked by `verify`). EU differs in text/UI graphics and some table offsets — handled when we reach menus.

### 3.5 Dependencies/tests
- Zero NuGet for the shipped tool. Unit tests (separate project, xUnit allowed there) on synthetic data; optional ROM-backed tests that skip when no ROM is present.

## 4. Behaviour-porting workflow (per actor / tile type)
1. Sprite ID → class ID (overlay-0 table, USA) → profile table → overlay + vtable → named symbols from the decomp project.
2. Disassemble/decompile locally (Ghidra headless or `dsd` + decomp symbols/types). Output stays in `RomAnalysis/` (gitignored).
3. Write a short **spec note** (state machine, constants, hitboxes, interactions, sprite-data bytes meaning) — prose + numbers, no listings.
4. Implement `NsmbXxx : NsmbActor` with `FixedStep()`; use shared `FixedPoint`, `ActorPhysics`, `TileQuery`.
5. Trace-compare (D7) for the actors where it is cheap; otherwise manual play-test checklist.

## 5. Milestones

Sizes are relative: S ≈ one focused session, M ≈ a few, L ≈ many, XL = long tail.

### Phase 1 — "All levels walkable" (your first goal)
**M0 – Foundations (S).** Folder layout, asmdef, `.gitignore`, tool skeleton, .NET SDK check, reverse-engineering toolchain setup on your PC, licence decision.
*Done when:* `dotnet run … info rom.nds` works for the US and EU ROM.

**M1 – ROM, file system, compression (M).** NDS container, overlays, BLZ/LZ77, NARC, overlay-0 tables per region, dump commands, tests.
*Done when:* `dump-fs` matches the file list of both ROMs and every table-referenced file decompresses.

**M2 – Tilesets + levels + census (M–L).** Tileset graphics/palettes/Map16/object definitions/behaviours; level blocks; tile-object expansion incl. slopes; entrances/views/zones/sprites/paths; index of all ~190 areas; PNG renders of every area (contact sheet); census report.
*Done when:* all areas extract from both ROMs, `verify` is clean, rendered area PNGs match what you know of the game, census report exists.

**M3 – Editor import + debug level select (M).** *(v2: the viewer is the Unity Editor itself.)* Editor importer generates tile assets, palettes and one scene/prefab per area (FG/BG Tilemaps, entrance/sprite/zone/view GameObjects with gizmos); committed scene `NSMBDS_DebugSelect` with a small in-game world/level/area list (read from the generated index) and a free-fly/view-aware camera. **Debug tile overlay**: every Map16 tile tinted + labelled by behaviour class (solid, semisolid, slope types, water/lava, coin, ? block + content, brick, pipe, door, spikes …), entrances/sprites/zones/views as gizmos.
*Done when:* every area can be opened from the list and looks right (static, no animation yet).

**M4 – Player + tile collision + warps (L).** Mario's walk/run/jump/crouch/swim/slope physics and tile collision derived from disassembly (placeholder visuals); spawn at entrances; **pipes (all 4 directions, mini, connected mini), doors, area transitions via DestArea/DestEntrance**, pipe-exit and door-exit behaviours, camera views/zones, death pit, debug HUD (position, velocity, tile under feet, behaviour word). ? blocks / bricks are **solid debug tiles** for now.
*Done when:* an automated "entrance graph" check passes for all areas (every entrance resolves to an existing destination) and you can manually walk through the pipes/doors of every world.

### Phase 2 — Real interaction
**M5 – Tile logic & items (L).** Bump animation, ? blocks / bricks / coins / hidden blocks / ice / switches, tile replacement, content tables, power-up states (mushroom, fire, mini, mega, shell, star), 1-ups, coin counter, HUD basics, animated tiles.
**M6 – Actor framework + first enemy batch (L).** `NsmbActor` base: view-based spawn/despawn, actor↔actor and actor↔tile collision, stomp/fireball/shell rules; model pipeline (NSBMD/NSBTX/NSBCA first pass). Enemies in census order (Goomba, Koopa family, Piranha, Bullet Bill, …).
**M7 – Level mechanics (L).** Goal pole/flag, midway flags, timer, score/lives, star coins, moving platforms/lifts/seesaws/springs/switch blocks/vines/water currents, scroll-lock zones.
**M8 – Remaining actors world by world (XL).** Driven by the census: world 1 → 8, then secrets, towers, castles, **bosses**, Toad houses, cannon levels.

### Phase 3 — Everything around the levels
**M9 – Audio (L).** SDAT (SSEQ/SBNK/SWAR/STRM) parser and a renderer (offline to WAV/OGG at extraction time preferred) for music + SFX with loop points.
**M10 – Menus, world map, save, title, mini-games, game flow (XL).** Touch-screen UI mapped to mouse/keyboard/gamepad; world map; save; Toad houses; the 14 mini-games.
**M11 – Polish & completion (L).** Full playthrough tests, performance, widescreen policy, input mapping, packaging.

## 6. Risks
- **Scale of M8** (hundreds of actor behaviours). Mitigation: census-driven order, shared helpers, trace tests.
- **Hand-porting assembly is error-prone.** Mitigation: D7.
- **NSBMD and SDAT are each a mini-project** (M6/M9).
- **Format assumptions come from NSMBe's source, not from your ROMs.** M1 verifies them first; if something differs I'll report before building on it.
- **EU specifics**: no sprite→class table offset known for EU; mitigated by doing code analysis on USA only.
- **Legal**: nothing from the ROM (data, disassembly, decompiled code) enters git. Ported logic is still derived from Nintendo's code; keep the project non-distributed (no builds with assets).

## 7. Decisions from the user's review (2026-11-01)
1. Separate MonoBehaviour layer; online multiplayer / Quantum is given up. MvL scripts and scenes get deleted once everything is ported.
2. "Debug tiles" = colour/label overlay by tile behaviour; blocks are plain solids until M5.
3. Float physics at fixed 60 Hz is fine; tunable and easy to experiment with.
4. Scope = the entire single-player campaign. No Mario vs Luigi / Vs / wireless.
5. Ghidra is in `C:\Users\jupah\Tools`, DeSmuME in `C:\Users\jupah\Documents\Nds-hacking`; newer versions may be downloaded if that works without user input.
6. .NET 10.
7. ROMs live in `Roms/` in the repo (gitignored); copy from `C:\Users\jupah\Documents\Nds-hacking`.
8. No license for now (experiment).
9. Assets are extracted into the Unity project and available in the Editor (D4/D5); runtime-supplied-asset builds come later if there is demand.
10. **Git workflow:** commit and push every *tested* milestone; record milestone → commit in `docs/NSMBDS_MILESTONES.md` (and tag `nsmbds-mN`) so there is always a known-good fallback.

## 8. M1 verification results (2026-11-01)
Checked against real USA and EU ROMs with `check` and `compare-fs`.

**Confirmed:** game codes, FNT/FAT layout, unnamed files are exactly the overlays, overlay counts (131 USA, 135 EU), BLZ-compressed overlays and ARM9, all 13 overlay-0 tileset tables at the expected offsets in both regions, 76 tileset slots, table values relative to the overlay count, graphics/palette/screen files LZ77-compressed, object/behaviour/Map16 files stored plain.

**Differs from the plan:**
1. **Level data is not region-identical.** 15 level files differ, by a few bytes up to several hundred. One area exists only in EU; one orphan bgdat file exists only in USA. So `verify` cannot be "clean" as M2 assumed: it must report these as known regional differences.
2. Table entries are not plain file IDs: the low 16 bits are the file ID, and the high 16 bits are 0 or a tag. The tag's meaning is unconfirmed (probably a data-archive group and member index).
3. Value "first ID past the last file" means "no file" (mostly the animated-graphics table).
4. One EU overlay is stored uncompressed, so the per-overlay flag must be honoured.
5. A ROM copy can be edited: the header and table checks alone do not catch this, so `check` must pass before extraction.

**Decisions:** USA is the source of level data. EU ROMs stay accepted and extract their own data. The EU-only area `F11_3` is left out for now. M2's `verify` reports regional differences instead of expecting none.

## 9. Changelog
- v1: initial plan.
- v2: review answers applied (D2, D4, D5, D7, M3, section 7).
- v3: M1 verification results (section 8).
