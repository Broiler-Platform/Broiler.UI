// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Paging;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.PrintPreview.Standard;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.ScanDialog.Standard;

/// <summary>
/// The standard scan dialog: the choices down the left, the scanned pages on the right, and
/// Preview, Scan, and Cancel below. Any option the scanner cannot honour is not offered.
/// </summary>
public sealed class StandardScanDialog : UiScanDialog, IStandardThemedControl
{
    private static readonly (UiScanSource Value, string Id, string Text)[] SourceChoices =
    [
        (UiScanSource.Flatbed, "flatbed", "Flatbed"),
        (UiScanSource.FeederOneSided, "feeder-one-sided", "Feeder (one-sided)"),
        (UiScanSource.FeederTwoSided, "feeder-two-sided", "Feeder (two-sided)"),
    ];

    private static readonly (UiScanColorMode Value, string Id, string Text)[] ColorChoices =
    [
        (UiScanColorMode.Color, "color", "Colour"),
        (UiScanColorMode.Grayscale, "grayscale", "Grayscale"),
        (UiScanColorMode.BlackAndWhite, "black-and-white", "Black and white"),
    ];

    private static readonly (BPageOrientation Value, string Id, string Text)[] OrientationChoices =
    [
        (BPageOrientation.Portrait, "portrait", "Portrait"),
        (BPageOrientation.Landscape, "landscape", "Landscape"),
    ];

    private readonly StandardComboBox _scannerCombo;
    private readonly StandardComboBox _sourceCombo;
    private readonly StandardComboBox _colorCombo;
    private readonly StandardComboBox _resolutionCombo;
    private readonly StandardComboBox _paperCombo;
    private readonly StandardComboBox _orientationCombo;
    private readonly StandardSpinBox _pagesSpin;
    private readonly StandardPrintPreview _preview;
    private readonly StandardButton _previousButton;
    private readonly StandardButton _nextButton;
    private readonly StandardButton _previewButton;
    private readonly StandardButton _scanButton;
    private readonly StandardButton _cancelButton;
    private readonly List<(string Label, StandardComboBox? Combo, UiElement Control)> _rows = [];
    private readonly Dictionary<UiElement, BRect> _labelBounds = [];
    private IReadOnlyList<UiScanSource> _sources = [];
    private IReadOnlyList<UiScanColorMode> _colors = [];
    private IReadOnlyList<int> _resolutions = [];
    private IReadOnlyList<BPaperSize> _papers = [];
    private BRect _problemBounds;
    private bool _syncing;

    public StandardScanDialog()
    {
        _scannerCombo = CreateCombo();
        _sourceCombo = CreateCombo();
        _colorCombo = CreateCombo();
        _resolutionCombo = CreateCombo();
        _paperCombo = CreateCombo();
        _orientationCombo = CreateCombo();
        _pagesSpin = new StandardSpinBox
        {
            PreferredSize = new BSize(96, 28),
            CornerRadius = 0,
            Minimum = 1,
            Maximum = UiScanSettings.MaximumPages,
            DecimalPlaces = 0,
            SmallChange = 1,
            LargeChange = 10,
            Value = 1,
        };
        _preview = new StandardPrintPreview { ShowCaption = true };
        _previousButton = CreateButton("Previous");
        _nextButton = CreateButton("Next");
        _previewButton = CreateButton("Preview");
        _scanButton = CreateButton("Scan");
        _scanButton.IsDefault = true;
        _cancelButton = CreateButton("Cancel");
        _cancelButton.IsCancel = true;

        var orientations = new List<UiComboBoxItem>();
        foreach ((BPageOrientation _, string id, string text) in OrientationChoices)
            orientations.Add(new UiComboBoxItem(id, text));
        _orientationCombo.SetItems(orientations);

        _rows.Add(("Scanner", _scannerCombo, _scannerCombo));
        _rows.Add(("Source", _sourceCombo, _sourceCombo));
        _rows.Add(("Colour", _colorCombo, _colorCombo));
        _rows.Add(("Resolution", _resolutionCombo, _resolutionCombo));
        _rows.Add(("Paper", _paperCombo, _paperCombo));
        _rows.Add(("Orientation", _orientationCombo, _orientationCombo));
        _rows.Add(("Pages", null, _pagesSpin));

        _scannerCombo.SelectionChanged += (_, _) => CommitScanner();
        _sourceCombo.SelectionChanged += (_, _) => CommitSource();
        _colorCombo.SelectionChanged += (_, _) => CommitColor();
        _resolutionCombo.SelectionChanged += (_, _) => CommitResolution();
        _paperCombo.SelectionChanged += (_, _) => CommitPaper();
        _orientationCombo.SelectionChanged += (_, _) => CommitOrientation();
        _pagesSpin.ValueChanged += (_, _) => CommitPages();
        _preview.PageIndexChanged += (_, _) => SyncNavigation();
        _previousButton.Clicked += (_, _) => _preview.PreviousPage();
        _nextButton.Clicked += (_, _) => _preview.NextPage();
        _previewButton.Clicked += (_, _) => _ = PreviewAsync();
        _scanButton.Clicked += (_, _) => AcceptScan();
        _cancelButton.Clicked += (_, _) => Cancel();

        foreach ((string _, StandardComboBox? _, UiElement control) in _rows)
            AddChild(control);
        AddChild(_preview);
        AddChild(_previousButton);
        AddChild(_nextButton);
        AddChild(_previewButton);
        AddChild(_scanButton);
        AddChild(_cancelButton);

        SyncAll();
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF6, 0xFB);

    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor LabelForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor ProblemForeground { get; set; } = BColor.FromArgb(0xFF, 0xB0, 0x2A, 0x2A);

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    public BFontStyle LabelFont { get; set; } = BFontStyle.Default with { Size = 13 };

    public BSize PreferredSize { get; set; } = new(760, 520);

    public double TitleBarHeight { get; set; } = 30;

    public double Padding { get; set; } = 12;

    public double Gap { get; set; } = 8;

    /// <summary>The width of the column of choices; the preview takes the rest.</summary>
    public double OptionsWidth { get; set; } = 260;

    public StandardComboBox ScannerCombo => _scannerCombo;

    public StandardComboBox SourceCombo => _sourceCombo;

    public StandardComboBox ColorCombo => _colorCombo;

    public StandardComboBox ResolutionCombo => _resolutionCombo;

    public StandardComboBox PaperCombo => _paperCombo;

    public StandardComboBox OrientationCombo => _orientationCombo;

    public StandardSpinBox PagesSpin => _pagesSpin;

    public StandardPrintPreview Preview => _preview;

    public StandardButton PreviousButton => _previousButton;

    public StandardButton NextButton => _nextButton;

    public StandardButton PreviewButton => _previewButton;

    public StandardButton ScanButton => _scanButton;

    public StandardButton CancelButton => _cancelButton;

    /// <summary>What stops the dialog scanning, or null.</summary>
    public string? DisplayedProblem => Problem;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        BorderColor = theme.Border;
        LabelForeground = theme.TextMuted;
        Invalidate(UiInvalidationKind.Render);
    }

    protected override void OnScannersChanged() => SyncAll();

    protected override void OnSettingsChanged() => SyncAll();

    protected override void OnPagesChanged()
    {
        _preview.Pages = Pages;
        SyncNavigation();
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
    /// The choices stack down a fixed-width left column, a label over each control; the preview
    /// fills the right, with the page buttons under it, and Preview, Scan, and Cancel sit bottom right.
    /// </summary>
    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = GetClientBounds(finalRect);
        const double buttonHeight = 30;
        const double labelHeight = 18;
        const double rowHeight = 28;
        const double actionWidth = 84;
        double actionTop = Math.Max(client.Top, client.Bottom - buttonHeight);
        double contentBottom = Math.Max(client.Top, actionTop - Gap);

        double leftWidth = Math.Clamp(OptionsWidth, 0, Math.Max(0, client.Width - 200 - Gap));
        double y = client.Top;
        _labelBounds.Clear();
        foreach ((string _, StandardComboBox? _, UiElement control) in _rows)
        {
            _labelBounds[control] = new BRect(client.Left, y, leftWidth, labelHeight);
            double width = control == _pagesSpin ? Math.Min(96, leftWidth) : leftWidth;
            control.Arrange(new BRect(client.Left, y + labelHeight, width, rowHeight));
            y += labelHeight + rowHeight + Gap;
        }

        _problemBounds = new BRect(client.Left, y, leftWidth, Math.Max(0, contentBottom - y));

        double rightX = client.Left + leftWidth + Gap;
        double rightWidth = Math.Max(0, client.Right - rightX);
        double navTop = Math.Max(client.Top, contentBottom - buttonHeight);
        _preview.Arrange(new BRect(rightX, client.Top, rightWidth, Math.Max(0, navTop - Gap - client.Top)));
        _previousButton.Arrange(new BRect(rightX, navTop, actionWidth, buttonHeight));
        _nextButton.Arrange(new BRect(Math.Max(rightX, client.Right - actionWidth), navTop, actionWidth, buttonHeight));

        _cancelButton.Arrange(new BRect(client.Right - actionWidth, actionTop, actionWidth, buttonHeight));
        _scanButton.Arrange(new BRect(Math.Max(client.Left, _cancelButton.Bounds.Left - Gap - actionWidth), actionTop, actionWidth, buttonHeight));
        _previewButton.Arrange(new BRect(Math.Max(client.Left, _scanButton.Bounds.Left - Gap - actionWidth), actionTop, actionWidth, buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            StandardWindowChromePaint.DrawTitleText(context.RenderList,
                new BRect(Bounds.Left + Padding, Bounds.Top, Math.Max(0, Bounds.Width - Padding * 2), Math.Min(TitleBarHeight, Bounds.Height)),
                Title, TitleFont, TitleForeground);

        foreach ((string label, StandardComboBox? _, UiElement control) in _rows)
        {
            if (_labelBounds.TryGetValue(control, out BRect bounds) && !bounds.IsEmpty)
                context.RenderList.DrawText(StandardControlPaint.TextRun(label, LabelFont, LabelForeground), new BPoint(bounds.Left, bounds.Top + Math.Max(0, (bounds.Height - BTextMeasurer.GetLineHeight(LabelFont)) / 2)));
        }

        if (DisplayedProblem is string problem && !_problemBounds.IsEmpty)
            DrawWrapped(context, _problemBounds, problem);

        base.RenderCore(context);
        context.RenderList.StrokeRect(Bounds, BorderColor, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (base.OnInput(input))
            return true;

        if (input.Kind == UiInputEventKind.PointerButton &&
            input.MouseButton == MouseButton.Left && input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Activate();
            Session?.SetFocus(this);
            return true;
        }

        if (input.Kind == UiInputEventKind.KeyboardKey && input.KeyTransition == KeyboardKeyTransition.Down)
        {
            if (IsKey(input, BVirtualKey.Escape, "Escape"))
                return Cancel();
            if (IsKey(input, BVirtualKey.Enter, "Enter"))
                return AcceptScan();
        }

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)).Contains(position);

    private void SyncAll()
    {
        if (_scannerCombo is null)
            return;

        _syncing = true;
        try
        {
            SyncScanners();
            SyncSources();
            SyncColors();
            SyncResolutions();
            SyncPapers();
            _orientationCombo.SelectIndex(Settings.PageSetup.Orientation == BPageOrientation.Landscape ? 1 : 0);

            int maxPages = Math.Clamp(Capabilities.MaximumPages, 1, UiScanSettings.MaximumPages);
            _pagesSpin.Maximum = maxPages;
            _pagesSpin.Value = Settings.MaxPages;
            _pagesSpin.IsEnabled = Settings.Source != UiScanSource.Flatbed && maxPages > 1;
        }
        finally
        {
            _syncing = false;
        }

        SyncNavigation();
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private void SyncScanners()
    {
        var items = new List<UiComboBoxItem>(Scanners.Count);
        int selected = -1;
        for (int i = 0; i < Scanners.Count; i++)
        {
            UiScannerInfo scanner = Scanners[i];
            items.Add(new UiComboBoxItem(scanner.Name, scanner.IsDefault ? scanner.Name + " (default)" : scanner.Name));
            if (string.Equals(scanner.Name, Settings.ScannerName, StringComparison.Ordinal))
                selected = i;
        }

        _scannerCombo.SetItems(items);
        if (selected >= 0)
            _scannerCombo.SelectIndex(selected);
        _scannerCombo.IsEnabled = Scanners.Count > 0;
    }

    private void SyncSources()
    {
        IReadOnlyList<UiScanSource> offered = Capabilities.Sources.Count > 0
            ? Capabilities.Sources
            : [UiScanSource.Flatbed];
        _sources = offered;
        SetChoiceItems(_sourceCombo, SourceChoices, offered, Settings.Source);
        _sourceCombo.IsEnabled = offered.Count > 1;
    }

    private void SyncColors()
    {
        IReadOnlyList<UiScanColorMode> offered = Capabilities.ColorModes.Count > 0
            ? Capabilities.ColorModes
            : [UiScanColorMode.Color, UiScanColorMode.Grayscale, UiScanColorMode.BlackAndWhite];
        _colors = offered;
        SetChoiceItems(_colorCombo, ColorChoices, offered, Settings.ColorMode);
        _colorCombo.IsEnabled = offered.Count > 1;
    }

    private void SyncResolutions()
    {
        IReadOnlyList<int> offered = Capabilities.Resolutions.Count > 0
            ? Capabilities.Resolutions
            : [75, 150, 300, 600];
        _resolutions = offered;
        var items = new List<UiComboBoxItem>(offered.Count);
        int selected = 0;
        for (int i = 0; i < offered.Count; i++)
        {
            int dpi = offered[i];
            string text = dpi.ToString(CultureInfo.InvariantCulture);
            if (dpi == Settings.Dpi)
                selected = i;
            items.Add(new UiComboBoxItem(text, text + " dpi"));
        }

        _resolutionCombo.SetItems(items);
        if (items.Count > 0)
            _resolutionCombo.SelectIndex(selected);
        _resolutionCombo.IsEnabled = offered.Count > 1;
    }

    private void SyncPapers()
    {
        var papers = new List<BPaperSize>(Capabilities.Papers);
        BPaperSize current = Settings.PageSetup.Paper;
        int selected = papers.FindIndex(paper => paper.Matches(current.Size));
        if (selected < 0)
        {
            papers.Insert(0, current);
            selected = 0;
        }

        _papers = papers;
        var items = new List<UiComboBoxItem>(papers.Count);
        foreach (BPaperSize paper in papers)
            items.Add(new UiComboBoxItem(paper.Name, paper.Name));
        _paperCombo.SetItems(items);
        _paperCombo.SelectIndex(selected);
        _paperCombo.IsEnabled = papers.Count > 1;
    }

    private static void SetChoiceItems<T>(StandardComboBox combo, (T Value, string Id, string Text)[] choices, IReadOnlyList<T> offered, T selected)
        where T : struct, Enum
    {
        var items = new List<UiComboBoxItem>(offered.Count);
        int index = 0;
        foreach ((T value, string id, string text) in choices)
        {
            if (!Contains(offered, value))
                continue;

            if (EqualityComparer<T>.Default.Equals(value, selected))
                index = items.Count;
            items.Add(new UiComboBoxItem(id, text));
        }

        combo.SetItems(items);
        if (items.Count > 0)
            combo.SelectIndex(index);
    }

    private void SyncNavigation()
    {
        if (_previousButton is null)
            return;

        _previousButton.IsEnabled = _preview.PageIndex > 0;
        _nextButton.IsEnabled = _preview.PageIndex < _preview.PageCount - 1;
        _previewButton.IsEnabled = CanScan;
        _scanButton.IsEnabled = CanScan;
    }

    private void CommitScanner()
    {
        if (_syncing || _scannerCombo.SelectedItem is not UiComboBoxItem item)
            return;

        Settings = Settings with { ScannerName = item.Id };
    }

    private void CommitSource()
    {
        if (_syncing || (uint)_sourceCombo.SelectedIndex >= (uint)_sources.Count)
            return;

        Settings = Settings with { Source = _sources[_sourceCombo.SelectedIndex] };
    }

    private void CommitColor()
    {
        if (_syncing || (uint)_colorCombo.SelectedIndex >= (uint)_colors.Count)
            return;

        Settings = Settings with { ColorMode = _colors[_colorCombo.SelectedIndex] };
    }

    private void CommitResolution()
    {
        if (_syncing || (uint)_resolutionCombo.SelectedIndex >= (uint)_resolutions.Count)
            return;

        Settings = Settings with { Dpi = _resolutions[_resolutionCombo.SelectedIndex] };
    }

    private void CommitPaper()
    {
        if (_syncing || (uint)_paperCombo.SelectedIndex >= (uint)_papers.Count)
            return;

        BPaperSize paper = _papers[_paperCombo.SelectedIndex];
        Settings = Settings with { PageSetup = Settings.PageSetup with { Paper = paper } };
    }

    private void CommitOrientation()
    {
        if (_syncing || (uint)_orientationCombo.SelectedIndex >= (uint)OrientationChoices.Length)
            return;

        BPageOrientation orientation = OrientationChoices[_orientationCombo.SelectedIndex].Value;
        Settings = Settings with { PageSetup = Settings.PageSetup with { Orientation = orientation } };
    }

    private void CommitPages()
    {
        if (_syncing)
            return;

        int maxPages = (int)Math.Clamp(Math.Round(_pagesSpin.Value), 1, UiScanSettings.MaximumPages);
        Settings = Settings with { MaxPages = maxPages };
    }

    private void DrawWrapped(UiRenderContext context, BRect bounds, string text)
    {
        double lineHeight = BTextMeasurer.GetLineHeight(LabelFont);
        double y = bounds.Top;
        string line = string.Empty;
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && BTextMeasurer.MeasureAdvance(candidate, LabelFont) > bounds.Width)
            {
                context.RenderList.DrawText(StandardControlPaint.TextRun(line, LabelFont, ProblemForeground), new BPoint(bounds.Left, y));
                y += lineHeight;
                line = word;
                if (y + lineHeight > bounds.Bottom)
                    return;
                continue;
            }

            line = candidate;
        }

        if (line.Length > 0 && y + lineHeight <= bounds.Bottom + 0.5)
            context.RenderList.DrawText(StandardControlPaint.TextRun(line, LabelFont, ProblemForeground), new BPoint(bounds.Left, y));
    }

    private static bool Contains<T>(IReadOnlyList<T> values, T value) where T : struct, Enum
    {
        foreach (T candidate in values)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, value))
                return true;
        }

        return false;
    }

    private static StandardComboBox CreateCombo() =>
        new()
        {
            PreferredSize = new BSize(240, 28),
            CornerRadius = 0,
            ItemHeight = 26,
        };

    private static StandardButton CreateButton(string text) =>
        new()
        {
            Text = text,
            PreferredSize = new BSize(84, 30),
            CornerRadius = 0,
            PaddingX = 8,
            PaddingY = 5,
        };

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
