using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.ProgressBar.Standard;

public sealed class StandardProgressBar : UiProgressBar, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        FillColor = theme.Accent;
        TrackColor = theme.SurfaceDisabled;
        ValueTextColor = theme.OnAccent;
    }

    private static readonly BSize DefaultHorizontalSize = new(160, 16);

    private BColor _trackColor = BColor.FromArgb(0xFF, 0xE6, 0xEA, 0xF0);
    private BColor _fillColor = StandardControlPaint.Accent;
    private BColor _borderColor = BColor.Transparent;
    private BColor _valueTextColor = BColor.White;
    private BFontStyle _valueTextFont = new("Segoe UI", 12, BFontWeight.SemiBold);
    private bool _showValueText = true;
    private double _cornerRadius = StandardControlPaint.PillRadius;
    private double _indeterminateSegmentFraction = 0.35;

    public BColor TrackColor
    {
        get => _trackColor;
        set
        {
            if (_trackColor == value) return;
            _trackColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value) return;
            _fillColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor == value) return;
            _borderColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor ValueTextColor
    {
        get => _valueTextColor;
        set
        {
            if (_valueTextColor == value) return;
            _valueTextColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BFontStyle ValueTextFont
    {
        get => _valueTextFont;
        set
        {
            if (_valueTextFont == value) return;
            _valueTextFont = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public bool ShowValueText
    {
        get => _showValueText;
        set
        {
            if (_showValueText == value) return;
            _showValueText = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (_cornerRadius == value) return;
            _cornerRadius = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public double IndeterminateSegmentFraction
    {
        get => _indeterminateSegmentFraction;
        set
        {
            if (_indeterminateSegmentFraction == value) return;
            _indeterminateSegmentFraction = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize desired = Orientation == UiProgressBarOrientation.Vertical && PreferredSize == DefaultHorizontalSize
            ? new BSize(16, 160)
            : PreferredSize;
        return new BSize(ClampDesired(desired.Width, availableSize.Width), ClampDesired(desired.Height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, Bounds, TrackColor, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);

        BRect fill = IsIndeterminate ? GetIndeterminateFillRect() : GetDeterminateFillRect();
        if (!fill.IsEmpty)
            StandardControlPaint.FillRounded(context.RenderList, fill, FillColor, CornerRadius);

        if (ShowValueText && !IsIndeterminate && Bounds.Width >= 44 && Bounds.Height >= 14)
            DrawValueText(context, fill);
    }

    private void DrawValueText(UiRenderContext context, BRect fill)
    {
        string text = Math.Round(NormalizedValue * 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        BSize textSize = BTextMeasurer.Measure(text, ValueTextFont).Size;
        double x = fill.Width > textSize.Width + 12
            ? fill.Right - textSize.Width - 6
            : Bounds.Left + Math.Max(0, (Bounds.Width - textSize.Width) / 2);
        double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);
        context.RenderList.DrawText(new BTextRun(text, ValueTextFont, ValueTextColor), new BPoint(x, y));
    }

    private BRect GetDeterminateFillRect()
    {
        double visual = NormalizedValue;
        visual = Math.Clamp(visual, 0, 1);

        if (Orientation == UiProgressBarOrientation.Vertical)
        {
            double filled = Bounds.Height * visual;
            return IsDirectionReversed
                ? new BRect(Bounds.Left, Bounds.Top, Bounds.Width, filled)
                : new BRect(Bounds.Left, Bounds.Bottom - filled, Bounds.Width, filled);
        }

        double width = Bounds.Width * visual;
        return IsDirectionReversed
            ? new BRect(Bounds.Right - width, Bounds.Top, width, Bounds.Height)
            : new BRect(Bounds.Left, Bounds.Top, width, Bounds.Height);
    }

    private BRect GetIndeterminateFillRect()
    {
        double segment = Math.Clamp(IndeterminateSegmentFraction, 0.1, 1);
        double phase = IsReducedMotion || Session is null
            ? 0.5
            : (Session.Clock.Now.Elapsed.TotalSeconds % 1.4) / 1.4;

        if (Orientation == UiProgressBarOrientation.Vertical)
        {
            double height = Bounds.Height * segment;
            double top = Bounds.Top + ((Bounds.Height - height) * phase);
            if (IsDirectionReversed)
                top = Bounds.Bottom - height - ((Bounds.Height - height) * phase);
            return new BRect(Bounds.Left, top, Bounds.Width, height);
        }

        double width = Bounds.Width * segment;
        double left = Bounds.Left + ((Bounds.Width - width) * phase);
        if (IsDirectionReversed)
            left = Bounds.Right - width - ((Bounds.Width - width) * phase);
        return new BRect(left, Bounds.Top, width, Bounds.Height);
    }

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
