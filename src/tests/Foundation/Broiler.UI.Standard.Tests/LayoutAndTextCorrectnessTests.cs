using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.TabView;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class LayoutAndTextCorrectnessTests
{
    [Fact]
    public void TabView_AllocationAware_Measures_Content_At_Available_Width_Not_Preferred_Size()
    {
        using var tabView = new StandardTabView();
        var measuredSizes = new List<BSize>();
        var content = new MeasuringElement(size => measuredSizes.Add(size), new BSize(200, 100));

        tabView.AddTab("tab1", "Tab 1", content);

        // Measure TabView with available size 600x400 (larger than PreferredSize 320x220)
        BSize available = new(600, 400);
        BSize desired = tabView.Measure(available);

        // Tab content must be measured with available width 600 and available height (400 - HeaderHeight = 368)
        Assert.NotEmpty(measuredSizes);
        BSize lastMeasured = measuredSizes[^1];
        Assert.Equal(600, lastMeasured.Width);
        Assert.Equal(368, lastMeasured.Height);

        // Arrange at 600x400
        tabView.Arrange(new BRect(0, 0, 600, 400));
        Assert.Equal(new BRect(0, tabView.HeaderHeight, 600, 368), content.Bounds);
    }

    [Fact]
    public void ScrollView_ConstrainWidth_Wraps_Content_To_Viewport_Without_Oscillation()
    {
        using var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 12,
            Constraint = UiScrollConstraint.ConstrainWidth,
        };

        // A content element whose height depends inversely on width (like wrapping text)
        var content = new WrappingTextElement(lineCountAt400: 10, lineHeight: 20); // 10 lines * 20 = 200px at width 400; at narrower width, more lines
        scrollView.AddChild(content);

        // Available size 400x150 -> content height will exceed 150, so vertical scrollbar is needed
        BSize outerSize = new(400, 150);
        scrollView.Measure(outerSize);
        scrollView.Arrange(new BRect(0, 0, 400, 150));

        // Vertical scrollbar should be present, horizontal scrollbar should NOT
        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.False(scrollView.HasHorizontalScrollbar);

        // Viewport and content bounds width should be exactly 400 - 12 = 388
        Assert.Equal(388, scrollView.ContentBounds.Width);
        Assert.Equal(388, content.Bounds.Width);
        Assert.True(content.Bounds.Height > 150);

        // Child content width was constrained to ContentBounds.Width
        Assert.Equal(388, content.LastMeasuredWidth);
    }

    [Fact]
    public void ScrollView_Old_TwoDimensional_Scroll_Modes_Remain_Valid()
    {
        using var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 12,
            Constraint = UiScrollConstraint.None,
        };

        var content = new FixedElement(new BSize(800, 600));
        scrollView.AddChild(content);

        scrollView.Measure(new BSize(400, 300));
        scrollView.Arrange(new BRect(0, 0, 400, 300));

        // In unconstrained 2D mode, both scrollbars appear for large content
        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.True(scrollView.HasHorizontalScrollbar);
        Assert.Equal(800, content.Bounds.Width);
        Assert.Equal(600, content.Bounds.Height);
    }

    [Fact]
    public void ScrollView_ConstrainHeight_Constrains_Content_Height_To_Viewport()
    {
        using var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 12,
            Constraint = UiScrollConstraint.ConstrainHeight,
        };

        var measured = new List<BSize>();
        var content = new MeasuringElement(size => measured.Add(size), new BSize(600, 50));
        scrollView.AddChild(content);

        scrollView.Measure(new BSize(300, 200));
        scrollView.Arrange(new BRect(0, 0, 300, 200));

        Assert.NotEmpty(measured);
        // Height was constrained, width was infinite
        Assert.True(double.IsPositiveInfinity(measured[^1].Width));
        Assert.True(measured[^1].Height <= 200);
    }

    [Fact]
    public void StandardLabel_Caches_Line_Layout_Across_Measure_And_Render()
    {
        using var label = new StandardLabel
        {
            Text = "The quick brown fox jumps over the lazy dog and wraps to several lines of text.",
            Wrapping = UiTextWrapping.Wrap,
        };

        var host = new TestHost(new BSize(150, 400));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(label);

        label.Measure(new BSize(150, 400));
        int buildsAfterMeasure = label.LayoutBuildCount;
        Assert.Equal(1, buildsAfterMeasure);

        // Render with the same width (150)
        label.Arrange(new BRect(0, 0, 150, 200));
        session.RenderFrame();

        // Layout must be cached: zero additional builds during render
        Assert.Equal(buildsAfterMeasure, label.LayoutBuildCount);

        // Render again: still zero additional builds
        session.RenderFrame();
        Assert.Equal(buildsAfterMeasure, label.LayoutBuildCount);

        // Invalidate by changing text: layout should be rebuilt on next measure
        label.Text = "Different text content.";
        label.Measure(new BSize(150, 400));
        Assert.Equal(buildsAfterMeasure + 1, label.LayoutBuildCount);
    }

    [Fact]
    public void StandardLabel_Unicode_Segmentation_Never_Breaks_Surrogate_Pairs()
    {
        // Unbroken string of emoji surrogate pairs
        string emojiString = string.Concat(Enumerable.Repeat("😀🦆🎉🚀🔥", 10));
        using var label = new StandardLabel
        {
            Text = emojiString,
            Wrapping = UiTextWrapping.Wrap,
        };

        BRenderList list = RenderElement(label, new BSize(30, 1000));
        IEnumerable<BRenderCommand.DrawText> textCommands = list.Commands.OfType<BRenderCommand.DrawText>();
        Assert.NotEmpty(textCommands);

        foreach (BRenderCommand.DrawText command in textCommands)
        {
            AssertValidSurrogates(command.Text.Text);
        }
    }

    [Fact]
    public void StandardLabel_Unicode_Segmentation_Trimming_Does_Not_Break_Surrogates()
    {
        string emojiString = "😀🦆🎉🚀🔥";
        for (double width = 10; width <= 100; width += 5)
        {
            using var label = new StandardLabel
            {
                Text = emojiString,
                Wrapping = UiTextWrapping.NoWrap,
                Trimming = UiTextTrimming.CharacterEllipsis,
            };

            BRenderList list = RenderElement(label, new BSize(width, 50));
            foreach (BRenderCommand.DrawText command in list.Commands.OfType<BRenderCommand.DrawText>())
            {
                AssertValidSurrogates(command.Text.Text);
            }
        }
    }

    [Fact]
    public void Label_Literal_Ampersand_Survives_In_Rendering_And_Accessibility()
    {
        using var literalLabel = new StandardLabel
        {
            Text = "Salt & Pepper",
            UseMnemonic = false,
        };

        Assert.True(literalLabel.IsLiteral);
        Assert.Equal("Salt & Pepper", literalLabel.DisplayText);
        Assert.Null(literalLabel.EffectiveAccessKey);

        UiSemanticNode node = literalLabel.GetSemanticNode();
        Assert.Equal("Salt & Pepper", node.Name);

        // Default mnemonic label for comparison
        using var mnemonicLabel = new StandardLabel
        {
            Text = "Salt &Pepper",
            UseMnemonic = true,
        };

        Assert.False(mnemonicLabel.IsLiteral);
        Assert.Equal("Salt Pepper", mnemonicLabel.DisplayText);
        Assert.Equal('P', mnemonicLabel.EffectiveAccessKey);
    }

    private static void AssertValidSurrogates(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                Assert.True(i + 1 < text.Length, $"High surrogate at index {i} in '{text}' must have a following low surrogate.");
                Assert.True(char.IsLowSurrogate(text[i + 1]), $"Character after high surrogate at {i} must be low surrogate.");
                i++; // Skip the paired low surrogate
            }
            else
            {
                Assert.False(char.IsLowSurrogate(text[i]), $"Found isolated low surrogate at index {i} in '{text}'.");
            }
        }
    }

    private static BRenderList RenderElement(UiElement element, BSize size)
    {
        var host = new TestHost(size);
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(element);
        element.Measure(size);
        element.Arrange(new BRect(0, 0, size.Width, size.Height));
        return session.RenderFrame();
    }

    private sealed class MeasuringElement : UiElement
    {
        private readonly Action<BSize> _onMeasure;
        private readonly BSize _desired;

        public MeasuringElement(Action<BSize> onMeasure, BSize desired)
        {
            _onMeasure = onMeasure;
            _desired = desired;
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _onMeasure(availableSize);
            return _desired;
        }
    }

    private sealed class WrappingTextElement : UiElement
    {
        private readonly int _lineCountAt400;
        private readonly double _lineHeight;

        public WrappingTextElement(int lineCountAt400, double lineHeight)
        {
            _lineCountAt400 = lineCountAt400;
            _lineHeight = lineHeight;
        }

        public double LastMeasuredWidth { get; private set; }

        protected override BSize MeasureCore(BSize availableSize)
        {
            LastMeasuredWidth = availableSize.Width;
            double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 400;
            double factor = 400.0 / Math.Max(1.0, width);
            double height = _lineCountAt400 * factor * _lineHeight;
            return new BSize(width, height);
        }
    }

    private sealed class FixedElement : UiElement
    {
        private readonly BSize _size;
        public FixedElement(BSize size) => _size = size;
        protected override BSize MeasureCore(BSize availableSize) => _size;
    }

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize) => ViewportSize = viewportSize;
        public BSize ViewportSize { get; }
        public double Scale => 1.0;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
