# Split button

`UiSplitButton` ships in `Broiler.UI.Button`; `StandardSplitButton` ships in
`Broiler.UI.Button.Standard`. Both packages acquire their matching Menu dependency
through NuGet. The standard implementation composes existing controls and uses the
existing Broiler.Graphics APIs; it needs no Graphics release for this feature.

```csharp
using Broiler.UI.Button.Standard;
using Broiler.UI.Menu;

var launch = new StandardSplitButton { Text = "Launch Writer" };
launch.Primary.Clicked += (_, _) => LaunchWriter();
launch.Menu.SetItems([
    new UiMenuItem("user", "Install for user"),
    new UiMenuItem("all", "Install for all"),
]);
launch.Menu.ItemInvoked += (_, e) => InstallWriter(e.Item.Id);
launch.IsPrimaryEnabled = IsWriterAvailable();
// Add to a UiElement container and measure/arrange it like other controls.
```

Alternatively, bind `Primary.Command` to an `IUiCommand`, or use
`Primary.CommandName`/`Primary.CommandDispatcher` and
`PopupMenu.CommandDispatcher`. Product operations belong in the application.
The arrow remains enabled independently of the primary action. `IsMenuEnabled`
disables disclosure and closes an open popup. `Expand`/`Collapse` expose the same
managed capture/focus behavior used by pointer and keyboard input.

The primary action and arrow are separate tab stops with accessible names. Enter
or Space activates the focused segment; Alt+Down or unmodified F4 opens the menu
from either segment. Arrow keys navigate; Enter invokes; Escape or outside click
closes and restores focus to the arrow. Tab closes the popup before passing
traversal to the enclosing focus scope. Touch uses the session's normal pointer
fallback. Alt+F4 is left to the application. Popups open below the control and
flip above it when space requires; their width is constrained to the viewport.
Open state changes invalidate the disclosure's accessibility state, and closed
menu content is removed from the accessible child tree.

Use `Primary`, `Trigger` and `PopupMenu` for styling, or apply a
`StandardThemeTokens` palette to the composite. The primary command's CanExecute
controls the primary segment without disabling disclosure. The launcher is an
integration example; `SplitButtonTests` exercises the control in a real UiSession
with a deterministic host.
