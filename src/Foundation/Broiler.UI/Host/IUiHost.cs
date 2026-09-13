using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;

namespace Broiler.UI;

public interface IUiHost
{
    BSize ViewportSize { get; }

    double Scale { get; }

    BRenderList CreateRenderList(int capacity = 0);

    void Invalidate(UiInvalidation invalidation);

    void Present(BRenderList renderList);
}

