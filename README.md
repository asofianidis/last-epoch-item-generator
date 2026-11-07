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
| Choose affixes | Base items | Opens the affix panel |
| Make legendary | Uniques with Legendary Potential | On or off |
| Choose mods | Legendaries | Opens the affix panel |
| Min Legendary Potential | Uniques that aren't being made legendary (ignored for sets) | 0 to 4 |
| Corruption | All, unless a corrupted affix is chosen | Off, Random, Positive, Negative |
| Item level | All | 1 to 100 |
| Quantity | All | 1 to 20 |

Items go to your inventory; once it's full, they drop at your feet.

### Choosing affixes

Tick **Choose affixes** and select a base item. The panel on the right lists the prefixes, suffixes and experimental affixes the game allows on that item. Click one to add it, click it again to remove it, and set its tier with the slider.

- Gear can have up to 2 prefixes and 2 suffixes. Idols are limited to 1 of each.
- Items that allow experimental affixes can also have 1. Search for `Experimental` to find them.
- Tiers go as high as the game defines for each affix. Affixes the game marks as incompatible with T6 stop at T5.
- The value within each tier is still rolled by the game.
- Rarity follows from the affixes, and sealed affixes aren't supported.
- Without **Choose affixes**, the game rolls the affixes as normal. Uniques and sets don't use this panel.

### Making legendaries

On **Uniques and sets**, tick **Make legendary** to get the item as a legendary straight away, with no crafting in game.

- Tick **Choose mods** to pick them in the affix panel: up to 4, at most 2 prefixes and 2 suffixes, with a tier slider for each. You always get the mods you pick. A unique that comes with an affix of its own keeps it as well.
- On top of those 4, you can add 1 corrupted affix and 1 special affix. Search for `Corrupted` or `Special` to find them. The panel lists only what the game allows on that item, so some items have none.
- Special affixes are the game's personal, set, idol enchantment and idol weaver affixes.
- A corrupted affix makes the item corrupted, so the **Corruption** option is replaced by a note while one is chosen and no corruption outcome is rolled.
- With no mods chosen, the game's own conversion rolls random mods. It rolls how many itself, so Min Legendary Potential doesn't apply, and a unique may get no mods and stay a unique. The panel reports that the result differed from the request.

Sets and Weaver's Will uniques can't use this.

### Corrupting items

Set **Corruption** to corrupt each item the way a Rune of Corruption does, as the last step after any chosen affixes or legendary conversion.

- **Random** lets the game roll the side, as the rune does. **Positive** and **Negative** choose the side, and the game still picks the outcome from that side's list.
- Positive and negative are the game's own lists. An outcome can add or remove affixes, change the subtype or rarity, or ruin the item, so the result may no longer match what you chose. The log records each outcome.
- An item the game can't corrupt, or with nothing on the chosen side that fits it, is created uncorrupted, and the panel reports that it differed from the request.
- The rune treats every item as level 100 when applying the outcome, so the Item level option doesn't affect it.

## Checking that it works

Each result is compared with the request, and differences are reported in the panel and logged. See:

`F:\SteamLibrary\steamapps\common\Last Epoch\MelonLoader\Latest.log`

The rarity values and the tier numbering in `src/ItemSpawner.cs` are the most likely things to need correcting, as they could not be confirmed without running the game. If the tier you get doesn't match the slider, the log shows both the stored tier and the tier the game displays.

## Building

Requires the .NET SDK and a game that has run once with MelonLoader installed, which generates the assemblies the mod compiles against.

```
dotnet build -c Release
```

This copies the DLL into the game's `Mods` folder. If the game is installed elsewhere, add `-p:GameDir="<path>"`.

## Uninstalling

Delete `Mods\LastEpochItemGenerator.dll` from the game folder. Items you already generated stay on your character.
