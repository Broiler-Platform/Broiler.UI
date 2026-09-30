using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.UI.FileDialog;

/// <summary>
/// Lists the contents of one folder for a file dialog: the file system the dialog
/// browses, kept out of the dialog so that reading a slow or remote folder need not
/// hold up the dialog's input, and so that a host can browse something other than
/// the local disk.
/// </summary>
/// <remarks>
/// <para>
/// The dialog asks on its UI context and does not wait. A task that is already
/// complete when it is returned is shown at once, exactly as a synchronous listing
/// would be. One that completes later is brought back through the session's
/// <see cref="IUiDispatcher"/>, and until then the dialog shows the folder as
/// loading. Which of the two happens is the provider's choice.
/// </para>
/// <para>
/// A faulted or cancelled task is shown as the folder's error, with the
/// exception's message, and the dialog stays usable: the way up is still offered.
/// The token is cancelled when the dialog no longer wants the listing - the user
/// went somewhere else, or the dialog closed - and a result that arrives after
/// that is ignored, so honouring it early only saves work.
/// </para>
/// </remarks>
// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: a listing for a folder includes an entry whose FullPath is not directly inside that folder, so selecting it navigates to or returns a path elsewhere
// Broiler-Human:        PENDING
public interface IUiFileDialogDirectoryProvider
{
    /// <summary>
    /// The files and folders directly inside <paramref name="directory"/>, in any
    /// order: the dialog filters and sorts them itself.
    /// </summary>
    /// <param name="directory">A full path, as <see cref="UiFileDialog.CurrentDirectory"/> holds it.</param>
    /// <param name="cancellationToken">Cancelled when the listing is no longer wanted.</param>
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a listing for a folder includes an entry whose FullPath is not directly inside that folder, so selecting it navigates to or returns a path elsewhere
    // Broiler-Human:        PENDING
    Task<IReadOnlyList<UiFileDialogEntry>> GetEntriesAsync(string directory, CancellationToken cancellationToken);
}
