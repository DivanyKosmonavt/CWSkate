# CW Skate

**A skateboard for Content Warning** (BepInEx 5 mod). Ride through the Old World, do flips, grabs, grinds and frontflips, chain combos and farm **aura** — and film all of it for views and viewer comments about your skating.

**Download:** [CW Skate on Thunderstore](https://thunderstore.io/c/content-warning/p/JensenHuangTeam/CWSkate/) (install it with r2modman / the Thunderstore Mod Manager).
Everyone in the lobby needs the mod.

- Player guide: [English](README_EN.md) · [Русский](README_RU.md)
- What's in each version: [CHANGELOG](Thunderstore/CHANGELOG.md)
- Languages: English, Russian, French, Simplified Chinese

## Features

- Skate 3 style riding: separate pushes, carving, powerslides, curbs and walls that throw you off, Easy / Normal / Hard.
- Tricks: ollies, flips (doubles, triples), spins, 8 grabs, airwalk, superman, frontflips / backflips, manuals, grinds, wallrides, hippie jumps.
- Monster grinds, skitching (get towed by a monster or a player) and skate trains.
- Combos → aura → ranks up to **AURA GOD**; smack monsters with the board.
- Viewer comments about your skating on your videos.
- Board wear, upgrades (wheels, maple deck, a rocket booster), skins, five skate sponsor deals.
- Multiplayer sync for everyone with the mod, a settings menu (F8), a low-end PC mode.

## Building

You need the game, [BepInEx 5](https://thunderstore.io/c/content-warning/p/BepInEx/BepInExPack/) installed in it and the [.NET SDK](https://dotnet.microsoft.com/download) 9 or newer.

1. Put this repository **inside the game folder**, so it's `Content Warning\CWSkate\`. The project finds the game's DLLs from there (`Content Warning_Data\Managed`, `BepInEx\core`).
2. Make the publicized copy of the game code (the mod uses some of its internal members). It isn't in the repository: it's the game's own code.
   ```
   dotnet run --project tools/publicizer -- "../Content Warning_Data/Managed/Assembly-CSharp.dll" "lib/Assembly-CSharp.publicized.dll"
   ```
   (run it in the `CWSkate` folder; create the `lib` folder first)
3. Build:
   ```
   dotnet build CWSkate.csproj -c Release
   ```
   The DLL is copied straight into `BepInEx\plugins\CWSkate\`. Start the game and skate.

### Project layout

| Folder | What's there |
|---|---|
| `src/` | the mod (C#). `SkatePlugin.cs` is the entry point and all the settings, `SkateController*.cs` the riding and tricks, `NetSync.cs` multiplayer, `Lang*.cs` the translations |
| `Icons/` | shop icons, built into the DLL |
| `Thunderstore/` | the package files: manifest, icon, README and CHANGELOG |
| `tools/` | `publicizer` (see above) and `checklang.ps1` (checks a translation covers every text: `checklang.ps1 -Lang Zh`) |

### Translations

Every text in the code is written as `Lang.T("russian", "english")`. Other languages look the English text up in their own table (`src/LangFr.cs`, `src/LangZh.cs`). The viewer comments live in `src/SkateContent.cs` (`DefaultEn`, `DefaultFr`...). A new language: copy `LangFr.cs`, translate it and add it in `Lang.cs`. Pull requests are welcome.

## License

[MIT](LICENSE). Content Warning belongs to Landfall / Skog; this repository holds no game code or assets.
