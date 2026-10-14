using Broiler.Graphics.Paging;

namespace Broiler.UI.Tests;

/// <summary>
/// The print model every host and the print dialog share: page lists as a person types them,
/// the pages a job selects, and choices that refuse values no printer could honour.
/// </summary>
public sealed class UiPrintSettingsTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("3", "3")]
    [InlineData("1-3, 5", "1-3, 5")]
    [InlineData("8-", "8-")]
    [InlineData("-4", "1-4")]
    [InlineData("2;4–6", "2, 4-6")]
    public void A_Page_List_Reads_As_A_Person_Types_It(string text, string formatted)
    {
        Assert.True(UiPageRange.TryParse(text, out IReadOnlyList<UiPageRange> ranges));
        Assert.Equal(formatted, UiPageRange.Format(ranges));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5-3")]
    [InlineData("a")]
    [InlineData("1-2-3")]
    [InlineData("+2")]
    public void A_Page_List_That_Names_No_Real_Pages_Is_Refused(string text)
    {
        Assert.False(UiPageRange.TryParse(text, out IReadOnlyList<UiPageRange> ranges));
        Assert.Empty(ranges);
    }

    [Fact]
    public void All_Pages_Are_Selected_When_No_Range_Is_Given()
    {
        var settings = new UiPrintSettings();

        Assert.Equal([0, 1, 2], settings.SelectPages(3));
        Assert.Empty(settings.SelectPages(0));
    }

    [Fact]
    public void Ranges_Select_In_The_Order_Typed_And_Stop_At_The_Last_Page()
    {
        UiPageRange.TryParse("4-, 1, 2-3", out IReadOnlyList<UiPageRange> ranges);
        var settings = new UiPrintSettings { PageRanges = ranges };

        Assert.Equal([3, 4, 0, 1, 2], settings.SelectPages(5));
        Assert.Equal([0], settings.SelectPages(1));
    }

    [Fact]
    public void An_Empty_Range_List_Means_All_Pages()
    {
        var settings = new UiPrintSettings { PageRanges = [] };

        Assert.Null(settings.PageRanges);
        Assert.Equal(2, settings.SelectPages(2).Count);
    }

    [Fact]
    public void Choices_No_Printer_Could_Honour_Are_Refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiPrintSettings { Copies = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiPrintSettings { Copies = UiPrintSettings.MaximumCopies + 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiPrintSettings { Dpi = 0 });
        Assert.Throws<ArgumentNullException>(() => new UiPrintSettings { PageSetup = null! });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiPageRange(2, 1));
        Assert.Throws<ArgumentException>(() => new UiPrinterInfo(" "));
    }

    [Fact]
    public void A_Job_Prints_The_Pages_It_Was_Given_Or_Lays_The_Document_Out()
    {
        var document = new CountingDocument();
        var given = new BPageCollection([new BPageBuilder(new Broiler.Graphics.Geometry.BSize(10, 10)).Build()]);

        Assert.Same(given, new UiPrintJob(document, new UiPrintSettings(), given).ResolvePages());
        Assert.Equal(0, document.Paginations);

        IBPageSource laidOut = new UiPrintJob(document, new UiPrintSettings()).ResolvePages();
        Assert.Equal(1, document.Paginations);
        Assert.Equal(2, laidOut.PageCount);
        Assert.Equal("Counted", new UiPrintJob(document, new UiPrintSettings()).Title);
    }

    private sealed class CountingDocument : IBPagedDocument
    {
        public int Paginations { get; private set; }

        public string Title => "Counted";

        public BPageSetup PreferredSetup => BPageSetup.Default;

        public IBPageSource Paginate(BPageSetup setup, CancellationToken cancellationToken = default)
        {
            Paginations++;
            BPage page = new BPageBuilder(setup.PageSize).Build();
            return new BPageCollection([page, page]);
        }
    }
}
