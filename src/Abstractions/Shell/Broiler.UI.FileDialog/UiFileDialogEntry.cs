using System;
using System.IO;

namespace Broiler.UI.FileDialog;

/// <summary>
/// One file or folder in a listing from an <see cref="IUiFileDialogDirectoryProvider"/>:
/// everything the dialog shows about it or sorts it by, read once, so that showing
/// and sorting it again costs no further trip to the file system.
/// </summary>
/// <param name="Name">The name within its folder, extension included.</param>
/// <param name="FullPath">The full path, which is what selecting it navigates to or returns.</param>
/// <param name="IsDirectory">True for a folder, which is listed with the folders rather than filtered as a file.</param>
/// <param name="Length">The size in bytes. Ignored for a folder.</param>
/// <param name="LastWriteTimeUtc">When it was last written, in UTC.</param>
// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed record UiFileDialogEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Length,
    DateTime LastWriteTimeUtc)
{
    /// <summary>The extension, with its dot, or empty: what sorting by type orders by.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an entry named 'archive.tar.gz' reports an extension other than '.gz'
    // Broiler-Human:        PENDING
    public string Extension => Path.GetExtension(Name);
}
