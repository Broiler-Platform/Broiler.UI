namespace Broiler.UI.Menu;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
/// <summary>
/// Specifies how a <see cref="UiMenu"/> is presented to the user.
/// </summary>
public enum UiMenuPresentationMode
{
    /// <summary>
    /// Horizontal desktop menu bar across the top of a window (e.g. File, Edit, View).
    /// </summary>
    MenuBar = 0,

    /// <summary>
    /// Floating context menu opened at a pointer or caret location.
    /// </summary>
    ContextMenu = 1,

    /// <summary>
    /// Compact trigger button (e.g. hamburger or 3-dots) that opens the menu as a dropdown popup.
    /// Common in modern desktop applications and mobile toolbars.
    /// </summary>
    Overflow = 2,

    /// <summary>
    /// Vertical navigation drawer or side panel commonly used on smartphones, tablets, and responsive desktop sidebars.
    /// </summary>
    Drawer = 3,

    /// <summary>
    /// Modal bottom sheet anchored to the bottom of the screen with touch-friendly targets, optimized for one-handed mobile interactions.
    /// </summary>
    BottomSheet = 4,

    /// <summary>
    /// Horizontal navigation bar distributing primary actions equally across a row, commonly anchored at the bottom of smartphone or tablet screens.
    /// </summary>
    TabBar = 5,
}
