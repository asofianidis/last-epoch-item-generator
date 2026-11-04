# Last Epoch Item Generator

A MelonLoader mod for Last Epoch that creates items on demand in offline play. Built for Season 5 (Unity 6000.4.8, MelonLoader 0.7.3).

It refuses to generate anything if the game reports online play. Modifying the client is against Last Epoch's terms, so keep it offline.

**Status:** the mod compiles against the game's assemblies but has not yet been run in game. Back up your saves before first use.

## Using it

1. Load a character into the world and press **F4**. Game input is off while the panel is open.
2. Choose **Base items** or **Uniques and sets**, then search and select an item.
3. Set the options, then press **Generate**:

| Option | Applies to | Range |
| --- | --- | --- |
| Rarity | Base items | Normal, Magic, Rare, Exalted |
| Min Legendary Potential | Uniques (ignored for sets) | 0 to 4 |
| Item level | All | 1 to 100 |
| Quantity | All | 1 to 20 |

Affixes are rolled by the game's own generator, so you can't choose them. Items go to your inventory; once it's full, they drop at your feet.

## Checking that it works

Each result is compared with the request, and differences are reported in the panel and logged. See:

`F:\SteamLibrary\steamapps\common\Last Epoch\MelonLoader\Latest.log`

The rarity values in `src/ItemSpawner.cs` are the most likely thing to need correcting, as they could not be confirmed without running the game.

## Building

Requires the .NET SDK and a game that has run once with MelonLoader installed, which generates the assemblies the mod compiles against.

```
dotnet build -c Release
```

This copies the DLL into the game's `Mods` folder. If the game is installed elsewhere, add `-p:GameDir="<path>"`.

## Uninstalling

Delete `Mods\LastEpochItemGenerator.dll` from the game folder. Items you already generated stay on your character.
