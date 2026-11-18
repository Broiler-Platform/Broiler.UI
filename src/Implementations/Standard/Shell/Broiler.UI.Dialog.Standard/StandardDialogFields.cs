using System;
using Broiler.Graphics.Geometry;
using Broiler.UI.Button.Standard;

namespace Broiler.UI.Dialog.Standard;

/// <summary>Arranges form rows above a right-aligned dialog button bar.</summary>
public sealed class StandardDialogFields : UiElement
{
    private readonly UiElement[] _rows;
    private readonly StandardButton[] _buttons;
    private readonly UiElement? _stretchRow;

    public StandardDialogFields(UiElement[] rows, StandardButton[] buttons, UiElement? stretchRow = null)
    {
        _rows = rows;
        _buttons = buttons;
        _stretchRow = stretchRow;
        foreach (UiElement row in rows)
            AddChild(row);
        foreach (StandardButton button in buttons)
            AddChild(button);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double rowHeight = RowHeight(availableSize.Height);
        foreach (UiElement row in _rows)
            row.Measure(new BSize(availableSize.Width, HeightOf(row, availableSize.Height, rowHeight)));
        foreach (StandardButton button in _buttons)
            button.Measure(new BSize(ButtonWidth(availableSize.Width), rowHeight));
        return availableSize;
    }

    protected override void ArrangeCore(BRect rect)
    {
        double height = RowHeight(rect.Height);
        double top = rect.Top;
        foreach (UiElement row in _rows)
        {
            double rowHeight = HeightOf(row, rect.Height, height);
            row.Arrange(new BRect(rect.Left, top, rect.Width, rowHeight));
            top += rowHeight + 4;
        }
        double width = ButtonWidth(rect.Width);
        double left = rect.Right - _buttons.Length * (width + 6) + 6;
        foreach (StandardButton button in _buttons)
        {
            button.Arrange(new BRect(left, rect.Bottom - height, width, height));
            left += width + 6;
        }
    }

    private double RowHeight(double height) => Math.Clamp((height - 4 * _rows.Length) / (_rows.Length + 1), 0, 30);
    private double HeightOf(UiElement row, double available, double height) => ReferenceEquals(row, _stretchRow)
        ? Math.Max(0, available - _rows.Length * (height + 4)) : height;
    private double ButtonWidth(double width) => Math.Clamp((width + 6) / _buttons.Length - 6, 0, 102);
}
