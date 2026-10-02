using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Forms.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Accessible names come from labels or an explicit name, text stays the value, and content that a
/// container keeps alive but hides (inactive tabs) is not exposed to assistive technology.
/// </summary>
public sealed class AccessibleNamingAndHiddenContentTests
{
    [Fact]
    public void FormFieldLabelNamesItsEditWhileTheTextStaysTheValue()
    {
        var edit = new StandardEdit { Text = "reader@example.test", PlaceholderText = "name@example.com" };
        var field = new FormField("Email address", edit);

        Assert.Same(field.Label, edit.LabeledBy);
        UiSemanticNode node = edit.GetSemanticNode();
        Assert.Equal("Email address", node.Name);
        Assert.Equal("reader@example.test", node.TextInfo?.Value);
    }

    [Fact]
    public void UnlabeledEditIsNamedByItsPlaceholderNeverByItsText()
    {
        var edit = new StandardEdit { Text = "typed value", PlaceholderText = "Search mail" };
        Assert.Equal("Search mail", edit.GetSemanticNode().Name);
        Assert.Equal("typed value", edit.GetSemanticNode().TextInfo?.Value);

        var bare = new StandardEdit { Text = "typed value" };
        Assert.Equal("", bare.GetSemanticNode().Name);
    }

    [Fact]
    public void PasswordKeepsItsDefaultNameAndNeverPublishesItsValue()
    {
        var password = new StandardEdit { IsPassword = true, Text = "synthetic-secret" };
        Assert.Equal("Password field", password.GetSemanticNode().Name);
        Assert.Null(password.GetSemanticNode().TextInfo?.Value);

        _ = new FormField("Password / app password", password);
        Assert.Equal("Password / app password", password.GetSemanticNode().Name);
        Assert.DoesNotContain("synthetic-secret", password.GetSemanticNode().Name);
    }

    [Fact]
    public void ExplicitAccessibleNameWinsAndBlankNamesAreIgnored()
    {
        var edit = new StandardEdit { Text = "value" };
        _ = new FormField("Subject", edit);
        edit.AccessibleName = "Message subject";
        Assert.Equal("Message subject", edit.GetSemanticNode().Name);
        edit.AccessibleName = "   ";
        Assert.Equal("Subject", edit.GetSemanticNode().Name);

        var button = new StandardLabel { Text = "Visible text", AccessibleName = "Spoken text" };
        Assert.Equal("Spoken text", button.GetSemanticNode().Name);
    }

    [Fact]
    public void RetargetingALabelMovesTheRelationWithoutStealingAnotherOne()
    {
        var first = new StandardEdit();
        var second = new StandardEdit();
        var label = new StandardLabel { Text = "Host", Target = first };
        Assert.Same(label, first.LabeledBy);

        label.Target = second;
        Assert.Null(first.LabeledBy);
        Assert.Same(label, second.LabeledBy);

        // An existing, different label relation is kept.
        var other = new StandardLabel { Text = "Other" };
        var third = new StandardEdit { LabeledBy = other };
        label.Target = third;
        Assert.Same(other, third.LabeledBy);
        Assert.Equal("Other", third.GetSemanticNode().Name);
    }

    [Fact]
    public void ElementsThatLabelEachOtherDoNotRecurse()
    {
        var a = new StandardLabel { Text = "A" };
        var b = new StandardLabel { Text = "B" };
        a.LabeledBy = b;
        b.LabeledBy = a;
        Assert.Equal("B", a.GetSemanticNode().Name);
        Assert.Equal("A", b.GetSemanticNode().Name);
        a.LabeledBy = a;
        Assert.Null(a.LabeledBy);
    }

    [Fact]
    public void LayoutElementsHaveNoName()
    {
        Assert.Equal("", new StandardPanel().GetSemanticNode().Name);
        Assert.Equal("", new Plain().GetSemanticNode().Name);
    }

    [Fact]
    public void OnlyTheSelectedTabContentIsExposed()
    {
        var inboxContent = new StandardPanel();
        var inner = new StandardEdit { Text = "inner" };
        var accountContent = new StandardPanel();
        accountContent.AddChild(inner);
        var tabs = new StandardTabView();
        tabs.AddTab("inbox", "Inbox", inboxContent);
        tabs.AddTab("account", "Account", accountContent);
        using var session = new StandardUiSessionBuilder().Build(new Host());
        session.AddRoot(tabs);
        session.RenderFrame();

        Assert.False(inboxContent.IsHiddenFromAccessibility);
        Assert.True(accountContent.IsHiddenFromAccessibility);
        Assert.True(inner.IsHiddenFromAccessibility);
        UiSemanticNode hidden = accountContent.GetSemanticNode();
        Assert.True(hidden.State.HasFlag(UiSemanticState.Offscreen));
        Assert.False(hidden.State.HasFlag(UiSemanticState.Visible));

        tabs.SelectTab("account");
        Assert.True(inboxContent.IsHiddenFromAccessibility);
        Assert.False(accountContent.IsHiddenFromAccessibility);
        Assert.False(inner.IsHiddenFromAccessibility);
        Assert.True(accountContent.GetSemanticNode().State.HasFlag(UiSemanticState.Visible));

        // Removing a tab hands its content back unhidden.
        tabs.RemoveTab("inbox");
        Assert.False(inboxContent.IsHiddenFromAccessibility);
    }

    [Fact]
    public void HiddenChildrenAreLeftOutOfTheDefaultSemanticChildren()
    {
        var parent = new Plain();
        var shown = new StandardLabel { Text = "Shown" };
        var hidden = new StandardLabel { Text = "Hidden" };
        parent.AddChild(shown);
        parent.AddChild(hidden);
        Plain.Hide(hidden);
        Assert.Equal(["Shown"], parent.GetSemanticNode().Children.Select(child => child.Name));
    }

    private sealed class Plain : UiElement
    {
        public static void Hide(UiElement element) => SetHiddenFromAccessibility(element, true);
    }

    private sealed class Host : IUiHost
    {
        public BSize ViewportSize => new(800, 600);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
