using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.UI.Standard;

namespace Broiler.UI.Splitter.Standard;

/// <summary>
/// A standard themed split container control dividing space between two panes with a <see cref="StandardSplitter"/>.
/// </summary>
public sealed class StandardSplitContainer : UiSplitContainer, IStandardThemedControl
{
    public StandardSplitContainer()
        : base(new StandardSplitter())
    {
    }

    public StandardSplitContainer(UiSplitter splitter)
        : base(splitter)
    {
    }

    public BColor Background { get; set; } = BColor.Transparent;

    public StandardSplitter? TypedSplitter => Splitter as StandardSplitter;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        if (Splitter is IStandardThemedControl themedSplitter)
            themedSplitter.ApplyTheme(theme);

        Invalidate(UiInvalidationKind.Render);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        base.RenderCore(context);
    }
}
