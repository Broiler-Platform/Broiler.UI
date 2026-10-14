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
using Broiler.UI.Edit.Standard;
using Broiler.UI.PrintPreview.Standard;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.PrintDialog.Standard;

/// <summary>
/// The standard print dialog: the choices down the left, the document as it will print on the
/// right, and Print and Cancel below. Every choice that changes the page lays the preview out
/// again; one the printer cannot honour is not offered.
/// </summary>
public sealed class StandardPrintDialog : UiPrintDialog, IStandardThemedControl
{
    private static readonly (BPageOrientation Value, string Id, string Text)[] OrientationChoices =
    [
        (BPageOrientation.Portrait, "portrait", "Portrait"),
        (BPageOrientation.Landscape, "landscape", "Landscape"),
    ];

    private static readonly (UiPrintColorMode Value, string Id, string Text)[] ColorChoices =
    [
        (UiPrintColorMode.Color, "color", "Colour"),
        (UiPrintColorMode.Monochrome, "monochrome", "Black and white"),
    ];

    private static readonly (UiPrintDuplex Value, string Id, string Text)[] DuplexChoices =
    [
        (UiPrintDuplex.OneSided, "one-sided", "One-sided"),
        (UiPrintDuplex.LongEdge, "long-edge", "Both sides, long edge"),
        (UiPrintDuplex.ShortEdge, "short-edge", "Both sides, short edge"),
    ];

    private readonly StandardComboBox _printerCombo;
    private readonly StandardSpinBox _copiesSpin;
    private readonly StandardEdit _pagesEdit;
    private readonly StandardComboBox _paperCombo;
    private readonly StandardComboBox _orientationCombo;
    private readonly StandardComboBox _colorCombo;
    private readonly StandardComboBox _duplexCombo;
    private readonly StandardPrintPreview _preview;
    private readonly StandardButton _previousButton;
    private readonly StandardButton _nextButton;
    private readonly StandardButton _printButton;
    private readonly StandardButton _cancelButton;
    private readonly List<(string Label, StandardComboBox? Combo, UiElement Control)> _rows = [];
    private readonly Dictionary<UiElement, BRect> _labelBounds = [];
    private IReadOnlyList<BPaperSize> _papers = [];
    private IReadOnlyList<UiPrintColorMode> _colors = [];
    private IReadOnlyList<UiPrintDuplex> _duplexes = [];
    private string? _rangeError;
    private BRect _problemBounds;
    private bool _syncing;

    public StandardPrintDialog()
    {
        _printerCombo = CreateCombo();
        _copiesSpin = new StandardSpinBox
        {
            PreferredSize = new BSize(96, 28),
            CornerRadius = 0,
            Minimum = 1,
            Maximum = UiPrintSettings.MaximumCopies,
            DecimalPlaces = 0,
            SmallChange = 1,
            LargeChange = 10,
            Value = 1,
        };
        _pagesEdit = new StandardEdit
        {
            PreferredSize = new BSize(200, 28),
            CornerRadius = 0,
            PlaceholderText = "All pages",
        };
        _paperCombo = CreateCombo();
        _orientationCombo = CreateCombo();
        _colorCombo = CreateCombo();
        _duplexCombo = CreateCombo();
        _preview = new StandardPrintPreview { ShowCaption = true };
        _previousButton = CreateButton("Previous");
        _nextButton = CreateButton("Next");
        _printButton = CreateButton("Print");
        _printButton.IsDefault = true;
        _cancelButton = CreateButton("Cancel");
        _cancelButton.IsCancel = true;

        var orientations = new List<UiComboBoxItem>();
        foreach ((BPageOrientation _, string id, string text) in OrientationChoices)
            orientations.Add(new UiComboBoxItem(id, text));
        _orientationCombo.SetItems(orientations);

        _rows.Add(("Printer", _printerCombo, _printerCombo));
        _rows.Add(("Copies", null, _copiesSpin));
        _rows.Add(("Pages", null, _pagesEdit));
        _rows.Add(("Paper", _paperCombo, _paperCombo));
        _rows.Add(("Orientation", _orientationCombo, _orientationCombo));
        _rows.Add(("Colour", _colorCombo, _colorCombo));
        _rows.Add(("Two-sided", _duplexCombo, _duplexCombo));

        _printerCombo.SelectionChanged += (_, _) => CommitPrinter();
        _copiesSpin.ValueChanged += (_, _) => CommitCopies();
        _pagesEdit.TextChanged += (_, _) => CommitPages();
        _paperCombo.SelectionChanged += (_, _) => CommitPaper();
        _orientationCombo.SelectionChanged += (_, _) => CommitOrientation();
        _colorCombo.SelectionChanged += (_, _) => CommitColor();
        _duplexCombo.SelectionChanged += (_, _) => CommitDuplex();
        _preview.PageIndexChanged += (_, _) => SyncNavigation();
        _previousButton.Clicked += (_, _) => _preview.PreviousPage();
        _nextButton.Clicked += (_, _) => _preview.NextPage();
        _printButton.Clicked += (_, _) => AcceptPrint();
        _cancelButton.Clicked += (_, _) => Cancel();

        foreach ((string _, StandardComboBox? _, UiElement control) in _rows)
            AddChild(control);
        AddChild(_preview);
        AddChild(_previousButton);
        AddChild(_nextButton);
        AddChild(_printButton);
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

    public StandardComboBox PrinterCombo => _printerCombo;

    public StandardSpinBox CopiesSpin => _copiesSpin;

    public StandardEdit PagesEdit => _pagesEdit;

    public StandardComboBox PaperCombo => _paperCombo;

    public StandardComboBox OrientationCombo => _orientationCombo;

    public StandardComboBox ColorCombo => _colorCombo;

    public StandardComboBox DuplexCombo => _duplexCombo;

    public StandardPrintPreview Preview => _preview;

    public StandardButton PreviousButton => _previousButton;

    public StandardButton NextButton => _nextButton;

    public StandardButton PrintButton => _printButton;

    public StandardButton CancelButton => _cancelButton;

    /// <summary>What stops the dialog printing, including a page range it cannot read, or null.</summary>
    public string? DisplayedProblem => _rangeError ?? Problem;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        BorderColor = theme.Border;
        LabelForeground = theme.TextMuted;
        Invalidate(UiInvalidationKind.Render);
    }

    protected override void OnPrintersChanged() => SyncAll();

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
    /// fills the right, with the page buttons under it, and Print and Cancel sit bottom right.
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
            double width = control == _copiesSpin ? Math.Min(96, leftWidth) : leftWidth;
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
        _printButton.Arrange(new BRect(Math.Max(client.Left, _cancelButton.Bounds.Left - Gap - actionWidth), actionTop, actionWidth, buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 7));

        foreach ((string label, StandardComboBox? _, UiElement control) in _rows)
        {
            if (_labelBounds.TryGetValue(control, out BRect bounds) && !bounds.IsEmpty)
                context.RenderList.DrawText(new BTextRun(label, LabelFont, LabelForeground), new BPoint(bounds.Left, bounds.Top));
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
                return AcceptPrint();
        }

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)).Contains(position);

    private void SyncAll()
    {
        // Called from the base constructor's first settings assignment too, before any field here
        // is set; there is nothing to show yet then.
        if (_printerCombo is null)
            return;

        _syncing = true;
        try
        {
            SyncPrinters();
            _copiesSpin.Maximum = Math.Clamp(Capabilities.MaximumCopies, 1, UiPrintSettings.MaximumCopies);
            _copiesSpin.Value = Settings.Copies;
            // Only when the settings say something the box does not: rewriting it while the user is
            // typing "1," would take the comma away before the next page number arrived.
            if (_rangeError is null && !ShowsRanges(_pagesEdit.Text, Settings.PageRanges))
                _pagesEdit.Text = UiPageRange.Format(Settings.PageRanges);
            SyncPapers();
            _orientationCombo.SelectIndex(Settings.PageSetup.Orientation == BPageOrientation.Landscape ? 1 : 0);
            SyncColors();
            SyncDuplexes();
        }
        finally
        {
            _syncing = false;
        }

        SyncNavigation();
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private void SyncPrinters()
    {
        var items = new List<UiComboBoxItem>(Printers.Count);
        int selected = -1;
        for (int i = 0; i < Printers.Count; i++)
        {
            UiPrinterInfo printer = Printers[i];
            items.Add(new UiComboBoxItem(printer.Name, printer.IsDefault ? printer.Name + " (default)" : printer.Name));
            if (string.Equals(printer.Name, Settings.PrinterName, StringComparison.Ordinal))
                selected = i;
        }

        _printerCombo.SetItems(items);
        if (selected >= 0)
            _printerCombo.SelectIndex(selected);
    }

    /// <summary>
    /// The printer's papers, with the page the document is set to added when the printer does not
    /// list it, so the combo never claims a paper the preview is not showing.
    /// </summary>
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
    }

    private void SyncColors()
    {
        var colors = new List<UiPrintColorMode>();
        foreach ((UiPrintColorMode value, string _, string _) in ColorChoices)
        {
            if (value == UiPrintColorMode.Monochrome || Capabilities.SupportsColor)
                colors.Add(value);
        }

        _colors = colors;
        SetChoiceItems(_colorCombo, ColorChoices, colors, Settings.ColorMode);
    }

    private void SyncDuplexes()
    {
        var duplexes = new List<UiPrintDuplex>();
        foreach ((UiPrintDuplex value, string _, string _) in DuplexChoices)
        {
            if (value == UiPrintDuplex.OneSided || Capabilities.SupportsDuplex)
                duplexes.Add(value);
        }

        _duplexes = duplexes;
        SetChoiceItems(_duplexCombo, DuplexChoices, duplexes, Settings.Duplex);
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
        combo.SelectIndex(index);
    }

    private void SyncNavigation()
    {
        if (_previousButton is null)
            return;

        _previousButton.IsEnabled = _preview.PageIndex > 0;
        _nextButton.IsEnabled = _preview.PageIndex < _preview.PageCount - 1;
        _printButton.IsEnabled = _rangeError is null && CanPrint;
    }

    private void CommitPrinter()
    {
        if (_syncing || _printerCombo.SelectedItem is not UiComboBoxItem item)
            return;

        Settings = Settings with { PrinterName = item.Id };
    }

    private void CommitCopies()
    {
        if (_syncing)
            return;

        int copies = (int)Math.Clamp(Math.Round(_copiesSpin.Value), 1, UiPrintSettings.MaximumCopies);
        Settings = Settings with { Copies = copies };
    }

    private void CommitPages()
    {
        if (_syncing)
            return;

        if (!UiPageRange.TryParse(_pagesEdit.Text, out IReadOnlyList<UiPageRange> ranges))
        {
            _rangeError = "Type pages as 1-3, 5 or 8-.";
            SyncNavigation();
            Invalidate(UiInvalidationKind.Render);
            return;
        }

        _rangeError = null;
        Settings = Settings with { PageRanges = ranges.Count == 0 ? null : ranges };
        SyncNavigation();
        Invalidate(UiInvalidationKind.Render);
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

    private void CommitColor()
    {
        if (_syncing || (uint)_colorCombo.SelectedIndex >= (uint)_colors.Count)
            return;

        Settings = Settings with { ColorMode = _colors[_colorCombo.SelectedIndex] };
    }

    private void CommitDuplex()
    {
        if (_syncing || (uint)_duplexCombo.SelectedIndex >= (uint)_duplexes.Count)
            return;

        Settings = Settings with { Duplex = _duplexes[_duplexCombo.SelectedIndex] };
    }

    private void DrawWrapped(UiRenderContext context, BRect bounds, string text)
    {
        // A word at a time onto lines as wide as the column: the reasons are short sentences, and
        // one that ran off the edge would be the one the user most needed to read.
        double lineHeight = BTextMeasurer.GetLineHeight(LabelFont);
        double y = bounds.Top;
        string line = string.Empty;
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && BTextMeasurer.MeasureAdvance(candidate, LabelFont) > bounds.Width)
            {
                context.RenderList.DrawText(new BTextRun(line, LabelFont, ProblemForeground), new BPoint(bounds.Left, y));
                y += lineHeight;
                line = word;
                if (y + lineHeight > bounds.Bottom)
                    return;
                continue;
            }

            line = candidate;
        }

        if (line.Length > 0 && y + lineHeight <= bounds.Bottom + 0.5)
            context.RenderList.DrawText(new BTextRun(line, LabelFont, ProblemForeground), new BPoint(bounds.Left, y));
    }

    private static bool ShowsRanges(string text, IReadOnlyList<UiPageRange>? ranges)
    {
        if (!UiPageRange.TryParse(text, out IReadOnlyList<UiPageRange> typed))
            return false;

        IReadOnlyList<UiPageRange> expected = ranges ?? [];
        if (typed.Count != expected.Count)
            return false;

        for (int i = 0; i < typed.Count; i++)
        {
            if (typed[i] != expected[i])
                return false;
        }

        return true;
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
