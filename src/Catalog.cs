using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;

namespace LastEpochItemGenerator;

internal sealed record CatalogEntry(string Label, int BaseType, int SubType, int UniqueId, bool IsSet, bool CanBeLegendary)
{
    internal bool IsUniqueOrSet => UniqueId >= 0;
}

internal enum AffixSlot { Prefix, Suffix, Experimental, Corrupted, Special }

internal sealed record AffixOption(string Label, int AffixId, AffixSlot Slot, int MaxTierIndex)
{
    /// <summary>Whether this counts toward a legendary's mods. Corrupted and special affixes come on top of them.</summary>
    internal bool IsMod => Slot is AffixSlot.Prefix or AffixSlot.Suffix or AffixSlot.Experimental;
}

internal sealed class AffixPick
{
    internal AffixPick(AffixOption option)
    {
        Option = option;
        Tier = option.MaxTierIndex;
    }

    internal AffixOption Option { get; }

    /// <summary>Index into the affix's tiers, so 0 is T1.</summary>
    internal int Tier { get; set; }
}

/// <summary>Everything the generator can create, read from the game's own item lists.</summary>
internal static class Catalog
{
    // Affixes the game marks as incompatible with T6 stop at T5.
    private const int HighestTierIndexWithoutT6 = 4;

    // FakeUniqueMod is left out because it stands in for a unique's mod rather than being an affix to choose.
    private static readonly AffixList.SpecialAffixType[] LegendaryOnlyTypes =
    {
        AffixList.SpecialAffixType.Corrupted,
        AffixList.SpecialAffixType.Personal,
        AffixList.SpecialAffixType.Set,
        AffixList.SpecialAffixType.IdolEnchantment,
        AffixList.SpecialAffixType.IdolWeaver,
    };

    internal static IReadOnlyList<CatalogEntry> BaseItems { get; private set; } = Array.Empty<CatalogEntry>();
    internal static IReadOnlyList<CatalogEntry> Uniques { get; private set; } = Array.Empty<CatalogEntry>();

    /// <summary>Returns true once the lists are loaded. The game loads them late, so this is retried until then.</summary>
    internal static bool TryLoad()
    {
        if (BaseItems.Count > 0) return true;

        var items = ItemList.get();
        var uniques = UniqueList.get();
        if (items == null || uniques == null || items.EquippableItems == null || uniques.uniques == null) return false;

        var baseItems = ReadBaseItems(items);
        var uniqueItems = ReadUniques(items, uniques);
        if (baseItems.Count == 0) return false;

        BaseItems = baseItems;
        Uniques = uniqueItems;
        return true;
    }

    /// <summary>The affixes the game says can roll on this item, for any class. Uniques also get corrupted and special affixes, for use on legendaries.</summary>
    internal static List<AffixOption> AffixesFor(CatalogEntry item)
    {
        var options = ReadAffixes(item, AffixList.SpecialAffixType.Standard);
        options.AddRange(ReadAffixes(item, AffixList.SpecialAffixType.Experimental));
        if (item.IsUniqueOrSet)
        {
            foreach (var type in LegendaryOnlyTypes) options.AddRange(ReadAffixes(item, type));
        }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static bool IsIdol(CatalogEntry item) => ItemList.IdolBaseTypes.Contains(item.BaseType);

    private static List<AffixOption> ReadAffixes(CatalogEntry item, AffixList.SpecialAffixType type)
    {
        var options = new List<AffixOption>();
        var compatible = AffixList.get().GetAllCompatibleAffixes(item.BaseType, item.SubType, type);
        if (compatible == null) return options;

        bool legendaryOnly = LegendaryOnlyTypes.Contains(type);
        for (int i = 0; i < compatible.Count; i++)
        {
            var affix = compatible[i];
            if (affix?.tiers == null || affix.tiers.Count == 0) continue;
            if (type == AffixList.SpecialAffixType.Standard && affix.type == AffixList.AffixType.SPECIAL) continue;

            // An affix tied to one unique belongs on that unique only.
            if (legendaryOnly && affix.uniqueId != 0 && affix.uniqueId != item.UniqueId) continue;

            string name = FirstNonEmpty(affix.affixDisplayName, affix.affixName);
            if (name.Length == 0) continue;

            var slot = SlotFor(type, affix);
            int maxTier = affix.tiers.Count - 1;
            if (affix.t6Compatibility == AffixList.T6Compatibility.Incompatible) maxTier = Math.Min(maxTier, HighestTierIndexWithoutT6);

            string kind = slot == AffixSlot.Special ? $"Special, {type}" : slot.ToString();
            options.Add(new AffixOption($"{name} ({kind})", affix.affixId, slot, maxTier));
        }
        return options;
    }

    private static AffixSlot SlotFor(AffixList.SpecialAffixType type, AffixList.Affix affix) => type switch
    {
        AffixList.SpecialAffixType.Standard => affix.type == AffixList.AffixType.PREFIX ? AffixSlot.Prefix : AffixSlot.Suffix,
        AffixList.SpecialAffixType.Experimental => AffixSlot.Experimental,
        AffixList.SpecialAffixType.Corrupted => AffixSlot.Corrupted,
        _ => AffixSlot.Special,
    };

    private static List<CatalogEntry> ReadBaseItems(ItemList items)
    {
        var entries = new List<CatalogEntry>();
        foreach (var baseItem in items.EquippableItems)
        {
            if (baseItem?.subItems == null) continue;
            string baseName = FirstNonEmpty(baseItem.displayName, baseItem.BaseTypeName);

            for (int i = 0; i < baseItem.subItems.Count; i++)
            {
                var sub = baseItem.subItems[i];
                // Legacy and undroppable subtypes aren't normal game items, so they stay out of the list.
                if (sub == null || sub.cannotDrop || sub.isLegacySubType) continue;

                string name = FirstNonEmpty(sub.displayName, sub.name);
                if (name.Length > 0) entries.Add(new CatalogEntry($"{baseName}: {name}", baseItem.baseTypeID, sub.subTypeID, -1, false, false));
            }
        }
        return entries.OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<CatalogEntry> ReadUniques(ItemList items, UniqueList uniques)
    {
        var entries = new List<CatalogEntry>();
        for (int i = 0; i < uniques.uniques.Count; i++)
        {
            var unique = uniques.uniques[i];
            if (unique == null) continue;

            string name = FirstNonEmpty(unique.displayName, unique.name);
            if (name.Length == 0) continue;

            int subType = unique.subTypes != null && unique.subTypes.Count > 0 ? unique.subTypes[0] : -1;
            string kind = unique.isSetItem ? "Set" : "Unique";
            // Sets have no Legendary Potential, and Weaver's Will uniques use their own property instead.
            bool canBeLegendary = !unique.isSetItem && unique.legendaryType == UniqueList.LegendaryType.LegendaryPotential;
            entries.Add(new CatalogEntry($"{name} ({items.GetBaseTypeDisplayName(unique.baseType)}, {kind})",
                unique.baseType, subType, unique.uniqueID, unique.isSetItem, canBeLegendary));
        }
        return entries.OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string FirstNonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second ?? "";
}
