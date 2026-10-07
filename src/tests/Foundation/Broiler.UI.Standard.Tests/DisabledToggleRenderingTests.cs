using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.RadioButton.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// A disabled checkbox or radio button shows its state in the disabled colour. A disabled checked box
/// filled itself with the surface and drew its tick white over it, so it looked unchecked; a disabled
/// checked radio button kept its accent dot, so it looked enabled.
/// </summary>
public sealed class DisabledToggleRenderingTests
{
    [Fact]
    public void A_Disabled_Checked_Box_Draws_Its_Tick_Over_The_Disabled_Colour()
    {
        var box = new StandardCheckBox { IsChecked = true, IsEnabled = false };

        BRenderList list = Render(box);

        BRenderCommand.FillRoundedRect fill = Assert.Single(list.Commands.OfType<BRenderCommand.FillRoundedRect>());
        Assert.Equal(box.DisabledForeground, fill.Color);
        BRenderCommand.DrawText tick = Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "✓");
        Assert.NotEqual(fill.Color, tick.Text.Color);
    }

    [Fact]
    public void A_Checked_Box_Is_Filled_With_The_Accent_And_A_Disabled_Unchecked_One_With_The_Surface()
    {
        var enabled = new StandardCheckBox { IsChecked = true };
        var empty = new StandardCheckBox { IsEnabled = false };

        Assert.Equal(enabled.Accent, Assert.Single(Render(enabled).Commands.OfType<BRenderCommand.FillRoundedRect>()).Color);
        Assert.Equal(StandardControlPaint.Surface, Assert.Single(Render(empty).Commands.OfType<BRenderCommand.FillRoundedRect>()).Color);
    }

    [Fact]
    public void A_Disabled_Checked_Radio_Button_Draws_Its_Dot_In_The_Disabled_Colour()
    {
        var enabled = new StandardRadioButton { IsChecked = true };
        var disabled = new StandardRadioButton { IsChecked = true, IsEnabled = false };

        Assert.Equal(enabled.Accent, Dot(Render(enabled)));
        Assert.Equal(disabled.DisabledForeground, Dot(Render(disabled)));

        // The ring's fill comes first, then the dot.
        static BColor Dot(BRenderList list) => list.Commands.OfType<BRenderCommand.FillRoundedRect>().Last().Color;
    }

    private static BRenderList Render(UiElement element)
    {
        UiSession session = new StandardUiSessionBuilder().Build(new TestHost());
        session.AddRoot(element);
        element.Measure(new BSize(200, 40));
        element.Arrange(new BRect(10, 10, 160, 24));
        BRenderList list = session.RenderFrame();
        list.Validate();
        return list;
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(400, 200);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
