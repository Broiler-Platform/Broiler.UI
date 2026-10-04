using System;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;

namespace Broiler.UI.ListView;

/// <summary>
/// Provides context for rendering an individual list item cell.
/// </summary>
public sealed class UiListItemRenderContext
{
    private BColor? _selectedForeground;
    private BColor? _selectedSecondaryForeground;

    public required BRenderList RenderList { get; init; }
    public required BRect Bounds { get; init; }
    public required UiListItem Item { get; init; }
    public required UiListItemState State { get; init; }
    public required BFontStyle Font { get; init; }
    public required BColor Foreground { get; init; }
    public required BColor SecondaryForeground { get; init; }
    public required BColor Background { get; init; }
    public required BColor SelectedBackground { get; init; }
    public required BColor FocusRing { get; init; }
    public required BColor Accent { get; init; }
    public bool IsHighContrast { get; init; }

    /// <summary>
    /// The color of a selected item's text, drawn on <see cref="SelectedBackground"/>. It is
    /// <see cref="Foreground"/> unless the list gives selected text a color of its own.
    /// </summary>
    public BColor SelectedForeground
    {
        get => _selectedForeground ?? Foreground;
        init => _selectedForeground = value;
    }

    /// <summary>
    /// The color of a selected item's secondary text (a second line, a date), drawn on
    /// <see cref="SelectedBackground"/>. It is <see cref="SecondaryForeground"/> unless the list gives it a
    /// color of its own.
    /// </summary>
    public BColor SelectedSecondaryForeground
    {
        get => _selectedSecondaryForeground ?? SecondaryForeground;
        init => _selectedSecondaryForeground = value;
    }

    /// <summary>
    /// A copy of this context that renders <paramref name="item"/> instead, with every color, font, bound
    /// and state carried over, including members added after the caller was written. A presenter that adapts
    /// the item it was given and hands it to another presenter should use this rather than copying the
    /// members one by one, which silently drops any it does not know about.
    /// </summary>
    public UiListItemRenderContext WithItem(UiListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new UiListItemRenderContext
        {
            RenderList = RenderList,
            Bounds = Bounds,
            Item = item,
            State = State,
            Font = Font,
            Foreground = Foreground,
            SecondaryForeground = SecondaryForeground,
            Background = Background,
            SelectedBackground = SelectedBackground,
            FocusRing = FocusRing,
            Accent = Accent,
            IsHighContrast = IsHighContrast,
            _selectedForeground = _selectedForeground,
            _selectedSecondaryForeground = _selectedSecondaryForeground,
        };
    }
}
