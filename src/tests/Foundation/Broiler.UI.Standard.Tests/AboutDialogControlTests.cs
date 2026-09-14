using System.Reflection;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.AboutDialog;
using Broiler.UI.AboutDialog.Standard;
using Broiler.UI.Dialog;
using Broiler.UI.Label.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class AboutDialogControlTests
{
    [Fact]
    public void Defaults_Include_Actual_Loaded_Component_Versions()
    {
        using var dialog = new StandardAboutDialog();
        Assert.False(string.IsNullOrWhiteSpace(dialog.ProductName));
        Assert.False(string.IsNullOrWhiteSpace(dialog.ProductVersion));
        Assembly assembly = typeof(UiAboutDialog).Assembly;
        string expected = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        Assert.Equal(expected, dialog.ComponentVersions[assembly.GetName().Name!]);
        Assert.Contains(dialog.ComponentList.Items, item => item.Text.Contains(expected));
        Assert.All(dialog.ComponentVersions.Keys, name => Assert.StartsWith("Broiler.", name));
    }

    [Fact]
    public void Explicit_Assemblies_Replace_The_Default_Metadata()
    {
        using var dialog = new StandardAboutDialog();
        Assembly product = typeof(AboutDialogControlTests).Assembly;
        dialog.PopulateFromAssemblies(product, [typeof(string).Assembly]);
        Assert.Equal(product.GetCustomAttribute<AssemblyProductAttribute>()!.Product, dialog.ProductName);
        Assert.Equal(product.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0], dialog.ProductVersion);
        Assert.Equal(typeof(string).Assembly.GetName().Name, Assert.Single(dialog.ComponentVersions).Key);
    }

    [Fact]
    public void Property_Changes_Refresh_Content_And_Copy_The_Component_Dictionary()
    {
        using var dialog = new StandardAboutDialog();
        var versions = new Dictionary<string, string> { ["Zeta"] = "2.0", ["Alpha"] = "1.2-preview.3" };
        dialog.ProductName = "Example";
        dialog.ProductVersion = "3.2-preview.1";
        dialog.ComponentVersions = versions;
        versions["Alpha"] = "changed";
        Assert.Equal(new[] { "Alpha", "Zeta" }, dialog.ComponentList.Items.Select(item => item.Id));
        Assert.Contains("1.2-preview.3", dialog.ComponentList.Items[0].Text);
        Assert.Contains(dialog.Children.OfType<StandardLabel>(), label => label.Text == "About Example");
        Assert.Contains(dialog.Children.OfType<StandardLabel>(), label => label.Text == "Version: 3.2-preview.1");
        dialog.ComponentVersions = new Dictionary<string, string>();
        Assert.Empty(dialog.ComponentList.Items);
        Assert.Contains(dialog.Children.OfType<StandardLabel>(), label => label.Text == "No component version information available.");
    }

    [Theory]
    [InlineData("Enter", UiDialogResultKind.Accepted)]
    [InlineData("Escape", UiDialogResultKind.Cancelled)]
    public async Task Keyboard_Dismisses_Modal_Dialog_From_Component_List(string key, UiDialogResultKind expected)
    {
        using UiSession session = new StandardUiSessionBuilder().Build(new TestHost());
        var owner = new StandardWindow();
        session.AddRoot(owner);
        session.SetFocus(owner);
        var dialog = new StandardAboutDialog();
        Task<UiDialogResult> result = dialog.ShowModal(owner);
        session.SetFocus(dialog.ComponentList);
        Assert.True(session.DispatchInput(Key(key)));
        Assert.Equal(expected, (await result.WaitAsync(TimeSpan.FromSeconds(2))).Kind);
        Assert.False(dialog.IsPresented);
        Assert.Same(owner, session.FocusedElement);
    }

    [Theory]
    [InlineData(false, UiDialogResultKind.Accepted)]
    [InlineData(true, UiDialogResultKind.Closed)]
    public async Task Pointer_Can_Dismiss_Through_Ok_Or_Title_Bar(bool useChrome, UiDialogResultKind expected)
    {
        using UiSession session = new StandardUiSessionBuilder().Build(new TestHost());
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var dialog = new StandardAboutDialog();
        Task<UiDialogResult> result = dialog.ShowModal(owner, new BRect(20, 20, 620, 380));
        session.RenderFrame();
        BRect target = useChrome ? dialog.ChromeLayout.CloseButton : dialog.OkButton.Bounds;
        Assert.False(target.IsEmpty);
        Assert.True(session.DispatchInput(Mouse(target, MouseButtonTransition.Down)));
        Assert.True(session.DispatchInput(Mouse(target, MouseButtonTransition.Up)));
        Assert.Equal(expected, (await result.WaitAsync(TimeSpan.FromSeconds(2))).Kind);
    }

    [Fact]
    public void Rendering_Fills_Background_And_Preserves_List_Position()
    {
        using UiSession session = new StandardUiSessionBuilder().Build(new TestHost());
        var dialog = new StandardAboutDialog
        {
            ComponentVersions = Enumerable.Range(0, 100).ToDictionary(i => $"Component {i:D3}", _ => "1.0"),
        };
        session.AddRoot(dialog);
        session.RenderFrame();
        dialog.ComponentList.SelectItem("Component 099");
        dialog.ComponentList.ScrollIntoView("Component 099");
        double offset = dialog.ComponentList.VerticalOffset;
        Assert.True(offset > 0);
        dialog.Invalidate(UiInvalidationKind.Render);
        BRenderList rendered = session.RenderFrame();
        Assert.Equal(offset, dialog.ComponentList.VerticalOffset);
        Assert.Equal("Component 099", dialog.ComponentList.SelectedItemId);
        Assert.Contains(rendered.Commands.OfType<BRenderCommand.FillRoundedRect>(), command => command.Rect == dialog.Bounds);
    }

    [Theory]
    [InlineData(620, 380)]
    [InlineData(380, 260)]
    [InlineData(220, 160)]
    public void Layout_Keeps_Children_And_Action_Inside_The_Dialog(double width, double height)
    {
        using var dialog = new StandardAboutDialog();
        var bounds = new BRect(17, 29, width, height);
        dialog.Measure(bounds.Size);
        dialog.Arrange(bounds);
        Assert.Equal(30, dialog.OkButton.Bounds.Height);
        Assert.True(dialog.ComponentList.Bounds.Bottom < dialog.OkButton.Bounds.Top);
        foreach (UiElement child in dialog.Children)
        {
            Assert.True(child.Bounds.Left >= bounds.Left && child.Bounds.Right <= bounds.Right);
            Assert.True(child.Bounds.Top >= bounds.Top && child.Bounds.Bottom <= bounds.Bottom);
        }
    }

    [Fact]
    public void Theme_Reaches_The_List_And_Button()
    {
        using var dialog = new StandardAboutDialog();
        dialog.ApplyTheme(StandardThemeTokens.Dark);
        Assert.Equal(StandardThemeTokens.Dark.Surface, dialog.Background);
        Assert.Equal(StandardThemeTokens.Dark.Surface, dialog.ComponentList.Background);
        Assert.Equal(StandardThemeTokens.Dark.Text, dialog.ComponentList.Foreground);
        Assert.Equal(StandardThemeTokens.Dark.Accent, dialog.OkButton.PrimaryBackground);
    }

    [Theory]
    [InlineData(UiHostWindowChrome.System, false)]
    [InlineData(UiHostWindowChrome.Owner, true)]
    public async Task Native_Dialog_Uses_Host_Chrome_And_Dismisses(UiHostWindowChrome chrome, bool showsTitleBar)
    {
        var host = new UiWindowBreakOutTests.FakeWindowHost { ChromeOverride = chrome };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var dialog = new StandardAboutDialog();
        Task<UiDialogResult> result = dialog.ShowModal(owner);
        var nativeWindow = Assert.Single(host.Created);
        Assert.True(dialog.IsBrokenOut);
        nativeWindow.BoundSession!.RenderFrame();
        Assert.Equal(showsTitleBar, dialog.ChromeLayout.IsVisible);
        Assert.True(dialog.OkButton.Bounds.Bottom <= nativeWindow.ViewportSize.Height);
        nativeWindow.BoundSession.SetFocus(dialog.ComponentList);
        Assert.True(nativeWindow.BoundSession.DispatchInput(Key("Escape")));
        Assert.Equal(UiDialogResultKind.Cancelled, (await result.WaitAsync(TimeSpan.FromSeconds(2))).Kind);
        Assert.True(nativeWindow.IsDisposed);
    }

    private static InputEventHeader Header() => new(InputDeviceId.FromOpaqueValue("about-test"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"), 1);
    private static UiInputEvent Key(string name) => UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
        Header(), KeyboardKey.FromName(name), KeyboardKeyTransition.Down, KeyboardModifierState.None, 0, 0, 0, false, false, Source: InputEventSource.Synthetic));
    private static UiInputEvent Mouse(BRect bounds, MouseButtonTransition transition) => UiInputEvent.FromMouseButton(new MouseButtonEvent(
        Header(), InputPoint.ClientDeviceIndependentPixels(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2),
        transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None, MouseButton.Left, transition, InputEventSource.Synthetic));

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize => new(800, 600);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
