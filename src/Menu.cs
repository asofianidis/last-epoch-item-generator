using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace LastEpochItemGenerator;

/// <summary>Item generator panel, toggled with F4.</summary>
internal static class Menu
{
    private const float Width = 520f, RowHeight = 26f, Padding = 14f, DesignHeight = 1080f, CorruptionLabelWidth = 80f;
    private const int MaxItemLevel = 100, MaxCount = 20, MaxLegendaryPotential = 4;

    private static readonly Rect Panel = new(40f, 120f, Width, 662f);
    private static readonly Rarity[] Rarities = (Rarity[])Enum.GetValues(typeof(Rarity));
    private static readonly CorruptionChoice[] CorruptionChoices = (CorruptionChoice[])Enum.GetValues(typeof(CorruptionChoice));
    private static readonly SearchList<CatalogEntry> Items = new(e => e.Label);

    private static bool _visible, _inputBlocked, _drawErrorLogged, _catalogLoaded, _catalogErrorLogged, _showUniques, _chooseAffixes, _makeLegendary;
    private static string _status = "";
    private static int _itemLevel = MaxItemLevel, _count = 1, _minLegendaryPotential;
    private static Rarity _rarity = Rarity.Rare;
    private static CorruptionChoice _corruption = CorruptionChoice.Off;
    private static CatalogEntry _selected;

    private static bool MakingLegendary => _makeLegendary && _showUniques && _selected is { CanBeLegendary: true };
    private static bool ChoosingAffixes => _chooseAffixes && (!_showUniques || MakingLegendary);

    internal static void Update()
    {
        if (Input.GetKeyDown(KeyCode.F4) && !IsGameTextInputActive())
        {
            _visible = !_visible;
            if (_visible && !_catalogLoaded) LoadCatalog();
        }

        // Typing in a search box must not trigger the game's hotkeys, so input stays off while the panel is open.
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

    internal static void DrawBackground(Rect panel)
    {
        var previous = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    internal static string Marked(string label, bool selected) => selected ? $"> {label} <" : label;

    private static void DrawPanel()
    {
        DrawBackground(Panel);

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

        var clicked = Items.Draw(x, ref y, w, e => e == _selected);
        if (clicked != null && clicked != _selected) Select(clicked);

        DrawOptions(x, ref y, w);
        DrawGenerate(x, y, w);

        if (ChoosingAffixes) AffixPanel.Draw(Panel);
    }

    private static void DrawModeTabs(float x, ref float y, float w)
    {
        float half = (w - 6f) / 2f;
        if (GUI.Button(new Rect(x, y, half, RowHeight), Marked("Base items", !_showUniques))) SetMode(false);
        if (GUI.Button(new Rect(x + half + 6f, y, half, RowHeight), Marked("Uniques and sets", _showUniques))) SetMode(true);
        y += RowHeight + 6f;
    }

    private static void DrawOptions(float x, ref float y, float w)
    {
        if (_showUniques)
        {
            if (_selected is { CanBeLegendary: false }) GUI.Label(new Rect(x, y, w, RowHeight), "This item can't become legendary.");
            else _makeLegendary = GUI.Toggle(new Rect(x, y, w, RowHeight - 2f), _makeLegendary, " Make legendary");
            y += RowHeight;

            // The conversion rolls its own Legendary Potential, so the slider has no effect on a legendary.
            if (MakingLegendary)
            {
                _chooseAffixes = GUI.Toggle(new Rect(x, y, w, RowHeight - 2f), _chooseAffixes, " Choose mods (random if none are chosen)");
                y += RowHeight;
            }
            else IntSlider(x, ref y, w, "Min Legendary Potential", ref _minLegendaryPotential, 0, MaxLegendaryPotential);
        }
        else
        {
            _chooseAffixes = GUI.Toggle(new Rect(x, y, w, RowHeight - 2f), _chooseAffixes, " Choose affixes");
            y += RowHeight;

            if (_chooseAffixes) GUI.Label(new Rect(x, y, w, RowHeight), "Rarity follows from the affixes you choose.");
            else _rarity = DrawChoice(x, y, w, Rarities, _rarity);
            y += RowHeight;
        }

        GUI.Label(new Rect(x, y, CorruptionLabelWidth, RowHeight), "Corruption");
        _corruption = DrawChoice(x + CorruptionLabelWidth, y, w - CorruptionLabelWidth, CorruptionChoices, _corruption);
        y += RowHeight;

        IntSlider(x, ref y, w, "Item level", ref _itemLevel, 1, MaxItemLevel);
        IntSlider(x, ref y, w, "Quantity", ref _count, 1, MaxCount);
        y += 6f;
    }

    private static T DrawChoice<T>(float x, float y, float w, T[] choices, T current) where T : struct, Enum
    {
        float width = (w - 6f * (choices.Length - 1)) / choices.Length;
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            if (GUI.Button(new Rect(x + i * (width + 6f), y, width, RowHeight - 2f), Marked(choice.ToString(), choice.Equals(current)))) current = choice;
        }
        return current;
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
            var affixes = ChoosingAffixes ? AffixPanel.Picks : null;
            _status = ItemSpawner.Spawn(new SpawnRequest(_selected, _rarity, _itemLevel, _minLegendaryPotential, MakingLegendary, _corruption, _count, affixes));
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

    private static void Select(CatalogEntry entry)
    {
        _selected = entry;
        if (entry == null || !entry.IsUniqueOrSet || entry.CanBeLegendary) AffixPanel.SetItem(entry);
    }

    private static void SetMode(bool showUniques)
    {
        if (_showUniques == showUniques) return;
        _showUniques = showUniques;
        Select(null);
        Items.SetSource(showUniques ? Catalog.Uniques : Catalog.BaseItems);
    }

    private static void LoadCatalog()
    {
        try
        {
            _catalogLoaded = Catalog.TryLoad();
            if (!_catalogLoaded) return;

            Melon<Mod>.Logger.Msg($"Catalog loaded: {Catalog.BaseItems.Count} base items, {Catalog.Uniques.Count} uniques and sets.");
            Items.SetSource(_showUniques ? Catalog.Uniques : Catalog.BaseItems);
        }
        catch (Exception ex)
        {
            // Retried every frame until the game has loaded, so only the first failure is logged.
            if (_catalogErrorLogged) return;
            _catalogErrorLogged = true;
            Melon<Mod>.Logger.Warning($"Catalog not ready: {ex.GetBaseException().Message}");
        }
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
