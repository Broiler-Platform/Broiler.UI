using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// How stored text becomes what is drawn: capitals that are only displayed,
/// tabs resolved separately, soft breaks, and justified words.
/// </summary>
public sealed class RichEditTextShapingTests
{
    private static readonly BFontStyle Font = new("Segoe UI", 20);

    [Fact]
    public void All_Caps_Draws_Capitals_Without_Touching_The_Stored_Text()
    {
        const string text = "Mixed case";

        ShapedPiece piece = Assert.Single(RichEditTextShaping.ShapePieces(
            text, InlineStyle.Default with { Capitalization = TextCapitalization.AllCaps }, Font));

        Assert.Equal("MIXED CASE", piece.Text);
        Assert.Equal(Font, piece.Font);
        Assert.Equal("Mixed case", text);
    }

    [Fact]
    public void Small_Caps_Splits_Where_The_Typed_Case_Changes()
    {
        ShapedPiece[] pieces = RichEditTextShaping.ShapePieces(
            "Small Caps", InlineStyle.Default with { Capitalization = TextCapitalization.SmallCaps }, Font).ToArray();

        Assert.Equal(["S", "MALL", " C", "APS"], pieces.Select(piece => piece.Text));
        Assert.Equal(Font.Size, pieces[0].Font.Size);
        Assert.Equal(Font.Size * RichEditTextShaping.SmallCapsScale, pieces[1].Font.Size, 6);
    }

    [Fact]
    public void Plain_Text_Is_Measured_As_One_String()
    {
        Assert.Equal(
            BTextMeasurer.MeasureAdvance("plain", Font),
            RichEditTextShaping.MeasurePieces("plain", InlineStyle.Default, Font));
        Assert.Empty(RichEditTextShaping.ShapePieces(string.Empty, InlineStyle.Default, Font));
    }

    [Fact]
    public void Tabs_Are_Split_Out_Of_The_Text_Around_Them()
    {
        (string, bool)[] pieces = RichEditTextShaping.SplitTabs("xa\tb\t\tcx", 1, 7).ToArray();

        Assert.Equal([("a", false), ("\t", true), ("b", false), ("\t", true), ("\t", true), ("c", false)], pieces);
    }

    [Fact]
    public void A_Soft_Break_Ends_One_Segment_And_Belongs_To_Neither()
    {
        string text = "one" + (char)0x2028 + (char)0x2028 + "two";

        Assert.Equal([(0, 3), (4, 4), (5, 8)], RichEditTextShaping.HardSegments(text));
        Assert.Equal([(0, 0)], RichEditTextShaping.HardSegments(string.Empty));
    }

    [Fact]
    public void Justified_Text_Is_Cut_After_Each_Run_Of_Spaces()
    {
        Assert.Equal(["ab  ", "cd ", "e"], RichEditTextShaping.StretchChunks("ab  cd e", 1.5));
        Assert.Equal(["ab  cd e"], RichEditTextShaping.StretchChunks("ab  cd e", 0));
        Assert.Equal(3, RichEditTextShaping.CountSpaces("ab  cd e", 0, 8));
        Assert.Equal(1, RichEditTextShaping.CountSpaces("ab  cd e", 4, 8));
    }
}
