using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LastEpochItemGenerator;

/// <summary>Second panel, beside the main one, for choosing the affixes on a base item or the mods on a legendary.</summary>
internal static class AffixPanel
{
    private const float Width = 520f, RowHeight = 26f, Padding = 14f;
    private const int MaxSlotRows = 6, MaxLegendaryMods = 4;

    private static readonly SearchList<AffixOption> Options = new(a => a.Label);
    private static readonly List<AffixPick> PickList = new();

    private static CatalogEntry _item;
    private static int _maxPrefixes, _maxSuffixes, _maxExperimental, _maxCorrupted, _maxSpecial, _maxMods;
    private static string _message = "";

    internal static IReadOnlyList<AffixPick> Picks => PickList;
    internal static bool HasCorruptedPick => PickList.Any(p => p.Option.Slot == AffixSlot.Corrupted);

    internal static void SetItem(CatalogEntry item)
    {
        _item = item;
        PickList.Clear();
        _message = "";

        bool hasItem = item != null;
        var options = hasItem ? Catalog.AffixesFor(item) : new List<AffixOption>();
        Options.SetSource(options);
        _maxPrefixes = _maxSuffixes = hasItem && Catalog.IsIdol(item) ? 1 : 2;
        _maxExperimental = options.Any(o => o.Slot == AffixSlot.Experimental) ? 1 : 0;
        _maxCorrupted = options.Any(o => o.Slot == AffixSlot.Corrupted) ? 1 : 0;
        _maxSpecial = options.Any(o => o.Slot == AffixSlot.Special) ? 1 : 0;
        _maxMods = hasItem && item.IsUniqueOrSet ? MaxLegendaryMods : _maxPrefixes + _maxSuffixes + _maxExperimental;
    }

    internal static void Draw(Rect beside)
    {
        var panel = new Rect(beside.xMax + 10f, beside.y, Width, beside.height);
        Menu.DrawBackground(panel);

        float x = panel.x + Padding, y = panel.y + 10f, w = Width - Padding * 2f;

        GUI.Label(new Rect(x, y, w, RowHeight), _item == null ? "Select an item to choose its affixes" : $"Affixes for {_item.Label}");
        y += RowHeight + 4f;
        if (_item == null) return;

        DrawPicks(x, ref y, w);

        var clicked = Options.Draw(x, ref y, w, IsPicked);
        if (clicked != null) Toggle(clicked);

        GUI.Label(new Rect(x, y, w, RowHeight), _message);
    }

    // Always the same height, so the list below doesn't move as affixes are added.
    private static void DrawPicks(float x, ref float y, float w)
    {
        int slots = Math.Min(_maxPrefixes + _maxSuffixes + _maxExperimental, _maxMods) + _maxCorrupted + _maxSpecial;
        for (int i = 0; i < slots; i++)
        {
            if (i < PickList.Count) DrawPick(PickList[i], x, y, w);
            else GUI.Label(new Rect(x, y, w, RowHeight), "(empty slot)");
            y += RowHeight;
        }
        y += RowHeight * (MaxSlotRows - slots) + 6f;
    }

    private static void DrawPick(AffixPick pick, float x, float y, float w)
    {
        const float SliderWidth = 110f, TierWidth = 34f, RemoveWidth = 26f;
        float labelWidth = w - SliderWidth - TierWidth - RemoveWidth - 12f;

        GUI.Label(new Rect(x, y, labelWidth, RowHeight), pick.Option.Label);
        GUI.Label(new Rect(x + labelWidth + 4f, y, TierWidth, RowHeight), $"T{pick.Tier + 1}");
        pick.Tier = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + labelWidth + TierWidth + 6f, y + 7f, SliderWidth, 16f), pick.Tier, 0, pick.Option.MaxTierIndex));
        if (GUI.Button(new Rect(x + w - RemoveWidth, y, RemoveWidth, RowHeight - 2f), "X")) PickList.Remove(pick);
    }

    private static bool IsPicked(AffixOption option) => PickList.Any(p => p.Option == option);

    private static void Toggle(AffixOption option)
    {
        _message = "";
        if (IsPicked(option))
        {
            PickList.RemoveAll(p => p.Option == option);
            return;
        }

        if (option.IsMod && PickList.Count(p => p.Option.IsMod) >= _maxMods)
        {
            _message = $"This item can have at most {_maxMods} chosen mods.";
            return;
        }

        int limit = LimitFor(option.Slot);
        if (PickList.Count(p => p.Option.Slot == option.Slot) >= limit)
        {
            _message = $"This item can have at most {limit} {option.Slot.ToString().ToLowerInvariant()} affix{(limit == 1 ? "" : "es")}.";
            return;
        }

        PickList.Add(new AffixPick(option));
    }

    private static int LimitFor(AffixSlot slot) => slot switch
    {
        AffixSlot.Prefix => _maxPrefixes,
        AffixSlot.Suffix => _maxSuffixes,
        AffixSlot.Experimental => _maxExperimental,
        AffixSlot.Corrupted => _maxCorrupted,
        _ => _maxSpecial,
    };
}
