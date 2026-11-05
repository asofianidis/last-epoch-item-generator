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

/// <summary><paramref name="Affixes"/> is null when the game should roll the affixes.</summary>
internal readonly record struct SpawnRequest(CatalogEntry Entry, Rarity Rarity, int ItemLevel, int MinLegendaryPotential, int Count, IReadOnlyList<AffixPick> Affixes);

/// <summary>Creates items with the game's own generator, then delivers them to the inventory or the ground.</summary>
internal static class ItemSpawner
{
    // The value the game stores in ItemData.rarity. Normal, magic and rare count their affixes.
    private const int RarityNormal = 0, RarityMagic = 2, RarityRare = 4, RarityUnique = 7, RaritySet = 8;

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

            if (request.Affixes != null)
            {
                // Chosen affixes start from a plain item. Clearing a rolled one could leave its other state behind.
                if (item.affixes.Count > 0)
                {
                    Melon<Mod>.Logger.Warning($"Expected no affixes on the base item, got: {Describe(item)}. The item was discarded.");
                    return "The base item came with affixes, so nothing was created; see the log.";
                }
                ApplyAffixes(item, request.Affixes);
            }

            if (!IsAsRequested(item, request))
            {
                unexpected++;
                Melon<Mod>.Logger.Warning($"Asked for {request.Entry.Label} as {request.Rarity}, got {Describe(item)}.");
            }

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

    private static void ApplyAffixes(ItemDataUnpacked item, IReadOnlyList<AffixPick> picks)
    {
        foreach (var pick in picks)
        {
            var statChanges = new Il2CppStatList();
            item.AddAffixNoCostOrChecks(pick.Option.AffixId, false, pick.Tier, ref statChanges, new Il2CppNullableByte());
        }

        // Adding affixes directly doesn't go through crafting, so the item's derived data is brought up to date here.
        item.setRarityFromAffixesForNormalMagicOrRareItem();
        item.RefreshIDAndValues();
        item.MakeFullName();
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

    private static bool HasExactly(ItemDataUnpacked item, IReadOnlyList<AffixPick> picks)
    {
        if (item.affixes.Count != picks.Count) return false;

        var actual = new List<(int Id, int Tier)>();
        for (int i = 0; i < item.affixes.Count; i++) actual.Add((item.affixes[i].affixId, item.affixes[i].affixTier));

        return picks.All(p => actual.Contains((p.Option.AffixId, p.Tier)));
    }

    private static string Describe(ItemDataUnpacked item)
    {
        var affixes = new List<string>();
        for (int i = 0; i < item.affixes.Count; i++)
        {
            var affix = item.affixes[i];
            affixes.Add($"{affix.affixId} tier {affix.affixTier} (shown T{affix.DisplayTier})");
        }
        return $"type {item.itemType}/{item.subType}, unique {item.uniqueID}, rarity {item.rarity}, affixes [{string.Join(", ", affixes)}]";
    }
}
