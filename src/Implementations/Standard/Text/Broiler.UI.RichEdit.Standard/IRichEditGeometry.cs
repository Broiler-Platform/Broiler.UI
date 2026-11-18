using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// Editing queries over the current layout. Coordinates are local to a fragment (a flow region
/// or a page), with scrolling and conversion to control coordinates handled by the viewport.
/// Line IDs belong to the provider and the layout revision that produced them.
/// </summary>
public interface IRichEditGeometry
{
    RichEditTextHit? HitTest(int fragment, BPoint point, RichEditHitTestMode mode);

    // A source position can have multiple visual occurrences. A valid line hint takes
    // precedence over the preferred fragment, which helps resolve repeated occurrences.
    RichEditLineGeometry? GetLine(RichTextPosition position, RichEditLineId? preferredLine = null, int? preferredFragment = null);

    // Delta follows visual reading order, which may continue into another fragment.
    RichEditLineGeometry? GetRelativeLine(RichEditLineId line, int delta, bool clamp = false);

    RichTextPosition? GetPositionInLine(RichEditLineId line, double inlineCoordinate);

    RichEditCaretGeometry? GetCaret(RichTextPosition position, RichEditLineId? preferredLine = null, int? preferredFragment = null);

    // Each rectangle is clipped both to its layout region (for example a table cell) and to clip.
    IEnumerable<BRect> GetSelectionBounds(int fragment, RichTextRange range, BRect clip);
}

public enum RichEditHitTestMode { Exact, Nearest }

public readonly record struct RichEditLineId(int Revision, int Fragment, int Index);

public readonly record struct RichEditLineGeometry(
    RichEditLineId Id, RichTextPosition Start, RichTextPosition End, double InlineStart, double Top, double Height);

// A hit may supply a source position without enough geometry to identify a visual line.
public readonly record struct RichEditTextHit(RichTextPosition Position, RichEditLineId? Line);

// InlineCoordinate is the line's navigation coordinate; Bounds is the painted caret and may be empty.
public readonly record struct RichEditCaretGeometry(RichEditLineId Line, double InlineCoordinate, BRect Bounds);
