using System;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.ListView.Standard;

/// <summary>
/// Default single-line list item presenter with Unicode-aware ellipsis trimming and accessibility semantics.
/// </summary>
public sealed class DefaultListItemPresenter : IUiListItemPresenter
{
    public static readonly DefaultListItemPresenter Instance = new();

    /// <summary>What <see cref="TruncateWithEllipsis"/> appends to text it shortens.</summary>
    internal const string Ellipsis = "...";

    public double GetItemHeight(UiListItem? item, UiDensity density, double availableWidth) =>
        density switch
        {
            UiDensity.Compact => 24,
            UiDensity.Spacious => 36,
            _ => 28,
        };

    /// <summary>At least the density's height, and tall enough for a line in <paramref name="font"/> with the same margin.</summary>
    public double GetItemHeight(UiListItem? item, UiDensity density, double availableWidth, BFontStyle font)
    {
        ArgumentNullException.ThrowIfNull(font);
        double height = GetItemHeight(item, density, availableWidth);
        double margin = Math.Max(0, height - BTextMeasurer.GetLineHeight(BFontStyle.Default));
        return Math.Max(height, Math.Ceiling(BTextMeasurer.GetLineHeight(font) + margin));
    }

    public void Render(UiListItemRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BRect bounds = context.Bounds;
        BRenderList list = context.RenderList;

        if (context.State.IsSelected)
        {
            BRect selectionRect = StandardControlPaint.Inset(bounds, 2);
            list.FillRect(selectionRect, context.SelectedBackground);
            if (context.IsHighContrast)
                list.StrokeRect(selectionRect, context.Foreground, 1);
        }

        double lineHeight = BTextMeasurer.GetLineHeight(context.Font);
        double textTop = bounds.Top + Math.Max(0, (bounds.Height - lineHeight) / 2);
        double textLeft = bounds.Left + 8;
        double maxTextWidth = Math.Max(0, bounds.Width - 16);

        string trimmed = TruncateWithEllipsis(context.Item.Text, context.Font, maxTextWidth);
        if (!string.IsNullOrEmpty(trimmed))
        {
            BColor foreground = context.State.IsSelected ? context.SelectedForeground : context.Foreground;
            list.DrawText(new BTextRun(trimmed, context.Font, foreground), new BPoint(textLeft, textTop));
        }

        if (context.State.IsFocused && context.State.IsSelected)
        {
            StrokeFocusRing(context);
        }
    }

    /// <summary>
    /// The focus ring of a focused, selected row, 2 DIP inside the row. In high contrast it is drawn 2 DIP
    /// inside the selection outline instead of over it, so both cues stay visible, and in the selected text
    /// color when the focus ring color does not stand out against the selection fill, as when a palette built
    /// from a system highlight pair uses the highlight for both.
    /// </summary>
    internal static void StrokeFocusRing(UiListItemRenderContext context)
    {
        if (!context.IsHighContrast)
        {
            context.RenderList.StrokeRect(StandardControlPaint.Inset(context.Bounds, 2), context.FocusRing, 1);
            return;
        }

        BColor ring = StandardContrast.Ratio(context.FocusRing, context.SelectedBackground) >= StandardContrast.AaLargeOrUi
            ? context.FocusRing
            : context.SelectedForeground;
        context.RenderList.StrokeRect(StandardControlPaint.Inset(context.Bounds, 4), ring, 1);
    }

    public UiSemanticNode CreateSemanticNode(UiListItemSemanticContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
        if (context.State.IsSelected)
            state |= UiSemanticState.Selected;
        if (context.State.IsFocused)
            state |= UiSemanticState.Focused;

        return new UiSemanticNode(
            UiSemanticRole.ListItem,
            context.Item.Text,
            context.Bounds,
            state,
            []);
    }

    public static string TruncateWithEllipsis(string text, BFontStyle font, double maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return string.Empty;

        if (BTextMeasurer.MeasureAdvance(text, font) <= maxWidth)
            return text;

        double ellipsisWidth = BTextMeasurer.MeasureAdvance(Ellipsis, font);
        if (ellipsisWidth > maxWidth)
            return string.Empty;

        string result = string.Empty;
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            string candidate = result + element;
            if (BTextMeasurer.MeasureAdvance(candidate, font) + ellipsisWidth > maxWidth)
                break;

            result = candidate;
        }

        return result + Ellipsis;
    }
}
