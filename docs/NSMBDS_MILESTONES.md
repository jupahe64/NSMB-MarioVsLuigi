# NSMB DS port – milestone log

Tested milestones only. Each row is a known-good fallback: `git checkout nsmbds-mN`.

| Milestone | Tag | Commit | Date | Notes |
|---|---|---|---|---|
| M0 foundations | `nsmbds-m0` | `75fb07621` | 2026-11-01 | `info` on USA and EU ROMs, asmdefs compile, ignore rules in place. |
| M1 ROM, file system, compression | `nsmbds-m1` | `afaf2a2c8` | 2026-11-01 | `check` passes on USA and unmodified EU, edited EU refused, format findings in plan section 8. |
| M2 tilesets, levels, census | `nsmbds-m2` | `8c6bbb081` | 2026-11-01 | 190 areas on USA and EU, pipes drawn from 3D textures, stress test: all campaign entrances load. |
| M3 Editor import, debug level select | `nsmbds-m3` | `ded5fe1a6` | 2026-11-02 | 190 editable area scenes, 52 tileset palettes, backdrop layers; painting, path handles and gradient tested in the Editor. |
| M4 player, collision, warps | `nsmbds-m4` | `416a043d2` | 2026-11-02 | Base project's Mario ported without Quantum; 204 of 219 pipes reachable by test, 334 warps resolve, play-tested in the Editor. Open items moved to M5. |
