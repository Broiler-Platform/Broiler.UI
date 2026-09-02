using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.FontDialog;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.FontDialog.Standard;

public sealed class StandardFontDialog : UiFontDialog, IStandardThemedControl
{
    /// <summary>
    /// The weights the box offers, in the order it offers them. Every one is a real DirectWrite /
    /// CSS weight, so a family that has the face is drawn in it and one that does not falls back
    /// the way the renderer would anyway.
    /// </summary>
    private static readonly (BFontWeight Weight, string Id, string Text)[] WeightChoices =
    [
        (BFontWeight.Thin, "weight:100", "Thin"),
        (BFontWeight.Light, "weight:300", "Light"),
        (BFontWeight.Normal, "weight:400", "Normal"),
        (BFontWeight.Medium, "weight:500", "Medium"),
        (BFontWeight.SemiBold, "weight:600", "Semi-bold"),
        (BFontWeight.Bold, "weight:700", "Bold"),
        (BFontWeight.Black, "weight:900", "Black"),
    ];

    private readonly StandardListView _familyList;
    private readonly StandardSpinBox _sizeSpin;
    private readonly StandardComboBox _weightCombo;
    private readonly StandardToggleButton _italicToggle;
    private readonly StandardToggleButton _underlineToggle;
    private readonly StandardToggleButton _strikethroughToggle;
    private readonly StandardButton _okButton;
    private readonly StandardButton _cancelButton;
    private BRect _familyLabelBounds;
    private BRect _sizeLabelBounds;
    private BRect _weightLabelBounds;
    private BRect _styleLabelBounds;
    private BRect _previewLabelBounds;
    private BRect _previewBounds;
    private bool _syncing;
    private bool _scrollSelectionIntoView = true;

    public StandardFontDialog()
    {
        Title = "Font";

        _familyList = new StandardListView
        {
            PreferredSize = new BSize(240, 260),
            ItemHeight = 24,
            CornerRadius = 0,
        };
        _sizeSpin = new StandardSpinBox
        {
            PreferredSize = new BSize(96, 28),
            CornerRadius = 0,
            Minimum = MinimumFontSize,
            Maximum = MaximumFontSize,
            // Half a point is the finest any word processor offers, and font sizes are written that
            // way — 10.5, never 10.50.
            DecimalPlaces = 1,
            SmallChange = 1,
            LargeChange = 10,
        };
        _weightCombo = new StandardComboBox
        {
            PreferredSize = new BSize(140, 28),
            CornerRadius = 0,
            ItemHeight = 26,
            MaxDropDownItems = WeightChoices.Length,
        };
        _italicToggle = CreateStyleToggle("Italic");
        _underlineToggle = CreateStyleToggle("Underline");
        _strikethroughToggle = CreateStyleToggle("Strike");
        _okButton = new StandardButton
        {
            Text = "OK",
            IsDefault = true,
            PreferredSize = new BSize(76, 30),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };
        _cancelButton = new StandardButton
        {
            Text = "Cancel",
            IsCancel = true,
            PreferredSize = new BSize(76, 30),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };

        var weightItems = new List<UiComboBoxItem>(WeightChoices.Length);
        foreach ((BFontWeight _, string id, string text) in WeightChoices)
            weightItems.Add(new UiComboBoxItem(id, text));
        _weightCombo.SetItems(weightItems);

        _familyList.SelectionChanged += (_, e) => SelectFamily(e.NewItemId);
        _sizeSpin.ValueChanged += (_, _) => CommitSize();
        _weightCombo.SelectionChanged += (_, _) => CommitWeight();
        _italicToggle.ToggleStateChanged += (_, _) => CommitItalic();
        _underlineToggle.ToggleStateChanged += (_, _) => CommitUnderline();
        _strikethroughToggle.ToggleStateChanged += (_, _) => CommitStrikethrough();
        _okButton.Clicked += (_, _) => AcceptSelection();
        _cancelButton.Clicked += (_, _) => Cancel();

        AddChild(_familyList);
        AddChild(_sizeSpin);
        AddChild(_weightCombo);
        AddChild(_italicToggle);
        AddChild(_underlineToggle);
        AddChild(_strikethroughToggle);
        AddChild(_okButton);
        AddChild(_cancelButton);

        SyncFontFamilies();
        SyncSelectedFont();
        SyncDecorations();
    }

    /// <summary>The smallest size the box offers. Below this a preview shows nothing legible.</summary>
    private const double MinimumFontSize = 1;

    /// <summary>Matches the ceiling <see cref="UiFontDialog"/> coerces a font size to.</summary>
    private const double MaximumFontSize = 512;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        BorderColor = theme.Border;
        LabelForeground = theme.TextMuted;
        PreviewForeground = theme.Text;
        PreviewBorderColor = theme.Border;
        PreviewBackground = theme.SurfaceAlt;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF6, 0xFB);

    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor LabelForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor PreviewForeground { get; set; } = StandardControlPaint.Text;

    public BColor PreviewBackground { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BColor PreviewBorderColor { get; set; } = StandardControlPaint.Border;

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    public BFontStyle LabelFont { get; set; } = BFontStyle.Default with { Size = 13 };

    public BSize PreferredSize { get; set; } = new(560, 384);

    /// <summary>
    /// The smallest the dialog still lays out as designed. It is resizable, so this is what the
    /// arrangement below is written against rather than a size anything enforces.
    /// </summary>
    public BSize MinimumSize { get; set; } = new(420, 300);

    public double TitleBarHeight { get; set; } = 30;

    public double Padding { get; set; } = 12;

    public double Gap { get; set; } = 8;

    public StandardListView FamilyList => _familyList;

    public StandardSpinBox SizeSpin => _sizeSpin;

    public StandardComboBox WeightCombo => _weightCombo;

    public StandardToggleButton ItalicToggle => _italicToggle;

    public StandardToggleButton UnderlineToggle => _underlineToggle;

    public StandardToggleButton StrikethroughToggle => _strikethroughToggle;

    public StandardButton OkButton => _okButton;

    public StandardButton CancelButton => _cancelButton;

    protected override void OnFontFamiliesChanged()
    {
        SyncFontFamilies();
        SyncSelectedFont();
    }

    protected override void OnSelectedFontChanged()
    {
        SyncSelectedFont();
    }

    protected override void OnDecorationsChanged()
    {
        SyncDecorations();
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize clientAvailable = new(
            Math.Max(0, availableSize.Width - Padding * 2),
            Math.Max(0, availableSize.Height - TitleBarHeight - Padding * 2));

        foreach (UiElement child in Children)
            child.Measure(clientAvailable);

        return new BSize(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));
    }

    /// <summary>
    /// Lays the dialog out at whatever size it has been dragged to: the family list takes the left
    /// column and the right one stacks size, weight, style and the preview. Every width is derived
    /// from the client rectangle rather than fixed, and the preview takes what is left over — it is
    /// the part worth the extra room a user drags the window out for.
    /// </summary>
    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = GetClientBounds(finalRect);
        double buttonHeight = 30;
        double labelHeight = 18;
        double rowHeight = 28;
        double actionWidth = 76;
        double actionTop = Math.Max(client.Top, client.Bottom - buttonHeight);
        double contentBottom = Math.Max(client.Top, actionTop - Gap);

        // The list is the taller half and the right column the wider one, so the split favours the
        // right column as the dialog narrows: a preview or a weight name that does not fit is
        // unreadable, while a truncated family name is still recognisable.
        double leftWidth = Math.Clamp(client.Width * 0.42, 0, Math.Max(0, client.Width - 232 - Gap));
        double rightX = client.Left + leftWidth + Gap;
        double rightWidth = Math.Max(0, client.Right - rightX);

        _familyLabelBounds = new BRect(client.Left, client.Top, leftWidth, labelHeight);
        _familyList.Arrange(new BRect(
            client.Left,
            client.Top + labelHeight,
            leftWidth,
            Math.Max(0, contentBottom - client.Top - labelHeight)));

        // Only now does the list know how tall it is, and a scroll offset worked out before that is
        // worked out against a zero-height viewport — which is why the selected family used to open
        // just off the top of the list. Once per selection, so a user who scrolls away from it and
        // then resizes the dialog is not dragged back.
        if (_scrollSelectionIntoView)
        {
            _scrollSelectionIntoView = false;
            _familyList.ScrollIntoView(_familyList.SelectedItemId ?? string.Empty);
        }

        double sizeWidth = Math.Min(96, rightWidth);
        double weightX = rightX + sizeWidth + Gap;
        double weightWidth = Math.Max(0, client.Right - weightX);
        _sizeLabelBounds = new BRect(rightX, client.Top, sizeWidth, labelHeight);
        _weightLabelBounds = new BRect(weightX, client.Top, weightWidth, labelHeight);
        _sizeSpin.Arrange(new BRect(rightX, client.Top + labelHeight, sizeWidth, rowHeight));
        _weightCombo.Arrange(new BRect(weightX, client.Top + labelHeight, weightWidth, rowHeight));

        double styleTop = client.Top + labelHeight + rowHeight + Gap;
        _styleLabelBounds = new BRect(rightX, styleTop, rightWidth, labelHeight);
        ArrangeStyleToggles(new BRect(rightX, styleTop + labelHeight, rightWidth, rowHeight));

        double previewTop = styleTop + labelHeight + rowHeight + Gap;
        _previewLabelBounds = new BRect(rightX, previewTop, rightWidth, labelHeight);
        _previewBounds = new BRect(
            rightX,
            previewTop + labelHeight,
            rightWidth,
            Math.Max(0, contentBottom - previewTop - labelHeight));

        _cancelButton.Arrange(new BRect(client.Right - actionWidth, actionTop, actionWidth, buttonHeight));
        _okButton.Arrange(new BRect(
            Math.Max(client.Left, _cancelButton.Bounds.Left - Gap - actionWidth),
            actionTop,
            actionWidth,
            buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 7));

        DrawLabel(context, _familyLabelBounds, "Family");
        DrawLabel(context, _sizeLabelBounds, "Size");
        DrawLabel(context, _weightLabelBounds, "Weight");
        DrawLabel(context, _styleLabelBounds, "Style");
        DrawLabel(context, _previewLabelBounds, "Preview");
        DrawPreview(context);

        base.RenderCore(context);
        context.RenderList.StrokeRect(Bounds, BorderColor, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (base.OnInput(input))
            return true;

        if (input.Kind == UiInputEventKind.PointerButton)
            return HandlePointerButton(input);
        if (input.Kind == UiInputEventKind.KeyboardKey)
            return HandleKeyboard(input);

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)).Contains(position);

    private static StandardToggleButton CreateStyleToggle(string text) =>
        new()
        {
            Text = text,
            PreferredSize = new BSize(86, 28),
            CornerRadius = 0,
            PaddingX = 6,
            PaddingY = 5,
        };

    /// <summary>
    /// The three style toggles share the row evenly, so the group narrows with the dialog instead
    /// of the last one falling off the right edge.
    /// </summary>
    private void ArrangeStyleToggles(BRect row)
    {
        StandardToggleButton[] toggles = [_italicToggle, _underlineToggle, _strikethroughToggle];
        double width = Math.Max(0, (row.Width - (Gap * (toggles.Length - 1))) / toggles.Length);
        for (int index = 0; index < toggles.Length; index++)
        {
            toggles[index].Arrange(new BRect(
                row.Left + (index * (width + Gap)),
                row.Top,
                width,
                row.Height));
        }
    }

    private void SyncFontFamilies()
    {
        _syncing = true;
        try
        {
            var items = new List<UiListItem>(FontFamilies.Count);
            foreach (string family in FontFamilies)
                items.Add(new UiListItem(family, family));

            _familyList.SetItems(items);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncSelectedFont()
    {
        _syncing = true;
        try
        {
            _familyList.SelectedItemId = FindListedFamily(SelectedFont.FamilyName);
            _familyList.ScrollIntoView(_familyList.SelectedItemId ?? string.Empty);
            _scrollSelectionIntoView = true;
            _sizeSpin.Value = SelectedFont.Size;
            _weightCombo.SelectIndex(FindWeightIndex(SelectedFont.Weight));
            _italicToggle.ToggleState = SelectedFont.Slant == BFontSlant.Normal ? UiToggleState.Off : UiToggleState.On;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncDecorations()
    {
        _syncing = true;
        try
        {
            _underlineToggle.ToggleState = Underline ? UiToggleState.On : UiToggleState.Off;
            _strikethroughToggle.ToggleState = Strikethrough ? UiToggleState.On : UiToggleState.Off;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SelectFamily(string? itemId)
    {
        if (_syncing || string.IsNullOrWhiteSpace(itemId))
            return;

        SelectedFont = SelectedFont with { FamilyName = itemId };
    }

    private void CommitSize()
    {
        if (_syncing)
            return;

        SelectedFont = SelectedFont with { Size = _sizeSpin.Value };
    }

    private void CommitWeight()
    {
        if (_syncing || (uint)_weightCombo.SelectedIndex >= (uint)WeightChoices.Length)
            return;

        SelectedFont = SelectedFont with { Weight = WeightChoices[_weightCombo.SelectedIndex].Weight };
    }

    private void CommitItalic()
    {
        if (_syncing)
            return;

        SelectedFont = SelectedFont with { Slant = _italicToggle.ToggleState == UiToggleState.On ? BFontSlant.Italic : BFontSlant.Normal };
    }

    private void CommitUnderline()
    {
        if (_syncing)
            return;

        Underline = _underlineToggle.ToggleState == UiToggleState.On;
    }

    private void CommitStrikethrough()
    {
        if (_syncing)
            return;

        Strikethrough = _strikethroughToggle.ToggleState == UiToggleState.On;
    }

    private void DrawLabel(UiRenderContext context, BRect bounds, string text)
    {
        if (bounds.IsEmpty || string.IsNullOrEmpty(text))
            return;

        context.RenderList.DrawText(new BTextRun(text, LabelFont, LabelForeground), new BPoint(bounds.Left, bounds.Top));
    }

    private void DrawPreview(UiRenderContext context)
    {
        if (_previewBounds.IsEmpty)
            return;

        context.RenderList.FillRect(_previewBounds, PreviewBackground);
        context.RenderList.StrokeRect(_previewBounds, PreviewBorderColor, 1);
        BRect textBounds = new(
            _previewBounds.Left + 8,
            _previewBounds.Top + 8,
            Math.Max(0, _previewBounds.Width - 16),
            Math.Max(0, _previewBounds.Height - 16));
        context.RenderList.PushClip(textBounds);
        context.RenderList.DrawText(
            new BTextRun(SampleText, SelectedFont, PreviewForeground),
            new BPoint(textBounds.Left, textBounds.Top));
        DrawPreviewDecorations(context, textBounds);
        context.RenderList.PopClip();
    }

    /// <summary>
    /// Draws the underline and the strike-through the same way the rich editor does — a thin rule
    /// in the text colour, positioned off the line box. A preview that showed the family, size,
    /// weight and slant but not these two would be a preview of most of the choice.
    /// </summary>
    private void DrawPreviewDecorations(UiRenderContext context, BRect textBounds)
    {
        if (!Underline && !Strikethrough)
            return;

        double advance = BTextMeasurer.MeasureAdvance(SampleText, SelectedFont);
        if (advance <= 0)
            return;

        double lineHeight = BTextMeasurer.GetLineHeight(SelectedFont);
        double thickness = Math.Max(1, Math.Round(SelectedFont.Size / 14));
        if (Underline)
        {
            context.RenderList.FillRect(
                new BRect(textBounds.Left, textBounds.Top + lineHeight - thickness - 1, advance, thickness),
                PreviewForeground);
        }

        if (Strikethrough)
        {
            context.RenderList.FillRect(
                new BRect(textBounds.Left, textBounds.Top + (lineHeight / 2), advance, thickness),
                PreviewForeground);
        }
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Activate();
        Session?.SetFocus(this);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
            return AcceptSelection();

        return false;
    }

    private string? FindListedFamily(string family)
    {
        foreach (string item in FontFamilies)
        {
            if (string.Equals(item, family, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    /// <summary>
    /// The listed weight nearest the selected one. A font can carry any weight from 1 to 999, and
    /// one that came from a document may sit between two the box offers; showing the closest is
    /// better than showing none, which would read as "no weight".
    /// </summary>
    private static int FindWeightIndex(BFontWeight weight)
    {
        int best = 0;
        int bestDistance = int.MaxValue;
        for (int index = 0; index < WeightChoices.Length; index++)
        {
            int distance = Math.Abs((int)WeightChoices[index].Weight - (int)weight);
            if (distance >= bestDistance)
                continue;

            best = index;
            bestDistance = distance;
        }

        return best;
    }

    private BRect GetClientBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + TitleBarHeight + Padding,
            Math.Max(0, bounds.Width - Padding * 2),
            Math.Max(0, bounds.Height - TitleBarHeight - Padding * 2));

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
