using System;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.ColorDialog;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=7; Fingerprint=TBF
// Broiler-Human:        PENDING
public abstract class UiColorDialog : UiDialog
{
    private BColor _selectedColor = BColor.Black;

    protected UiColorDialog()
    {
        CanResize = true;
    }

    public event EventHandler? SelectedColorChanged;

    public BColor SelectedColor
    {
        get => _selectedColor;
        set
        {
            ThrowIfDisposed();
            if (_selectedColor == value)
                return;

            _selectedColor = value;
            OnSelectedColorChanged();
            SelectedColorChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool AcceptSelection() => Accept(FormatColorValue(SelectedColor));

    public Task<UiDialogResult> ShowColorModal(UiWindow owner, BRect placement = default) =>
        ShowModal(owner, placement);

    public Task<UiDialogResult> ShowColorModeless(UiWindow owner, BRect placement = default) =>
        ShowModeless(owner, placement);

    public static string FormatColorValue(BColor color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    public static bool TryParseColorValue(string? value, out BColor color)
    {
        color = BColor.Black;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('#') || value.Length != 9)
            return false;

        try
        {
            byte r = byte.Parse(value.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(value.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(value.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            byte a = byte.Parse(value.Substring(7, 2), System.Globalization.NumberStyles.HexNumber);
            color = BColor.FromArgb(a, r, g, b);
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected virtual void OnSelectedColorChanged()
    {
    }
}
