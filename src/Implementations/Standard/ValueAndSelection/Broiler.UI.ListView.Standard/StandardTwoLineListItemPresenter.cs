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
/// A reusable two-line list item presenter for rich content, presenting primary text and tertiary metadata
/// (such as timestamps or badges) on the first line, and secondary content on the second line.
/// </summary>
public sealed class StandardTwoLineListItemPresenter : IUiListItemPresenter
{
    public static readonly StandardTwoLineListItemPresenter Instance = new();

    /// <summary>How many characters of the primary text, before its ellipsis, the tertiary text leaves room for.</summary>
    private const int MinimumPrimaryCharacters = 3;

    /// <summary>The gap between the primary text and the tertiary text.</summary>
    private const double TertiaryGap = 10;

    public double GetItemHeight(UiListItem? item, UiDensity density, double availableWidth) =>
        density switch
        {
            UiDensity.Compact => 38,
            UiDensity.Spacious => 64,
            _ => 52,
        };

    /// <summary>
    /// At least the density's height, and tall enough for both lines in <paramref name="font"/> with the
    /// margin the density leaves around them at the default font, so the default size is unchanged.
    /// </summary>
    public double GetItemHeight(UiListItem? item, UiDensity density, double availableWidth, BFontStyle font)
    {
        ArgumentNullException.ThrowIfNull(font);
        double height = GetItemHeight(item, density, availableWidth);
        // Not clamped: compact rows are slightly shorter than their two lines at the default font, and
        // that geometry is kept rather than changed at the default size.
        double margin = height - LinesHeight(BFontStyle.Default);
        return Math.Max(height, Math.Ceiling(LinesHeight(font) + margin));
    }

    private static double LinesHeight(BFontStyle font) =>
        BTextMeasurer.GetLineHeight(font) + 2 + BTextMeasurer.GetLineHeight(font with { Size = Math.Max(10, font.Size - 1) });

    private static double TopPadding(UiDensity density) => density switch
    {
        UiDensity.Compact => 4,
        UiDensity.Spacious => 10,
        _ => 6,
    };

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

        bool isUnread = !context.State.IsRead;
        bool isSelected = context.State.IsSelected;
        BColor foreground = isSelected ? context.SelectedForeground : context.Foreground;
        BColor secondaryForeground = isSelected ? context.SelectedSecondaryForeground : context.SecondaryForeground;
        double primaryLineHeight = BTextMeasurer.GetLineHeight(context.Font);
        double primaryTop = bounds.Top + TopPadding(context.State.Density);

        double primaryLeft = bounds.Left + 10;

        // Unread indicator dot
        if (isUnread)
        {
            double dotSize = 6;
            double dotTop = primaryTop + Math.Max(0, (primaryLineHeight - dotSize) / 2);
            list.FillRect(new BRect(primaryLeft, dotTop, dotSize, dotSize), UnreadDotColor(context));
            primaryLeft += dotSize + 6;
        }

        BFontStyle primaryFont = isUnread ? context.Font with { Weight = BFontWeight.Bold } : context.Font;
        double primaryRoom = Math.Max(0, bounds.Right - 8 - primaryLeft);

        // Line 1: Tertiary text (right aligned, 8 DIP before the edge). It is drawn whole or not at all, since a
        // shortened date or time reads as a different one ("10:..." for "10:42"). It is drawn only when it fits
        // beside the gap and the least of the primary text that is still readable: its first few characters and
        // an ellipsis, or all of it when that is shorter, and never less than 10 DIP, the least a date that fit
        // left it before. In a narrower row it is left out and the primary text gets the line; the semantic
        // node keeps it either way.
        double tertiaryWidth = 0;
        if (!string.IsNullOrEmpty(context.Item.TertiaryText))
        {
            BFontStyle tertiaryFont = context.Font with { Size = Math.Max(9, context.Font.Size - 2) };
            double width = BTextMeasurer.MeasureAdvance(context.Item.TertiaryText, tertiaryFont);
            double primaryMinimum = Math.Max(TertiaryGap, MinimumPrimaryWidth(context.Item.Text, primaryFont));
            if (width + TertiaryGap + primaryMinimum <= primaryRoom)
            {
                tertiaryWidth = width;
                list.DrawText(new BTextRun(context.Item.TertiaryText, tertiaryFont, secondaryForeground), new BPoint(bounds.Right - 8 - width, primaryTop + 1));
            }
        }

        // Line 1: Primary text (sender / title)
        double maxPrimaryWidth = Math.Max(0, primaryRoom - (tertiaryWidth > 0 ? tertiaryWidth + TertiaryGap : 0));
        string primaryText = DefaultListItemPresenter.TruncateWithEllipsis(context.Item.Text, primaryFont, maxPrimaryWidth);
        if (!string.IsNullOrEmpty(primaryText))
        {
            list.DrawText(new BTextRun(primaryText, primaryFont, foreground), new BPoint(primaryLeft, primaryTop));
        }

        // Line 2: Secondary text (subject / snippet)
        if (!string.IsNullOrEmpty(context.Item.SecondaryText))
        {
            double secondaryTop = primaryTop + primaryLineHeight + 2;
            BFontStyle secondaryFont = context.Font with { Size = Math.Max(10, context.Font.Size - 1) };
            double secondaryLeft = bounds.Left + 10;
            double maxSecondaryWidth = Math.Max(0, bounds.Width - 20);
            string secondaryText = DefaultListItemPresenter.TruncateWithEllipsis(context.Item.SecondaryText, secondaryFont, maxSecondaryWidth);
            if (!string.IsNullOrEmpty(secondaryText))
            {
                list.DrawText(new BTextRun(secondaryText, secondaryFont, secondaryForeground), new BPoint(secondaryLeft, secondaryTop));
            }
        }

        if (context.State.IsFocused && context.State.IsSelected)
        {
            DefaultListItemPresenter.StrokeFocusRing(context);
        }
    }

    /// <summary>
    /// The color of the unread dot: a mark that is not text, so it needs 3:1 against the fill it is drawn on, the
    /// row's selection fill or the list's background. A selection with a text color of its own can share its fill
    /// with the accent (a system highlight pair is both), so the dot takes the selected text color there. Otherwise
    /// it is the accent where that stands out from the fill, then the accent text shade, which a theme chooses to
    /// read on its surfaces and its selection fill (Dark's accent is 2.76:1 on its selection fill, its accent text
    /// 6.27:1), and failing both the row's text color. A fill that is not opaque keeps the accent: what shows
    /// through it is not known.
    /// </summary>
    private static BColor UnreadDotColor(UiListItemRenderContext context)
    {
        bool isSelected = context.State.IsSelected;
        if (isSelected && context.SelectedForeground != context.Foreground)
            return context.SelectedForeground;

        BColor fill = isSelected ? context.SelectedBackground : context.Background;
        if (fill.A != 255 || StandardContrast.Ratio(context.Accent, fill) >= StandardContrast.AaLargeOrUi)
            return context.Accent;
        if (StandardContrast.Ratio(context.AccentText, fill) >= StandardContrast.AaLargeOrUi)
            return context.AccentText;
        return isSelected ? context.SelectedForeground : context.Foreground;
    }

    /// <summary>
    /// The narrowest the primary text is made to give the tertiary text room: its first
    /// <see cref="MinimumPrimaryCharacters"/> characters and an ellipsis, measured the way
    /// <see cref="DefaultListItemPresenter.TruncateWithEllipsis"/> measures them so that it keeps them, or the
    /// whole text when that is no wider.
    /// </summary>
    private static double MinimumPrimaryWidth(string text, BFontStyle font)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int length = 0;
        for (int count = 0; count < MinimumPrimaryCharacters && length < text.Length; count++)
            length += StringInfo.GetNextTextElementLength(text.AsSpan(length));

        double whole = BTextMeasurer.MeasureAdvance(text, font);
        if (length >= text.Length)
            return whole;

        double shortened = BTextMeasurer.MeasureAdvance(text[..length], font) + BTextMeasurer.MeasureAdvance(DefaultListItemPresenter.Ellipsis, font);
        return Math.Min(whole, shortened);
    }

    public UiSemanticNode CreateSemanticNode(UiListItemSemanticContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
        if (context.State.IsSelected)
            state |= UiSemanticState.Selected;
        if (context.State.IsFocused)
            state |= UiSemanticState.Focused;

        string unreadPrefix = !context.State.IsRead ? "Unread, " : string.Empty;
        string tertiarySuffix = !string.IsNullOrEmpty(context.Item.TertiaryText) ? $", {context.Item.TertiaryText}" : string.Empty;
        string secondaryPart = !string.IsNullOrEmpty(context.Item.SecondaryText) ? $", {context.Item.SecondaryText}" : string.Empty;
        string fullLabel = $"{unreadPrefix}{context.Item.Text}{secondaryPart}{tertiarySuffix}";

        return new UiSemanticNode(
            UiSemanticRole.ListItem,
            fullLabel,
            context.Bounds,
            state,
            []);
    }
}
