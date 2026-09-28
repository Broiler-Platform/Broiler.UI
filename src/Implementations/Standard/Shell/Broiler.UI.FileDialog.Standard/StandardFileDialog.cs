using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.FileDialog.Standard;

/// <summary>
/// The Broiler-drawn standard <see cref="UiFileDialog"/>: places, folders, and
/// files, with a name box, a file type box, and a sort box.
/// </summary>
/// <remarks>
/// <para>
/// The folder being shown is read through an <see cref="IUiFileDialogDirectoryProvider"/>
/// rather than by the dialog, and the dialog never waits for it. A listing that is
/// ready at once is shown at once; one that is not leaves the folder showing as
/// loading until it arrives, and one that fails leaves it showing the error, with
/// the way up still offered. Moving somewhere else cancels the listing in flight
/// and ignores it if it arrives anyway.
/// </para>
/// <para>
/// The dialog keeps the last listing and sorts and filters that, so changing the
/// sort order or the file type rearranges what is on screen without reading the
/// folder again. <see cref="Refresh"/> reads it again.
/// </para>
/// </remarks>
public sealed class StandardFileDialog : UiFileDialog, IStandardThemedControl
{
    /// <summary>
    /// The orders the sort box offers, in the order it offers them. The labels name the direction
    /// each one runs in, so the box needs no separate caption and no separate direction toggle.
    /// </summary>
    private static readonly (UiFileDialogSortOrder Order, string Id, string Text)[] SortChoices =
    [
        (UiFileDialogSortOrder.Name, "sort:name", "Sort: Name"),
        (UiFileDialogSortOrder.Type, "sort:type", "Sort: Type"),
        (UiFileDialogSortOrder.Modified, "sort:modified", "Sort: Modified"),
        (UiFileDialogSortOrder.Size, "sort:size", "Sort: Size"),
    ];

    private readonly IUiFileDialogDirectoryProvider _directoryProvider;
    private readonly StandardListView _placesList;
    private readonly StandardEdit _fileNameEdit;
    private readonly StandardListView _filesList;
    private readonly StandardListView _directoriesList;
    private readonly StandardButton _upButton;
    private readonly StandardComboBox _fileTypeCombo;
    private readonly StandardComboBox _sortCombo;
    private readonly StandardButton _okButton;
    private readonly StandardButton _cancelButton;
    private readonly Dictionary<string, string> _placePaths = [];
    private readonly Dictionary<string, UiFileDialogEntry> _fileEntries = [];
    private readonly Dictionary<string, string> _directoryPaths = [];

    /// <summary>The listing of <see cref="UiFileDialog.CurrentDirectory"/>, unfiltered and unsorted.</summary>
    private IReadOnlyList<UiFileDialogEntry> _entries = [];

    /// <summary>The listing in flight, or null when none is.</summary>
    private DirectoryLoad? _load;

    /// <summary>
    /// True when a listing could not be waited for because the dialog had no
    /// session to bring it back to, and is started again once it has one.
    /// </summary>
    private bool _reloadOnAttach;
    private Exception? _loadError;
    private BRect _pathBounds;
    private BRect _descriptionBounds;
    private BRect _placesHeaderBounds;
    private BRect _placesPanelBounds;
    private BRect _foldersHeaderBounds;
    private BRect _filesHeaderBounds;
    private BRect _fileNameLabelBounds;
    private BRect _fileTypeLabelBounds;
    private BRect _statusBounds;
    private BRect _footerRuleBounds;
    private bool _refreshing;
    private bool _syncingFileName;
    private bool _syncingSelectors;

    /// <summary>A dialog over the local file system, listed on the calling thread.</summary>
    public StandardFileDialog()
        : this(UiFileSystemDirectoryProvider.Synchronous)
    {
    }

    /// <summary>A dialog over whatever <paramref name="directoryProvider"/> lists.</summary>
    public StandardFileDialog(IUiFileDialogDirectoryProvider directoryProvider)
    {
        ArgumentNullException.ThrowIfNull(directoryProvider);
        _directoryProvider = directoryProvider;
        Title = "Open";

        _placesList = new StandardListView
        {
            PreferredSize = new BSize(150, 260),
            ItemHeight = 28,
            CornerRadius = StandardControlPaint.SmallRadius,
            Background = StandardControlPaint.SurfaceAlt,
        };
        _filesList = new StandardListView
        {
            PreferredSize = new BSize(250, 230),
            ItemHeight = 24,
            CornerRadius = StandardControlPaint.SmallRadius,
        };
        _directoriesList = new StandardListView
        {
            PreferredSize = new BSize(210, 230),
            ItemHeight = 24,
            CornerRadius = StandardControlPaint.SmallRadius,
        };
        _fileNameEdit = new StandardEdit
        {
            PreferredSize = new BSize(430, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _upButton = new StandardButton
        {
            Text = "Up one level",
            PreferredSize = new BSize(104, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _fileTypeCombo = new StandardComboBox
        {
            PreferredSize = new BSize(430, 30),
            ItemHeight = 26,
            CornerRadius = StandardControlPaint.SmallRadius,
        };
        _sortCombo = new StandardComboBox
        {
            PreferredSize = new BSize(132, 26),
            ItemHeight = 26,
            CornerRadius = StandardControlPaint.SmallRadius,
        };
        _okButton = new StandardButton
        {
            Text = "Open",
            IsDefault = true,
            PreferredSize = new BSize(92, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };
        _cancelButton = new StandardButton
        {
            Text = "Cancel",
            IsCancel = true,
            PreferredSize = new BSize(92, 30),
            CornerRadius = StandardControlPaint.SmallRadius,
            PaddingX = 8,
            PaddingY = 5,
        };

        _placesList.SelectionChanged += (_, e) => NavigateToPlace(e.NewItemId);
        _filesList.SelectionChanged += (_, e) => SelectFile(e.NewItemId);
        _directoriesList.SelectionChanged += (_, e) => NavigateToDirectory(e.NewItemId);
        _fileNameEdit.Submitted += (_, _) => AcceptSelection();
        _upButton.Clicked += (_, _) => NavigateUp();
        _fileTypeCombo.SelectionChanged += (_, e) => SelectFileTypeFilter(e.NewIndex);
        _sortCombo.SelectionChanged += (_, e) => SelectSortOrder(e.NewIndex);
        _okButton.Clicked += (_, _) => AcceptSelection();
        _cancelButton.Clicked += (_, _) => Cancel();

        AddChild(_placesList);
        AddChild(_filesList);
        AddChild(_directoriesList);
        AddChild(_fileNameEdit);
        AddChild(_upButton);
        AddChild(_fileTypeCombo);
        AddChild(_sortCombo);
        AddChild(_okButton);
        AddChild(_cancelButton);

        SyncFileTypeCombo();
        SyncSortCombo();
        Refresh();
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        Background = theme.Surface;
        PanelBackground = theme.SurfaceAlt;
        TitleBarBackground = theme.Accent;
        TitleForeground = theme.OnAccent;
        BorderColor = theme.BorderStrong;
        DividerColor = theme.Border;
        PathBackground = theme.Surface;
        PathForeground = theme.Text;
        LabelForeground = theme.TextMuted;
        HeaderForeground = theme.Text;
        StatusForeground = theme.TextMuted;

        foreach (UiElement child in Children)
        {
            if (child is IStandardThemedControl themed)
                themed.ApplyTheme(theme);
        }

        _placesList.Background = theme.SurfaceAlt;
        _placesList.SelectedBackground = theme.AccentSoft;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor PanelBackground { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor TitleBarBackground { get; set; } = StandardControlPaint.Accent;

    public BColor TitleForeground { get; set; } = BColor.White;

    public BColor BorderColor { get; set; } = StandardControlPaint.BorderStrong;

    public BColor DividerColor { get; set; } = StandardControlPaint.Border;

    public BColor PathBackground { get; set; } = StandardControlPaint.Surface;

    public BFontStyle TitleFont { get; set; } = new("Segoe UI", 14, BFontWeight.SemiBold);

    public BFontStyle DescriptionFont { get; set; } = new("Segoe UI", 12);

    public BFontStyle HeaderFont { get; set; } = new("Segoe UI", 12, BFontWeight.SemiBold);

    public BFontStyle LabelFont { get; set; } = new("Segoe UI", 12);

    public BFontStyle PathFont { get; set; } = new("Segoe UI", 12);

    public BFontStyle StatusFont { get; set; } = new("Segoe UI", 12);

    public BColor PathForeground { get; set; } = StandardControlPaint.Text;

    public BColor LabelForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor HeaderForeground { get; set; } = StandardControlPaint.Text;

    public BColor StatusForeground { get; set; } = StandardControlPaint.TextMuted;

    public BSize PreferredSize { get; set; } = new(820, 520);

    public double TitleBarHeight { get; set; } = 34;

    public double PathRowHeight { get; set; } = 28;

    public double Padding { get; set; } = 14;

    public double Gap { get; set; } = 10;

    /// <summary>What the dialog lists folders with.</summary>
    public IUiFileDialogDirectoryProvider DirectoryProvider => _directoryProvider;

    /// <summary>True while the current folder's listing has not arrived.</summary>
    public bool IsLoadingDirectory => _load is not null || _reloadOnAttach;

    /// <summary>Why the current folder could not be listed, or null when it could.</summary>
    public Exception? DirectoryLoadError => _loadError;

    public StandardEdit FileNameEdit => _fileNameEdit;

    public StandardListView PlacesList => _placesList;

    public StandardListView FilesList => _filesList;

    public StandardListView DirectoriesList => _directoriesList;

    public StandardButton UpButton => _upButton;

    public StandardComboBox FileTypeComboBox => _fileTypeCombo;

    public StandardComboBox SortComboBox => _sortCombo;

    public StandardButton OkButton => _okButton;

    public StandardButton CancelButton => _cancelButton;

    /// <summary>Reads the places and the current folder again.</summary>
    public void Refresh()
    {
        ThrowIfDisposed();
        RefreshPlaces();
        LoadDirectory();
        SyncFileNameEdit();
    }

    public bool AcceptSelection()
    {
        ThrowIfDisposed();
        string fileName = _fileNameEdit.Text.Trim();
        if (fileName.Length == 0)
            return false;

        FileName = fileName;
        return Accept(SelectedPath);
    }

    public bool NavigateUp()
    {
        ThrowIfDisposed();
        DirectoryInfo? parent = Directory.GetParent(CurrentDirectory);
        if (parent is null)
            return false;

        CurrentDirectory = parent.FullName;
        return true;
    }

    protected override void OnModeChanged()
    {
        Title = Mode == UiFileDialogMode.Save ? "Save As" : "Open";
        _okButton.Text = Mode == UiFileDialogMode.Save ? "Save" : "Open";
    }

    protected override void OnCurrentDirectoryChanged()
    {
        // Only the folder is read again. The places do not change with it, and
        // reading them on every step would make each step wait for any place on
        // a disconnected network drive.
        if (!_refreshing)
            LoadDirectory();
    }

    protected override void OnFileNameChanged()
    {
        if (!_syncingFileName)
            SyncFileNameEdit();

        SelectMatchingFile();
    }

    protected override void OnDefaultExtensionChanged()
    {
        SelectMatchingFile();
    }

    protected override void OnFileNameFilterChanged()
    {
        if (!_refreshing)
            ShowEntries();
    }

    protected override void OnFileTypeFiltersChanged()
    {
        SyncFileTypeCombo();
    }

    protected override void OnSelectedFileTypeFilterChanged()
    {
        SyncFileTypeCombo();
    }

    protected override void OnSortOrderChanged()
    {
        SyncSortCombo();
        if (!_refreshing)
            ShowEntries();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (_reloadOnAttach)
            LoadDirectory();
    }

    protected override void OnDetached()
    {
        // A listing still on its way has nowhere to come back to once the dialog
        // leaves its session. It is started again if the dialog comes back, which
        // a dialog breaking out into its own window does straight away.
        if (_load is not null)
        {
            CancelLoad();
            _reloadOnAttach = true;
        }

        base.OnDetached();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelLoad();
            _reloadOnAttach = false;
        }

        base.Dispose(disposing);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize clientAvailable = new(
            Math.Max(0, availableSize.Width - Padding * 2),
            Math.Max(0, availableSize.Height - TitleBarHeight - Padding * 2));

        foreach (UiElement child in Children)
            child.Measure(clientAvailable);

        return new BSize(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = GetClientBounds(finalRect);
        // The floor is what "Up one level" needs: the button column narrows with the dialog, and
        // a column that drops below its longest caption clips the caption instead.
        double buttonWidth = Math.Min(112, Math.Max(104, client.Width * 0.14));
        double buttonHeight = 30;
        double editHeight = 30;
        bool showFileType = FileTypeFilters.Count > 0;
        double labelHeight = 18;
        double rowGap = 8;
        double statusHeight = 20;

        // The list headers share their row with the sort box, so the row is a control tall rather
        // than a caption tall.
        double headerHeight = 26;
        double topInfoHeight = 58;

        // Every labelled row is a label *plus* its control: the label sits above the control it
        // names instead of behind it.
        double footerStackHeight = labelHeight + editHeight +
            (showFileType ? rowGap + labelHeight + editHeight : Gap + buttonHeight);
        double footerHeight = footerStackHeight + rowGap + statusHeight;
        double placesWidth = Math.Min(178, Math.Max(138, client.Width * 0.24));
        double rightX = Math.Max(client.Left, client.Right - buttonWidth);
        double contentLeft = client.Left + placesWidth + Gap;
        double contentWidth = Math.Max(0, rightX - contentLeft - Gap);
        double footerTop = Math.Max(client.Top + topInfoHeight + headerHeight + Gap, client.Bottom - footerHeight);
        double listHeaderTop = client.Top + topInfoHeight;
        double listTop = listHeaderTop + headerHeight;
        double listHeight = Math.Max(0, footerTop - listTop - Gap);
        double folderWidth = Math.Min(Math.Max(160, contentWidth * 0.38), Math.Max(0, contentWidth - 160 - Gap));
        double fileWidth = Math.Max(0, contentWidth - folderWidth - Gap);
        double filesLeft = contentLeft + folderWidth + Gap;
        double sortWidth = Math.Min(Math.Max(112, fileWidth * 0.46), fileWidth);
        double sortHeight = Math.Min(headerHeight, _sortCombo.PreferredSize.Height);
        double fileNameTop = footerTop + labelHeight;
        double fileTypeLabelTop = fileNameTop + editHeight + rowGap;
        double fileTypeTop = fileTypeLabelTop + labelHeight;
        double cancelTop = showFileType ? fileTypeTop : fileNameTop + editHeight + Gap;

        _placesHeaderBounds = new BRect(client.Left, client.Top, placesWidth, labelHeight);
        _placesPanelBounds = new BRect(client.Left, client.Top, placesWidth, Math.Max(0, footerTop - client.Top - Gap));
        _descriptionBounds = new BRect(contentLeft, client.Top, contentWidth, 20);
        _pathBounds = new BRect(contentLeft, client.Top + 24, contentWidth, Math.Min(PathRowHeight, Math.Max(0, topInfoHeight - 28)));
        _foldersHeaderBounds = new BRect(contentLeft, listHeaderTop, folderWidth, headerHeight);
        _filesHeaderBounds = new BRect(filesLeft, listHeaderTop, Math.Max(0, fileWidth - sortWidth - Gap), headerHeight);
        _fileNameLabelBounds = new BRect(contentLeft, footerTop, contentWidth, labelHeight);
        _fileTypeLabelBounds = showFileType
            ? new BRect(contentLeft, fileTypeLabelTop, contentWidth, labelHeight)
            : default;
        _statusBounds = new BRect(client.Left, client.Bottom - statusHeight, Math.Max(0, client.Width - buttonWidth - Gap), statusHeight);
        _footerRuleBounds = new BRect(client.Left, Math.Max(client.Top, footerTop - Gap / 2), client.Width, 1);

        _placesList.Arrange(new BRect(client.Left, client.Top + labelHeight, placesWidth, Math.Max(0, footerTop - client.Top - labelHeight - Gap)));
        _directoriesList.Arrange(new BRect(contentLeft, listTop, folderWidth, listHeight));
        _filesList.Arrange(new BRect(filesLeft, listTop, fileWidth, listHeight));
        _sortCombo.Arrange(new BRect(
            filesLeft + Math.Max(0, fileWidth - sortWidth),
            listHeaderTop + Math.Max(0, (headerHeight - sortHeight) / 2),
            sortWidth,
            sortHeight));
        _fileNameEdit.Arrange(new BRect(contentLeft, fileNameTop, contentWidth, editHeight));
        _fileTypeCombo.Arrange(showFileType
            ? new BRect(contentLeft, fileTypeTop, contentWidth, editHeight)
            : new BRect(client.Left, client.Bottom, 0, 0));
        _upButton.Arrange(new BRect(rightX, client.Top + 24, buttonWidth, buttonHeight));
        _okButton.Arrange(new BRect(rightX, fileNameTop, buttonWidth, buttonHeight));
        _cancelButton.Arrange(new BRect(rightX, cancelTop, buttonWidth, buttonHeight));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        context.RenderList.FillRect(Bounds, Background);
        context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)), TitleBarBackground);
        if (!string.IsNullOrWhiteSpace(Title))
            context.RenderList.DrawText(new BTextRun(Title, TitleFont, TitleForeground), new BPoint(Bounds.Left + Padding, Bounds.Top + 7));

        if (!_placesPanelBounds.IsEmpty)
            StandardControlPaint.FillRounded(context.RenderList, _placesPanelBounds, PanelBackground, StandardControlPaint.SmallRadius);

        DrawDialogText(context);
        DrawCurrentDirectory(context);
        base.RenderCore(context);
        DrawStatus(context);
        context.RenderList.StrokeRect(Bounds, BorderColor, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (base.OnInput(input))
            return true;

        if (input.Kind == UiInputEventKind.PointerButton)
            return HandlePointerButton(input);
        if (input.Kind == UiInputEventKind.KeyboardKey)
            return HandleKeyboard(input);

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        new BRect(Bounds.Left, Bounds.Top, Bounds.Width, Math.Min(TitleBarHeight, Bounds.Height)).Contains(position);

    // --- Listing -----------------------------------------------------------

    /// <summary>
    /// Asks the provider for the current folder, replacing any listing still in
    /// flight. A listing that is ready at once is shown at once; otherwise the
    /// folder shows as loading until the listing is brought back through the
    /// session's dispatcher.
    /// </summary>
    private void LoadDirectory()
    {
        CancelLoad();
        _reloadOnAttach = false;
        _loadError = null;
        _entries = [];

        var load = new DirectoryLoad();
        Task<IReadOnlyList<UiFileDialogEntry>> listing =
            _directoryProvider.GetEntriesAsync(CurrentDirectory, load.Cancellation.Token) ??
            throw new InvalidOperationException("The directory provider returned no listing.");

        if (listing.IsCompleted)
        {
            ShowListing(listing);
            return;
        }

        if (Session?.Dispatcher is not IUiDispatcher dispatcher)
        {
            // Not in a session: there is no UI context to bring the listing back
            // to, so it is not waited for. Attaching starts it again.
            load.Cancellation.Cancel();
            _reloadOnAttach = true;
            _ = listing.ContinueWith(
                static abandoned => _ = abandoned.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            ShowEntries();
            return;
        }

        _load = load;
        ShowEntries();
        _ = listing.ContinueWith(
            completed =>
            {
                // Observed here, so a listing that fails after it stopped being
                // wanted is not reported as an unobserved task exception.
                _ = completed.Exception;
                dispatcher.Post(() => CompleteLoad(load, completed));
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Shows a listing that arrived later, unless it is no longer the one wanted.</summary>
    private void CompleteLoad(DirectoryLoad load, Task<IReadOnlyList<UiFileDialogEntry>> listing)
    {
        if (IsDisposed || !ReferenceEquals(_load, load))
            return;

        _load = null;
        ShowListing(listing);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private void ShowListing(Task<IReadOnlyList<UiFileDialogEntry>> listing)
    {
        if (listing.IsCompletedSuccessfully)
        {
            _entries = listing.Result ?? [];
            _loadError = null;
        }
        else
        {
            _entries = [];
            _loadError = listing.Exception?.InnerException ??
                new OperationCanceledException("The folder listing was cancelled.");
        }

        ShowEntries();
    }

    private void CancelLoad()
    {
        if (_load is not DirectoryLoad load)
            return;

        _load = null;
        load.Cancellation.Cancel();
    }

    /// <summary>
    /// Fills the folder and file lists from the listing the dialog holds, filtered
    /// and sorted the way the dialog says now.
    /// </summary>
    private void ShowEntries()
    {
        bool wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            var files = new List<UiListItem>();
            var directories = new List<UiListItem>();
            _fileEntries.Clear();
            _directoryPaths.Clear();

            // The way up is offered whatever became of the listing, so a folder
            // that is still loading, or could not be read, is never a dead end.
            DirectoryInfo? parent = Directory.GetParent(CurrentDirectory);
            if (parent is not null)
                AddDirectoryItem(directories, "..", parent.FullName);

            (UiFileDialogEntry[] folders, UiFileDialogEntry[] matching) =
                StandardFileDialogListing.Arrange(_entries, FileNameFilter, SortOrder);
            foreach (UiFileDialogEntry folder in folders)
                AddDirectoryItem(directories, folder.Name, folder.FullPath);

            foreach (UiFileDialogEntry file in matching)
                AddFileItem(files, file);

            _directoriesList.SetItems(directories);
            _directoriesList.SelectedItemId = null;
            _filesList.SetItems(files);
            SelectMatchingFile();
            SyncSelectedPlace();
        }
        finally
        {
            _refreshing = wasRefreshing;
        }
    }

    private void AddFileItem(List<UiListItem> items, UiFileDialogEntry file)
    {
        // A provider that lists one path twice gets it shown once: the list
        // refuses duplicate item IDs, and the second copy says nothing new.
        string id = "file:" + file.FullPath;
        if (_fileEntries.TryAdd(id, file))
            items.Add(new UiListItem(id, file.Name));
    }

    private void AddDirectoryItem(List<UiListItem> items, string text, string path)
    {
        string id = "dir:" + path;
        if (_directoryPaths.TryAdd(id, path))
            items.Add(new UiListItem(id, text));
    }

    private void SelectFile(string? itemId)
    {
        if (_refreshing || itemId is null || !_fileEntries.TryGetValue(itemId, out UiFileDialogEntry? file))
            return;

        FileName = Path.GetFileName(file.FullPath);
    }

    /// <summary>
    /// Goes to a place. Whether it is still there is the listing's to say, not a
    /// check made here: a place on a disconnected drive would make the click wait.
    /// </summary>
    private void NavigateToPlace(string? itemId)
    {
        if (_refreshing || itemId is null || !_placePaths.TryGetValue(itemId, out string? path))
            return;

        CurrentDirectory = path;
    }

    private void NavigateToDirectory(string? itemId)
    {
        if (_refreshing || itemId is null || !_directoryPaths.TryGetValue(itemId, out string? path))
            return;

        CurrentDirectory = path;
    }

    private void SelectFileTypeFilter(int index)
    {
        if (_syncingSelectors || index < 0)
            return;

        SelectedFileTypeFilterIndex = index;
    }

    private void SelectSortOrder(int index)
    {
        if (_syncingSelectors || (uint)index >= (uint)SortChoices.Length)
            return;

        SortOrder = SortChoices[index].Order;
    }

    private void SyncFileNameEdit()
    {
        _syncingFileName = true;
        try
        {
            _fileNameEdit.Text = FileName;
        }
        finally
        {
            _syncingFileName = false;
        }
    }

    private void SelectMatchingFile()
    {
        string expected = Path.Combine(CurrentDirectory, ApplyDefaultExtension(FileName));
        string? selected = _fileEntries
            .Where(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Value.FullPath, expected))
            .Select(static pair => pair.Key)
            .FirstOrDefault();

        _filesList.SelectedItemId = selected;
    }

    /// <summary>
    /// Mirrors the dialog's filter set onto the file type box. The box is the input as well as
    /// the display, so its own selection change is routed back here; the guard is what stops that
    /// round trip from re-entering while the box is being filled.
    /// </summary>
    private void SyncFileTypeCombo()
    {
        _syncingSelectors = true;
        try
        {
            // Keyed by position rather than by name: two formats may well share a display name,
            // and the box refuses duplicate item IDs.
            _fileTypeCombo.SetItems(FileTypeFilters
                .Select(static (filter, index) => new UiComboBoxItem(
                    "filter:" + index.ToString(CultureInfo.InvariantCulture),
                    filter.Name)));
            _fileTypeCombo.SelectIndex(SelectedFileTypeFilterIndex);
            _fileTypeCombo.IsEnabled = FileTypeFilters.Count > 1;
        }
        finally
        {
            _syncingSelectors = false;
        }
    }

    private void SyncSortCombo()
    {
        _syncingSelectors = true;
        try
        {
            if (_sortCombo.Items.Count != SortChoices.Length)
                _sortCombo.SetItems(SortChoices.Select(static choice => new UiComboBoxItem(choice.Id, choice.Text)));

            _sortCombo.SelectIndex(Array.FindIndex(SortChoices, choice => choice.Order == SortOrder));
        }
        finally
        {
            _syncingSelectors = false;
        }
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Activate();
        Session?.SetFocus(this);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
            return NavigateUp();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
            return AcceptSelection();

        return false;
    }

    private void DrawCurrentDirectory(UiRenderContext context)
    {
        if (_pathBounds.IsEmpty)
            return;

        StandardControlPaint.FillRounded(context.RenderList, _pathBounds, PathBackground, StandardControlPaint.SmallRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, _pathBounds, DividerColor, StandardControlPaint.SmallRadius, 1);

        double y = _pathBounds.Top + Math.Max(0, (_pathBounds.Height - BTextMeasurer.GetLineHeight(PathFont)) / 2);
        context.RenderList.PushClip(_pathBounds);
        context.RenderList.DrawText(new BTextRun(CurrentDirectory, PathFont, PathForeground), new BPoint(_pathBounds.Left + 8, y));
        context.RenderList.PopClip();
    }

    private void DrawDialogText(UiRenderContext context)
    {
        if (!_footerRuleBounds.IsEmpty)
            context.RenderList.FillRect(_footerRuleBounds, DividerColor);

        DrawText(context, "Places", _placesHeaderBounds, HeaderFont, HeaderForeground);
        DrawText(context, BuildModeDescription(), _descriptionBounds, DescriptionFont, LabelForeground);
        DrawText(context, "Folders (" + _directoriesList.Items.Count.ToString(CultureInfo.InvariantCulture) + ")", _foldersHeaderBounds, HeaderFont, HeaderForeground);
        DrawText(context, BuildFilesHeader(), _filesHeaderBounds, HeaderFont, HeaderForeground);
        DrawText(context, "File name", _fileNameLabelBounds, LabelFont, LabelForeground);

        if (!_fileTypeLabelBounds.IsEmpty)
            DrawText(context, "File type", _fileTypeLabelBounds, LabelFont, LabelForeground);
    }

    private void DrawStatus(UiRenderContext context) =>
        DrawText(context, BuildStatusText(), _statusBounds, StatusFont, StatusForeground);

    private void DrawText(UiRenderContext context, string text, BRect bounds, BFontStyle font, BColor color)
    {
        if (bounds.IsEmpty || string.IsNullOrWhiteSpace(text))
            return;

        double y = bounds.Top + Math.Max(0, (bounds.Height - BTextMeasurer.GetLineHeight(font)) / 2);
        context.RenderList.PushClip(bounds);
        context.RenderList.DrawText(new BTextRun(text, font, color), new BPoint(bounds.Left, y));
        context.RenderList.PopClip();
    }

    private string BuildModeDescription() =>
        Mode == UiFileDialogMode.Save
            ? "Choose a folder, name the document, and pick the format to save."
            : "Choose a document to open, or jump to a common location from Places.";

    /// <summary>
    /// The files header. The filter it used to name is the file type box's own label now, and the
    /// room that frees is where the sort box sits.
    /// </summary>
    private string BuildFilesHeader() =>
        "Files (" + _filesList.Items.Count.ToString(CultureInfo.InvariantCulture) + ")";

    private string BuildStatusText()
    {
        if (IsLoadingDirectory)
            return "Loading " + DescribeDirectory(CurrentDirectory) + "...";

        if (_loadError is Exception error)
            return "Cannot open " + DescribeDirectory(CurrentDirectory) + ": " + error.Message;

        string? selectedId = _filesList.SelectedItemId;
        if (selectedId is not null && _fileEntries.TryGetValue(selectedId, out UiFileDialogEntry? file))
        {
            return "Selected: " + file.Name + " | " + FormatByteSize(file.Length) + " | Modified " +
                file.LastWriteTimeUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        string displayName = ApplyDefaultExtension(FileName);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return (Mode == UiFileDialogMode.Save ? "Ready to save: " : "Typed file: ") +
                displayName + " in " + DescribeDirectory(CurrentDirectory);
        }

        return _directoriesList.Items.Count.ToString(CultureInfo.InvariantCulture) + " folders and " +
            _filesList.Items.Count.ToString(CultureInfo.InvariantCulture) + " matching files in " +
            DescribeDirectory(CurrentDirectory);
    }

    // --- Places ------------------------------------------------------------

    private void RefreshPlaces()
    {
        bool wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            var items = new List<UiListItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _placePaths.Clear();

            AddPlace(items, seen, "Home", GetKnownFolder(Environment.SpecialFolder.UserProfile));
            AddPlace(items, seen, "Desktop", GetKnownFolder(Environment.SpecialFolder.DesktopDirectory));
            AddPlace(items, seen, "Documents", GetKnownFolder(Environment.SpecialFolder.MyDocuments));
            AddPlace(items, seen, "Downloads", GetDownloadsFolder());
            AddPlace(items, seen, "Pictures", GetKnownFolder(Environment.SpecialFolder.MyPictures));
            AddPlace(items, seen, "Working folder", Environment.CurrentDirectory);

            foreach (string drive in EnumerateDriveRoots())
                AddPlace(items, seen, DescribeDriveRoot(drive), drive);

            _placesList.SetItems(items);
            SyncSelectedPlace();
        }
        finally
        {
            _refreshing = wasRefreshing;
        }
    }

    private void AddPlace(List<UiListItem> items, HashSet<string> seen, string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch (Exception ex) when (IsFileSystemReadException(ex) || ex is ArgumentException or NotSupportedException)
        {
            return;
        }

        if (!Directory.Exists(normalized) || !seen.Add(NormalizeForComparison(normalized)))
            return;

        string id = "place:" + items.Count.ToString(CultureInfo.InvariantCulture);
        _placePaths[id] = normalized;
        items.Add(new UiListItem(id, label));
    }

    /// <summary>
    /// Selects the place the current folder is in. Selecting it is not going to
    /// it, so the list's own selection change is kept from navigating.
    /// </summary>
    private void SyncSelectedPlace()
    {
        bool wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            string current = NormalizeForComparison(CurrentDirectory);
            string? selected = _placePaths
                .Where(pair => current.StartsWith(AddTrailingSeparator(NormalizeForComparison(pair.Value)), StringComparison.OrdinalIgnoreCase) ||
                               StringComparer.OrdinalIgnoreCase.Equals(current, NormalizeForComparison(pair.Value)))
                .OrderByDescending(pair => NormalizeForComparison(pair.Value).Length)
                .Select(static pair => pair.Key)
                .FirstOrDefault();

            _placesList.SelectedItemId = selected;
        }
        finally
        {
            _refreshing = wasRefreshing;
        }
    }

    private BRect GetClientBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + TitleBarHeight + Padding,
            Math.Max(0, bounds.Width - Padding * 2),
            Math.Max(0, bounds.Height - TitleBarHeight - Padding * 2));

    private static bool IsFileSystemReadException(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException;

    private static string? GetKnownFolder(Environment.SpecialFolder folder)
    {
        try
        {
            string path = Environment.GetFolderPath(folder);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? GetDownloadsFolder()
    {
        string? home = GetKnownFolder(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, "Downloads");
    }

    private static string[] EnumerateDriveRoots()
    {
        try
        {
            return Directory.GetLogicalDrives();
        }
        catch (Exception ex) when (IsFileSystemReadException(ex) || ex is NotSupportedException)
        {
            return [];
        }
    }

    private static string DescribeDriveRoot(string path)
    {
        string root = Path.GetPathRoot(path) ?? path;
        if (StringComparer.Ordinal.Equals(root, Path.DirectorySeparatorChar.ToString()))
            return "File system";

        return "Drive " + root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string DescribeDirectory(string path)
    {
        string root = Path.GetPathRoot(path) ?? string.Empty;
        if (StringComparer.OrdinalIgnoreCase.Equals(AddTrailingSeparator(root), AddTrailingSeparator(path)))
            return string.IsNullOrWhiteSpace(root) ? path : root;

        string? name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = unit == 0 ? "0" : "0.#";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
    }

    private static string NormalizeForComparison(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (IsFileSystemReadException(ex) || ex is ArgumentException or NotSupportedException)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string AddTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path) ||
            path.EndsWith(Path.DirectorySeparatorChar) ||
            path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    /// <summary>
    /// One request for a listing. Its identity is what tells a result that is
    /// still wanted from one that was superseded.
    /// </summary>
    private sealed class DirectoryLoad
    {
        public CancellationTokenSource Cancellation { get; } = new();
    }
}
