using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;

namespace LastEpochItemGenerator;

internal sealed record CatalogEntry(string Label, int BaseType, int SubType, int UniqueId, bool IsSet)
{
    internal bool IsUniqueOrSet => UniqueId >= 0;
}

/// <summary>Everything the generator can create, read from the game's own item lists.</summary>
internal static class Catalog
{
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
