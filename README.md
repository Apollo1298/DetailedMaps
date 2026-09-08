# Detailed Maps

This is a visual mod for **The Long Dark** by Hinterland Studios

Detailed Maps replaces the existing charcoal region maps in game with more detailed ones in order to make mapping a better use of time in-game and make navigation a bit easier. No more need to look up maps online!

Maps are based on [Topographic Maps](https://steamcommunity.com/sharedfiles/filedetails/?id=1142193220) by delta. Charcoal is a filtered stylized set; Color is the full topographic set.

## Installation

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases).
2. Install [ModSettings](https://github.com/zeobviouslyfakeacc/ModSettings/releases) into `Mods\`.
3. Copy into `Mods\`:
   - `DetailedMaps.dll`
   - `DetailedMaps.Charcoal.zip` (required)
   - `DetailedMaps.Color.zip` (optional — needed for Color style)
4. In Options → Mod Settings → **Detailed Maps**, choose **Charcoal** or **Color**.

## Building map packs

From the repo root:

```powershell
.\build-map-packs.ps1 -Deploy
```

Charcoal/color PNGs live in `Maps\` (`*_new.png` / `*_color.png`). The script zips them into the sidecar packs.

## Special Thanks

Thanks to DigitalZombie and Fuar for providing guidance (and letting me directly copy their code in Fuar's case), enabling me to make this mod!
