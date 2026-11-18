using System;
using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RichEdit.Standard;

internal sealed partial class RichEditLayout
{
    internal const int FlowFragment = 0;
    private int _revision;

    RichEditTextHit? IRichEditGeometry.HitTest(int fragment, BPoint point, RichEditHitTestMode mode)
    {
        if (!_isValid || fragment != FlowFragment || _lines.Count == 0)
            return null;

        int index = LineIndexAt(point);
        VisualLine line = _lines[index];
        if (mode == RichEditHitTestMode.Exact)
        {
            double left = LineLeft(line, 0);
            double right = CaretX(line, line.End, 0);
            var text = new BRect(left, line.Top, right - left, line.Height);
            if (!text.Contains(point) || (line.CellClip is { } clip && !clip.Contains(point)))
                return null;
        }

        return new RichEditTextHit(PositionInLineAtX(line, point.X, 0), LineId(index));
    }

    RichEditLineGeometry? IRichEditGeometry.GetLine(RichTextPosition position, RichEditLineId? preferredLine, int? preferredFragment) =>
        ResolveLine(position, preferredLine) is int index ? DescribeLine(index) : null;

    RichEditLineGeometry? IRichEditGeometry.GetRelativeLine(RichEditLineId line, int delta, bool clamp)
    {
        if (!ContainsLine(line))
            return null;
        long target = (long)line.Index + delta;
        if (clamp)
            target = Math.Clamp(target, 0, _lines.Count - 1);
        return target >= 0 && target < _lines.Count ? DescribeLine((int)target) : null;
    }

    RichTextPosition? IRichEditGeometry.GetPositionInLine(RichEditLineId line, double inlineCoordinate) =>
        ContainsLine(line) ? PositionInLineAtX(_lines[line.Index], inlineCoordinate, 0) : null;

    RichEditCaretGeometry? IRichEditGeometry.GetCaret(RichTextPosition position, RichEditLineId? preferredLine, int? preferredFragment)
    {
        if (ResolveLine(position, preferredLine) is not int index)
            return null;
        VisualLine line = _lines[index];
        double x = CaretX(line, position.Offset, 0);
        BRect bounds = new(x, line.Top + 1, 1, Math.Max(1, line.Height - 2));
        if (line.CellClip is { } cell)
            bounds = bounds.Intersect(cell);
        return new RichEditCaretGeometry(LineId(index), x, bounds);
    }

    IEnumerable<BRect> IRichEditGeometry.GetSelectionBounds(int fragment, RichTextRange range, BRect clip)
    {
        if (!_isValid || fragment != FlowFragment || range.IsEmpty || clip.IsEmpty)
            yield break;

        (int start, int count) = GetVisibleLineRange(clip.Top, clip.Bottom);
        for (int index = start; index < start + count; index++)
        {
            VisualLine line = _lines[index];
            if (!TrySelectionSpan(line, range, 0, out double left, out double width))
                continue;

            BRect bounds = new BRect(left, line.Top, width, line.Height).Intersect(clip);
            if (line.CellClip is { } cell)
                bounds = bounds.Intersect(cell);
            if (!bounds.IsEmpty)
                yield return bounds;
        }
    }

    private RichEditLineId LineId(int index) => new(_revision, FlowFragment, index);

    private bool ContainsLine(RichEditLineId line) =>
        _isValid && line.Revision == _revision && line.Fragment == FlowFragment && (uint)line.Index < (uint)_lines.Count;

    private int? ResolveLine(RichTextPosition position, RichEditLineId? preferred)
    {
        if (!_isValid || _lines.Count == 0)
            return null;
        if (preferred is { } id && ContainsLine(id))
        {
            VisualLine line = _lines[id.Index];
            if (line.ParagraphIndex == position.ParagraphIndex && position.Offset >= line.Start && position.Offset <= line.End)
                return id.Index;
        }
        return LineForPosition(position).Index;
    }

    private RichEditLineGeometry DescribeLine(int index)
    {
        VisualLine line = _lines[index];
        return new RichEditLineGeometry(LineId(index), new RichTextPosition(line.ParagraphIndex, line.Start),
            new RichTextPosition(line.ParagraphIndex, line.End), LineLeft(line, 0), line.Top, line.Height);
    }
}
