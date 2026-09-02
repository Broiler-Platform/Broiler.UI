namespace Broiler.UI.FileDialog;

/// <summary>
/// The order a file dialog lists a folder's entries in.
/// </summary>
/// <remarks>
/// Each order carries its own direction, because the useful end differs per key: names read
/// forwards, while the interesting file is the newest or the largest one. The direction is part
/// of the order rather than a separate toggle, so a dialog can name it in the control that picks
/// it and the user is never left guessing which way a list runs.
/// </remarks>
public enum UiFileDialogSortOrder
{
    /// <summary>By name, A to Z. The default.</summary>
    Name,

    /// <summary>By extension, A to Z, then by name within an extension.</summary>
    Type,

    /// <summary>By last write time, newest first.</summary>
    Modified,

    /// <summary>By size, largest first. Folders carry no size and stay ordered by name.</summary>
    Size,
}
