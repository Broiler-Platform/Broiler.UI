using System;
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

    public double GetItemHeight(UiListItem? item, UiDensity density, double availableWidth) =>
        density switch
        {
            UiDensity.Compact => 38,
            UiDensity.Spacious => 64,
            _ => 52,
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
        double primaryLineHeight = BTextMeasurer.GetLineHeight(context.Font);
        double primaryTop = bounds.Top + (context.State.Density switch
        {
            UiDensity.Compact => 4,
            UiDensity.Spacious => 10,
            _ => 6,
        });

        double primaryLeft = bounds.Left + 10;

        // Unread indicator dot
        if (isUnread)
        {
            double dotSize = 6;
            double dotTop = primaryTop + Math.Max(0, (primaryLineHeight - dotSize) / 2);
            list.FillRect(new BRect(primaryLeft, dotTop, dotSize, dotSize), context.Accent);
            primaryLeft += dotSize + 6;
        }

        // Line 1: Tertiary text (right aligned)
        double tertiaryWidth = 0;
        if (!string.IsNullOrEmpty(context.Item.TertiaryText))
        {
            BFontStyle tertiaryFont = context.Font with { Size = Math.Max(9, context.Font.Size - 2) };
            tertiaryWidth = BTextMeasurer.MeasureAdvance(context.Item.TertiaryText, tertiaryFont);
            double tertiaryLeft = Math.Max(primaryLeft + 20, bounds.Right - 8 - tertiaryWidth);
            list.DrawText(new BTextRun(context.Item.TertiaryText, tertiaryFont, context.SecondaryForeground), new BPoint(tertiaryLeft, primaryTop + 1));
        }

        // Line 1: Primary text (sender / title)
        double maxPrimaryWidth = Math.Max(0, bounds.Right - 8 - (tertiaryWidth > 0 ? tertiaryWidth + 10 : 0) - primaryLeft);
        BFontStyle primaryFont = isUnread ? context.Font with { Weight = BFontWeight.Bold } : context.Font;
        string primaryText = DefaultListItemPresenter.TruncateWithEllipsis(context.Item.Text, primaryFont, maxPrimaryWidth);
        if (!string.IsNullOrEmpty(primaryText))
        {
            list.DrawText(new BTextRun(primaryText, primaryFont, context.Foreground), new BPoint(primaryLeft, primaryTop));
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
                list.DrawText(new BTextRun(secondaryText, secondaryFont, context.SecondaryForeground), new BPoint(secondaryLeft, secondaryTop));
            }
        }

        if (context.State.IsFocused && context.State.IsSelected)
        {
            list.StrokeRect(StandardControlPaint.Inset(bounds, 2), context.FocusRing, 1);
        }
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
