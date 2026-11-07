using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using Il2CppNullableAffixEffectiveness = Il2CppSystem.Nullable<Il2Cpp.ItemList.AffixEffectiveness>;
using Il2CppNullableByte = Il2CppSystem.Nullable<byte>;
using Il2CppNullableFaction = Il2CppSystem.Nullable<Il2CppLE.Factions.FactionID>;
using Il2CppNullableInt = Il2CppSystem.Nullable<int>;
using Il2CppNullableSpecialAffix = Il2CppSystem.Nullable<Il2Cpp.AffixList.SpecialAffixType>;
using Il2CppStatList = Il2CppSystem.Collections.Generic.List<Il2Cpp.Stats.Stat>;

namespace LastEpochItemGenerator;

internal enum Rarity { Normal, Magic, Rare, Exalted }

internal enum CorruptionChoice { Off, Random, Positive, Negative }

/// <summary><paramref name="Affixes"/> is null when the game should roll the affixes.</summary>
internal readonly record struct SpawnRequest(CatalogEntry Entry, Rarity Rarity, int ItemLevel, int MinLegendaryPotential, bool MakeLegendary, CorruptionChoice Corruption, int Count, IReadOnlyList<AffixPick> Affixes);

/// <summary>Creates items with the game's own generator, then delivers them to the inventory or the ground.</summary>
internal static class ItemSpawner
{
    // The value the game stores in ItemData.rarity. Normal, magic and rare count their affixes.
    private const int RarityNormal = 0, RarityMagic = 2, RarityRare = 4, RarityUnique = 7, RaritySet = 8;
    private const byte RarityLegendary = 9;

    // The Rune of Corruption always passes this level, whatever the item's level.
    private const int RuneCorruptionLevel = 100;

    /// <summary>Returns the message to show in the menu.</summary>
    internal static string Spawn(SpawnRequest request)
    {
        Melon<Mod>.Logger.Msg($"Generate pressed: {request.Entry.Label} x{request.Count}, {request.Affixes?.Count.ToString() ?? "rolled"} affixes.");

        if (!Mod.Offline)
        {
            Melon<Mod>.Logger.Warning("Generation refused: the game reports online play.");
            return "Offline play only.";
        }

        var player = FindPlayer();
        var generator = Resolve(GenerateItems.instance);
        var containers = Resolve(ItemContainersManager.Instance);
        var ground = Resolve(GroundItemManager.instance);

        var missing = new List<string>();
        if (player == null) missing.Add("player");
        if (generator == null) missing.Add("item generator");
        if (containers == null) missing.Add("inventory");
        if (missing.Count > 0)
        {
            string names = string.Join(", ", missing);
            Melon<Mod>.Logger.Warning($"Generation stopped, not found: {names}. Ground manager found: {ground != null}.");
            return $"Not found: {names}. Enter the world with a character first.";
        }

        int inventory = 0, onGround = 0, unexpected = 0;
        for (int i = 0; i < request.Count; i++)
        {
            var item = Generate(generator, request);
            if (item == null)
            {
                Melon<Mod>.Logger.Warning($"The game returned no item for {request.Entry.Label}.");
                break;
            }

            if (request.MakeLegendary) MakeLegendary(item, player, request);
            else if (request.Affixes != null)
            {
                // Chosen affixes start from a plain item. Clearing a rolled one could leave its other state behind.
                if (item.affixes.Count > 0)
                {
                    Melon<Mod>.Logger.Warning($"Expected no affixes on the base item, got: {Describe(item)}. The item was discarded.");
                    return "The base item came with affixes, so nothing was created; see the log.";
                }
                ApplyAffixes(item, request.Affixes);
            }

            // Checked before corrupting, because a corruption outcome can change the rarity, subtype and affixes.
            bool asRequested = IsAsRequested(item, request);
            if (!asRequested) Melon<Mod>.Logger.Warning($"Asked for {request.Entry.Label} as {request.Rarity}, got {Describe(item)}.");

            // A chosen corrupted affix already made the item corrupted, and the game can't corrupt it twice.
            if (request.Corruption != CorruptionChoice.Off && !HasCorruptedPick(request.Affixes) && !Corrupt(item, player, request.Corruption))
            {
                asRequested = false;
                Melon<Mod>.Logger.Warning($"Couldn't corrupt {request.Entry.Label} with corruption {request.Corruption}: {Describe(item)}.");
            }

            if (!asRequested) unexpected++;

            // A full inventory returns false, so the item lands at the player's feet instead.
            if (containers.attemptToPickupItem(item, player.position())) inventory++;
            else
            {
                if (ground == null)
                {
                    Melon<Mod>.Logger.Warning($"Inventory refused {request.Entry.Label} and the ground manager wasn't found. Created {inventory}.");
                    return $"Created {inventory}, then the inventory was full. Make room and try again.";
                }

                ground.dropItemForPlayer(player, item, player.position(), false);
                onGround++;
            }
        }

        Melon<Mod>.Logger.Msg($"Generated {request.Entry.Label}: {inventory} in inventory, {onGround} on the ground, {unexpected} unexpected.");

        string message = $"Created {inventory + onGround}: {inventory} in inventory, {onGround} on the ground.";
        return unexpected > 0 ? $"{message} {unexpected} differed from the request; see the log." : message;
    }

    private static Actor FindPlayer()
    {
        var player = PlayerFinder.getPlayerActor();
        if (player != null) return player;

        var actors = PlayerFinder.FindPlayerActors();
        return actors != null && actors.Length > 0 ? actors[0] : null;
    }

    // The game's static instance fields can be empty, so the scene is the fallback.
    private static T Resolve<T>(T fromStatic) where T : UnityEngine.Object =>
        fromStatic != null ? fromStatic : UnityEngine.Object.FindAnyObjectByType<T>(UnityEngine.FindObjectsInactive.Include);

    private static ItemDataUnpacked Generate(GenerateItems generator, SpawnRequest request)
    {
        var entry = request.Entry;
        bool exalted = !entry.IsUniqueOrSet && request.Affixes == null && request.Rarity == Rarity.Exalted;

        // The Nullable parameters default to null in the signature, but the game's interop throws on null.
        // A new instance is the same as "no value".
        return generator.initialiseRandomItemData(
            giftable: false,
            ilvl: request.ItemLevel,
            requireClassCompatibility: false,
            baseType: entry.BaseType,
            subType: entry.SubType,
            rarity: RarityValue(request),
            uniqueID: entry.UniqueId,
            forceExalted: exalted,
            minimumLegendaryPotential: entry.IsUniqueOrSet && !entry.IsSet ? request.MinLegendaryPotential : 0,
            factionID: new Il2CppNullableFaction(),
            replacementClassID: new Il2CppNullableInt(),
            corruption: new Il2CppNullableInt(),
            requiredSubTypeAffixEffectiveness: new Il2CppNullableAffixEffectiveness(),
            specialAffix: new Il2CppNullableSpecialAffix(),
            minSpecialAffixTier: new Il2CppNullableInt(),
            overrideLegendaryPotential: new Il2CppNullableInt());
    }

    private static bool HasCorruptedPick(IReadOnlyList<AffixPick> picks) =>
        picks != null && picks.Any(p => p.Option.Slot == AffixSlot.Corrupted);

    private static void AddPicks(ItemDataUnpacked item, IReadOnlyList<AffixPick> picks)
    {
        // A legendary keeps its corrupted affix last, because that's where the game puts it when corrupting one.
        foreach (var pick in picks.OrderBy(p => p.Option.Slot == AffixSlot.Corrupted))
        {
            var statChanges = new Il2CppStatList();
            item.AddAffixNoCostOrChecks(pick.Option.AffixId, false, pick.Tier, ref statChanges, new Il2CppNullableByte());
        }
    }

    private static void ApplyAffixes(ItemDataUnpacked item, IReadOnlyList<AffixPick> picks)
    {
        AddPicks(item, picks);

        // Adding affixes directly doesn't go through crafting, so the item's derived data is brought up to date here.
        item.setRarityFromAffixesForNormalMagicOrRareItem();
        item.RefreshIDAndValues();
        item.MakeFullName();
    }

    // Chosen mods follow the end of the game's own conversion, which adds the mods and sets the rarity.
    // Its crafting version can't be used for this: it shuffles the mods and keeps a random selection of them.
    private static void MakeLegendary(ItemDataUnpacked item, Actor player, SpawnRequest request)
    {
        if (request.Affixes is { Count: > 0 } picks)
        {
            // Set before adding, because the game places a corrupted affix according to the item's rarity.
            item.rarity = RarityLegendary;
            AddPicks(item, picks);

            // A crafted legendary never has more mods than Legendary Potential.
            int mods = picks.Count(p => p.Option.IsMod);
            if (item.legendaryPotential < mods) item.legendaryPotential = (byte)mods;

            // The game only sets this when applying a corruption outcome, so adding the affix directly doesn't.
            if (HasCorruptedPick(picks)) item.SetAsCorrupted();
        }
        else
        {
            item.GainRandomModsToBecomeLegendary(player, request.ItemLevel);
        }

        item.RefreshIDAndValues();
        item.MakeFullName();
    }

    // The same steps as the Rune of Corruption in the game's crafting code, so the game picks the outcome.
    // The rune always uses the rolled side. Here the choice can replace it.
    private static bool Corrupt(ItemDataUnpacked item, Actor player, CorruptionChoice choice)
    {
        if (!item.CanBeCorrupted() || !item.TryGetCorruptionConfig(out var config, out bool rolledPositive)) return false;

        bool isPositive = choice switch
        {
            CorruptionChoice.Positive => true,
            CorruptionChoice.Negative => false,
            _ => rolledPositive,
        };

        // The interop throws on a null filter. A new instance is empty, which permits every outcome.
        var outcome = item.DetermineCorruptionOutcome(config, isPositive, new CorruptionOutcomeFilter());

        // Null means nothing on this side fits the item. Applying it would only set the flag and log an error.
        if (outcome == null) return false;

        item.ApplyCorruptionOutcome(player, outcome, false, RuneCorruptionLevel, out _, out _, out _);

        item.RefreshIDAndValues();
        item.MakeFullName();

        Melon<Mod>.Logger.Msg($"Corrupted with {(isPositive ? "positive" : "negative")} outcome {outcome.corruptionOutcome}: {Describe(item)}.");
        return item.IsCorrupted();
    }

    private static int RarityValue(SpawnRequest request)
    {
        if (request.Entry.IsUniqueOrSet) return request.Entry.IsSet ? RaritySet : RarityUnique;
        if (request.Affixes != null) return RarityNormal;
        return request.Rarity switch
        {
            Rarity.Normal => RarityNormal,
            Rarity.Magic => RarityMagic,
            _ => RarityRare,
        };
    }

    // The rarity values and forced IDs come from reading the game's API, so each result is checked with the game's own helpers.
    private static bool IsAsRequested(ItemDataUnpacked item, SpawnRequest request)
    {
        var entry = request.Entry;
        if (item.itemType != entry.BaseType) return false;
        if (entry.SubType >= 0 && item.subType != entry.SubType) return false;

        if (request.MakeLegendary)
        {
            // Some uniques come with an affix of their own, so a legendary can have more than the picks.
            bool modsMatch = request.Affixes is not { Count: > 0 } || HasAll(item, request.Affixes);
            return item.uniqueID == entry.UniqueId && item.isLegendary() && modsMatch;
        }
        if (entry.IsUniqueOrSet) return item.uniqueID == entry.UniqueId && (entry.IsSet ? item.isSet() : item.isUnique());
        if (request.Affixes != null) return HasExactly(item, request.Affixes);

        return request.Rarity switch
        {
            Rarity.Normal => item.isNormal(),
            Rarity.Magic => item.isMagic(),
            Rarity.Rare => item.isRare(),
            Rarity.Exalted => item.isExaltedItem(),
            _ => false,
        };
    }

    private static bool HasExactly(ItemDataUnpacked item, IReadOnlyList<AffixPick> picks) =>
        item.affixes.Count == picks.Count && HasAll(item, picks);

    private static bool HasAll(ItemDataUnpacked item, IReadOnlyList<AffixPick> picks)
    {
        var actual = new List<(int Id, int Tier, bool Experimental, bool Corrupted)>();
        for (int i = 0; i < item.affixes.Count; i++)
        {
            var affix = item.affixes[i];
            actual.Add((affix.affixId, affix.affixTier, affix.IsExperimental, affix.IsSealedCorrupted));
        }

        return picks.All(p => actual.Contains((p.Option.AffixId, p.Tier, p.Option.Slot == AffixSlot.Experimental, p.Option.Slot == AffixSlot.Corrupted)));
    }

    private static string Describe(ItemDataUnpacked item)
    {
        var affixes = new List<string>();
        for (int i = 0; i < item.affixes.Count; i++)
        {
            var affix = item.affixes[i];
            string kind = affix.specialAffixType == AffixList.SpecialAffixType.Standard ? "" : $", {affix.specialAffixType}";
            affixes.Add($"{affix.affixId} tier {affix.affixTier} (shown T{affix.DisplayTier}{kind}{(affix.IsSealedCorrupted ? ", sealed" : "")})");
        }
        return $"type {item.itemType}/{item.subType}, unique {item.uniqueID}, rarity {item.rarity}, corrupted {item.IsCorrupted()}, LP {item.legendaryPotential}, affixes [{string.Join(", ", affixes)}]";
    }
}
