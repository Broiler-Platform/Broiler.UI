using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Forms.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// A field's error and hint describe the control that has the focus, the control itself reports
/// Invalid and Required, and the error text reaches clients that ignore relations. ADR 0028.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ValidationRelationTests
{
    [Fact]
    public void FieldErrorMakesTheControlInvalidAndLeadsItsDescription()
    {
        var edit = new StandardEdit();
        var field = new FormField("Email address", edit, "Used to sign in.");

        UiElement hint = edit.DescribedBy!;
        Assert.Equal("Used to sign in.", ((StandardLabel)hint).Text);
        Assert.Null(edit.ErrorMessage);
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Used to sign in.", edit.GetSemanticNode().Description);

        field.SetError("Enter an email address without a display name.");

        UiSemanticNode node = edit.GetSemanticNode();
        Assert.NotNull(edit.ErrorMessage);
        Assert.Equal("Error: Enter an email address without a display name.", ((StandardLabel)edit.ErrorMessage!).Text);
        Assert.True(node.State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Error: Enter an email address without a display name. Used to sign in.", node.Description);
        // The name stays the label's; the error is a description, not part of the name.
        Assert.Equal("Email address", node.Name);

        field.SetError(null);

        node = edit.GetSemanticNode();
        Assert.Null(edit.ErrorMessage);
        Assert.False(node.State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Used to sign in.", node.Description);
    }

    [Fact]
    public void TypingClearsTheControlsErrorState()
    {
        var edit = new StandardEdit();
        var field = new FormField("Cc", edit);
        field.SetError("Enter valid email addresses separated by commas.");
        Assert.True(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Error: Enter valid email addresses separated by commas.", edit.GetSemanticNode().Description);

        edit.Text = "reader@example.test";

        Assert.Equal("", field.Error);
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Null(edit.GetSemanticNode().Description);
    }

    [Fact]
    public void RequiredIsReportedByTheControlNotTheGroup()
    {
        var edit = new StandardEdit();
        var field = new FormField("To", edit);
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Required));

        field.IsRequired = true;

        Assert.True(field.IsRequired);
        Assert.True(edit.IsRequired);
        Assert.True(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Required));
        Assert.False(field.GetSemanticNode().State.HasFlag(UiSemanticState.Required));

        field.IsRequired = false;
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Required));
    }

    [Fact]
    public void AFieldErrorIsNotAnnouncedButReachesTheControlsSemantics()
    {
        var edit = new StandardEdit();
        using var field = new FormField("Server", edit);
        using var session = new StandardUiSessionBuilder().Build(new Host());
        session.AddRoot(field);
        session.RenderFrame();
        var events = new List<UiSemanticChangedEventArgs>();
        session.SemanticChanged += (_, e) => events.Add(e);

        field.SetError("Enter a host name.");
        Assert.Contains(events, e => ReferenceEquals(e.Element, edit));
        Assert.DoesNotContain(events, e => e.Change == UiSemanticChangeKind.StatusAnnounced);

        // The message element is unchanged on the second call; the control must still be told.
        events.Clear();
        field.SetError("Enter a host name without a scheme.");
        Assert.Contains(events, e => ReferenceEquals(e.Element, edit));
        Assert.DoesNotContain(events, e => e.Change == UiSemanticChangeKind.StatusAnnounced);
        Assert.Equal("Error: Enter a host name without a scheme.", edit.GetSemanticNode().Description);
    }

    [Fact]
    public void AnApplicationsOwnDescriptionIsKept()
    {
        var hint = new StandardLabel { Text = "Shown to recipients." };
        var edit = new StandardEdit { DescribedBy = hint };
        _ = new FormField("Display name", edit, "Field description.");
        Assert.Same(hint, edit.DescribedBy);
    }

    [Fact]
    public void OnlyAShownMessageMakesAnElementInvalid()
    {
        var message = new StandardLabel { Text = "Too short." };
        var edit = new StandardEdit { ErrorMessage = message };
        Assert.True(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Too short.", edit.GetSemanticNode().Description);

        message.Visibility = UiVisibility.Collapsed;
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Null(edit.GetSemanticNode().Description);

        message.Visibility = UiVisibility.Visible;
        message.Text = "   ";
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));

        message.Text = "Too short.";
        message.Dispose();
        Assert.False(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));

        // An element cannot describe or invalidate itself.
        edit.DescribedBy = edit;
        edit.ErrorMessage = edit;
        Assert.Null(edit.DescribedBy);
        Assert.Null(edit.ErrorMessage);
    }

    [Fact]
    public void ElementsThatDescribeEachOtherDoNotRecurse()
    {
        var first = new StandardLabel { Text = "First" };
        var second = new StandardLabel { Text = "Second" };
        first.DescribedBy = second;
        second.ErrorMessage = first;

        Assert.Equal("Second", first.GetSemanticNode().Description);
        Assert.Equal("First", second.GetSemanticNode().Description);
        Assert.True(second.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
    }

    [Fact]
    public void AMessageInsideAHiddenContainerIsNotShown()
    {
        var panel = new StandardPanel();
        var message = new StandardLabel { Text = "Server is required." };
        panel.AddChild(message);
        var hint = new StandardLabel { Text = "For example imap.example.test." };
        var hints = new StandardPanel();
        hints.AddChild(hint);
        var edit = new StandardEdit { ErrorMessage = message, DescribedBy = hint };
        Assert.True(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));

        panel.Visibility = UiVisibility.Collapsed;
        hints.Visibility = UiVisibility.Hidden;
        UiSemanticNode node = edit.GetSemanticNode();
        Assert.False(node.State.HasFlag(UiSemanticState.Invalid));
        Assert.Null(node.Description);

        panel.Visibility = UiVisibility.Visible;
        Assert.True(edit.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Server is required.", edit.GetSemanticNode().Description);
    }

    [Fact]
    public void RelationsThatLeadBackToTheElementEndInsteadOfRecursing()
    {
        // An ancestor contains the control, so it neither describes it nor makes it invalid.
        var group = new StandardPanel();
        var edit = new StandardEdit();
        group.AddChild(edit);
        group.AddChild(new StandardLabel { Text = "Fix the errors below." });
        edit.DescribedBy = group;
        edit.ErrorMessage = group;
        UiSemanticNode node = edit.GetSemanticNode();
        Assert.Null(node.Description);
        Assert.False(node.State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal(2, group.GetSemanticNode().Children.Count);

        // An ancestor can still label an element, by its own name.
        using var section = new FormSection("Advanced", collapsible: true);
        section.Toggle!.LabeledBy = section;
        Assert.Equal("Advanced", section.Toggle.GetSemanticNode().Name);
        Assert.Equal("Advanced", section.GetSemanticNode().Name);

        // Relations that lead back through each other's containers stop after one step.
        using var left = new FormSection("Left");
        using var right = new FormSection("Right");
        var first = new StandardEdit();
        var second = new StandardEdit();
        left.Content.AddChild(first);
        right.Content.AddChild(second);
        first.LabeledBy = right;
        first.DescribedBy = right;
        second.LabeledBy = left;
        second.ErrorMessage = left;

        Assert.Equal("Right", first.GetSemanticNode().Name);
        Assert.Equal("Right", first.GetSemanticNode().Description);
        Assert.Equal("Left", second.GetSemanticNode().Name);
        Assert.True(second.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
        Assert.Equal("Left", second.GetSemanticNode().Description);
        Assert.Equal("Left", left.GetSemanticNode().Name);
        Assert.Equal("Right", right.GetSemanticNode().Name);
    }

    private sealed class Host : IUiHost
    {
        public BSize ViewportSize => new(640, 480);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
