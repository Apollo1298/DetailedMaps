# Detailed Maps

Visual mod for **The Long Dark** — replaces charcoal FOW region maps with detailed generated art.

**Charcoal** vs **Color**, optional **100 m grid** and **noise**, from one BC7 base per region (levels/color balance prebaked). Vignette is always applied.

## Installation

Requires **TLD 2.59+** and **MelonLoader 0.7.3+**.

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases).
2. Install [ModSettings](https://github.com/zeobviouslyfakeacc/ModSettings/releases) into `Mods\`.
3. Copy into `Mods\`:
   - `DetailedMaps.dll`
   - `DetailedMaps.zip` (from `out\DetailedMaps.zip` after a release pack build)
4. Remove any old packs: `DetailedMaps.Charcoal.zip`, `DetailedMaps.Color.zip`, loose `map_bg_*_new.bundle` / `*_color.bundle`, and `DetailedMaps.LayeredPoc.*`.
5. Options → Mod Settings → **Detailed Maps**: Map Style, Grid, Noise.

## Building the release pack

Requires Unity **6000.0.60f1** (TLD). Base PNGs come from TerrainDumper `out\maps_base` (`map_bg_*_new.png`, renamed to `*_base` for packaging).

```powershell
.\build-release-pack.ps1
```

That syncs bases → `Maps\*_base.png`, builds per-region BC7 bundles + recipe shader, and writes `out\DetailedMaps.zip`.

Or step-by-step:

```powershell
.\build-assetbundle.ps1 -Pack base
.\build-assetbundle.ps1 -Pack recipe
.\build-assetbundle.ps1 -Pack packzip
```

## Repo notes

- Source art in `Maps\` is `map_bg_*_base.png` (+ `map_details_blank.png`). Release pack is `out\DetailedMaps.zip` (gitignored).
- Rebuild with `.\build-release-pack.ps1` (Unity 6000.0.60f1).

## Special Thanks

From the original Detailed Maps author (CaptainThorne): thanks to DigitalZombie and Fuar for guidance (and allowing direct use of Fuar's code).
