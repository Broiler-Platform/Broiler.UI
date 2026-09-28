using System;
using Broiler.Documents.Model;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// Word boundaries within a paragraph, for Ctrl+Left and Ctrl+Right and for
/// selecting the word under a double-click. Neither crosses into another
/// paragraph except to step over the break at its edge.
/// </summary>
internal static class RichEditWordNavigation
{
    /// <summary>
    /// The start of the word before <paramref name="position"/>, skipping the
    /// whitespace between; the end of the previous paragraph from its start.
    /// </summary>
    public static RichTextPosition WordLeft(RichTextDocument document, RichTextPosition position)
    {
        string text = document.Paragraphs[position.ParagraphIndex].Text;
        int i = position.Offset;
        if (i <= 0)
            return document.PositionLeftOf(position);
        i--;
        while (i > 0 && char.IsWhiteSpace(text[i]))
            i--;
        while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
            i--;
        return new RichTextPosition(position.ParagraphIndex, i);
    }

    /// <summary>
    /// The start of the word after <paramref name="position"/>, past the rest of
    /// the current word and the whitespace after it; the start of the next
    /// paragraph from the end of this one.
    /// </summary>
    public static RichTextPosition WordRight(RichTextDocument document, RichTextPosition position)
    {
        string text = document.Paragraphs[position.ParagraphIndex].Text;
        int n = text.Length;
        int i = position.Offset;
        if (i >= n)
            return document.PositionRightOf(position);
        while (i < n && !char.IsWhiteSpace(text[i]))
            i++;
        while (i < n && char.IsWhiteSpace(text[i]))
            i++;
        return new RichTextPosition(position.ParagraphIndex, i);
    }

    /// <summary>
    /// The word around <paramref name="position"/>, or the run of whitespace
    /// around it when it is not in a word - what a double-click selects.
    /// </summary>
    public static RichTextRange WordAt(RichTextDocument document, RichTextPosition position)
    {
        string text = document.Paragraphs[position.ParagraphIndex].Text;
        int start = Math.Clamp(position.Offset, 0, text.Length);
        int end = start;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;
        while (end < text.Length && IsWordChar(text[end]))
            end++;
        if (start == end)
        {
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                start--;
            while (end < text.Length && char.IsWhiteSpace(text[end]))
                end++;
        }

        return new RichTextRange(
            new RichTextPosition(position.ParagraphIndex, start),
            new RichTextPosition(position.ParagraphIndex, end));
    }

    private static bool IsWordChar(char character) => char.IsLetterOrDigit(character) || character == '_';
}
