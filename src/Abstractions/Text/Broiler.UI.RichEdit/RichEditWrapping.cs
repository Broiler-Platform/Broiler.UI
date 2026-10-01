namespace Broiler.UI.RichEdit;

/// <summary>
/// Controls whether text lines in a <see cref="UiRichEdit"/> wrap at the viewport
/// boundary or continue horizontally.
/// </summary>
public enum RichEditWrapping
{
    /// <summary>Lines wrap to fit within the available content column width.</summary>
    Wrap = 0,

    /// <summary>
    /// Lines do not wrap; hard line breaks and paragraphs preserve their length,
    /// enabling preformatted/code whitespace and horizontal scrolling.
    /// </summary>
    NoWrap,
}
