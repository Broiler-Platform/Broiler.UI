using System;
using Broiler.UI.Menu;

namespace Broiler.UI.Button;

/// <summary>A primary action and independently enabled menu disclosure.</summary>
public abstract class UiSplitButton : UiElement, IUiExpandable
{
    protected UiSplitButton(UiButton primaryButton, UiButton menuButton, UiMenu menu)
    {
        PrimaryButton = primaryButton;
        MenuButton = menuButton;
        Menu = menu;
        MenuButton.Discloses = this;
        MenuButton.Controls = menu;
        MenuButton.Clicked += ToggleMenu;
        Menu.OpenStateChanged += MenuStateChanged;
        SetHiddenFromAccessibility(Menu, true);
        AddChild(primaryButton);
        AddChild(menuButton);
        AddChild(menu);
    }

    public UiButton PrimaryButton { get; }
    public UiButton MenuButton { get; }
    public UiMenu Menu { get; }
    public bool IsExpanded => Menu.IsOpen;

    public string Text
    {
        get => PrimaryButton.Text;
        set
        {
            PrimaryButton.Text = value;
            MenuButton.AccessibleName = $"More actions for {value}";
        }
    }

    public bool IsPrimaryEnabled
    {
        get => PrimaryButton.IsEnabled;
        set => PrimaryButton.IsEnabled = value;
    }

    public bool IsMenuEnabled
    {
        get => MenuButton.IsEnabled;
        set
        {
            MenuButton.IsEnabled = value;
            if (!value) Collapse();
        }
    }

    public abstract bool Expand();
    public abstract bool Collapse();

    private void ToggleMenu(object? sender, UiButtonClickEventArgs e)
    {
        if (IsExpanded) Collapse(); else Expand();
    }

    private void MenuStateChanged(object? sender, EventArgs e)
    {
        SetHiddenFromAccessibility(Menu, !Menu.IsOpen);
        MenuButton.Invalidate(UiInvalidationKind.Semantic);
        Invalidate(UiInvalidationKind.Semantic);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MenuButton.Clicked -= ToggleMenu;
            Menu.OpenStateChanged -= MenuStateChanged;
        }
        base.Dispose(disposing);
    }
}
