using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace LastEpochItemGenerator;

/// <summary>Item generator panel, toggled with F4.</summary>
internal static class Menu
{
    private const float Width = 520f, RowHeight = 26f, Padding = 14f, DesignHeight = 1080f;
    private const int PageSize = 12, MaxItemLevel = 100, MaxCount = 20, MaxLegendaryPotential = 4;

    private static readonly Rect Panel = new(40f, 120f, Width, 610f);
    private static readonly Rarity[] Rarities = (Rarity[])Enum.GetValues(typeof(Rarity));

    private static bool _visible, _inputBlocked, _drawErrorLogged, _catalogLoaded, _catalogErrorLogged, _showUniques;
    private static string _search = "", _status = "";
    private static int _page, _itemLevel = MaxItemLevel, _count = 1, _minLegendaryPotential;
    private static Rarity _rarity = Rarity.Rare;
    private static CatalogEntry _selected;
    private static List<CatalogEntry> _matches = new();

    internal static void Update()
    {
        if (Input.GetKeyDown(KeyCode.F4) && !IsGameTextInputActive())
        {
            _visible = !_visible;
            if (_visible) LoadCatalog();
        }

        // Typing in the search box must not trigger the game's hotkeys, so input stays off while the panel is open.
        BlockGameInput(_visible);
    }

    internal static void Draw()
    {
        if (!_visible) return;
        try
        {
            float scale = Math.Max(Screen.height / DesignHeight, 1f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            DrawPanel();
        }
        catch (Exception ex)
        {
            if (_drawErrorLogged) return;
            _drawErrorLogged = true;
            Melon<Mod>.Logger.Error($"Menu failed: {ex.GetBaseException().Message}");
        }
    }

    private static void DrawPanel()
    {
        var previous = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.96f);
        GUI.DrawTexture(Panel, Texture2D.whiteTexture);
        GUI.color = previous;

        float x = Panel.x + Padding, y = Panel.y + 10f, w = Width - Padding * 2f;

        GUI.Label(new Rect(x, y, w - 30f, RowHeight), "Item Generator");
        if (GUI.Button(new Rect(x + w - 26f, y, 26f, 24f), "X"))
        {
            _visible = false;
            return;
        }
        y += RowHeight + 4f;

        if (!Mod.Offline)
        {
            GUI.Label(new Rect(x, y, w, RowHeight), "Offline play only.");
            return;
        }

        if (!_catalogLoaded) LoadCatalog();
        if (!_catalogLoaded)
        {
            GUI.Label(new Rect(x, y, w, RowHeight), "Waiting for the game to load its item lists...");
            return;
        }

        DrawModeTabs(x, ref y, w);
        DrawSearch(x, ref y, w);
        DrawList(x, ref y, w);
        DrawOptions(x, ref y, w);
        DrawGenerate(x, y, w);
    }

    private static void DrawModeTabs(float x, ref float y, float w)
    {
        float half = (w - 6f) / 2f;
        if (GUI.Button(new Rect(x, y, half, RowHeight), Marked("Base items", !_showUniques))) SetMode(false);
        if (GUI.Button(new Rect(x + half + 6f, y, half, RowHeight), Marked("Uniques and sets", _showUniques))) SetMode(true);
        y += RowHeight + 6f;
    }

    private static void DrawSearch(float x, ref float y, float w)
    {
        GUI.Label(new Rect(x, y, 60f, RowHeight), "Search");
        string typed = GUI.TextField(new Rect(x + 60f, y, w - 60f, RowHeight - 2f), _search);
        if (typed != _search)
        {
            _search = typed;
            RefreshMatches();
        }
        y += RowHeight + 4f;
    }

    private static void DrawList(float x, ref float y, float w)
    {
        int pages = Math.Max(1, (_matches.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);

        foreach (var entry in _matches.Skip(_page * PageSize).Take(PageSize))
        {
            if (GUI.Button(new Rect(x, y, w, RowHeight - 2f), Marked(entry.Label, entry == _selected))) _selected = entry;
            y += RowHeight;
        }
        y += RowHeight * (PageSize - Math.Min(PageSize, _matches.Count - _page * PageSize));

        if (GUI.Button(new Rect(x, y, 70f, RowHeight - 2f), "Prev")) _page = Math.Max(0, _page - 1);
        if (GUI.Button(new Rect(x + w - 70f, y, 70f, RowHeight - 2f), "Next")) _page = Math.Min(pages - 1, _page + 1);
        GUI.Label(new Rect(x + 80f, y, w - 160f, RowHeight), $"Page {_page + 1} of {pages}  |  {_matches.Count} items");
        y += RowHeight + 6f;
    }

    private static void DrawOptions(float x, ref float y, float w)
    {
        if (_showUniques)
        {
            IntSlider(x, ref y, w, "Min Legendary Potential", ref _minLegendaryPotential, 0, MaxLegendaryPotential);
        }
        else
        {
            float width = (w - 6f * (Rarities.Length - 1)) / Rarities.Length;
            for (int i = 0; i < Rarities.Length; i++)
            {
                var rarity = Rarities[i];
                if (GUI.Button(new Rect(x + i * (width + 6f), y, width, RowHeight - 2f), Marked(rarity.ToString(), rarity == _rarity))) _rarity = rarity;
            }
            y += RowHeight;
        }

        IntSlider(x, ref y, w, "Item level", ref _itemLevel, 1, MaxItemLevel);
        IntSlider(x, ref y, w, "Quantity", ref _count, 1, MaxCount);
        y += 6f;
    }

    private static void DrawGenerate(float x, float y, float w)
    {
        if (GUI.Button(new Rect(x, y, w, RowHeight + 4f), _selected == null ? "Select an item above" : $"Generate {_selected.Label}") && _selected != null)
        {
            Generate();
        }
        y += RowHeight + 8f;
        GUI.Label(new Rect(x, y, w, RowHeight), _status);
    }

    private static void Generate()
    {
        try
        {
            _status = ItemSpawner.Spawn(new SpawnRequest(_selected, _rarity, _itemLevel, _minLegendaryPotential, _count));
        }
        catch (Exception ex)
        {
            _status = "Generation failed; see the log.";
            Melon<Mod>.Logger.Error($"Generating {_selected.Label} failed: {ex.GetBaseException()}");
        }
    }

    private static void IntSlider(float x, ref float y, float w, string label, ref int value, int min, int max)
    {
        GUI.Label(new Rect(x, y, 190f, RowHeight), $"{label}: {value}");
        value = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 195f, y + 7f, w - 195f, 16f), value, min, max));
        y += RowHeight;
    }

    private static string Marked(string label, bool selected) => selected ? $"> {label} <" : label;

    private static void SetMode(bool showUniques)
    {
        if (_showUniques == showUniques) return;
        _showUniques = showUniques;
        _selected = null;
        RefreshMatches();
    }

    private static void LoadCatalog()
    {
        try
        {
            _catalogLoaded = Catalog.TryLoad();
            if (!_catalogLoaded) return;

            Melon<Mod>.Logger.Msg($"Catalog loaded: {Catalog.BaseItems.Count} base items, {Catalog.Uniques.Count} uniques and sets.");
            RefreshMatches();
        }
        catch (Exception ex)
        {
            // Retried every frame until the game has loaded, so only the first failure is logged.
            if (_catalogErrorLogged) return;
            _catalogErrorLogged = true;
            Melon<Mod>.Logger.Warning($"Catalog not ready: {ex.GetBaseException().Message}");
        }
    }

    private static void RefreshMatches()
    {
        var source = _showUniques ? Catalog.Uniques : Catalog.BaseItems;
        _matches = source.Where(e => e.Label.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        _page = 0;
    }

    private static bool IsGameTextInputActive()
    {
        try
        {
            var input = EpochInputManager.instance;
            return input != null && input.IsInputFieldActive;
        }
        catch { return false; }
    }

    // Re-applied while wanted because a scene change creates a new input manager with the flag reset.
    private static void BlockGameInput(bool block)
    {
        try
        {
            var input = EpochInputManager.instance;
            if (input == null)
            {
                _inputBlocked = false;
                return;
            }
            if (block)
            {
                input.forceDisableInput = true;
                _inputBlocked = true;
            }
            else if (_inputBlocked)
            {
                input.forceDisableInput = false;
                _inputBlocked = false;
            }
        }
        catch { /* never let a menu problem interrupt the game */ }
    }
}
