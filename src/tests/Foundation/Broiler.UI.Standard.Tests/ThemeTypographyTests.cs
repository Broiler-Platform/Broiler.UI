using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Forms.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.ListView.Standard;
using Broiler.UI.TabView.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

/// <summary>Controls and labels take their fonts from the theme and follow a text-scaled theme.</summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ThemeTypographyTests
{
    [Fact]
    public void Text_Scale_Scales_Every_Font_Without_Compounding()
    {
        StandardThemeTokens light = StandardThemeTokens.Light;
        StandardThemeTokens large = light.WithTextScale(1.5);

        Assert.Equal(1.5, large.TextScale);
        Assert.Equal(light.FontBody.Size * 1.5, large.FontBody.Size, 2);
        Assert.Equal(light.FontTitle.Size * 1.5, large.FontTitle.Size, 2);
        Assert.Equal(light.FontSubtitle.Size * 1.5, large.FontSubtitle.Size, 2);
        Assert.Equal(light.FontCaption.Size * 1.5, large.FontCaption.Size, 2);
        Assert.Equal(light.FontCode.Size * 1.5, large.FontCode.Size, 2);
        Assert.Equal(light.FontTitle.Weight, large.FontTitle.Weight);
        // Scaling a scaled theme is relative to the unscaled sizes.
        Assert.Equal(light.FontBody.Size * 2, large.WithTextScale(2).FontBody.Size, 2);
        Assert.Same(light, light.WithTextScale(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => light.WithTextScale(0));
    }

    [Fact]
    public void System_Settings_Carry_The_Text_Scale_Into_The_Theme()
    {
        var settings = UiSystemSettings.Default with { TextScale = 1.25 };
        Assert.Equal(StandardThemeTokens.Light.FontBody.Size * 1.25, StandardThemeTokens.Select(settings).FontBody.Size, 2);
        Assert.Equal(StandardThemeTokens.Light.FontBody.Size, StandardThemeTokens.Select(settings with { TextScale = 0 }).FontBody.Size, 2);
    }

    [Fact]
    public void The_Type_Scale_Is_Ranked_Around_The_Body_Size_Controls_Draw()
    {
        StandardThemeTokens theme = StandardThemeTokens.Light;
        Assert.Equal(BFontStyle.Default.Size, theme.FontBody.Size);
        Assert.True(theme.FontTitle.Size > theme.FontSubtitle.Size);
        Assert.True(theme.FontSubtitle.Size > theme.FontBody.Size);
        Assert.True(theme.FontCaption.Size < theme.FontBody.Size);
    }

    [Fact]
    public void Controls_Follow_A_Text_Scaled_Theme_And_Grow()
    {
        StandardThemeTokens previous = StandardControlPaint.Theme;
        try
        {
            var button = new StandardButton { Text = "Send" };
            var edit = new StandardEdit { Text = "someone@example.test" };
            var list = new StandardListView();
            var tabs = new StandardTabView();
            var combo = new StandardComboBox();
            var label = new StandardLabel("Body text");
            var title = StandardLabel.Title("Subject");
            var section = new FormSection("Account identity");
            var root = new Container(button, edit, list, tabs, combo, label, title, section);
            using var session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new Host());
            session.AddRoot(root);
            session.RenderFrame();
            double buttonWidth = button.DesiredSize.Width;
            double labelHeight = label.DesiredSize.Height;
            StandardLabel heading = Descendants(section).OfType<StandardLabel>().First(item => item.Text == "Account identity");
            Assert.Equal(StandardThemeTokens.Light.FontSubtitle, heading.Font);

            StandardThemeTokens large = StandardThemeTokens.Light.WithTextScale(2);
            StandardThemeController.Apply(session, large);
            session.RenderFrame();

            foreach (BFontStyle font in new[] { button.Font, edit.Font, list.Font, tabs.Font, combo.Font, label.Font })
                Assert.Equal(large.FontBody, font);
            Assert.Equal(large.FontTitle, title.Font);
            Assert.Equal(large.FontSubtitle, heading.Font);
            Assert.True(button.DesiredSize.Width > buttonWidth, "A larger font must re-measure the button.");
            Assert.True(label.DesiredSize.Height > labelHeight, "A larger font must re-measure the label.");
        }
        finally
        {
            StandardControlPaint.ApplyTheme(previous);
        }
    }

    [Fact]
    public void A_Font_The_Application_Set_Is_Kept()
    {
        StandardThemeTokens previous = StandardControlPaint.Theme;
        try
        {
            var custom = new BFontStyle("Consolas", 11);
            var button = new StandardButton { Text = "Code", Font = custom };
            var label = new StandardLabel("Fixed") { Font = custom };
            using var session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new Host());
            session.AddRoot(new Container(button, label));

            StandardThemeController.Apply(session, StandardThemeTokens.Dark.WithTextScale(1.5));
            Assert.Equal(custom, button.Font);
            Assert.Equal(custom, label.Font);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(previous);
        }
    }

    [Fact]
    public void Controls_Created_After_A_Theme_Change_Start_With_Its_Fonts()
    {
        StandardThemeTokens previous = StandardControlPaint.Theme;
        try
        {
            StandardThemeTokens large = StandardThemeTokens.Light.WithTextScale(1.5);
            StandardControlPaint.ApplyTheme(large);
            Assert.Equal(large.FontBody, new StandardButton().Font);
            Assert.Equal(large.FontBody, new StandardLabel("New").Font);
            Assert.Equal(large.FontCaption, StandardLabel.Caption("Note").Font);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(previous);
        }
    }

    [Fact]
    public void List_Rows_And_Tab_Headers_Keep_Their_Sizes_And_Grow_With_Larger_Text()
    {
        StandardThemeTokens previous = StandardControlPaint.Theme;
        try
        {
            var twoLine = new StandardListView { ItemPresenter = StandardTwoLineListItemPresenter.Instance };
            var single = new StandardListView();
            var tabs = new StandardTabView();
            using var session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new Host());
            session.AddRoot(new Container(twoLine, single, tabs));
            session.RenderFrame();
            // The default font keeps today's sizes.
            Assert.Equal(52, twoLine.EffectiveItemHeight);
            Assert.Equal(28, single.EffectiveItemHeight);
            Assert.Equal(tabs.HeaderHeight, tabs.EffectiveHeaderHeight);

            StandardThemeController.Apply(session, StandardThemeTokens.Light.WithTextScale(2));
            session.RenderFrame();
            double primary = BTextMeasurer.GetLineHeight(twoLine.Font);
            Assert.True(twoLine.EffectiveItemHeight >= primary * 2, $"Two lines of {primary} need more than {twoLine.EffectiveItemHeight}.");
            Assert.True(single.EffectiveItemHeight > 28);
            Assert.True(tabs.EffectiveHeaderHeight >= BTextMeasurer.GetLineHeight(tabs.Font));
        }
        finally
        {
            StandardControlPaint.ApplyTheme(previous);
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
            foreach (UiElement nested in Descendants(child))
                yield return nested;
    }

    private sealed class Container : UiElement
    {
        public Container(params UiElement[] children)
        {
            foreach (UiElement child in children)
                AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double height = 0;
            foreach (UiElement child in Children)
                height += child.Measure(new BSize(availableSize.Width, double.PositiveInfinity)).Height;
            return new BSize(availableSize.Width, height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double y = finalRect.Top;
            foreach (UiElement child in Children)
            {
                child.Arrange(new BRect(finalRect.Left, y, finalRect.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
        }
    }

    private sealed class Host : IUiHost
    {
        public BSize ViewportSize => new(800, 2000);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
