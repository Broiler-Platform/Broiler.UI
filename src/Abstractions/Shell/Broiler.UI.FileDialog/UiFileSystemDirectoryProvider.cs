using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.UI.FileDialog;

/// <summary>
/// Lists folders of the local file system, either on the calling thread or on the
/// thread pool.
/// </summary>
/// <remarks>
/// <para>
/// Which to use is a property of the host rather than of the dialog. Listing on
/// the thread pool keeps a large or remote folder from holding up the dialog's
/// input, but its result then has to come back to the UI context, and only a
/// session whose <see cref="IUiDispatcher"/> actually marshals can bring it - a
/// queued one such as <c>Broiler.UI.Standard.StandardQueuedUiDispatcher</c>. The
/// immediate dispatcher runs a posted callback on whichever thread posts it -
/// here, a thread-pool thread - so <see cref="Background"/> is for a host with a
/// real UI dispatcher, and <see cref="Synchronous"/>, which never leaves the
/// calling thread, is the default that is safe with every dispatcher.
/// </para>
/// <para>
/// Either way the listing is one pass over the folder: the sizes and times the
/// dialog shows and sorts by are the ones the directory scan already read.
/// </para>
/// </remarks>
// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: a missing or access-denied folder makes GetEntriesAsync throw to its caller instead of returning a faulted task
// Broiler-Human:        PENDING
public sealed class UiFileSystemDirectoryProvider : IUiFileDialogDirectoryProvider
{
    private readonly bool _background;

    private UiFileSystemDirectoryProvider(bool background) => _background = background;

    /// <summary>
    /// Lists on the calling thread and returns a task that is already complete, so
    /// the dialog shows the folder before the input that opened it returns.
    /// </summary>
    public static UiFileSystemDirectoryProvider Synchronous { get; } = new(background: false);

    /// <summary>
    /// Lists on the thread pool. Use it only in a session whose dispatcher posts
    /// onto the UI context from another thread, such as one built on
    /// <c>StandardQueuedUiDispatcher</c>.
    /// </summary>
    public static UiFileSystemDirectoryProvider Background { get; } = new(background: true);

    /// <summary>True for <see cref="Background"/>.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: the Background provider reports ListsInBackground as false
    // Broiler-Human:        PENDING
    public bool ListsInBackground => _background;

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a missing or access-denied folder makes the Synchronous provider throw from GetEntriesAsync instead of returning a faulted task
    // Broiler-Human:        PENDING
    public Task<IReadOnlyList<UiFileDialogEntry>> GetEntriesAsync(string directory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (_background)
            return Task.Run(() => List(directory, cancellationToken), cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<IReadOnlyList<UiFileDialogEntry>>(cancellationToken);

        try
        {
            return Task.FromResult(List(directory, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IReadOnlyList<UiFileDialogEntry>>(cancellationToken);
        }
        catch (Exception exception) when (IsListingFailure(exception))
        {
            // Returned rather than thrown, the way an asynchronous listing would
            // report it: the dialog shows a faulted listing as the folder's error.
            return Task.FromException<IReadOnlyList<UiFileDialogEntry>>(exception);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: cancelling the token while a large folder is enumerated does not stop the scan before its last entry
    // Broiler-Human:        PENDING
    private static IReadOnlyList<UiFileDialogEntry> List(string directory, CancellationToken cancellationToken)
    {
        var entries = new List<UiFileDialogEntry>();
        foreach (FileSystemInfo item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(item is FileInfo file
                ? new UiFileDialogEntry(file.Name, file.FullName, IsDirectory: false, file.Length, file.LastWriteTimeUtc)
                : new UiFileDialogEntry(item.Name, item.FullName, IsDirectory: true, 0, item.LastWriteTimeUtc));
        }

        return [.. entries];
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an UnauthorizedAccessException or DirectoryNotFoundException from listing a folder is not classified as a listing failure
    // Broiler-Human:        PENDING
    private static bool IsListingFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or ArgumentException or NotSupportedException;
}
