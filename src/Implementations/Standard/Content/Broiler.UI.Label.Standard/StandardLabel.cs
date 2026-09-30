using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.Label.Standard;

public sealed class StandardLabel : UiLabel, IStandardThemedControl
{
    private StandardLabelRole _role = StandardLabelRole.Default;
    private double _cachedWidth = double.NaN;
    private string? _cachedDisplayText;
    private BFontStyle? _cachedFont;
    private UiTextWrapping _cachedWrapping;
    private UiTextTrimming _cachedTrimming;
    private IReadOnlyList<LabelLine>? _cachedLines;
    private int _layoutBuildCount;

    public StandardLabel()
    {
        Foreground = GetRoleColor(_role, StandardControlPaint.Theme);
    }

    public StandardLabel(string text, StandardLabelRole role = StandardLabelRole.Default) : this()
    {
        Text = text;
        Role = role;
    }

    public int LayoutBuildCount => _layoutBuildCount;

    public StandardLabelRole Role
    {
        get => _role;
        set
        {
            _role = value;
            if (value != StandardLabelRole.Custom)
            {
                Foreground = GetRoleColor(value, StandardControlPaint.GetTheme(this));
            }
        }
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (Role != StandardLabelRole.Custom)
        {
            Foreground = GetRoleColor(Role, theme);
        }
    }

    public static BColor GetRoleColor(StandardLabelRole role, StandardThemeTokens theme) =>
        role switch
        {
            StandardLabelRole.Muted => theme.TextMuted,
            StandardLabelRole.Warning => theme.Warning,
            StandardLabelRole.Danger => theme.Danger,
            StandardLabelRole.Success => theme.Success,
            StandardLabelRole.Accent => theme.Accent,
            StandardLabelRole.Info => theme.Info,
            StandardLabelRole.Disabled => theme.TextDisabled,
            _ => theme.Text,
        };

    public static StandardLabel Muted(string text = "") => new(text, StandardLabelRole.Muted);
    public static StandardLabel Warning(string text = "") => new(text, StandardLabelRole.Warning);
    public static StandardLabel Danger(string text = "") => new(text, StandardLabelRole.Danger);
    public static StandardLabel Success(string text = "") => new(text, StandardLabelRole.Success);
    public static StandardLabel Accent(string text = "") => new(text, StandardLabelRole.Accent);
    public static StandardLabel Info(string text = "") => new(text, StandardLabelRole.Info);

    public static StandardLabel Title(string text = "") => new(text) { Font = StandardControlPaint.FontTitle };
    public static StandardLabel Subtitle(string text = "") => new(text) { Font = StandardControlPaint.FontSubtitle };
    public static StandardLabel Caption(string text = "") => new(text, StandardLabelRole.Muted) { Font = StandardControlPaint.FontCaption };
    public static StandardLabel Code(string text = "") => new(text) { Font = StandardControlPaint.FontCode };

    protected override BSize MeasureCore(BSize availableSize)
    {
        IReadOnlyList<LabelLine> lines = GetOrCreateLines(availableSize.Width);
        double width = 0;
        foreach (LabelLine line in lines)
            width = Math.Max(width, line.Width);

        double height = lines.Count * BTextMeasurer.GetLineHeight(Font);
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        IReadOnlyList<LabelLine> lines = GetOrCreateLines(Bounds.Width);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);

        context.RenderList.PushClip(Bounds);
        for (int index = 0; index < lines.Count; index++)
        {
            LabelLine line = lines[index];
            if (line.Text.Length == 0)
                continue;

            double x = Direction == UiTextDirection.RightToLeft
                ? Bounds.Right - line.Width
                : Bounds.Left;
            double y = Bounds.Top + index * lineHeight;
            context.RenderList.DrawText(new BTextRun(line.Text, Font, Foreground), new BPoint(x, y));
        }

        context.RenderList.PopClip();
    }

    private IReadOnlyList<LabelLine> GetOrCreateLines(double availableWidth)
    {
        string text = DisplayText;
        if (_cachedLines != null &&
            _cachedDisplayText == text &&
            _cachedFont == Font &&
            _cachedWrapping == Wrapping &&
            _cachedTrimming == Trimming &&
            (double.IsInfinity(availableWidth) && double.IsInfinity(_cachedWidth) ||
             (!double.IsInfinity(availableWidth) && !double.IsInfinity(_cachedWidth) && Math.Abs(_cachedWidth - availableWidth) < 0.001)))
        {
            return _cachedLines;
        }

        _cachedLines = BuildLines(availableWidth);
        _cachedWidth = availableWidth;
        _cachedDisplayText = text;
        _cachedFont = Font;
        _cachedWrapping = Wrapping;
        _cachedTrimming = Trimming;
        _layoutBuildCount++;
        return _cachedLines;
    }

    private IReadOnlyList<LabelLine> BuildLines(double availableWidth)
    {
        string text = DisplayText;
        double maxWidth = double.IsInfinity(availableWidth) || availableWidth <= 0
            ? double.PositiveInfinity
            : availableWidth;

        if (Wrapping == UiTextWrapping.NoWrap)
            return [CreateLine(ApplyTrimming(text, maxWidth))];

        var lines = new List<LabelLine>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            AddWrappedParagraph(paragraph, maxWidth, lines);

        return lines.Count == 0 ? [CreateLine(string.Empty)] : lines;
    }

    private void AddWrappedParagraph(string paragraph, double maxWidth, List<LabelLine> lines)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(CreateLine(string.Empty));
            return;
        }

        if (double.IsInfinity(maxWidth))
        {
            lines.Add(CreateLine(paragraph));
            return;
        }

        string current = string.Empty;
        foreach (string word in paragraph.Split(' ', StringSplitOptions.None))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (Measure(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(CreateLine(current));
                current = string.Empty;
            }

            if (Measure(word) <= maxWidth)
            {
                current = word;
                continue;
            }

            BreakWord(word, maxWidth, lines, ref current);
        }

        if (current.Length > 0)
            lines.Add(CreateLine(current));
    }

    private void BreakWord(string word, double maxWidth, List<LabelLine> lines, ref string current)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(word);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            string candidate = current + element;
            if (candidate.Length > element.Length && Measure(candidate) > maxWidth)
            {
                lines.Add(CreateLine(current));
                current = element;
            }
            else
            {
                current = candidate;
            }
        }
    }

    private string ApplyTrimming(string text, double maxWidth)
    {
        if (Trimming != UiTextTrimming.CharacterEllipsis || double.IsInfinity(maxWidth) || Measure(text) <= maxWidth)
            return text;

        const string ellipsis = "...";
        double ellipsisWidth = Measure(ellipsis);
        if (ellipsisWidth > maxWidth)
            return string.Empty;

        string result = string.Empty;
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            string candidate = result + element;
            if (Measure(candidate) + ellipsisWidth > maxWidth)
                break;

            result = candidate;
        }

        return result + ellipsis;
    }

    private LabelLine CreateLine(string text) =>
        new(text, BTextMeasurer.MeasureAdvance(text, Font));

    private double Measure(string text) => BTextMeasurer.MeasureAdvance(text, Font);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private readonly record struct LabelLine(string Text, double Width);
}
