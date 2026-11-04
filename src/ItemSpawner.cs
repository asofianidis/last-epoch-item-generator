using Il2Cpp;
using MelonLoader;
using Il2CppNullableAffixEffectiveness = Il2CppSystem.Nullable<Il2Cpp.ItemList.AffixEffectiveness>;
using Il2CppNullableFaction = Il2CppSystem.Nullable<Il2CppLE.Factions.FactionID>;
using Il2CppNullableInt = Il2CppSystem.Nullable<int>;
using Il2CppNullableSpecialAffix = Il2CppSystem.Nullable<Il2Cpp.AffixList.SpecialAffixType>;

namespace LastEpochItemGenerator;

internal enum Rarity { Normal, Magic, Rare, Exalted }

internal readonly record struct SpawnRequest(CatalogEntry Entry, Rarity Rarity, int ItemLevel, int MinLegendaryPotential, int Count);

/// <summary>Creates items with the game's own generator, then delivers them to the inventory or the ground.</summary>
internal static class ItemSpawner
{
    // The value the game stores in ItemData.rarity. Normal, magic and rare count their affixes.
    private const int RarityNormal = 0, RarityMagic = 2, RarityRare = 4, RarityUnique = 7, RaritySet = 8;

    /// <summary>Returns the message to show in the menu.</summary>
    internal static string Spawn(SpawnRequest request)
    {
        if (!Mod.Offline) return "Offline play only.";

        var player = PlayerFinder.getPlayerActor();
        var generator = GenerateItems.instance;
        var containers = ItemContainersManager.Instance;
        var ground = GroundItemManager.instance;
        if (player == null || generator == null || containers == null || ground == null) return "Enter the world with a character first.";

        int inventory = 0, onGround = 0, unexpected = 0;
        for (int i = 0; i < request.Count; i++)
        {
            var item = Generate(generator, request);
            if (item == null)
            {
                Melon<Mod>.Logger.Warning($"The game returned no item for {request.Entry.Label}.");
                break;
            }

            if (!IsAsRequested(item, request))
            {
                unexpected++;
                Melon<Mod>.Logger.Warning($"Asked for {request.Entry.Label} as {request.Rarity}, got type {item.itemType}/{item.subType}, unique {item.uniqueID}, rarity {item.rarity}.");
            }

            // A full inventory returns false, so the item lands at the player's feet instead.
            if (containers.attemptToPickupItem(item, player.position())) inventory++;
            else
            {
                ground.dropItemForPlayer(player, item, player.position(), false);
                onGround++;
            }
        }

        Melon<Mod>.Logger.Msg($"Generated {request.Entry.Label}: {inventory} in inventory, {onGround} on the ground, {unexpected} unexpected.");

        string message = $"Created {inventory + onGround}: {inventory} in inventory, {onGround} on the ground.";
        return unexpected > 0 ? $"{message} {unexpected} differed from the request; see the log." : message;
    }

    private static ItemDataUnpacked Generate(GenerateItems generator, SpawnRequest request)
    {
        var entry = request.Entry;
        bool exalted = !entry.IsUniqueOrSet && request.Rarity == Rarity.Exalted;

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

    private static int RarityValue(SpawnRequest request)
    {
        if (request.Entry.IsUniqueOrSet) return request.Entry.IsSet ? RaritySet : RarityUnique;
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

        return request.Rarity switch
        {
            Rarity.Normal => item.isNormal(),
            Rarity.Magic => item.isMagic(),
            Rarity.Rare => item.isRare(),
            Rarity.Exalted => item.isExaltedItem(),
            _ => false,
        };
    }
}
