using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// The pieces of input handling that need no control: telling a double-click
/// from two clicks, and finding word boundaries.
/// </summary>
public sealed class RichEditInputHelperTests
{
    private static UiTimestamp At(int milliseconds) => new(TimeSpan.FromMilliseconds(milliseconds));

    [Fact]
    public void A_Second_Press_Soon_After_And_Near_The_First_Is_A_Double_Click()
    {
        var clicks = new RichEditClickTracker();
        Assert.False(clicks.IsDoubleClick(At(0), new BPoint(10, 10)));

        clicks.Record(At(0), new BPoint(10, 10));

        Assert.True(clicks.IsDoubleClick(At(400), new BPoint(14, 6)));
        Assert.False(clicks.IsDoubleClick(At(401), new BPoint(10, 10)));
        Assert.False(clicks.IsDoubleClick(At(100), new BPoint(15, 10)));

        // A clock that went backwards says nothing about how quick the click was.
        clicks.Record(At(1000), new BPoint(10, 10));
        Assert.False(clicks.IsDoubleClick(At(900), new BPoint(10, 10)));
    }

    [Fact]
    public void Word_Steps_Skip_The_Space_Between_Words_And_Cross_Paragraph_Edges()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("one two  three\nnext");

        Assert.Equal(new RichTextPosition(0, 4), RichEditWordNavigation.WordRight(document, new RichTextPosition(0, 1)));
        Assert.Equal(new RichTextPosition(0, 9), RichEditWordNavigation.WordRight(document, new RichTextPosition(0, 4)));
        Assert.Equal(new RichTextPosition(0, 4), RichEditWordNavigation.WordLeft(document, new RichTextPosition(0, 9)));
        Assert.Equal(new RichTextPosition(0, 0), RichEditWordNavigation.WordLeft(document, new RichTextPosition(0, 3)));

        Assert.Equal(new RichTextPosition(1, 0), RichEditWordNavigation.WordRight(document, new RichTextPosition(0, 14)));
        Assert.Equal(new RichTextPosition(0, 14), RichEditWordNavigation.WordLeft(document, new RichTextPosition(1, 0)));
    }

    [Fact]
    public void A_Double_Click_Takes_The_Word_Or_The_Space_It_Landed_In()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("say snake_case   now");

        Assert.Equal(
            new RichTextRange(new RichTextPosition(0, 4), new RichTextPosition(0, 14)),
            RichEditWordNavigation.WordAt(document, new RichTextPosition(0, 7)));
        Assert.Equal(
            new RichTextRange(new RichTextPosition(0, 14), new RichTextPosition(0, 17)),
            RichEditWordNavigation.WordAt(document, new RichTextPosition(0, 15)));
    }
}
