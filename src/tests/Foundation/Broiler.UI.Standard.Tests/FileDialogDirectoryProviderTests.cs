using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.FileDialog;
using Broiler.UI.FileDialog.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The file dialog reading folders through a provider it does not wait for: a
/// slow folder shows as loading, a failed one shows why, a listing nobody wants
/// any more is cancelled and ignored, and sorting or filtering never reads the
/// folder again. None of it touches the disk: the provider here is a script.
/// </summary>
public sealed class FileDialogDirectoryProviderTests
{
    private static readonly DateTime Monday = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>A path that is never created. The dialog only ever hands it to the provider.</summary>
    private static string Virtual(string name) =>
        Path.Combine(Path.GetTempPath(), "broiler-virtual-" + Guid.NewGuid().ToString("N"), name);

    private static UiFileDialogEntry File(string directory, string name, long length = 0, DateTime? modified = null) =>
        new(name, Path.Combine(directory, name), IsDirectory: false, length, modified ?? Monday);

    private static UiFileDialogEntry Folder(string directory, string name) =>
        new(name, Path.Combine(directory, name), IsDirectory: true, 0, Monday);

    private static string[] FileNames(StandardFileDialog dialog) =>
        dialog.FilesList.Items.Select(static item => item.Text).ToArray();

    private static string[] FolderNames(StandardFileDialog dialog) =>
        dialog.DirectoriesList.Items.Select(static item => item.Text).ToArray();

    private static string[] DrawnText(UiSession session) =>
        session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().Select(static text => text.Text.Text).ToArray();

    private static UiSession Session() => new StandardUiSessionBuilder().Build(new TestHost());

    [Fact]
    public void Lists_What_Its_Provider_Returns()
    {
        string library = Virtual("library");
        var provider = new ScriptedProvider(directory => directory == library
            ? [File(library, "b.md"), Folder(library, "sub"), File(library, "a.txt")]
            : []);

        var dialog = new StandardFileDialog(provider) { CurrentDirectory = library };

        Assert.Same(provider, dialog.DirectoryProvider);
        Assert.Equal(["..", "sub"], FolderNames(dialog));
        Assert.Equal(["a.txt", "b.md"], FileNames(dialog));
        Assert.False(dialog.IsLoadingDirectory);
        Assert.Null(dialog.DirectoryLoadError);

        dialog.FilesList.SelectedItemId = dialog.FilesList.Items[1].Id;
        Assert.Equal(Path.Combine(library, "b.md"), dialog.SelectedPath);
    }

    [Fact]
    public void A_Slow_Folder_Shows_As_Loading_Until_Its_Listing_Arrives()
    {
        var provider = new ScriptedProvider();
        var dialog = new StandardFileDialog(provider);
        using UiSession session = Session();
        session.AddRoot(dialog);
        string slow = Virtual("slow");
        TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>> listing = provider.HoldNext();

        dialog.CurrentDirectory = slow;

        Assert.True(dialog.IsLoadingDirectory);
        Assert.Empty(dialog.FilesList.Items);
        Assert.Equal([".."], FolderNames(dialog));
        Assert.Contains("Loading slow...", DrawnText(session));

        listing.SetResult([File(slow, "late.txt")]);

        Assert.False(dialog.IsLoadingDirectory);
        Assert.Equal(["late.txt"], FileNames(dialog));
        Assert.Contains(session.Invalidations, item => ReferenceEquals(item.Element, dialog) && item.Kind.HasFlag(UiInvalidationKind.Render));
        Assert.DoesNotContain("Loading slow...", DrawnText(session));
    }

    [Fact]
    public void A_Folder_That_Cannot_Be_Read_Says_Why_And_Keeps_The_Way_Up_Open()
    {
        string locked = Virtual("locked");
        var provider = new ScriptedProvider(directory => directory == locked
            ? throw new UnauthorizedAccessException("Access denied.")
            : []);
        var dialog = new StandardFileDialog(provider);
        using UiSession session = Session();
        session.AddRoot(dialog);

        dialog.CurrentDirectory = locked;

        Assert.IsType<UnauthorizedAccessException>(dialog.DirectoryLoadError);
        Assert.Equal([".."], FolderNames(dialog));
        Assert.Empty(dialog.FilesList.Items);
        Assert.Contains("Cannot open locked: Access denied.", DrawnText(session));

        Assert.True(dialog.NavigateUp());
        Assert.Null(dialog.DirectoryLoadError);
    }

    [Fact]
    public void Moving_On_Cancels_The_Listing_In_Flight_And_Ignores_It_If_It_Arrives_Anyway()
    {
        var provider = new ScriptedProvider();
        var dialog = new StandardFileDialog(provider);
        using UiSession session = Session();
        session.AddRoot(dialog);
        string first = Virtual("first");
        string second = Virtual("second");

        TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>> abandoned = provider.HoldNext();
        dialog.CurrentDirectory = first;
        CancellationToken firstToken = provider.Requests[^1].Token;
        TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>> wanted = provider.HoldNext();
        dialog.CurrentDirectory = second;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(provider.Requests[^1].Token.IsCancellationRequested);

        abandoned.SetResult([File(first, "stale.txt")]);
        Assert.True(dialog.IsLoadingDirectory);
        Assert.Empty(dialog.FilesList.Items);

        wanted.SetResult([File(second, "fresh.txt")]);
        Assert.Equal(["fresh.txt"], FileNames(dialog));
    }

    [Fact]
    public void Sorting_And_Filtering_Rearrange_The_Listing_Without_Reading_The_Folder_Again()
    {
        string root = Virtual("root");
        var provider = new ScriptedProvider(directory => directory == root
            ?
            [
                File(root, "beta.md", 300, Monday),
                File(root, "alpha.txt", 100, Monday.AddMonths(5)),
                File(root, "gamma.rtf", 200, Monday.AddYears(-1)),
                Folder(root, "zeta"),
                Folder(root, "Eta"),
            ]
            : []);
        var dialog = new StandardFileDialog(provider) { CurrentDirectory = root };
        int requests = provider.Requests.Count;

        Assert.Equal(["..", "Eta", "zeta"], FolderNames(dialog));
        Assert.Equal(["alpha.txt", "beta.md", "gamma.rtf"], FileNames(dialog));

        dialog.SortOrder = UiFileDialogSortOrder.Size;
        Assert.Equal(["beta.md", "gamma.rtf", "alpha.txt"], FileNames(dialog));

        dialog.SortOrder = UiFileDialogSortOrder.Modified;
        Assert.Equal(["alpha.txt", "beta.md", "gamma.rtf"], FileNames(dialog));

        dialog.FileNameFilter = "*.md;*.RTF";
        Assert.Equal(["beta.md", "gamma.rtf"], FileNames(dialog));
        Assert.Equal(requests, provider.Requests.Count);

        dialog.Refresh();
        Assert.Equal(requests + 1, provider.Requests.Count);
    }

    [Fact]
    public void A_Listing_Still_Arriving_When_The_Dialog_Is_Disposed_Is_Dropped()
    {
        var provider = new ScriptedProvider();
        var dialog = new StandardFileDialog(provider);
        using UiSession session = Session();
        session.AddRoot(dialog);
        string slow = Virtual("slow");
        TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>> listing = provider.HoldNext();
        dialog.CurrentDirectory = slow;

        dialog.Dispose();

        Assert.True(provider.Requests[^1].Token.IsCancellationRequested);
        listing.SetResult([File(slow, "late.txt")]);
    }

    [Fact]
    public void A_Slow_Listing_Asked_For_Before_The_Dialog_Is_Shown_Is_Asked_For_Again_Once_It_Is()
    {
        var provider = new ScriptedProvider { HoldEveryRequest = true };

        // Constructed with no session: there is no UI context to bring a slow
        // listing back to, so it is not waited for.
        var dialog = new StandardFileDialog(provider);

        Assert.True(dialog.IsLoadingDirectory);
        Assert.True(provider.Requests[^1].Token.IsCancellationRequested);
        int requests = provider.Requests.Count;

        using UiSession session = Session();
        session.AddRoot(dialog);

        Assert.Equal(requests + 1, provider.Requests.Count);
        provider.Held[^1].SetResult([File(dialog.CurrentDirectory, "shown.txt")]);
        Assert.False(dialog.IsLoadingDirectory);
        Assert.Equal(["shown.txt"], FileNames(dialog));
    }

    [Fact]
    public void A_Provider_That_Lists_A_Path_Twice_Gets_It_Shown_Once()
    {
        string root = Virtual("root");
        var provider = new ScriptedProvider(directory => directory == root
            ? [File(root, "twice.txt"), File(root, "twice.txt"), Folder(root, "dup"), Folder(root, "dup")]
            : []);

        var dialog = new StandardFileDialog(provider) { CurrentDirectory = root };

        Assert.Equal(["twice.txt"], FileNames(dialog));
        Assert.Equal(["..", "dup"], FolderNames(dialog));
    }

    [Fact]
    public async Task The_File_System_Provider_Lists_Files_And_Folders_With_Their_Details()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "folder"));
        System.IO.File.WriteAllBytes(Path.Combine(temp.Path, "data.bin"), new byte[42]);

        Task<IReadOnlyList<UiFileDialogEntry>> synchronous =
            UiFileSystemDirectoryProvider.Synchronous.GetEntriesAsync(temp.Path, CancellationToken.None);

        Assert.True(synchronous.IsCompletedSuccessfully);
        foreach (IReadOnlyList<UiFileDialogEntry> entries in new[]
        {
            await synchronous,
            await UiFileSystemDirectoryProvider.Background.GetEntriesAsync(temp.Path, CancellationToken.None),
        })
        {
            UiFileDialogEntry file = Assert.Single(entries, static entry => !entry.IsDirectory);
            UiFileDialogEntry folder = Assert.Single(entries, static entry => entry.IsDirectory);
            Assert.Equal("data.bin", file.Name);
            Assert.Equal(42, file.Length);
            Assert.Equal(".bin", file.Extension);
            Assert.Equal(Path.Combine(temp.Path, "folder"), folder.FullPath);
        }

        Assert.False(UiFileSystemDirectoryProvider.Synchronous.ListsInBackground);
        Assert.True(UiFileSystemDirectoryProvider.Background.ListsInBackground);
    }

    [Fact]
    public void The_File_System_Provider_Reports_A_Missing_Folder_As_A_Failed_Listing()
    {
        Task<IReadOnlyList<UiFileDialogEntry>> listing =
            UiFileSystemDirectoryProvider.Synchronous.GetEntriesAsync(Virtual("missing"), CancellationToken.None);

        Assert.True(listing.IsFaulted);
        Assert.IsType<DirectoryNotFoundException>(listing.Exception?.InnerException);
    }

    [Fact]
    public void The_File_System_Provider_Honours_A_Cancelled_Token()
    {
        using var temp = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.True(UiFileSystemDirectoryProvider.Synchronous.GetEntriesAsync(temp.Path, cancellation.Token).IsCanceled);
    }

    [Fact]
    public void The_Default_Dialog_Reads_The_Local_File_System_On_The_Calling_Thread()
    {
        Assert.Same(UiFileSystemDirectoryProvider.Synchronous, new StandardFileDialog().DirectoryProvider);
    }

    [Fact]
    public void A_Background_Listing_Reaches_The_Dialog_Only_Through_Its_Dispatcher()
    {
        using var temp = new TempDirectory();
        System.IO.File.WriteAllText(Path.Combine(temp.Path, "listed.txt"), string.Empty);
        using var woken = new SemaphoreSlim(0);
        var dispatcher = new StandardQueuedUiDispatcher(() => woken.Release());
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(dispatcher).Build(new TestHost());
        var dialog = new StandardFileDialog(UiFileSystemDirectoryProvider.Background);
        session.AddRoot(dialog);
        WaitAndDrain(dispatcher, woken);

        dialog.CurrentDirectory = temp.Path;

        // Listed on the thread pool, then queued for the UI thread: nothing
        // changes on screen until that thread drains its queue.
        Assert.True(dialog.IsLoadingDirectory);
        WaitAndDrain(dispatcher, woken);
        Assert.False(dialog.IsLoadingDirectory);
        Assert.Equal(["listed.txt"], FileNames(dialog));
    }

    /// <summary>Waits for the dispatcher to ask for a drain, as a host's loop would, then drains it here.</summary>
    private static void WaitAndDrain(StandardQueuedUiDispatcher dispatcher, SemaphoreSlim woken)
    {
        Assert.True(woken.Wait(TimeSpan.FromSeconds(30)), "The dispatcher never asked to be drained.");
        Assert.True(dispatcher.Drain() > 0);
    }

    /// <summary>
    /// A provider that answers from a script, or holds a request open until the
    /// test completes it, and remembers every request with its token.
    /// </summary>
    private sealed class ScriptedProvider(Func<string, IReadOnlyList<UiFileDialogEntry>>? script = null) : IUiFileDialogDirectoryProvider
    {
        private int _holdNext;

        public List<(string Directory, CancellationToken Token)> Requests { get; } = [];

        public List<TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>>> Held { get; } = [];

        /// <summary>When true every request is held, whatever the script says.</summary>
        public bool HoldEveryRequest { get; init; }

        /// <summary>Holds the next request open and returns what completes it.</summary>
        public TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>> HoldNext()
        {
            _holdNext++;
            var source = new TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>>();
            Held.Add(source);
            return source;
        }

        public Task<IReadOnlyList<UiFileDialogEntry>> GetEntriesAsync(string directory, CancellationToken cancellationToken)
        {
            Requests.Add((directory, cancellationToken));
            if (HoldEveryRequest)
            {
                var held = new TaskCompletionSource<IReadOnlyList<UiFileDialogEntry>>();
                Held.Add(held);
                return held.Task;
            }

            if (_holdNext > 0)
            {
                _holdNext--;
                return Held[^(_holdNext + 1)].Task;
            }

            try
            {
                return Task.FromResult(script?.Invoke(directory) ?? []);
            }
            catch (Exception exception)
            {
                return Task.FromException<IReadOnlyList<UiFileDialogEntry>>(exception);
            }
        }
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(900, 600);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "broiler-filedialog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
