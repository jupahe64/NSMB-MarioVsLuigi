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

**M3 – Editor import + debug level select (M).** *(v2: the viewer is the Unity Editor itself.)* Editor importer generates tile assets from the **real extracted atlases** and one scene/prefab per area (Tilemaps, entrance/sprite/zone/view GameObjects with gizmos); committed scene `NSMBDS_DebugSelect` with a small in-game world/level/area list (read from the generated index) and a free-fly/view-aware camera. *(v4: levels show their real tile graphics. Behaviour is an optional toggleable overlay for inspection, not the main view.)* Tiles whose art the game draws another way and that are not mapped yet (water, lava, fences and similar) get a simple placeholder until they are.
*Done when:* every area can be opened from the list and looks right (static, no animation yet).

**M4 – Player + tile collision + warps (L).** Mario's walk/run/jump/crouch/swim/slope physics and tile collision derived from disassembly (placeholder visuals); spawn at entrances; **pipes (all 4 directions, mini, connected mini), doors, area transitions via DestArea/DestEntrance**, pipe-exit and door-exit behaviours, camera views/zones, death pit, debug HUD (position, velocity, tile under feet, behaviour word). ? blocks / bricks keep their real graphics and act as plain solids until M5.
*Done when:* an automated "entrance graph" check passes for all areas (every entrance resolves to an existing destination) and you can manually walk through the pipes/doors of every world.

### Phase 2 — Real interaction
**M5 – Tile logic & items (L).** Bump animation, ? blocks / bricks / coins / hidden blocks / ice / switches, tile replacement, content tables, power-up states (mushroom, fire, mini, mega, shell, star), 1-ups, coin counter, HUD basics, animated tiles. *Carried over from M4:* harmful tiles and death, climbing (vines, ropes, fences), the 14 side pipes with no floor in front (does the DS allow entering them mid-air?), the route animation between connected pipes.
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
2. Levels use real tile graphics from M3 on; a colour/label behaviour overlay is an optional inspection toggle. Blocks are plain solids until M5.
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

### M2 findings
1. Sprite blocks end in a 4-byte terminator, not 2.
2. Map16 words address the BG VRAM shared by the three tilesets, so a tile can use another set's chars and palettes. Atlases are therefore extracted per tileset combination, not per tileset.
3. Object tile references are global across the sets, and the extended flag is an offset inside a set; cells keep their slot explicitly.
4. **Pipes are not BG graphics.** They are drawn as textured 3D quads (`polygon_unit/I_dokan.nsbtx`, 28 16x16 textures, 3 colour palettes) over tiles with behaviour `0x0101xxxx`, so their Map16 entries are blank. Behaviour low byte: bits 6-7 colour, bit 5 collision variant, bits 0-4 piece. The piece to texture map (`PipeArt`) is inferred from texture names, object definitions and level layouts, not read from the code, and is checked by renders. Pieces `10 12 16 18 1A 1C 1D 1E` are mini pipes (`I_mamedokan`), and `11 13 15 17` are cracked pipes. (This corrects an earlier note that called them invisible.) Junction tiles need the tile number, not just the behaviour. Pieces `31 33 35 37` are not placed in any level and stay unmapped.
   Code `3F` covers tiles `7C 7D 8C 8D`, the metal hub block where pipes join, drawn from the four `I_do_kanagu_*` files; confirmed against the running game. Tile `95` shares the code and stays invisible.

   **Game-truth checks:** `RomAnalysis/emu/tools/hl.ps1 <area> [-X tile -Y tile] [-Frames n] [-Press key:from-to] [-Shot frame] [-Log]` boots a scratch USA ROM directly into any area with no window, using `py-desmume` in the gitignored venv `RomAnalysis/emu/venv`. A run takes about 4 seconds, runs at 90-170 fps, and repeated runs give identical logs. `-Log` writes Mario's state per frame, which replaces the side-by-side spot measurements of D7 for the player. Player actor pointer: `0x0208B35C`; offsets: position `+0x60/+0x64`, x speed `+0xB4`, gravity `+0xBC`, fall limit `+0xC0`, all 20.12 fixed point, y negated. First measurements: walk 1.5 px/frame, gravity -0.0625 rising with A held and -0.34375 otherwise, fall limit -4 px/frame, standing full jump 67.4 px. Do not use `skip_next_frame`: it leaves a blank display buffer and saves almost no time. `RomAnalysis/emu/tools/stress.py` boots every entrance and writes `RomAnalysis/stress/`. First full USA run: 741 entrances in 23.5 minutes on 6 workers; 622 OK, 98 moved away from the entrance, 21 failed with no player, all failures in arena areas `J07_1`-`J10_1`. Booting an entrance directly skips the pipe or door transition that normally leads to it, so this checks loading, not the transitions (M4). The older windowed `golevel.ps1` still works. These tools stay gitignored because they embed game instruction bytes. It stores an uncompressed ARM9, patches `Scene::prepareFirstScene` to skip the intro and save check and call the stage launcher, repoints World 1-1's FAT entries at the wanted area, and sets the midway entrance to the start entrance because this boot path starts at the midway point. Input comes from a `.dsm` movie (`--play-movie`), which starts with blank save memory. Known limit: `J01_1` (arena group) froze; other J areas are untested.
5. Other `polygon_unit` textures follow the same idea (water, lava, poison, quicksand, fences, pillars, mini pipes) and are not drawn yet. They need the same mapping work and probably belong with tile logic (M5) or level mechanics (M7).

### M5 notes
- `data_02085abc` bit 0 disables all sprite spawning; `bootlevel.py` used to set it, so emulator runs before M5 had no sprites at all. Fixed by passing 1 for the start-stage argument at stack offset 0x1C.
- No tile has the water flag. Liquids are sprites: 231 water, 234 lava (surface 3 px below the sprite), 259 poison (8 px below). They set a per-player type (`0x020CACE0`) and height (`0x020CAE0C`) once the sprite comes into view, and a newer one replaces the surface.
- Swimming numbers match the base project. Differences from it, measured in the game: the kick always adds 1.207 px/frame, also from the ground; entering faster than 3 px/frame sets the speed to 1 px/frame, with 9 frames of drag.
- A block hit sets the tile to empty, and 14 frames later places Map16 tile 0x18 (the used block, which is red) and spawns the item. Content tables are in `NsmbBlocks`: bricks and ? blocks use different ones. Item rises 0.3125 px/frame for 51 frames, then walks at 1 px/frame.
- Tiles with flags 0x0008 (stone) and 0x0010 (brick) are solid even without the solid flag.
- Liquids draw as tinted quads with no surface art yet. Item art is the base project's, and the 1-up uses the mushroom sprite.
- Open from M5: climbing, switches, animated tiles, fireballs, the special behaviour of Mini, Mega, Shell and Star (they change power state only), moving liquids, the 14 floorless side pipes, connected pipe routes.

### M4 notes
- The player's movement is a plain C# port of the base project's `MarioPlayerSystem` (user's call after play-testing), with its numbers in units per second, instead of values measured from the DS. Mario is the base project's small model: `NSMB DS ▸ Build Mario Prefab` makes a Quantum-free copy. `PlayerShader2` fails to compile on this Unity version (also in the MvL scenes), so the copy uses `NSMBDS/Mario`, which is unlit-style and has no recolour masks or star effect yet.
- Tile behaviour words: flags in the high 16 bits (0x0001 solid, 0x0002 coin, 0x0010 brick, 0x0020 slope, 0x0040 flipped slope, 0x0100 door/pipe, 0x0200 water, 0x0400 climbable, 0x0800 partial solid, 0x1000 harmful, 0x2000 invisible, 0x8000 solid on top), subtype in bits 12-15, parameter in the low byte. Slope parameter lists the shape.
- `NsmbPlayerMotor` is plain C#, tested in `Tools/NsmbRom.Tests` against per-frame logs from the real game made by `RomAnalysis/emu/tools/physics.ps1`. Walk, tap jump, held jump and running jump match with 0.00 px error, run and skid with 0.07 px.
- Entrance `Settings` bit 0x80 means exit only. `DestArea` 0 means the same area. `entrance-graph` checks all warps: all 334 resolve. Setting 0x08 marks connected pipes: they pair up by `ConnectedPipe` inside an area and ignore the destination fields (`DestArea` only says which end). The route between them is not animated yet.

- Unity rule: every `MonoBehaviour` and `ScriptableObject` needs its own file named after the class. Otherwise scene references break or get an embedded script; `checkscenes.ps1` reports both.

### M3 notes
- Run `dotnet run --project Tools/NsmbExtractor -- extract Roms/NSMB_USA.nds`, then **NSMB DS > Import (keep existing scenes)**, or batch `NsmbDs.Editor.NsmbImportMenu.ImportBatch` for a full rebuild (about 2 minutes). **Reimport All** overwrites scene edits.
- Output in the gitignored `Assets/NSMBDS/Extracted/`: one Unity scene per area in `Scenes/<World>/`, editable with the built-in Tilemap tools; one Tile Palette per tileset in `Palettes/` (`common_V` colour variants, `level_NN`, `sub`); shared tile assets in `Tiles/`. Scene edits are not in git.
- Tiles are `NsmbTile` assets that carry their behaviour word, shared by pixels across tileset combinations. A few common tiles look different per level tileset and have extra palette columns. Palettes hold tiles that some area uses.
- `NsmbAreaIndex` (a resource found by name) lists the scenes; `NSMBDS_DebugSelect` loads them additively in the Editor.
- Batch `NsmbDs.Editor.NsmbPlayCheck.Run` runs the debug scene in Play mode and writes captures to `RomAnalysis/unity_check/`.
- MvL's `GlobalKeybinds` and `TranslationManager` throw null references every frame in this scene; harmless, and they go away with the MvL scripts.
- Backdrop layers are 512x512 PNGs in `layers/`, named `bg_<bg>_<fg>` and `fg_<bg>_<fg>` because both share BG VRAM: tile numbers from 576 use the background graphics, 256-575 the foreground graphics, and palettes start at 10 and 8. Backgrounds use this for the gradient rows between sky and ground (confirmed against VRAM). Tile maps are linear 64x64 in the file. Remaining transparent areas are blank tiles and show the black backdrop. Parallax speeds on `NsmbArea` are unmeasured defaults.
- Not imported yet: animated tiles, and 3D-textured tiles other than pipes (water, lava, fences).

## 9. Changelog
- v1: initial plan.
- v2: review answers applied (D2, D4, D5, D7, M3, section 7).
- v3: M1 verification results (section 8).
- v4: M3 and M4 use real tile graphics instead of debug tiles; behaviour overlay is optional.
