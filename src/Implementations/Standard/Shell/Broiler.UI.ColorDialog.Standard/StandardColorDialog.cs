using System;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Slider.Standard;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.ColorDialog.Standard;

public sealed class StandardColorDialog : UiColorDialog, IStandardThemedControl
{
    private const double Padding = 16;
    private const double Gap = 10;
    private const int PaletteColumns = 8;
    private const int PaletteRows = 3;

    private static readonly (string Name, uint Rgb)[] Presets =
    [
        ("Black", 0x000000), ("Charcoal", 0x343A40), ("Slate", 0x64748B), ("Gray", 0x808080),
        ("Silver", 0xC0C0C0), ("Mist", 0xE2E8F0), ("Ivory", 0xFFF8E7), ("White", 0xFFFFFF),
        ("Red", 0xFF0000), ("Orange", 0xFFA500), ("Yellow", 0xFFFF00), ("Lime", 0x00FF00),
        ("Green", 0x008000), ("Cyan", 0x00FFFF), ("Blue", 0x0000FF), ("Magenta", 0xFF00FF),
        ("Rose", 0xBE185D), ("Coral", 0xFF7F50), ("Gold", 0xD4A017), ("Olive", 0x808000),
        ("Teal", 0x008080), ("Sky", 0x38BDF8), ("Indigo", 0x4F46E5), ("Violet", 0x8B5CF6),
    ];

    private readonly UiWindowChromeController _chrome;
    private readonly StandardButton[] _swatches;
    private readonly Channel[] _channels;
    private readonly StandardEdit _hexEdit;
    private readonly StandardButton _resetButton;
    private readonly StandardButton _cancelButton;
    private readonly StandardButton _okButton;

    private BRect _planeLabel;
    private BRect _plane;
    private BRect _paletteLabel;
    private BRect _valuesLabel;
    private BRect _originalPreview;
    private BRect _currentPreview;
    private BRect _previewLabels;
    private BRect _hexLabel;
    private BRect _hexHint;
    private BColor _originalColor;
    private BColor _accent = StandardControlPaint.Accent;
    private BFontStyle _labelFont = StandardControlPaint.Theme.FontBody;
    private double _hue;
    private double _saturation;
    private double _value;
    private bool _syncingControls;
    private bool _settingHsv;
    private bool _editingHex;
    private bool _hexValid = true;
    private bool _draggingPlane;

    public StandardColorDialog()
    {
        Title = "Color";
        _chrome = new UiWindowChromeController(this) { Metrics = UiWindowChromeMetrics.Compact };

        _swatches = new StandardButton[Presets.Length];
        for (int index = 0; index < Presets.Length; index++)
        {
            var (name, rgb) = Presets[index];
            BColor color = BColor.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            var swatch = new StandardButton
            {
                Text = name,
                AccessibleName = name + " preset",
                ToolTipText = $"{name}  #{rgb:X6}",
                IconExtent = 18,
                IconPainter = (list, bounds, _) =>
                {
                    list.FillRect(bounds, color);
                    list.StrokeRect(bounds, color == SelectedColor ? _accent : BorderColor, color == SelectedColor ? 2 : 1);
                },
            };
            swatch.Clicked += (_, _) => SetColor(color);
            _swatches[index] = swatch;
            AddChild(swatch);
        }

        _channels =
        [
            CreateChannel("Hue °", "Hue in degrees", 360, value => SetHsv(value, _saturation, _value)),
            CreateChannel("Sat. %", "Saturation percent", 100, value => SetHsv(_hue, value / 100, _value)),
            CreateChannel("Value %", "Value percent", 100, value => SetHsv(_hue, _saturation, value / 100)),
            CreateChannel("Red", "Red", 255, value => SetColor(BColor.FromArgb(SelectedColor.A, (byte)value, SelectedColor.G, SelectedColor.B))),
            CreateChannel("Green", "Green", 255, value => SetColor(BColor.FromArgb(SelectedColor.A, SelectedColor.R, (byte)value, SelectedColor.B))),
            CreateChannel("Blue", "Blue", 255, value => SetColor(BColor.FromArgb(SelectedColor.A, SelectedColor.R, SelectedColor.G, (byte)value))),
            CreateChannel("Alpha", "Alpha (0–255)", 255, SetAlpha),
        ];

        _hexEdit = new StandardEdit { AccessibleName = "Hex color", ToolTipText = "RGB or RGBA hex, with optional #" };
        _hexEdit.TextChanged += (_, _) => UpdateFromHex();
        _hexEdit.Submitted += (_, _) =>
        {
            if (_hexValid)
                SyncControls();
        };
        AddChild(_hexEdit);

        _resetButton = new StandardButton { Text = "Reset", ToolTipText = "Restore the original color" };
        _resetButton.Clicked += (_, _) => SetColor(_originalColor);
        _cancelButton = new StandardButton { Text = "Cancel" };
        _cancelButton.Clicked += (_, _) => Cancel();
        _okButton = new StandardButton { Text = "OK", IsDefault = true };
        _okButton.Clicked += (_, _) => ConfirmColor();
        AddChild(_resetButton);
        AddChild(_cancelButton);
        AddChild(_okButton);

        _originalColor = SelectedColor;
        ReadHsv();
        SyncControls();
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;
    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF6, 0xFB);
    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;
    public BColor BorderColor { get; set; } = StandardControlPaint.Border;
    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;
    public BSize PreferredSize { get; set; } = new(680, 486);

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        BorderColor = theme.Border;
        _accent = theme.Accent;
        _labelFont = theme.FontBody;
    }

    protected override void OnOpened()
    {
        _originalColor = SelectedColor;
        base.OnOpened();
    }

    protected override void OnSelectedColorChanged()
    {
        bool keepHsv = _settingHsv;
        _settingHsv = false;
        if (!keepHsv)
            ReadHsv();
        SyncControls();
    }

    private Channel CreateChannel(string label, string name, double maximum, Action<double> setValue)
    {
        var slider = new StandardSlider { AccessibleName = name + " slider", Maximum = maximum, StepFrequency = 1 };
        var input = new StandardSpinBox { AccessibleName = name, Maximum = maximum };
        slider.ValueChanged += (_, _) =>
        {
            if (!_syncingControls)
                setValue(slider.Value);
        };
        input.ValueChanged += (_, _) =>
        {
            if (!_syncingControls)
                setValue(input.Value);
        };
        AddChild(slider);
        AddChild(input);
        return new Channel(label, slider, input);
    }

    private void SyncControls()
    {
        BColor color = SelectedColor;
        Span<double> values = [Math.Round(_hue), Math.Round(_saturation * 100), Math.Round(_value * 100),
            color.R, color.G, color.B, color.A];
        _syncingControls = true;
        try
        {
            for (int index = 0; index < _channels.Length; index++)
            {
                _channels[index].Slider.Value = values[index];
                _channels[index].Input.Value = values[index];
            }

            if (!_editingHex)
            {
                _hexEdit.Text = FormatColorValue(color);
                SetHexValidity(true);
            }
        }
        finally
        {
            _syncingControls = false;
        }

        Invalidate(UiInvalidationKind.Render);
    }

    private void SetColor(BColor color)
    {
        if (color == SelectedColor)
            SyncControls();
        else
            SelectedColor = color;
    }

    private void UpdateFromHex()
    {
        if (_syncingControls)
            return;

        string text = _hexEdit.Text.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        bool validLength = text.Length is 6 or 8;
        if (!validLength || !uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgba))
        {
            SetHexValidity(false);
            return;
        }

        if (text.Length == 6)
            rgba = (rgba << 8) | 255;
        SetHexValidity(true);
        _editingHex = true;
        try
        {
            SetColor(BColor.FromArgb((byte)rgba, (byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8)));
        }
        finally
        {
            _editingHex = false;
        }
    }

    private void SetHexValidity(bool isValid)
    {
        if (_hexValid == isValid)
            return;

        _hexValid = isValid;
        _okButton.IsEnabled = isValid;
        Invalidate(UiInvalidationKind.Render);
    }

    private bool ConfirmColor() => _hexValid && AcceptSelection();

    private void SetHsv(double hue, double saturation, double value)
    {
        _hue = hue;
        _saturation = saturation;
        _value = value;
        BColor color = FromHsv(hue, saturation, value, SelectedColor.A);
        _settingHsv = color != SelectedColor;
        SetColor(color);
    }

    private void SetAlpha(double alpha)
    {
        BColor color = SelectedColor;
        _settingHsv = color.A != (byte)alpha;
        SetColor(BColor.FromArgb((byte)alpha, color.R, color.G, color.B));
    }

    private void ReadHsv()
    {
        double red = SelectedColor.R / 255.0;
        double green = SelectedColor.G / 255.0;
        double blue = SelectedColor.B / 255.0;
        double max = Math.Max(red, Math.Max(green, blue));
        double chroma = max - Math.Min(red, Math.Min(green, blue));
        _value = max;

        // Black has no defined saturation, and gray has no defined hue. Keep the last choice
        // so raising value or saturation brings back the user's color.
        if (max == 0)
            return;
        _saturation = chroma / max;
        if (chroma == 0)
            return;

        double sector = max == red ? (green - blue) / chroma
            : max == green ? (blue - red) / chroma + 2
            : (red - green) / chroma + 4;
        _hue = (sector < 0 ? sector + 6 : sector) * 60;
    }

    private static BColor FromHsv(double hue, double saturation, double value, byte alpha)
    {
        double sector = hue % 360 / 60;
        double chroma = value * saturation;
        double second = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (red, green, blue) = (int)sector switch
        {
            0 => (chroma, second, 0.0),
            1 => (second, chroma, 0.0),
            2 => (0.0, chroma, second),
            3 => (0.0, second, chroma),
            4 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second),
        };
        double offset = value - chroma;
        return BColor.FromArgb(alpha, ToByte(red + offset), ToByte(green + offset), ToByte(blue + offset));
    }

    private static byte ToByte(double channel) => (byte)Math.Round(Math.Clamp(channel, 0, 1) * 255);

    protected override BSize MeasureCore(BSize availableSize)
    {
        base.MeasureCore(availableSize);
        return new BSize(
            Math.Min(PreferredSize.Width, availableSize.Width),
            Math.Min(PreferredSize.Height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect content = StandardControlPaint.Inset(_chrome.UpdateLayout(finalRect).Content, Padding);
        double gap = Math.Min(Gap, content.Width / 12);
        double columnGap = gap * 2;
        double leftWidth = (content.Width - columnGap) * 0.48;
        double right = content.Left + leftWidth + columnGap;
        double rightWidth = content.Right - right;

        // Compress vertical spacing before controls overlap in a small host viewport.
        double scale = Math.Min(1, content.Height / 420);
        double labelHeight = 24 * scale;
        double rowHeight = 32 * scale;
        double rowGap = 5 * scale;
        double footerTop = content.Bottom - rowHeight;
        double previewTop = footerTop - 88 * scale;
        double mainBottom = previewTop - 10 * scale;
        _planeLabel = new BRect(content.Left, content.Top, leftWidth, labelHeight);
        _valuesLabel = new BRect(right, content.Top, rightWidth, labelHeight);

        double swatchHeight = 28 * scale;
        double paletteTop = mainBottom - PaletteRows * (swatchHeight + rowGap) - labelHeight;
        _plane = new BRect(content.Left, _planeLabel.Bottom, leftWidth,
            Math.Max(0, paletteTop - rowGap - _planeLabel.Bottom));
        _paletteLabel = new BRect(content.Left, paletteTop, leftWidth, labelHeight);
        double swatchStride = leftWidth / PaletteColumns;
        for (int index = 0; index < _swatches.Length; index++)
        {
            var swatch = _swatches[index];
            swatch.IconExtent = Math.Min(18, Math.Min(swatchHeight, swatchStride) * 0.62);
            swatch.Arrange(new BRect(content.Left + index % PaletteColumns * swatchStride,
                _paletteLabel.Bottom + index / PaletteColumns * (swatchHeight + rowGap),
                Math.Max(0, swatchStride - gap / 3), swatchHeight));
        }

        double channelLabelWidth = Math.Min(66, rightWidth * 0.27);
        double inputWidth = Math.Min(76, rightWidth * 0.36);
        double channelTop = _valuesLabel.Bottom;
        foreach (Channel channel in _channels)
        {
            channel.LabelBounds = new BRect(right, channelTop, channelLabelWidth, rowHeight);
            channel.Slider.Arrange(new BRect(right + channelLabelWidth, channelTop,
                Math.Max(0, rightWidth - channelLabelWidth - inputWidth - gap), rowHeight));
            channel.Input.Arrange(new BRect(content.Right - inputWidth, channelTop, inputWidth, rowHeight));
            channelTop += rowHeight + rowGap;
        }

        double previewWidth = (leftWidth - gap) / 2;
        _previewLabels = new BRect(content.Left, previewTop, leftWidth, labelHeight);
        _originalPreview = new BRect(content.Left, _previewLabels.Bottom, previewWidth, 42 * scale);
        _currentPreview = new BRect(content.Left + previewWidth + gap, _previewLabels.Bottom, previewWidth, 42 * scale);

        _hexLabel = new BRect(right, previewTop, rightWidth, labelHeight);
        _hexEdit.Arrange(new BRect(right, _hexLabel.Bottom, rightWidth, rowHeight));
        _hexHint = new BRect(right, _hexEdit.Bounds.Bottom, rightWidth, labelHeight);

        double buttonWidth = Math.Min(86, (content.Width - 2 * gap) / 3);
        _resetButton.Arrange(new BRect(content.Left, footerTop, buttonWidth, rowHeight));
        _okButton.Arrange(new BRect(content.Right - buttonWidth, footerTop, buttonWidth, rowHeight));
        _cancelButton.Arrange(new BRect(_okButton.Bounds.Left - gap - buttonWidth, footerTop, buttonWidth, rowHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRenderList list = context.RenderList;
        list.FillRect(Bounds, Background);
        RenderChrome(context);

        DrawLabel(list, _planeLabel, "Saturation / value");
        DrawPlane(list);
        DrawLabel(list, _paletteLabel, "Preset colors");
        DrawLabel(list, _valuesLabel, "Color values");
        foreach (Channel channel in _channels)
            DrawLabel(list, channel.LabelBounds, channel.Label);

        DrawLabel(list, new BRect(_originalPreview.Left, _previewLabels.Top, _originalPreview.Width, _previewLabels.Height), "Original");
        DrawLabel(list, new BRect(_currentPreview.Left, _previewLabels.Top, _currentPreview.Width, _previewLabels.Height), "Current");
        DrawPreview(list, _originalPreview, _originalColor);
        DrawPreview(list, _currentPreview, SelectedColor);
        DrawLabel(list, _hexLabel, "Hex color");
        DrawLabel(list, _hexHint, _hexValid ? "#RRGGBB or #RRGGBBAA" : "Enter 6 or 8 hex digits");

        base.RenderCore(context);
        list.StrokeRect(Bounds, BorderColor, 1);
    }

    private void DrawLabel(BRenderList list, BRect bounds, string text) =>
        StandardWindowChromePaint.DrawTitleText(list, bounds, text, _labelFont, TitleForeground);

    private void DrawPlane(BRenderList list)
    {
        if (_plane.IsEmpty)
            return;

        const int steps = 96;
        double dx = _plane.Width / steps;
        double dy = _plane.Height / steps;
        list.PushClip(_plane);
        for (int index = 0; index < steps; index++)
        {
            double fraction = index / (steps - 1.0);
            list.FillRect(new BRect(_plane.Left + index * dx, _plane.Top, dx, _plane.Height),
                FromHsv(_hue, fraction, 1, 255));
        }
        for (int index = 1; index < steps; index++)
        {
            double fraction = index / (steps - 1.0);
            list.FillRect(new BRect(_plane.Left, _plane.Top + index * dy, _plane.Width, dy),
                BColor.FromArgb(ToByte(fraction), 0, 0, 0));
        }

        double x = _plane.Left + _saturation * _plane.Width;
        double y = _plane.Top + (1 - _value) * _plane.Height;
        list.StrokeRoundedRect(new BRect(x - 6, y - 6, 12, 12), BColor.Black, 6, 6, 3);
        list.StrokeRoundedRect(new BRect(x - 6, y - 6, 12, 12), BColor.White, 6, 6, 1);
        list.PopClip();
        list.StrokeRect(_plane, BorderColor, 1);
    }

    private void DrawPreview(BRenderList list, BRect bounds, BColor color)
    {
        if (bounds.IsEmpty)
            return;

        const double tile = 8;
        list.FillRect(bounds, BColor.White);
        if (color.A < 255)
        {
            BColor gray = BColor.FromArgb(255, 196, 196, 196);
            int row = 0;
            for (double top = bounds.Top; top < bounds.Bottom; top += tile, row++)
            {
                for (double left = bounds.Left + (row % 2) * tile; left < bounds.Right; left += 2 * tile)
                    list.FillRect(new BRect(left, top, Math.Min(tile, bounds.Right - left), Math.Min(tile, bounds.Bottom - top)), gray);
            }
        }
        if (color.A > 0)
            list.FillRect(bounds, color);
        list.StrokeRect(bounds, BorderColor, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (HandlePlaneInput(input) || _chrome.HandleInput(input) || base.OnInput(input))
            return true;
        if (input.Kind != UiInputEventKind.KeyboardKey || input.KeyTransition != KeyboardKeyTransition.Down)
            return false;
        if (input.NativeKeyCode == BVirtualKey.Escape || input.KeyName is "Escape" or "VirtualKey:27")
            return Cancel();
        return (input.NativeKeyCode == BVirtualKey.Enter || input.KeyName is "Enter" or "VirtualKey:13") && ConfirmColor();
    }

    private bool HandlePlaneInput(UiInputEvent input)
    {
        if (input.Kind == UiInputEventKind.PointerMove)
        {
            if (!_draggingPlane || Session?.CapturedElement != this)
                return false;
        }
        else
        {
            if (input.Kind != UiInputEventKind.PointerButton || input.MouseButton != MouseButton.Left)
                return false;

            if (input.MouseButtonTransition == MouseButtonTransition.Down && !_plane.IsEmpty && _plane.Contains(input.Position))
            {
                _draggingPlane = true;
                Session?.SetFocus(this);
                Session?.CaptureInput(this);
            }
            else if (input.MouseButtonTransition == MouseButtonTransition.Up && _draggingPlane)
            {
                _draggingPlane = false;
                Session?.ReleaseInputCapture(this);
            }
            else
            {
                return false;
            }
        }

        if (!_plane.IsEmpty)
        {
            double saturation = Math.Clamp((input.Position.X - _plane.Left) / _plane.Width, 0, 1);
            double value = 1 - Math.Clamp((input.Position.Y - _plane.Top) / _plane.Height, 0, 1);
            SetHsv(_hue, saturation, value);
        }
        return true;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        _chrome.Layout.HitTest(position) == UiWindowChromePart.TitleBar;
    private void RenderChrome(UiRenderContext context)
    {
        UiWindowChromeLayout layout = _chrome.Layout;
        if (!layout.IsVisible)
            return;

        var renderList = context.RenderList;
        renderList.FillRect(layout.TitleBar, TitleBarBackground);
        StandardWindowChromePaint.DrawTitleText(renderList, layout.Title, Title, TitleFont, TitleForeground);
        if (Icon is not null)
            StandardWindowChromePaint.DrawIcon(renderList, layout.Icon, Icon.Image);

        DrawButton(layout.MinimizeButton, StandardWindowChromeGlyph.Minimize, UiWindowChromePart.Minimize);
        DrawButton(layout.MaximizeButton,
            State == UiWindowState.Maximized ? StandardWindowChromeGlyph.Restore : StandardWindowChromeGlyph.Maximize,
            UiWindowChromePart.Maximize);
        DrawButton(layout.CloseButton, StandardWindowChromeGlyph.Close, UiWindowChromePart.Close);

        void DrawButton(BRect bounds, StandardWindowChromeGlyph glyph, UiWindowChromePart part) =>
            StandardWindowChromePaint.DrawButton(renderList, bounds, glyph,
                _chrome.HotPart == part, _chrome.PressedPart == part, TitleForeground);
    }
    private sealed class Channel(string label, StandardSlider slider, StandardSpinBox input)
    {
        public string Label { get; } = label;
        public StandardSlider Slider { get; } = slider;
        public StandardSpinBox Input { get; } = input;
        public BRect LabelBounds { get; set; }
    }
}
