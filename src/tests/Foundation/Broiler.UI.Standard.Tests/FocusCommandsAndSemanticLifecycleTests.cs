using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.Button.Standard;
using Broiler.UI.CheckBox;
using Broiler.UI.CodeEditor;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.FormatCodeView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Menu;
using Broiler.UI.RadioButton;
using Broiler.UI.RichEdit;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.Slider;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView.Standard;
using Broiler.UI.TreeView;
using Xunit;

namespace Broiler.UI.Standard.Tests;

public sealed class FocusCommandsAndSemanticLifecycleTests
{
    [Fact]
    public void Interactive_Controls_Default_To_Focusable_True_And_TabStop_True()
    {
        using var button = new StandardButton { Text = "Click" };
        using var edit = new StandardEdit();
        using var combo = new StandardComboBox();
        using var list = new StandardListView();
        using var spin = new StandardSpinBox();
        using var tab = new StandardTabView();
        using var check = new TestCheckBox();
        using var radio = new TestRadioButton();
        using var slider = new TestSlider();
        using var richEdit = new TestRichEdit();
        using var tree = new TestTreeView();
        using var codeEditor = new TestCodeEditor();
        using var formatCode = new TestFormatCodeView();

        Assert.True(button.Focusable);
        Assert.True(button.IsTabStop);

        Assert.True(edit.Focusable);
        Assert.True(edit.IsTabStop);

        Assert.True(combo.Focusable);
        Assert.True(combo.IsTabStop);

        Assert.True(list.Focusable);
        Assert.True(list.IsTabStop);

        Assert.True(spin.Focusable);
        Assert.True(spin.IsTabStop);

        Assert.True(tab.Focusable);
        Assert.True(tab.IsTabStop);

        Assert.True(check.Focusable);
        Assert.True(check.IsTabStop);

        Assert.True(radio.Focusable);
        Assert.True(radio.IsTabStop);

        Assert.True(slider.Focusable);
        Assert.True(slider.IsTabStop);

        Assert.True(richEdit.Focusable);
        Assert.True(richEdit.IsTabStop);

        Assert.True(tree.Focusable);
        Assert.True(tree.IsTabStop);

        Assert.True(codeEditor.Focusable);
        Assert.True(codeEditor.IsTabStop);

        Assert.True(formatCode.Focusable);
        Assert.True(formatCode.IsTabStop);
    }

    [Fact]
    public void CanFocus_Requires_Attached_Visible_Enabled_And_Visible_Ancestors()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);

        var root = new TestContainerElement();
        var parentPanel = new TestContainerElement();
        var button = new StandardButton { Text = "Submit" };

        parentPanel.AddChild(button);
        root.AddChild(parentPanel);

        // 1. Not attached to session -> CanFocus must be false
        Assert.False(button.CanFocus);

        session.AddRoot(root);

        // 2. Attached, visible, enabled -> CanFocus must be true
        Assert.True(button.CanFocus);

        // 3. Disabled control -> CanFocus must be false
        button.IsEnabled = false;
        Assert.False(button.CanFocus);
        button.IsEnabled = true;
        Assert.True(button.CanFocus);

        // 4. Hidden or collapsed control -> CanFocus must be false
        button.Visibility = UiVisibility.Collapsed;
        Assert.False(button.CanFocus);
        button.Visibility = UiVisibility.Hidden;
        Assert.False(button.CanFocus);
        button.Visibility = UiVisibility.Visible;
        Assert.True(button.CanFocus);

        // 5. Ancestor collapsed -> CanFocus must be false
        parentPanel.Visibility = UiVisibility.Collapsed;
        Assert.False(button.CanFocus);
        parentPanel.Visibility = UiVisibility.Visible;
        Assert.True(button.CanFocus);

        // 6. Element Focusable = false -> CanFocus must be false
        button.Focusable = false;
        Assert.False(button.CanFocus);
        button.Focusable = true;
        Assert.True(button.CanFocus);

        // 7. Disposed -> CanFocus must be false
        button.Dispose();
        Assert.False(button.CanFocus);
    }

    [Fact]
    public void Focus_Method_Sets_FocusedElement_And_IsFocused()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);

        var root = new TestContainerElement();
        var button1 = new StandardButton { Text = "One" };
        var button2 = new StandardButton { Text = "Two" };
        root.AddChild(button1);
        root.AddChild(button2);
        session.AddRoot(root);

        Assert.True(button1.Focus());
        Assert.Same(button1, session.FocusedElement);
        Assert.True(button1.IsFocused);
        Assert.False(button2.IsFocused);

        Assert.True(button2.Focus());
        Assert.Same(button2, session.FocusedElement);
        Assert.False(button1.IsFocused);
        Assert.True(button2.IsFocused);

        // Cannot focus disabled control
        button1.IsEnabled = false;
        Assert.False(button1.Focus());
        Assert.Same(button2, session.FocusedElement);
    }

    [Fact]
    public void MoveFocus_Navigates_Forward_And_Backward_Ordered_By_TabIndex()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var buttonA = new StandardButton { Text = "A", TabIndex = 20 };
        var buttonB = new StandardButton { Text = "B", TabIndex = 10 };
        var buttonC = new StandardButton { Text = "C", TabIndex = 30 };

        root.AddChild(buttonA);
        root.AddChild(buttonB);
        root.AddChild(buttonC);
        session.AddRoot(root);

        // Focus first tab-stop by moving forward from null
        Assert.True(focus.MoveFocus(1));
        Assert.Same(buttonB, session.FocusedElement); // TabIndex 10 is first

        Assert.True(focus.MoveFocus(1));
        Assert.Same(buttonA, session.FocusedElement); // TabIndex 20 is second

        Assert.True(focus.MoveFocus(1));
        Assert.Same(buttonC, session.FocusedElement); // TabIndex 30 is third

        // Wrap around
        Assert.True(focus.MoveFocus(1));
        Assert.Same(buttonB, session.FocusedElement);

        // Move backward
        Assert.True(focus.MoveFocus(-1));
        Assert.Same(buttonC, session.FocusedElement);

        Assert.True(focus.MoveFocus(-1));
        Assert.Same(buttonA, session.FocusedElement);
    }

    [Fact]
    public void MoveFocus_Skips_Disabled_Collapsed_And_NonTabStops()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var b1 = new StandardButton { Text = "B1" };
        var b2Disabled = new StandardButton { Text = "B2", IsEnabled = false };
        var b3Collapsed = new StandardButton { Text = "B3", Visibility = UiVisibility.Collapsed };
        var b4NotTabStop = new StandardButton { Text = "B4", IsTabStop = false };
        var b5 = new StandardButton { Text = "B5" };

        root.AddChild(b1);
        root.AddChild(b2Disabled);
        root.AddChild(b3Collapsed);
        root.AddChild(b4NotTabStop);
        root.AddChild(b5);
        session.AddRoot(root);

        session.SetFocus(b1);
        Assert.Same(b1, session.FocusedElement);

        // Moving forward must skip B2 (disabled), B3 (collapsed), and B4 (not tabstop) -> directly to B5
        Assert.True(focus.MoveFocus(1));
        Assert.Same(b5, session.FocusedElement);

        // Moving backward from B5 must skip B4, B3, B2 -> directly to B1
        Assert.True(focus.MoveFocus(-1));
        Assert.Same(b1, session.FocusedElement);

        // Non-tab-stop can still be directly focused programmatically
        Assert.True(b4NotTabStop.CanFocus);
        session.SetFocus(b4NotTabStop);
        Assert.Same(b4NotTabStop, session.FocusedElement);
    }

    [Fact]
    public void MoveFocus_Confined_To_ScopeRoot_And_ActiveTab()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var tabView = new StandardTabView();
        var tab1Content = new TestContainerElement();
        var tab1Button1 = new StandardButton { Text = "Tab1-B1" };
        var tab1Button2 = new StandardButton { Text = "Tab1-B2" };
        tab1Content.AddChild(tab1Button1);
        tab1Content.AddChild(tab1Button2);

        var tab2Content = new TestContainerElement();
        var tab2Button1 = new StandardButton { Text = "Tab2-B1" };
        tab2Content.AddChild(tab2Button1);

        tabView.AddTab("tab1", "Tab 1", tab1Content);
        tabView.AddTab("tab2", "Tab 2", tab2Content);
        root.AddChild(tabView);
        session.AddRoot(root);

        // Cycle focus strictly within tab1Content
        session.SetFocus(tab1Button1);
        Assert.Same(tab1Button1, session.FocusedElement);

        Assert.True(focus.MoveFocus(1, tab1Content));
        Assert.Same(tab1Button2, session.FocusedElement);

        // Wraps back to tab1Button1 without escaping to tabView or tab2
        Assert.True(focus.MoveFocus(1, tab1Content));
        Assert.Same(tab1Button1, session.FocusedElement);
    }

    [Fact]
    public void MoveFocus_Keeps_Document_Order_Among_Equal_TabIndexes()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        // More than 16 candidates: List.Sort switches from insertion sort to an unstable introsort there.
        var root = new TestContainerElement();
        var buttons = Enumerable.Range(0, 40).Select(index => new StandardButton { Text = $"B{index}", TabIndex = index % 3 == 0 ? 1 : 0 }).ToList();
        foreach (var button in buttons)
            root.AddChild(button);
        session.AddRoot(root);

        var expected = buttons.Where(button => button.TabIndex == 0).Concat(buttons.Where(button => button.TabIndex == 1)).ToList();
        var visited = new List<UiElement>();
        for (int step = 0; step < expected.Count; step++)
        {
            Assert.True(focus.MoveFocus(1));
            visited.Add(session.FocusedElement!);
        }

        Assert.Equal(expected, visited);
    }

    [Fact]
    public void MoveFocus_Skips_Inactive_Tab_Content_Without_A_Scope_Root()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var tabView = new StandardTabView();
        var first = new StandardButton { Text = "First tab button" };
        var second = new StandardButton { Text = "Second tab button" };
        tabView.AddTab("first", "First", first);
        tabView.AddTab("second", "Second", second);
        root.AddChild(tabView);
        session.AddRoot(root);

        var visited = new List<UiElement>();
        for (int step = 0; step < 4; step++)
        {
            Assert.True(focus.MoveFocus(1));
            visited.Add(session.FocusedElement!);
        }

        Assert.Contains(first, visited);
        Assert.DoesNotContain(second, visited);

        tabView.SelectedIndex = 1;
        visited.Clear();
        for (int step = 0; step < 4; step++)
        {
            Assert.True(focus.MoveFocus(1));
            visited.Add(session.FocusedElement!);
        }

        Assert.Contains(second, visited);
        Assert.DoesNotContain(first, visited);
    }

    [Fact]
    public void MoveFocus_Confined_To_ModalElement()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var backgroundButton = new StandardButton { Text = "Background" };
        var modalDialog = new TestContainerElement();
        var dialogOk = new StandardButton { Text = "OK" };
        var dialogCancel = new StandardButton { Text = "Cancel" };
        modalDialog.AddChild(dialogOk);
        modalDialog.AddChild(dialogCancel);

        root.AddChild(backgroundButton);
        root.AddChild(modalDialog);
        session.AddRoot(root);

        // Activate modal scope
        session.PushModalElement(modalDialog);

        // Move focus -> must enter and cycle within modal
        Assert.True(focus.MoveFocus(1));
        Assert.Same(dialogOk, session.FocusedElement);

        Assert.True(focus.MoveFocus(1));
        Assert.Same(dialogCancel, session.FocusedElement);

        Assert.True(focus.MoveFocus(1));
        Assert.Same(dialogOk, session.FocusedElement);
    }

    [Fact]
    public void FindAdjacentStop_Places_An_Element_That_Is_No_Stop_By_TabIndex_And_Document_Order()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var first = new StandardButton { Text = "First", TabIndex = 1 };
        var gone = new StandardButton { Text = "Gone", TabIndex = 1, Visibility = UiVisibility.Collapsed };
        var last = new StandardButton { Text = "Last", TabIndex = 1 };
        var early = new StandardButton { Text = "Early", TabIndex = 0 };
        root.AddChild(first);
        root.AddChild(gone);
        root.AddChild(last);
        root.AddChild(early);
        session.AddRoot(root);

        // Tab order is Early, First, Last; Gone, collapsed, keeps its place between First and Last.
        Assert.False(gone.CanFocus);
        Assert.Same(last, focus.FindAdjacentStop(gone, 1));
        Assert.Same(first, focus.FindAdjacentStop(gone, -1));
        Assert.Same(first, focus.FindAdjacentStop(early, 1));

        // It does not wrap, and finds nothing for an element outside its scope.
        Assert.Null(focus.FindAdjacentStop(early, -1));
        Assert.Null(focus.FindAdjacentStop(last, 1));
        Assert.Null(focus.FindAdjacentStop(gone, 1, scopeRoot: early));

        // The active modal element is the scope by default, as for MoveFocus.
        var dialog = new TestContainerElement();
        var ok = new StandardButton { Text = "OK" };
        var cancel = new StandardButton { Text = "Cancel" };
        dialog.AddChild(ok);
        dialog.AddChild(cancel);
        session.AddRoot(dialog);
        session.PushModalElement(dialog);
        Assert.Same(cancel, focus.FindAdjacentStop(ok, 1));
        Assert.Null(focus.FindAdjacentStop(gone, 1));

        // It only finds: nothing was focused.
        Assert.Null(session.FocusedElement);
    }

    [Fact]
    public void Focus_Capture_And_Restoration_Restores_Focus_On_Dispose()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var mainField = new StandardEdit { Text = "Initial focus" };
        var dialogButton = new StandardButton { Text = "Dialog Action" };
        root.AddChild(mainField);
        root.AddChild(dialogButton);
        session.AddRoot(root);

        session.SetFocus(mainField);
        Assert.Same(mainField, session.FocusedElement);

        // Capture focus before opening dialog/preview
        using (focus.CaptureFocus())
        {
            session.SetFocus(dialogButton);
            Assert.Same(dialogButton, session.FocusedElement);
        }

        // After disposing the capture cookie, focus is restored to mainField
        Assert.Same(mainField, session.FocusedElement);
    }

    [Fact]
    public void Focus_Save_And_Restore_Functions_Correctly()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var focus = new StandardFocusScope(session);

        var root = new TestContainerElement();
        var edit = new StandardEdit();
        var button = new StandardButton { Text = "Action" };
        root.AddChild(edit);
        root.AddChild(button);
        session.AddRoot(root);

        session.SetFocus(edit);
        Assert.Same(edit, session.FocusedElement);

        focus.SaveFocus();
        session.SetFocus(button);
        Assert.Same(button, session.FocusedElement);

        Assert.True(focus.RestoreFocus());
        Assert.Same(edit, session.FocusedElement);
    }

    [Fact]
    public void BringIntoView_Scrolls_ScrollView_To_Reveal_Child()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);

        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 12,
        };

        var child = new TestFixedElement(new BRect(0, 500, 100, 40), desiredSize: new BSize(100, 600));
        scrollView.AddChild(child);

        var root = new TestContainerElement();
        root.AddChild(scrollView);
        session.AddRoot(root);

        scrollView.Measure(new BSize(200, 200));
        scrollView.Arrange(new BRect(0, 0, 200, 200));

        Assert.Equal(0, scrollView.VerticalOffset);

        // Bring child into view
        child.BringIntoView();

        // ScrollView should have scrolled down to reveal the child
        Assert.True(scrollView.VerticalOffset > 0);
        Assert.True(scrollView.VerticalOffset >= 500 - 200);
    }

    [Fact]
    public void Stable_Semantic_Ids_Are_Unique_And_Match_Semantic_Node()
    {
        var b1 = new StandardButton { Text = "One" };
        var b2 = new StandardButton { Text = "Two" };
        var edit = new StandardEdit();

        Assert.True(b1.SemanticId > 0);
        Assert.True(b2.SemanticId > b1.SemanticId);
        Assert.True(edit.SemanticId > b2.SemanticId);

        Assert.Equal(b1.SemanticId, b1.GetSemanticNode().Id);
        Assert.Equal(b2.SemanticId, b2.GetSemanticNode().Id);
        Assert.Equal(edit.SemanticId, edit.GetSemanticNode().Id);
    }

    [Fact]
    public void Semantic_Roles_Include_All_Required_Fidelity()
    {
        Assert.True(Enum.IsDefined(UiSemanticRole.ListItem));
        Assert.True(Enum.IsDefined(UiSemanticRole.TabItem));
        Assert.True(Enum.IsDefined(UiSemanticRole.MenuItem));
        Assert.True(Enum.IsDefined(UiSemanticRole.StatusAnnouncement));
        Assert.True(Enum.IsDefined(UiSemanticRole.Group));
        Assert.True(Enum.IsDefined(UiSemanticRole.Hyperlink));
    }

    [Fact]
    public void SemanticChanged_Event_Raised_On_Focus_And_State_Changes()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);

        var root = new TestContainerElement();
        var button = new StandardButton { Text = "Test" };
        root.AddChild(button);
        session.AddRoot(root);

        var events = new List<UiSemanticChangedEventArgs>();
        session.SemanticChanged += (_, e) => events.Add(e);

        // 1. Focus change
        session.SetFocus(button);
        Assert.Contains(events, e => e.Change == UiSemanticChangeKind.FocusChanged && e.SemanticId == button.SemanticId);

        events.Clear();

        // 2. Invalidate state
        button.Invalidate(UiInvalidationKind.Semantic);
        Assert.Contains(events, e => e.Change == UiSemanticChangeKind.StateChanged && e.SemanticId == button.SemanticId);
    }

    [Fact]
    public void AnnounceStatus_Raises_SemanticChanged_With_StatusAnnouncement_Role()
    {
        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);

        var root = new TestContainerElement();
        var statusLabel = new TestContainerElement();
        root.AddChild(statusLabel);
        session.AddRoot(root);

        UiSemanticChangedEventArgs? raised = null;
        session.SemanticChanged += (_, e) => raised = e;

        session.AnnounceStatus(statusLabel, "Inbox synchronized: 5 new messages");

        Assert.NotNull(raised);
        Assert.Equal(UiSemanticChangeKind.StatusAnnounced, raised.Change);
        Assert.Equal(statusLabel.SemanticId, raised.SemanticId);
        Assert.Equal(statusLabel, raised.Element);
        Assert.Equal("Inbox synchronized: 5 new messages", raised.Message);
    }

    [Fact]
    public void UiCommand_Synchronizes_Label_Tooltip_Enablement_And_Invocation_With_UiButton()
    {
        int executions = 0;
        bool isAllowed = false;

        var command = new UiCommand(
            "Send Message",
            () => executions++,
            () => isAllowed,
            acceleratorText: "Ctrl+Enter",
            tooltipText: "Send message immediately");

        var host = new TestUiHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainerElement();
        using var button = new StandardButton();
        root.AddChild(button);
        session.AddRoot(root);

        button.Command = command;

        // Label and tooltip propagate to empty button text/tooltip
        Assert.Equal("Send Message", button.Text);
        Assert.Equal("Send message immediately", button.ToolTipText);

        // Button disabled because canExecute is false
        Assert.False(button.IsEnabled);
        Assert.False(button.CanFocus);

        // Toggle canExecute and raise changed
        isAllowed = true;
        command.RaiseCanExecuteChanged();

        Assert.True(button.IsEnabled);
        Assert.True(button.CanFocus);

        // Clicking button executes command
        button.Click();
        Assert.Equal(1, executions);

        button.Click();
        Assert.Equal(2, executions);
    }

    [Fact]
    public void UiCommand_Wires_Into_UiMenuItem_With_Accelerator_And_Invoke()
    {
        int executed = 0;
        var command = new UiCommand(
            execute: _ => executed++,
            label: "Paste",
            acceleratorText: "Ctrl+V",
            tooltipText: "Paste from clipboard");

        var menuItem = new UiMenuItem("paste", command);

        Assert.Equal("Paste", menuItem.Text);
        Assert.Equal("Ctrl+V", menuItem.Accelerator);
        Assert.Equal("Paste from clipboard", menuItem.Command?.TooltipText);
        Assert.Same(command, menuItem.Command);

        menuItem.Invoke();
        Assert.Equal(1, executed);
    }

    [Fact]
    public void StandardCommand_Implements_IUiCommand_And_Executes_Correctly()
    {
        int count = 0;
        bool allowed = true;
        var cmd = new StandardCommand("refresh", () => count++, () => allowed);

        IUiCommand uiCmd = cmd;
        Assert.Equal("refresh", uiCmd.Label);
        Assert.True(uiCmd.CanExecute());

        uiCmd.Execute();
        Assert.Equal(1, count);

        allowed = false;
        Assert.False(uiCmd.CanExecute());
    }

    // --- Helper Test Types ---

    private sealed class TestUiHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; set; } = viewportSize;
        public double Scale { get; set; } = 1.0;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }

    private sealed class TestContainerElement : UiElement
    {
    }

    private sealed class TestFixedElement : UiElement
    {
        private readonly BSize _desiredSize;

        public TestFixedElement(BRect bounds, BSize? desiredSize = null)
        {
            _desiredSize = desiredSize ?? bounds.Size;
            Arrange(bounds);
        }

        protected override BSize MeasureCore(BSize availableSize) => _desiredSize;
    }

    private sealed class TestCheckBox : UiCheckBox
    {
    }

    private sealed class TestRadioButton : UiRadioButton
    {
    }

    private sealed class TestSlider : UiSlider
    {
    }

    private sealed class TestRichEdit : UiRichEdit
    {
    }

    private sealed class TestTreeView : UiTreeView
    {
    }

    private sealed class TestCodeEditor : UiCodeEditor
    {
    }

    private sealed class TestFormatCodeView : UiFormatCodeView
    {
    }
}
