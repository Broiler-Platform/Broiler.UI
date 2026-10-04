using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Forms.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// An expandable element reports exactly one of Expanded and Collapsed, a disclosure button carries
/// the state of what it discloses, and hosts can act through <see cref="IUiExpandable"/>. ADR 0028.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class DisclosureSemanticsTests
{
    private const UiSemanticState ExpandState = UiSemanticState.Expanded | UiSemanticState.Collapsed;

    [Fact]
    public void CollapsibleSectionToggleReportsCollapsedThenExpanded()
    {
        using var section = new FormSection("Cc and Bcc", collapsible: true, expanded: false);
        StandardButton toggle = section.Toggle!;

        Assert.Same(section, toggle.Discloses);
        Assert.Equal(UiSemanticState.Collapsed, toggle.GetSemanticNode().State & ExpandState);
        Assert.Equal("Show Cc and Bcc", toggle.GetSemanticNode().Name);

        toggle.Click();

        Assert.True(section.IsExpanded);
        Assert.Equal(UiSemanticState.Expanded, toggle.GetSemanticNode().State & ExpandState);
        Assert.Equal("Hide Cc and Bcc", toggle.GetSemanticNode().Name);
    }

    [Fact]
    public void TheSectionGroupReportsNoExpandState()
    {
        using var section = new FormSection("Advanced", collapsible: true, expanded: false);
        Assert.Equal(UiSemanticState.None, section.GetSemanticNode().State & ExpandState);
        section.IsExpanded = true;
        Assert.Equal(UiSemanticState.None, section.GetSemanticNode().State & ExpandState);

        // A section that cannot collapse has no toggle and nothing to report.
        using var fixedSection = new FormSection("Server");
        Assert.Null(fixedSection.Toggle);
        Assert.Equal(UiSemanticState.None, fixedSection.GetSemanticNode().State & ExpandState);
    }

    [Fact]
    public void HostsExpandAndCollapseThroughTheDisclosedTarget()
    {
        using var section = new FormSection("Cc and Bcc", collapsible: true, expanded: false);
        var bcc = new StandardEdit { Text = "hidden@example.test" };
        section.Content.AddChild(new FormField("Bcc", bcc));
        using var session = new StandardUiSessionBuilder().Build(new Host(640, 480));
        session.AddRoot(section);
        session.RenderFrame();

        // What a host does for an expand/collapse pattern on the focused toggle.
        IUiExpandable target = section.Toggle!.Discloses!;
        Assert.True(target.Expand());
        Assert.False(target.Expand());
        Assert.True(section.IsExpanded);

        session.SetFocus(bcc);
        Assert.True(target.Collapse());
        Assert.False(target.Collapse());
        Assert.False(section.IsExpanded);
        Assert.Same(section.Toggle, session.FocusedElement);
        Assert.Equal("hidden@example.test", bcc.Text);

        using var fixedSection = new FormSection("Server");
        Assert.False(((IUiExpandable)fixedSection).Collapse());
        Assert.True(fixedSection.IsExpanded);
    }

    [Fact]
    public void ChangingTheSectionInvalidatesTheToggleSemantics()
    {
        using var section = new FormSection("Cc and Bcc", collapsible: true, expanded: false);
        var host = new Host(640, 480);
        using var session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(section);
        session.RenderFrame();
        var changed = new List<UiElement>();
        session.SemanticChanged += (_, e) => changed.Add(e.Element);

        section.Expand();

        Assert.Contains(section.Toggle!, changed);
    }

    [Fact]
    public void ADisclosureCanTargetAnyExpandableAndIgnoresADisposedTarget()
    {
        var button = new StandardButton { Text = "Details" };
        Assert.Equal(UiSemanticState.None, button.GetSemanticNode().State & ExpandState);

        var details = new Expandable();
        button.Discloses = details;
        Assert.Equal(UiSemanticState.Collapsed, button.GetSemanticNode().State & ExpandState);
        details.Expand();
        Assert.Equal(UiSemanticState.Expanded, button.GetSemanticNode().State & ExpandState);

        details.Dispose();
        Assert.Equal(UiSemanticState.None, button.GetSemanticNode().State & ExpandState);

        button.Discloses = null;
        Assert.Equal(UiSemanticState.None, button.GetSemanticNode().State & ExpandState);
    }

    [Fact]
    public void ComboBoxReportsCollapsedWhileClosedAndExpandsThroughTheInterface()
    {
        var combo = new StandardComboBox();
        combo.SetItems([new UiComboBoxItem("one", "One"), new UiComboBoxItem("two", "Two")]);
        Assert.Equal(UiSemanticState.Collapsed, combo.GetSemanticNode().State & ExpandState);

        IUiExpandable expandable = combo;
        Assert.True(expandable.Expand());
        Assert.True(combo.IsDropDownOpen);
        Assert.True(expandable.IsExpanded);
        Assert.Equal(UiSemanticState.Expanded, combo.GetSemanticNode().State & ExpandState);

        Assert.True(expandable.Collapse());
        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(UiSemanticState.Collapsed, combo.GetSemanticNode().State & ExpandState);
    }

    [Fact]
    public void MenuReportsCollapsedWhileClosedAndOpensThroughTheInterface()
    {
        var menu = new TestMenu { PresentationMode = UiMenuPresentationMode.ContextMenu };
        menu.SetItems([new UiMenuItem("copy", "Copy")]);
        Assert.Equal(UiSemanticState.Collapsed, menu.GetSemanticNode().State & ExpandState);

        Assert.True(((IUiExpandable)menu).Expand());
        Assert.True(menu.IsOpen);
        Assert.Equal(UiSemanticState.Expanded, menu.GetSemanticNode().State & ExpandState);

        Assert.True(((IUiExpandable)menu).Collapse());
        Assert.False(menu.IsOpen);
    }

    private sealed class TestMenu : UiMenu;

    private sealed class Expandable : UiElement, IUiExpandable
    {
        public bool IsExpanded { get; private set; }

        public bool Expand() => !IsExpanded && (IsExpanded = true);

        public bool Collapse() => IsExpanded && !(IsExpanded = false);
    }

    private sealed class Host(int width, int height) : IUiHost
    {
        public BSize ViewportSize => new(width, height);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
