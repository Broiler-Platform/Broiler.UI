using Broiler.Graphics.Geometry;
using Broiler.UI.Window;

namespace Broiler.UI.Dialog.Standard;

/// <summary>Provides a modal preview window for page layout content supplied by the application.</summary>
public static class StandardPageLayoutDialog
{
    public static StandardDialog Create(bool canClose = true) =>
        new() { Title = "Page Layout", CanClose = canClose };

    public static void Show(StandardDialog dialog, UiWindow owner, BSize viewport) =>
        dialog.ShowModal(owner, StandardDialogPlacement.Center(viewport, new BSize(1000, double.PositiveInfinity)));
}
