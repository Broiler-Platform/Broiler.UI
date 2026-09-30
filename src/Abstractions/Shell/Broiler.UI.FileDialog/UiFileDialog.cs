using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.FileDialog;

// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=2; Fingerprint=TBF
// Broiler-Falsified-If: a FileName of '..' yields a SelectedPath naming the parent of CurrentDirectory instead of a file inside it
// Broiler-Human:        PENDING
public abstract class UiFileDialog : UiDialog
{
    private UiFileDialogMode _mode;
    private string _currentDirectory = NormalizeDirectory(null);
    private string _fileName = string.Empty;
    private string _defaultExtension = string.Empty;
    private string _fileNameFilter = "*";
    private UiFileDialogFilter[] _fileTypeFilters = [];
    private int _selectedFileTypeFilterIndex = -1;
    private UiFileDialogSortOrder _sortOrder = UiFileDialogSortOrder.Name;

    // Broiler-AI:           Origin=AI; Spec=ADR-0026; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a new file dialog reports CanResize as false
    // Broiler-Human:        PENDING
    protected UiFileDialog()
    {
        // The one dialog whose content is a list of unknown length. A folder deeper than the
        // eight rows a fixed frame affords is the normal case, not the exceptional one, so this
        // is the dialog that takes UiDialog's fixed size back.
        CanResize = true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting Mode to the value it already holds raises OnModeChanged again
    // Broiler-Human:        PENDING
    public UiFileDialogMode Mode
    {
        get => _mode;
        set
        {
            ThrowIfDisposed();
            if (_mode == value)
                return;

            _mode = value;
            OnModeChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a value containing '..' segments is stored without being collapsed to a full path, so the folder listed differs from the one shown
    // Broiler-Human:        PENDING
    public string CurrentDirectory
    {
        get => _currentDirectory;
        set
        {
            ThrowIfDisposed();
            string normalized = NormalizeDirectory(value);
            if (StringComparer.Ordinal.Equals(_currentDirectory, normalized))
                return;

            _currentDirectory = normalized;
            OnCurrentDirectoryChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a value with a directory part, such as 'sub\a.txt' or an absolute path, is stored with that directory part instead of as the bare file name
    // Broiler-Human:        PENDING
    public string FileName
    {
        get => _fileName;
        set
        {
            ThrowIfDisposed();
            string normalized = Path.GetFileName(value ?? string.Empty);
            if (StringComparer.Ordinal.Equals(_fileName, normalized))
                return;

            _fileName = normalized;
            OnFileNameChanged();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a value given without its dot, such as 'txt', is stored without a leading dot, so the appended extension runs into the file name
    // Broiler-Human:        PENDING
    public string DefaultExtension
    {
        get => _defaultExtension;
        set
        {
            ThrowIfDisposed();
            string normalized = NormalizeExtension(value);
            if (StringComparer.Ordinal.Equals(_defaultExtension, normalized))
                return;

            _defaultExtension = normalized;
            OnDefaultExtensionChanged();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a blank value is stored as blank instead of '*', so the listing shows no files
    // Broiler-Human:        PENDING
    public string FileNameFilter
    {
        get => _fileNameFilter;
        set
        {
            ThrowIfDisposed();
            string normalized = string.IsNullOrWhiteSpace(value) ? "*" : value.Trim();
            if (StringComparer.Ordinal.Equals(_fileNameFilter, normalized))
                return;

            _fileNameFilter = normalized;
            OnFileNameFilterChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public IReadOnlyList<UiFileDialogFilter> FileTypeFilters => _fileTypeFilters;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an index past the last filter is stored as given instead of being clamped to the last filter
    // Broiler-Human:        PENDING
    public int SelectedFileTypeFilterIndex
    {
        get => _selectedFileTypeFilterIndex;
        set
        {
            ThrowIfDisposed();
            int normalized = NormalizeSelectedFilterIndex(value);
            if (_selectedFileTypeFilterIndex == normalized)
                return;

            _selectedFileTypeFilterIndex = normalized;
            ApplySelectedFileTypeFilter();
            OnSelectedFileTypeFilterChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The order folders and files are listed in. Each order carries its own direction; see
    /// <see cref="UiFileDialogSortOrder"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting SortOrder to the value it already holds raises OnSortOrderChanged again
    // Broiler-Human:        PENDING
    public UiFileDialogSortOrder SortOrder
    {
        get => _sortOrder;
        set
        {
            ThrowIfDisposed();
            if (_sortOrder == value)
                return;

            _sortOrder = value;
            OnSortOrderChanged();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an index of -1 or one past the last filter reads the filter array instead of returning null
    // Broiler-Human:        PENDING
    public UiFileDialogFilter? SelectedFileTypeFilter =>
        _selectedFileTypeFilterIndex >= 0 && _selectedFileTypeFilterIndex < _fileTypeFilters.Length
            ? _fileTypeFilters[_selectedFileTypeFilterIndex]
            : null;

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a FileName of '..' yields a SelectedPath naming the parent of CurrentDirectory instead of a file inside it
    // Broiler-Human:        PENDING
    public string SelectedPath =>
        string.IsNullOrWhiteSpace(FileName)
            ? CurrentDirectory
            : Path.GetFullPath(Path.Combine(CurrentDirectory, ApplyDefaultExtension(FileName)));

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a filter list containing null throws but is left in place as FileTypeFilters, null entry included
    // Broiler-Human:        PENDING
    public void SetFileTypeFilters(IEnumerable<UiFileDialogFilter>? filters, int selectedIndex = 0)
    {
        ThrowIfDisposed();
        _fileTypeFilters = filters?.ToArray() ?? [];
        if (_fileTypeFilters.Any(static filter => filter is null))
            throw new ArgumentException("File dialog filters cannot contain null values.", nameof(filters));

        _selectedFileTypeFilterIndex = NormalizeSelectedFilterIndex(selectedIndex);
        ApplySelectedFileTypeFilter();
        OnFileTypeFiltersChanged();
        OnSelectedFileTypeFilterChanged();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a name with a directory part, such as '..\x.txt' or 'C:\x', keeps that directory part in the returned name
    // Broiler-Human:        PENDING
    public string ApplyDefaultExtension(string fileName)
    {
        ThrowIfDisposed();
        string normalized = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrWhiteSpace(DefaultExtension) || Path.HasExtension(normalized))
            return normalized;

        return normalized + DefaultExtension;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: ShowOpenModal presents a dialog whose Mode is Save
    // Broiler-Human:        PENDING
    public Task<UiDialogResult> ShowOpenModal(UiWindow owner, BRect placement = default)
    {
        Mode = UiFileDialogMode.Open;
        return ShowModal(owner, placement);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: ShowSaveModal presents a dialog whose Mode is Open
    // Broiler-Human:        PENDING
    public Task<UiDialogResult> ShowSaveModal(UiWindow owner, BRect placement = default)
    {
        Mode = UiFileDialogMode.Save;
        return ShowModal(owner, placement);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnModeChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnCurrentDirectoryChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnFileNameChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnDefaultExtensionChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnFileNameFilterChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnFileTypeFiltersChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnSelectedFileTypeFilterChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnSortOrderChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: selecting a filter leaves the previous filter's pattern or default extension in effect
    // Broiler-Human:        PENDING
    private void ApplySelectedFileTypeFilter()
    {
        UiFileDialogFilter? filter = SelectedFileTypeFilter;
        if (filter is null)
            return;

        FileNameFilter = filter.Pattern;
        DefaultExtension = filter.DefaultExtension;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a negative index with at least one filter present is returned unchanged instead of clamped to 0
    // Broiler-Human:        PENDING
    private int NormalizeSelectedFilterIndex(int index)
    {
        if (_fileTypeFilters.Length == 0)
            return -1;

        return Math.Clamp(index, 0, _fileTypeFilters.Length - 1);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a null or blank directory yields an empty or relative path instead of the full path of the process working directory
    // Broiler-Human:        PENDING
    private static string NormalizeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            directory = Environment.CurrentDirectory;

        return Path.GetFullPath(directory);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an extension given as 'txt' or ' .txt ' is not returned as '.txt'
    // Broiler-Human:        PENDING
    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim();
        if (StringComparer.Ordinal.Equals(trimmed, "*"))
            return string.Empty;

        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }
}
