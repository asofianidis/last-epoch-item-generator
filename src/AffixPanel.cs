using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LastEpochItemGenerator;

/// <summary>Second panel, beside the main one, for choosing the affixes on a base item.</summary>
internal static class AffixPanel
{
    private const float Width = 520f, RowHeight = 26f, Padding = 14f;

    private static readonly SearchList<AffixOption> Options = new(a => a.Label);
    private static readonly List<AffixPick> PickList = new();

    private static CatalogEntry _item;
    private static int _maxPrefixes, _maxSuffixes;
    private static string _message = "";

    internal static IReadOnlyList<AffixPick> Picks => PickList;

    internal static void SetItem(CatalogEntry item)
    {
        _item = item;
        PickList.Clear();
        _message = "";

        bool hasItem = item != null;
        Options.SetSource(hasItem ? Catalog.AffixesFor(item) : new List<AffixOption>());
        _maxPrefixes = _maxSuffixes = hasItem && Catalog.IsIdol(item) ? 1 : 2;
    }

    internal static void Draw(Rect beside)
    {
        var panel = new Rect(beside.xMax + 10f, beside.y, Width, beside.height);
        Menu.DrawBackground(panel);

        float x = panel.x + Padding, y = panel.y + 10f, w = Width - Padding * 2f;

        GUI.Label(new Rect(x, y, w, RowHeight), _item == null ? "Select a base item to choose its affixes" : $"Affixes for {_item.Label}");
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
        for (int i = 0; i < _maxPrefixes + _maxSuffixes; i++)
        {
            if (i < PickList.Count) DrawPick(PickList[i], x, y, w);
            else GUI.Label(new Rect(x, y, w, RowHeight), "(empty slot)");
            y += RowHeight;
        }
        y += RowHeight * (4 - (_maxPrefixes + _maxSuffixes)) + 6f;
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

        int limit = option.IsPrefix ? _maxPrefixes : _maxSuffixes;
        if (PickList.Count(p => p.Option.IsPrefix == option.IsPrefix) >= limit)
        {
            _message = $"This item can have at most {limit} {(option.IsPrefix ? "prefix" : "suffix")} affix{(limit == 1 ? "" : "es")}.";
            return;
        }

        PickList.Add(new AffixPick(option));
    }
}
