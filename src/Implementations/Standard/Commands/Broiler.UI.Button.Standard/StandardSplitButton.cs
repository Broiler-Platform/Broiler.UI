using System;
using Broiler.Graphics.Geometry;
using Broiler.Input.Keyboard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Button.Standard;

/// <summary>Standard split button composed from the existing button and popup menu controls.</summary>
public sealed class StandardSplitButton : UiSplitButton, IStandardThemedControl
{
    public StandardSplitButton() : base(new StandardButton(), new StandardButton(), new StandardMenu())
    {
        MenuButton.Text = "▾";
        MenuButton.AccessibleName = "More actions";
        PopupMenu.PresentationMode = UiMenuPresentationMode.ContextMenu;
        Primary.PreferredSize = new BSize(100, 32);
        Trigger.PreferredSize = new BSize(32, 32);
        Trigger.PaddingX = 4;
    }

    public StandardButton Primary => (StandardButton)PrimaryButton;
    public StandardButton Trigger => (StandardButton)MenuButton;
    public StandardMenu PopupMenu => (StandardMenu)Menu;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Primary.ApplyTheme(theme);
        Trigger.ApplyTheme(theme);
        PopupMenu.ApplyTheme(theme);
    }

    public override bool Expand()
    {
        ThrowIfDisposed();
        if (!IsMenuEnabled || IsExpanded || Menu.Items.Count == 0) return false;
        // Restore focus to the disclosure even when opened from the primary action's keyboard shortcut.
        Session?.SetFocus(MenuButton);
        return PopupMenu.OpenPopup();
    }

    public override bool Collapse() => PopupMenu.ClosePopup();

    protected override BSize MeasureCore(BSize availableSize)
    {
        Trigger.Measure(new BSize(Math.Min(32, availableSize.Width), availableSize.Height));
        Primary.Measure(new BSize(Math.Max(0, availableSize.Width - Trigger.DesiredSize.Width), availableSize.Height));
        Menu.Measure(availableSize);
        return new BSize(Primary.DesiredSize.Width + Trigger.DesiredSize.Width,
            Math.Max(Primary.DesiredSize.Height, Trigger.DesiredSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        double arrowWidth = Math.Min(32, finalRect.Width);
        Primary.Arrange(new BRect(finalRect.Left, finalRect.Top, finalRect.Width - arrowWidth, finalRect.Height));
        Trigger.Arrange(new BRect(finalRect.Right - arrowWidth, finalRect.Top, arrowWidth, finalRect.Height));
        // A context menu has no visible trigger; its bounds locate the popup beneath the split button.
        Menu.Arrange(new BRect(finalRect.Left, finalRect.Bottom, finalRect.Width, 0));
        PopupMenu.PopupAnchorBounds = finalRect;
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (input.Kind != UiInputEventKind.KeyboardKey || input.KeyTransition != KeyboardKeyTransition.Down)
            return false;
        if (IsExpanded && (input.NativeKeyCode == 0x09 || string.Equals(input.KeyName, "Tab", StringComparison.OrdinalIgnoreCase)))
        {
            Collapse();
            return false; // The enclosing focus scope performs Tab traversal after capture is released.
        }
        bool f4 = input.KeyModifiers == KeyboardModifierState.None &&
            (input.NativeKeyCode == 0x73 || string.Equals(input.KeyName, "F4", StringComparison.OrdinalIgnoreCase));
        bool altDown = input.KeyModifiers.HasFlag(KeyboardModifierState.Alt) &&
            (input.NativeKeyCode == 0x28 || string.Equals(input.KeyName, "Down", StringComparison.OrdinalIgnoreCase));
        if (!f4 && !altDown) return false;
        return IsExpanded ? Collapse() : Expand();
    }
}
