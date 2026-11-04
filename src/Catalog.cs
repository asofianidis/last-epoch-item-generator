using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;

namespace LastEpochItemGenerator;

internal sealed record CatalogEntry(string Label, int BaseType, int SubType, int UniqueId, bool IsSet)
{
    internal bool IsUniqueOrSet => UniqueId >= 0;
}

internal sealed record AffixOption(string Label, int AffixId, bool IsPrefix, int MaxTierIndex);

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

    /// <summary>The prefixes and suffixes the game says can roll on this item, for any class.</summary>
    internal static List<AffixOption> AffixesFor(CatalogEntry item)
    {
        var compatible = AffixList.get().GetAllCompatibleAffixes(item.BaseType, item.SubType, AffixList.SpecialAffixType.Standard);

        var options = new List<AffixOption>();
        for (int i = 0; i < compatible.Count; i++)
        {
            var affix = compatible[i];
            if (affix?.tiers == null || affix.tiers.Count == 0 || affix.type == AffixList.AffixType.SPECIAL) continue;

            string name = FirstNonEmpty(affix.affixDisplayName, affix.affixName);
            if (name.Length == 0) continue;

            bool isPrefix = affix.type == AffixList.AffixType.PREFIX;
            int maxTier = affix.tiers.Count - 1;
            if (affix.t6Compatibility == AffixList.T6Compatibility.Incompatible) maxTier = Math.Min(maxTier, HighestTierIndexWithoutT6);

            options.Add(new AffixOption($"{name} ({(isPrefix ? "Prefix" : "Suffix")})", affix.affixId, isPrefix, maxTier));
        }
        return options.OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static bool IsIdol(CatalogEntry item) => ItemList.IdolBaseTypes.Contains(item.BaseType);

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
                if (name.Length > 0) entries.Add(new CatalogEntry($"{baseName}: {name}", baseItem.baseTypeID, sub.subTypeID, -1, false));
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
            entries.Add(new CatalogEntry($"{name} ({items.GetBaseTypeDisplayName(unique.baseType)}, {kind})",
                unique.baseType, subType, unique.uniqueID, unique.isSetItem));
        }
        return entries.OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string FirstNonEmpty(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second ?? "";
}
