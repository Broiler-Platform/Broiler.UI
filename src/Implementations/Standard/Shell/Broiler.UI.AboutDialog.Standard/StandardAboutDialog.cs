using System;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.UI.Button.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.AboutDialog.Standard;

/// <summary>A themed About dialog with a scrollable version list and an OK action.</summary>
public sealed class StandardAboutDialog : UiAboutDialog, IStandardThemedControl
{
    private readonly UiWindowChromeController _chrome;
    private readonly StandardLabel _titleLabel;
    private readonly StandardLabel _productLabel;
    private readonly StandardLabel _componentsLabel;
    private readonly StandardListView _componentList;
    private readonly StandardButton _okButton;

    public StandardAboutDialog()
    {
        _chrome = new UiWindowChromeController(this) { Metrics = UiWindowChromeMetrics.Compact };
        _titleLabel = new StandardLabel { Font = BFontStyle.Default with { Size = 18, Weight = BFontWeight.SemiBold } };
        _productLabel = new StandardLabel();
        _componentsLabel = new StandardLabel();
        _componentList = new StandardListView { ItemHeight = 24, CornerRadius = 0 };
        _okButton = new StandardButton
        {
            Text = "OK",
            IsDefault = true,
            PreferredSize = new BSize(80, 30),
            PaddingY = 5,
        };
        _okButton.Clicked += (_, _) => Accept();
        AddChild(_titleLabel);
        AddChild(_productLabel);
        AddChild(_componentsLabel);
        AddChild(_componentList);
        AddChild(_okButton);

        // Capture after creating the controls so their assemblies are included too.
        PopulateFromAssemblies();
    }

    public BSize PreferredSize { get; set; } = new(620, 380);
    public BColor Background { get; set; } = StandardControlPaint.Surface;
    public BColor TitleBarBackground { get; set; } = StandardControlPaint.SurfaceAlt;
    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;
    public BColor TextForeground { get; set; } = StandardControlPaint.Text;
    public BColor BorderColor { get; set; } = StandardControlPaint.Border;
    public StandardButton OkButton => _okButton;
    public StandardListView ComponentList => _componentList;
    public UiWindowChromeLayout ChromeLayout => _chrome.Layout;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        TextForeground = theme.Text;
        BorderColor = theme.Border;
        foreach (IStandardThemedControl child in Children.OfType<IStandardThemedControl>())
            child.ApplyTheme(theme);
        Invalidate(UiInvalidationKind.Render);
    }

    protected override void OnContentChanged()
    {
        _titleLabel.Text = string.IsNullOrWhiteSpace(ProductName) ? "About" : $"About {ProductName}";
        _productLabel.Text = string.IsNullOrWhiteSpace(ProductVersion) ? "Version: Unknown" : $"Version: {ProductVersion}";
        _componentsLabel.Text = ComponentVersions.Count == 0 ? "No component version information available." : "Component versions";
        _componentList.SetItems(ComponentVersions.Select(component =>
            new UiListItem(component.Key, $"{component.Key}  —  {component.Value}")));
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        foreach (UiElement child in Children)
            child.Measure(availableSize);
        return new BSize(ClampDesired(PreferredSize.Width, availableSize.Width), ClampDesired(PreferredSize.Height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));
        BRect client = StandardControlPaint.Inset(_chrome.UpdateLayout(finalRect).Content, 12);
        double buttonHeight = Math.Min(30, client.Height);
        double buttonWidth = Math.Min(80, client.Width);
        double buttonTop = client.Bottom - buttonHeight;
        _okButton.Arrange(new BRect(client.Right - buttonWidth, buttonTop, buttonWidth, buttonHeight));

        // Reserve the action row first so dismissal stays reachable in a small host viewport.
        double contentBottom = Math.Max(client.Top, buttonTop - 8);
        double top = client.Top;
        ArrangeLabel(_titleLabel, 30);
        ArrangeLabel(_productLabel, 24);
        ArrangeLabel(_componentsLabel, 22);
        _componentList.Arrange(new BRect(client.Left, top, client.Width, Math.Max(0, contentBottom - top)));

        void ArrangeLabel(StandardLabel label, double desiredHeight)
        {
            double height = Math.Min(desiredHeight, Math.Max(0, contentBottom - top));
            label.Arrange(new BRect(client.Left, top, client.Width, height));
            top = Math.Min(contentBottom, top + height + 8);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        double radius = IsBrokenOut ? 0 : 8;
        StandardControlPaint.FillRounded(context.RenderList, Bounds, Background, radius);
        UiWindowChromeLayout layout = _chrome.Layout;
        if (layout.IsVisible)
        {
            StandardWindowChromePaint.FillTitleBar(context.RenderList, layout.TitleBar, TitleBarBackground, radius);
            if (Icon is not null)
                StandardWindowChromePaint.DrawIcon(context.RenderList, layout.Icon, Icon.Image);
            StandardWindowChromePaint.DrawTitleText(context.RenderList, layout.Title, Title, BFontStyle.Default, TitleForeground);
            StandardWindowChromePaint.DrawButton(context.RenderList, layout.CloseButton, StandardWindowChromeGlyph.Close,
                _chrome.HotPart == UiWindowChromePart.Close, _chrome.PressedPart == UiWindowChromePart.Close, TitleForeground);
        }
        _titleLabel.Foreground = TitleForeground;
        _productLabel.Foreground = TextForeground;
        _componentsLabel.Foreground = TextForeground;
        _componentList.Foreground = TextForeground;
        _componentList.BorderColor = BorderColor;
        base.RenderCore(context);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, radius, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (_chrome.HandleInput(input) || base.OnInput(input))
            return true;
        if (input.Kind != UiInputEventKind.KeyboardKey || input.KeyTransition != KeyboardKeyTransition.Down)
            return false;
        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
        {
            if (!_okButton.IsEnabled)
                return false;
            _okButton.Click();
            return true;
        }
        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        _chrome.Layout.HitTest(position) == UiWindowChromePart.TitleBar;

    private static bool IsKey(UiInputEvent input, int code, string name) =>
        input.NativeKeyCode == code || string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + code.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
