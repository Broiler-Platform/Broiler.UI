#if BROILER_SHARED_PARAGRAPH_COMPOSITION
using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Documents.Model;
using Broiler.Documents.Model.Text;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard;

internal sealed partial class RichEditLayout
{
    private readonly record struct TextSlice(int Start, int Length);
    private readonly Dictionary<string, double> _sharedAdvances = new(StringComparer.Ordinal);
    private BFontStyle? _sharedAdvanceFont;

    private bool TryLayoutSharedParagraph(RichTextParagraph paragraph, int paragraphIndex, double width,
        double firstIndent, double defaultLineHeight, ref double y)
    {
        ParagraphStyle style = paragraph.Style;
        if (!_settings.UseSharedParagraphComposition || _settings.Wrapping != RichEditWrapping.Wrap ||
            paragraph.Length == 0 || style.RightToLeft || style.DropCap is not null)
            return false;
        foreach (StyleRun run in paragraph.Runs)
            if (IsHidden(run.Style) || run.Style.IsNote || run.Style.IsSymbolCharacter || SymbolFontText.IsSymbolFamily(run.Style.FontFamily))
                return false;

        string text = paragraph.Text;
        bool ascii = true;
        foreach (char c in text)
        {
            if ((char.IsControl(c) && c != '\t') || c is '\u2029' or UnicodeBoundaryClassifier.SoftHyphen)
                return false;
            ascii &= c <= 127;
        }

        BFontStyle? uniformFont = null;
        double uniformHeight = defaultLineHeight;
        if (paragraph.Runs.Count == 1)
        {
            InlineStyle inline = paragraph.Runs[0].Style;
            if (!inline.IsImage && inline.Shift is null && inline.Capitalization == TextCapitalization.None)
            {
                BFontStyle font = RunFont(inline);
                uniformFont = font;
                uniformHeight = LineHeight(style, Math.Max(_settings.DefaultLineHeight, BTextMeasurer.GetLineHeight(font)));
            }
        }

        double top = y;
        double indent = firstIndent;
        TextBand band = default;
        foreach (var (segmentStart, segmentEnd) in RichEditTextShaping.HardSegments(text))
        {
            int start = segmentStart;
            foreach (var line in ParagraphComposer.ComposeLines(MeasureItems(segmentStart, segmentEnd),
                ResolveWidth, SplitWord, slice => slice.Start, measureContextual: (tab, first, used) =>
                    TabAdvance(paragraph, tab.Start, first.Start, used).Advance))
            {
                // Keep the source range of gaps dropped at a wrap. The editor can still select them.
                int end = line.NextSourceOffset ?? segmentEnd;
                double height = start == end ? defaultLineHeight : uniformHeight;
                double padding = 0;
                if (start != end && uniformFont is null)
                {
                    (double natural, double topPadding) = MeasureLineHeight(paragraph, start, end, _settings.DefaultLineHeight);
                    height = LineHeight(style, natural);
                    padding = topPadding;
                }
                _lines.Add(new VisualLine(paragraphIndex, start, end, top, height,
                    indent + band.Left + AlignmentOffset(paragraph, start, end, band.Width),
                    LineWordSpacing(paragraph, start, end, band.Width), padding, indent));
                top += height;
                start = end;
                indent = 0;
            }
        }
        y = top;
        return true;

        double ResolveWidth(int _)
        {
            band = LineBand(ref top, defaultLineHeight, Math.Max(1, width - indent));
            return band.Width;
        }

        IEnumerable<ParagraphItem<TextSlice>> MeasureItems(int from, int end)
        {
            while (from < end)
            {
                if (text[from] == '\t')
                {
                    yield return new ParagraphItem<TextSlice>(new TextSlice(from++, 1), 0, IsContextual: true);
                    continue;
                }
                bool gap = UnicodeBoundaryClassifier.IsBreakableWhitespace(text[from]);
                int to = from + ElementLength(from, end);
                if (text[from] != InlineImage.Placeholder)
                    while (to < end && text[to] is not ('\t' or InlineImage.Placeholder) &&
                        UnicodeBoundaryClassifier.IsBreakableWhitespace(text[to]) == gap)
                        to += ElementLength(to, end);
                yield return new ParagraphItem<TextSlice>(new TextSlice(from, to - from), Measure(from, to), gap);
                from = to;
            }
        }

        IEnumerable<ParagraphItem<TextSlice>> SplitWord(TextSlice slice, double available)
        {
            foreach (var (offset, length, advance) in ParagraphComposer.SplitText(text.Substring(slice.Start, slice.Length), available,
                (offset, length) => Measure(slice.Start + offset, slice.Start + offset + length)))
                yield return new ParagraphItem<TextSlice>(new TextSlice(slice.Start + offset, length), advance);
        }

        int ElementLength(int from, int end) => ascii ? 1 : StringInfo.GetNextTextElementLength(text.AsSpan(from, end - from));

        double Measure(int from, int to) => uniformFont is { } font
            ? MeasureSharedText(text.AsSpan(from, to - from), font)
            : MeasureAdvance(paragraph, from, to);
    }

    private double MeasureSharedText(ReadOnlySpan<char> text, BFontStyle font)
    {
        if (_sharedAdvanceFont != font)
        {
            _sharedAdvances.Clear();
            _sharedAdvanceFont = font;
        }

        var lookup = _sharedAdvances.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(text, out double width))
            return width;
        string value = text.ToString();
        width = BTextMeasurer.MeasureAdvance(value, font);
        if (_sharedAdvances.Count >= 512)
            _sharedAdvances.Clear();
        _sharedAdvances.Add(value, width);
        return width;
    }
}
#endif
