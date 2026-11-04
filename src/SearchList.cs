using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LastEpochItemGenerator;

/// <summary>A search box above a paged list of buttons.</summary>
internal sealed class SearchList<T> where T : class
{
    private const float RowHeight = 26f;
    private const int PageSize = 12;

    private readonly Func<T, string> _label;
    private IReadOnlyList<T> _source = Array.Empty<T>();
    private List<T> _matches = new();
    private string _search = "";
    private int _page;

    internal SearchList(Func<T, string> label) => _label = label;

    internal void SetSource(IReadOnlyList<T> source)
    {
        _source = source;
        Refresh();
    }

    /// <summary>Returns the entry clicked this frame, or null.</summary>
    internal T Draw(float x, ref float y, float w, Func<T, bool> isSelected)
    {
        DrawSearch(x, ref y, w);

        int pages = Math.Max(1, (_matches.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);

        T clicked = null;
        foreach (var entry in _matches.Skip(_page * PageSize).Take(PageSize))
        {
            if (GUI.Button(new Rect(x, y, w, RowHeight - 2f), Menu.Marked(_label(entry), isSelected(entry)))) clicked = entry;
            y += RowHeight;
        }
        y += RowHeight * (PageSize - Math.Min(PageSize, _matches.Count - _page * PageSize));

        if (GUI.Button(new Rect(x, y, 70f, RowHeight - 2f), "Prev")) _page = Math.Max(0, _page - 1);
        if (GUI.Button(new Rect(x + w - 70f, y, 70f, RowHeight - 2f), "Next")) _page = Math.Min(pages - 1, _page + 1);
        GUI.Label(new Rect(x + 80f, y, w - 160f, RowHeight), $"Page {_page + 1} of {pages}  |  {_matches.Count} found");
        y += RowHeight + 6f;

        return clicked;
    }

    private void DrawSearch(float x, ref float y, float w)
    {
        GUI.Label(new Rect(x, y, 60f, RowHeight), "Search");
        string typed = GUI.TextField(new Rect(x + 60f, y, w - 60f, RowHeight - 2f), _search);
        if (typed != _search)
        {
            _search = typed;
            Refresh();
        }
        y += RowHeight + 4f;
    }

    private void Refresh()
    {
        _matches = _source.Where(e => _label(e).Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        _page = 0;
    }
}
