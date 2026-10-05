using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class ComboBoxControlTests
{
    [Fact]
    public void Open_Drop_Down_Renders_After_Later_Sibling_Controls()
    {
        var host = new TestHost(new BSize(160, 120));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        BColor popupColor = BColor.FromArgb(255, 17, 29, 43);
        BColor siblingColor = BColor.FromArgb(255, 191, 67, 91);
        var comboBox = new StandardComboBox
        {
            PopupBackground = popupColor,
            PreferredSize = new BSize(80, 24),
            ItemHeight = 24,
            MaxDropDownItems = 2,
        };
        comboBox.SetItems(
        [
            new UiComboBoxItem("one", "One"),
            new UiComboBoxItem("two", "Two"),
        ]);
        Assert.True(comboBox.OpenDropDown());

        var root = new PopupOverlapRoot(comboBox, new PaintElement(siblingColor));
        session.AddRoot(root);

        BRenderList renderList = session.RenderFrame();

        int siblingIndex = FindFillRect(renderList, siblingColor);
        int popupIndex = FindRoundedFill(renderList, popupColor);
        Assert.True(siblingIndex >= 0, "Expected overlapping sibling fill command to be recorded.");
        Assert.True(popupIndex >= 0, "Expected ComboBox popup fill command to be recorded.");
        Assert.True(
            popupIndex > siblingIndex,
            $"Expected ComboBox popup command at {popupIndex} to render after overlapping sibling command at {siblingIndex}.");
    }

    [Fact]
    public void The_Default_Size_Is_Unchanged_At_The_Default_Font_And_Grows_With_Larger_Text()
    {
        var comboBox = new StandardComboBox();
        comboBox.ApplyTheme(StandardThemeTokens.Light);
        Assert.Equal(32, Measure(comboBox).Height);
        Assert.Equal(28, comboBox.ItemHeight);

        comboBox.ApplyTheme(StandardThemeTokens.Light.WithTextScale(2));
        double line = BTextMeasurer.GetLineHeight(comboBox.Font);
        double defaultLine = BTextMeasurer.GetLineHeight(BFontStyle.Default);
        // The margin the default size leaves around a default line is kept around the larger one.
        Assert.Equal(Math.Ceiling(line + (32 - defaultLine)), Measure(comboBox).Height);
        Assert.Equal(Math.Ceiling(line + (28 - defaultLine)), comboBox.ItemHeight);
        Assert.True(comboBox.ItemHeight > line, "A drop-down row must hold a line of its font.");

        comboBox.ApplyTheme(StandardThemeTokens.Light);
        Assert.Equal(32, Measure(comboBox).Height);
        Assert.Equal(28, comboBox.ItemHeight);
    }

    [Fact]
    public void Sizes_The_Application_Sets_Are_Kept_At_Any_Font()
    {
        var comboBox = new StandardComboBox { PreferredSize = new BSize(120, 32), ItemHeight = 28 };
        comboBox.ApplyTheme(StandardThemeTokens.Light.WithTextScale(2));

        Assert.Equal(32, Measure(comboBox).Height);
        Assert.Equal(28, comboBox.ItemHeight);
    }

    [Fact]
    public void Drop_Down_Rows_Are_Laid_Out_At_The_Font_Height()
    {
        var host = new TestHost(new BSize(400, 400));
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(host);
        var comboBox = new StandardComboBox();
        comboBox.ApplyTheme(StandardThemeTokens.Light.WithTextScale(2));
        comboBox.SetItems([new UiComboBoxItem("one", "One"), new UiComboBoxItem("two", "Two")]);
        Assert.True(comboBox.OpenDropDown());
        session.AddRoot(new PopupOverlapRoot(comboBox, new PaintElement(BColor.Transparent)));

        BRenderList renderList = session.RenderFrame();

        Assert.Equal(comboBox.ItemHeight * 2, comboBox.PopupBounds.Height);
        BRenderCommand.DrawText second = Assert.Single(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Two");
        Assert.True(second.Origin.Y >= comboBox.PopupBounds.Top + comboBox.ItemHeight, "The second row starts below the first.");
        Assert.True(second.Origin.Y + BTextMeasurer.GetLineHeight(comboBox.Font) <= comboBox.PopupBounds.Bottom, "The second row's text fits its row.");
    }

    [Fact]
    public void The_Arrow_And_The_Text_Keep_Their_Places_At_The_Default_Font()
    {
        foreach (bool open in new[] { false, true })
        {
            ArrowFrame frame = RenderArrow(StandardThemeTokens.Light, open);

            Assert.Equal(open ? "^" : "v", frame.Arrow.Text.Text);
            Assert.Equal(frame.Bounds.Right - 18, frame.Arrow.Origin.X, 6);
            Assert.Equal(frame.Bounds.Right - 22, frame.TextClip.Right, 6);
        }
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2)]
    public void At_A_Larger_Text_Size_The_Arrow_Slot_Grows_With_The_Font(double textScale)
    {
        foreach (bool open in new[] { false, true })
        {
            ArrowFrame normal = RenderArrow(StandardThemeTokens.Light, open);
            ArrowFrame large = RenderArrow(StandardThemeTokens.Light.WithTextScale(textScale), open);
            double growth = BTextMeasurer.GetLineHeight(large.Font) / BTextMeasurer.GetLineHeight(normal.Font);
            Assert.True(growth > 1.4, "The test needs a larger font.");

            // The glyph is a glyph of the font and grows with it; the room beside it, to the frame on one side and
            // to the clipped text on the other, grows as much. A fixed slot let a 200 % arrow run to within 3 DIP
            // of the frame, and an open one past it. How wide the glyph is depends on the platform's font: DejaVu
            // Sans, which Linux falls back to, draws a "^" a fifth wider than Segoe UI, so the room is compared with
            // the room at the default font and with the focus ring the box draws, not with a fixed distance.
            double margin = large.Bounds.Right - large.ArrowRight;
            double normalMargin = normal.Bounds.Right - normal.ArrowRight;
            Assert.True(margin >= normalMargin * growth - 0.05, $"{large.Arrow.Text.Text}: {margin:F2} DIP to the frame, {normalMargin:F2} at the default font");
            Assert.True(
                large.ArrowRight <= large.FocusRing.Rect.Right - large.FocusRing.Thickness,
                $"{large.Arrow.Text.Text} ends at {large.ArrowRight:F2}, the focus ring starts at {large.FocusRing.Rect.Right - large.FocusRing.Thickness:F2}");

            double gap = large.Arrow.Origin.X - large.TextClip.Right;
            double normalGap = normal.Arrow.Origin.X - normal.TextClip.Right;
            Assert.True(gap >= normalGap * growth - 0.05, $"{large.Arrow.Text.Text}: the text stops {gap:F2} DIP before the arrow, {normalGap:F2} at the default font");
        }
    }

    private sealed record ArrowFrame(BRect Bounds, BRect TextClip, BRenderCommand.DrawText Arrow, BRenderCommand.StrokeRoundedRect FocusRing, BFontStyle Font)
    {
        public double ArrowRight => Arrow.Origin.X + BTextMeasurer.MeasureAdvance(Arrow.Text.Text, Font);
    }

    /// <summary>
    /// Draws a focused 200 DIP combo box showing a long item, closed or with its drop-down open, and returns its
    /// bounds, the clip its text is drawn in, the arrow and the focus ring.
    /// </summary>
    private static ArrowFrame RenderArrow(StandardThemeTokens theme, bool open)
    {
        const string longText = "Portable Document Format (PDF), every page";
        var host = new TestHost(new BSize(400, 400));
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(host);
        var comboBox = new StandardComboBox();
        comboBox.ApplyTheme(theme);
        comboBox.SetItems([new UiComboBoxItem("pdf", longText), new UiComboBoxItem("txt", "Text")]);
        Assert.True(comboBox.SelectIndex(0));
        session.AddRoot(new WidthRoot(comboBox, 200));
        session.RenderFrame();
        session.SetFocus(comboBox);
        if (open)
            Assert.True(comboBox.OpenDropDown());

        BRenderList renderList = session.RenderFrame();
        BRect clip = BRect.Empty;
        BRect? textClip = null;
        foreach (BRenderCommand command in renderList.Commands)
        {
            if (command is BRenderCommand.PushClip push)
                clip = push.Rect;
            else if (textClip is null && command is BRenderCommand.DrawText draw && draw.Text.Text == longText)
                textClip = clip;
        }

        Assert.NotNull(textClip);
        BRenderCommand.DrawText arrow = Assert.Single(
            renderList.Commands.OfType<BRenderCommand.DrawText>(),
            command => command.Text.Text is "v" or "^");
        BRenderCommand.StrokeRoundedRect ring = Assert.Single(
            renderList.Commands.OfType<BRenderCommand.StrokeRoundedRect>(),
            command => command.Color == comboBox.FocusRing);
        return new ArrowFrame(comboBox.Bounds, textClip.Value, arrow, ring, comboBox.Font);
    }

    private static BSize Measure(UiElement element)
    {
        element.Measure(new BSize(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize;
    }

    private static int FindFillRect(BRenderList renderList, BColor color)
    {
        for (int index = 0; index < renderList.Commands.Count; index++)
        {
            if (renderList.Commands[index] is BRenderCommand.FillRect fill && fill.Color == color)
                return index;
        }

        return -1;
    }

    private static int FindRoundedFill(BRenderList renderList, BColor color)
    {
        for (int index = 0; index < renderList.Commands.Count; index++)
        {
            if (renderList.Commands[index] is BRenderCommand.FillRoundedRect fill && fill.Color == color)
                return index;
        }

        return -1;
    }

    private sealed class PopupOverlapRoot : UiElement
    {
        private readonly UiElement _comboBox;
        private readonly UiElement _overlappingSibling;

        public PopupOverlapRoot(UiElement comboBox, UiElement overlappingSibling)
        {
            _comboBox = comboBox;
            _overlappingSibling = overlappingSibling;
            AddChild(comboBox);
            AddChild(overlappingSibling);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _comboBox.Measure(availableSize);
            _overlappingSibling.Measure(availableSize);
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            _comboBox.Arrange(new BRect(10, 10, 80, 24));
            _overlappingSibling.Arrange(new BRect(10, 34, 80, 48));
        }
    }

    /// <summary>Lays its one child out at a fixed width and the height the child asks for.</summary>
    private sealed class WidthRoot : UiElement
    {
        private readonly UiElement _child;
        private readonly double _width;

        public WidthRoot(UiElement child, double width)
        {
            _child = child;
            _width = width;
            AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _child.Measure(new BSize(_width, double.PositiveInfinity));
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect) =>
            _child.Arrange(new BRect(10, 10, _width, _child.DesiredSize.Height));
    }

    private sealed class PaintElement : UiElement
    {
        private readonly BColor _color;

        public PaintElement(BColor color)
        {
            _color = color;
        }

        protected override BSize MeasureCore(BSize availableSize) => availableSize;

        protected override void RenderCore(UiRenderContext context) =>
            context.RenderList.FillRect(Bounds, _color);
    }

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1.0;

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }
}
