using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.UI.Dialog;
using Broiler.UI.ListView;
using Broiler.UI.Button;
using Broiler.UI.Window;

namespace Broiler.UI.AboutDialog.Standard;

/// <summary>
/// Standard implementation of an About dialog. Displays the product name, version, and a list of component versions.
/// </summary>
public sealed class StandardAboutDialog : UiAboutDialog, IStandardThemedControl
{
    // UI elements
    private readonly StandardLabel _titleLabel;
    private readonly StandardLabel _productLabel;
    private readonly StandardListView _componentList;
    private readonly StandardButton _okButton;

    // Layout bounds (computed during Arrange)
    private BRect _titleBounds;
    private BRect _productBounds;
    private BRect _listBounds;
    private BRect _okButtonBounds;

    // Theming properties (exposed via IStandardThemedControl)
    public BColor Background { get; set; } = StandardControlPaint.Surface;
    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;
    public BColor TextForeground { get; set; } = StandardControlPaint.Text;
    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public StandardAboutDialog()
    {
        // Default sizing – the dialog can be resized but we provide a reasonable preferred size.
        PreferredSize = new BSize(460, 320);
        MinimumSize = new BSize(380, 260);
        CanResize = true;

        // Title label (shows "About <ProductName>")
        _titleLabel = new StandardLabel { PreferredSize = new BSize(0, 30) };
        // Product label (shows "Version: X.Y.Z")
        _productLabel = new StandardLabel { PreferredSize = new BSize(0, 24) };
        // Component list – two columns: component name and version.
        _componentList = new StandardListView
        {
            PreferredSize = new BSize(0, 180),
            ItemHeight = 22,
            ColumnHeaders = new[] { "Component", "Version" },
            // The concrete UI will be populated in SyncContent().
        };
        // OK button to close the dialog.
        _okButton = new StandardButton
        {
            Text = "OK",
            IsDefault = true,
            PreferredSize = new BSize(80, 30),
        };

        // Hook up OK button.
        _okButton.Clicked += (_, _) => Accept();

        // Add children to the dialog hierarchy.
        AddChild(_titleLabel);
        AddChild(_productLabel);
        AddChild(_componentList);
        AddChild(_okButton);
    }

    // Refresh UI whenever any of the public properties change.
    public override void Invalidate(UiInvalidationKind kind = UiInvalidationKind.Render)
    {
        base.Invalidate(kind);
        if ((kind & UiInvalidationKind.Render) != 0)
            SyncContent();
    }

    private void SyncContent()
    {
        // Title combines static word "About" with the product name.
        _titleLabel.Text = $"About {ProductName}";
        _titleLabel.Foreground = TitleForeground;

        _productLabel.Text = $"Version: {ProductVersion}";
        _productLabel.Foreground = TextForeground;

        // Build list items from the component dictionary.
        var items = ComponentVersions.Select(kv => new UiListViewItem(kv.Key, kv.Value)).ToArray();
        _componentList.SetItems(items);
        _componentList.Foreground = TextForeground;
        _componentList.BorderColor = BorderColor;
    }

    // Measure the dialog – use children measurements and compute desired size.
    protected override BSize MeasureCore(BSize availableSize)
    {
        // Allow children to measure with unlimited space (they will size themselves).
        foreach (UiElement child in Children)
            child.Measure(availableSize);

        // Desired size is the preferred size respecting the available space.
        return new BSize(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));
    }

    // Arrange children within the final rectangle.
    protected override void ArrangeCore(BRect finalRect)
    {
        // Compute layout constants.
        double padding = 12;
        double gap = 8;
        double buttonHeight = 30;
        double titleHeight = 30;
        double productHeight = 24;
        double listTop = finalRect.Top + padding + titleHeight + gap + productHeight + gap;
        double listHeight = Math.Max(0, finalRect.Height - padding * 2 - titleHeight - productHeight - buttonHeight - gap * 4);

        _titleBounds = new BRect(finalRect.Left + padding, finalRect.Top + padding, finalRect.Width - padding * 2, titleHeight);
        _productBounds = new BRect(finalRect.Left + padding, _titleBounds.Bottom + gap, finalRect.Width - padding * 2, productHeight);
        _listBounds = new BRect(finalRect.Left + padding, listTop, finalRect.Width - padding * 2, listHeight);
        _okButtonBounds = new BRect(finalRect.Right - padding - _okButton.PreferredSize.Width, finalRect.Bottom - padding - buttonHeight, _okButton.PreferredSize.Width, buttonHeight);

        _titleLabel.Arrange(_titleBounds);
        _productLabel.Arrange(_productBounds);
        _componentList.Arrange(_listBounds);
        _okButton.Arrange(_okButtonBounds);
    }

    // Rendering – simply delegate to children; background filled here.
    protected override void RenderCore(UiRenderContext ctx)
    {
        // Fill background.
        ctx.Renderer.FillRectangle(Background, BRect.Empty);
        // Children render themselves automatically after this call.
        base.RenderCore(ctx);
    }
}
