using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.UI.FileDialog.Standard;

/// <summary>
/// A folder's listing as the dialog shows it: its folders, and the files the
/// filter lets through, each in the dialog's sort order.
/// </summary>
/// <remarks>
/// A function of the listing and the dialog's settings and nothing else, so a
/// change of sort order or filter rearranges the listing the dialog already holds
/// instead of reading the folder again.
/// </remarks>
internal static class StandardFileDialogListing
{
    public static (UiFileDialogEntry[] Directories, UiFileDialogEntry[] Files) Arrange(
        IReadOnlyList<UiFileDialogEntry> entries,
        string fileNameFilter,
        UiFileDialogSortOrder sortOrder)
    {
        var directories = new List<UiFileDialogEntry>();
        var files = new List<UiFileDialogEntry>();
        foreach (UiFileDialogEntry entry in entries)
        {
            if (entry.IsDirectory)
                directories.Add(entry);
            else if (MatchesFilter(entry.Name, fileNameFilter))
                files.Add(entry);
        }

        return (SortDirectories(directories, sortOrder), SortFiles(files, sortOrder));
    }

    /// <summary>
    /// Whether a file name matches one of the patterns of a filter, which are
    /// separated by semicolons or commas and matched without regard to case.
    /// </summary>
    public static bool MatchesFilter(string fileName, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || StringComparer.Ordinal.Equals(filter, "*"))
            return true;

        foreach (string pattern in filter.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (PatternMatches(fileName, pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The folders in <paramref name="sortOrder"/>.
    /// </summary>
    /// <remarks>
    /// A folder carries neither an extension nor a size, so Type and Size leave folders by name:
    /// ordering them by a key they do not have would only scramble the column the user is reading
    /// to navigate. Modified they do have, and sorting by it is most of the point of asking for it.
    /// </remarks>
    private static UiFileDialogEntry[] SortDirectories(List<UiFileDialogEntry> directories, UiFileDialogSortOrder sortOrder) =>
        sortOrder == UiFileDialogSortOrder.Modified
            ? directories
                .OrderByDescending(static item => item.LastWriteTimeUtc)
                .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : directories
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

    /// <summary>
    /// The files in <paramref name="sortOrder"/>. Name breaks every tie, so a folder full of
    /// same-sized or same-dated files still lists in an order that holds still between refreshes.
    /// </summary>
    private static UiFileDialogEntry[] SortFiles(List<UiFileDialogEntry> files, UiFileDialogSortOrder sortOrder) =>
        sortOrder switch
        {
            UiFileDialogSortOrder.Type => files
                .OrderBy(static item => item.Extension, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            UiFileDialogSortOrder.Modified => files
                .OrderByDescending(static item => item.LastWriteTimeUtc)
                .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            UiFileDialogSortOrder.Size => files
                .OrderByDescending(static item => item.Length)
                .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => files
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };

    private static bool PatternMatches(string fileName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) ||
            StringComparer.Ordinal.Equals(pattern, "*") ||
            StringComparer.Ordinal.Equals(pattern, "*.*"))
            return true;

        return WildcardMatches(fileName, pattern);
    }

    private static bool WildcardMatches(string value, string pattern)
    {
        int valueIndex = 0;
        int patternIndex = 0;
        int lastStarIndex = -1;
        int valueAfterStar = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                valueIndex++;
                patternIndex++;
                continue;
            }

            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                lastStarIndex = patternIndex++;
                valueAfterStar = valueIndex;
                continue;
            }

            if (lastStarIndex >= 0)
            {
                patternIndex = lastStarIndex + 1;
                valueIndex = ++valueAfterStar;
                continue;
            }

            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == pattern.Length;
    }
}
