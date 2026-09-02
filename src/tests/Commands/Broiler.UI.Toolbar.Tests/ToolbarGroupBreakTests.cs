using Broiler.Graphics;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// Coverage for grouping a bar with space instead of ink.
/// </summary>
/// <remarks>
/// A bar used to have one way to start a group, and it both opened a gap and drew a rule in it. A
/// bar of evenly spaced controls reads as one long run, but a rule between every group reads as a
/// form, so the useful middle - extra space, no extra ink - was not expressible.
/// </remarks>
public sealed class ToolbarGroupBreakTests
{
    [Fact(Timeout = 600000)]
    public void A_Gap_Opens_Space_And_Draws_No_Rule()
    {
        var host = new TestHost(new BSize(400, 64));
        using UiSession session = CreateSession(host);
        StandardToolbar toolbar = CreateToolbar();
        (StandardButton first, StandardButton second, StandardButton third) = AddThree(toolbar);
        toolbar.SetBreakBefore(third, UiToolbarBreak.Gap);
        session.AddRoot(toolbar);

        BRenderList rendered = session.RenderFrame();

        Assert.Equal(UiToolbarBreak.Gap, toolbar.GetBreakBefore(third));
        Assert.False(toolbar.GetSeparatorBefore(third));

        // The gap is exactly GroupExtent wider than the plain spacing between the first two.
        double plain = second.Bounds.Left - first.Bounds.Right;
        double grouped = third.Bounds.Left - second.Bounds.Right;
        Assert.Equal(toolbar.Spacing, plain, 3);
        Assert.Equal(toolbar.Spacing + toolbar.GroupExtent, grouped, 3);

        Assert.DoesNotContain(
            rendered.Commands.OfType<BRenderCommand.FillRect>(),
            command => command.Color == toolbar.SeparatorColor);
    }

    [Fact(Timeout = 600000)]
    public void A_Separator_Still_Opens_Space_And_Draws_Its_Rule()
    {
        var host = new TestHost(new BSize(400, 64));
        using UiSession session = CreateSession(host);
        StandardToolbar toolbar = CreateToolbar();
        (_, StandardButton second, StandardButton third) = AddThree(toolbar);
        toolbar.SetBreakBefore(third, UiToolbarBreak.Separator);
        session.AddRoot(toolbar);

        BRenderList rendered = session.RenderFrame();

        Assert.True(toolbar.GetSeparatorBefore(third));
        Assert.Equal(toolbar.Spacing + toolbar.SeparatorExtent, third.Bounds.Left - second.Bounds.Right, 3);
        Assert.Contains(
            rendered.Commands.OfType<BRenderCommand.FillRect>(),
            command => command.Color == toolbar.SeparatorColor);
    }

    [Fact(Timeout = 600000)]
    public void The_Legacy_Separator_Api_Round_Trips_Through_The_Break_Kind()
    {
        var host = new TestHost(new BSize(400, 64));
        using UiSession session = CreateSession(host);
        StandardToolbar toolbar = CreateToolbar();
        (_, _, StandardButton third) = AddThree(toolbar);
        session.AddRoot(toolbar);

        toolbar.SetSeparatorBefore(third, true);
        Assert.Equal(UiToolbarBreak.Separator, toolbar.GetBreakBefore(third));

        toolbar.SetSeparatorBefore(third, false);
        Assert.Equal(UiToolbarBreak.None, toolbar.GetBreakBefore(third));

        // A gap is not a separator, so the older question has to answer no rather than throw.
        toolbar.SetBreakBefore(third, UiToolbarBreak.Gap);
        Assert.False(toolbar.GetSeparatorBefore(third));
    }

    [Fact(Timeout = 600000)]
    public void A_Break_Is_Forgotten_With_The_Child_It_Belonged_To()
    {
        var host = new TestHost(new BSize(400, 64));
        using UiSession session = CreateSession(host);
        StandardToolbar toolbar = CreateToolbar();
        (_, _, StandardButton third) = AddThree(toolbar);
        toolbar.SetBreakBefore(third, UiToolbarBreak.Gap);
        session.AddRoot(toolbar);

        toolbar.RemoveChild(third);

        Assert.Equal(UiToolbarBreak.None, toolbar.GetBreakBefore(third));
    }

    private static StandardToolbar CreateToolbar() =>
        new()
        {
            Title = "Grouped",
            Padding = 5,
            Spacing = 6,
            GroupExtent = 10,
            SeparatorExtent = 9,
            PreferredSize = new BSize(400, 44),
        };

    private static (StandardButton First, StandardButton Second, StandardButton Third) AddThree(StandardToolbar toolbar)
    {
        var first = new StandardButton { Text = "One", PreferredSize = new BSize(48, 30) };
        var second = new StandardButton { Text = "Two", PreferredSize = new BSize(48, 30) };
        var third = new StandardButton { Text = "Three", PreferredSize = new BSize(64, 30) };
        toolbar.AddChild(first);
        toolbar.AddChild(second);
        toolbar.AddChild(third);
        return (first, second, third);
    }

    private static UiSession CreateSession(TestHost host) =>
        new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
