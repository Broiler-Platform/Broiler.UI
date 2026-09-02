using Broiler.Graphics;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.FileDialog;
using Broiler.UI.FileDialog.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class FileDialogControlTests
{
    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Filters_Files_Appends_Default_Extension_And_Navigates_Up()
    {
        using var temp = new TempDirectory();
        string nested = Path.Combine(temp.Path, "docs");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(temp.Path, "alpha.txt"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "Beta.RTF"), string.Empty);

        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
            FileNameFilter = "*.rtf",
            DefaultExtension = "rtf",
        };

        Assert.Contains(dialog.FilesList.Items, item => item.Text == "Beta.RTF");
        Assert.DoesNotContain(dialog.FilesList.Items, item => item.Text == "alpha.txt");

        dialog.FileName = "draft";
        Assert.Equal(Path.Combine(temp.Path, "draft.rtf"), dialog.SelectedPath);

        dialog.CurrentDirectory = nested;
        dialog.UpButton.Click();

        Assert.Equal(temp.Path, dialog.CurrentDirectory);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Renders_Current_Directory_Line()
    {
        using var temp = new TempDirectory();
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
        };

        session.AddRoot(dialog);
        BRenderList renderList = session.RenderFrame();

        renderList.Validate();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == temp.Path);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Picks_Named_File_Type_Filters_From_The_Combo_Box()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "draft.rtf"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "page.html"), string.Empty);
        File.WriteAllText(Path.Combine(temp.Path, "notes.md"), string.Empty);

        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
        };
        dialog.SetFileTypeFilters(
            [
                new UiFileDialogFilter("All documents", "*.rtf;*.html;*.md", ".rtf"),
                new UiFileDialogFilter("Markdown", "*.md", ".md"),
            ]);

        Assert.Contains(dialog.FilesList.Items, item => item.Text == "draft.rtf");
        Assert.Contains(dialog.FilesList.Items, item => item.Text == "page.html");
        Assert.Contains(dialog.FilesList.Items, item => item.Text == "notes.md");
        Assert.Equal(["All documents", "Markdown"], dialog.FileTypeComboBox.Items.Select(item => item.Text));
        Assert.Equal("All documents", dialog.FileTypeComboBox.SelectedItem?.Text);

        dialog.FileName = "new";
        Assert.Equal(Path.Combine(temp.Path, "new.rtf"), dialog.SelectedPath);

        dialog.FileTypeComboBox.SelectIndex(1);

        Assert.DoesNotContain(dialog.FilesList.Items, item => item.Text == "draft.rtf");
        Assert.DoesNotContain(dialog.FilesList.Items, item => item.Text == "page.html");
        Assert.Contains(dialog.FilesList.Items, item => item.Text == "notes.md");
        Assert.Equal(1, dialog.SelectedFileTypeFilterIndex);
        Assert.Equal(Path.Combine(temp.Path, "new.md"), dialog.SelectedPath);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_File_Type_Combo_Follows_The_Dialog_Selection()
    {
        var dialog = new StandardFileDialog();
        dialog.SetFileTypeFilters(
            [
                new UiFileDialogFilter("All documents", "*.rtf;*.md", ".rtf"),
                new UiFileDialogFilter("Markdown", "*.md", ".md"),
            ]);

        dialog.SelectedFileTypeFilterIndex = 1;

        Assert.Equal(1, dialog.FileTypeComboBox.SelectedIndex);
        Assert.Equal("Markdown", dialog.FileTypeComboBox.SelectedItem?.Text);

        // A single filter leaves nothing to choose between, so the box stops accepting a choice.
        dialog.SetFileTypeFilters([new UiFileDialogFilter("Markdown", "*.md", ".md")]);

        Assert.False(dialog.FileTypeComboBox.IsEnabled);
        Assert.Equal(0, dialog.FileTypeComboBox.SelectedIndex);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Sort_Combo_Reorders_Files_And_Folders()
    {
        using var temp = new TempDirectory();
        WriteFile(temp.Path, "beta.md", 300, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteFile(temp.Path, "alpha.txt", 100, new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteFile(temp.Path, "gamma.rtf", 200, new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var dialog = new StandardFileDialog
        {
            CurrentDirectory = temp.Path,
        };

        Assert.Equal(UiFileDialogSortOrder.Name, dialog.SortOrder);
        Assert.Equal(["alpha.txt", "beta.md", "gamma.rtf"], FileNames(dialog));

        dialog.SortComboBox.SelectIndex(IndexOfSortItem(dialog, "Sort: Type"));
        Assert.Equal(UiFileDialogSortOrder.Type, dialog.SortOrder);
        Assert.Equal(["beta.md", "gamma.rtf", "alpha.txt"], FileNames(dialog));

        dialog.SortComboBox.SelectIndex(IndexOfSortItem(dialog, "Sort: Modified"));
        Assert.Equal(UiFileDialogSortOrder.Modified, dialog.SortOrder);
        Assert.Equal(["alpha.txt", "beta.md", "gamma.rtf"], FileNames(dialog));

        dialog.SortComboBox.SelectIndex(IndexOfSortItem(dialog, "Sort: Size"));
        Assert.Equal(UiFileDialogSortOrder.Size, dialog.SortOrder);
        Assert.Equal(["beta.md", "gamma.rtf", "alpha.txt"], FileNames(dialog));
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Sort_Combo_Follows_The_Dialog_Sort_Order()
    {
        var dialog = new StandardFileDialog();

        Assert.Equal("Sort: Name", dialog.SortComboBox.SelectedItem?.Text);

        dialog.SortOrder = UiFileDialogSortOrder.Size;

        Assert.Equal("Sort: Size", dialog.SortComboBox.SelectedItem?.Text);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Is_Resizable_Unlike_A_Plain_Dialog()
    {
        var dialog = new StandardFileDialog();
        var plain = new StandardDialog();

        Assert.True(dialog.CanResize);
        Assert.False(plain.CanResize);

        // Resizable is not maximizable: the file dialog stretches without gaining a maximize box.
        Assert.False(dialog.CanMaximize);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Keeps_Every_Label_Clear_Of_The_Control_It_Names()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFileDialog();
        dialog.SetFileTypeFilters(
            [
                new UiFileDialogFilter("All documents", "*.rtf;*.md", ".rtf"),
                new UiFileDialogFilter("Markdown", "*.md", ".md"),
            ]);

        session.AddRoot(dialog);
        session.RenderFrame();

        BRect fileTypeLabel = LabelBounds(session, "File type");
        BRect fileNameLabel = LabelBounds(session, "File name");

        Assert.True(fileNameLabel.Bottom <= dialog.FileNameEdit.Bounds.Top,
            $"File name label {fileNameLabel} overlaps the name box {dialog.FileNameEdit.Bounds}.");
        Assert.True(fileTypeLabel.Bottom <= dialog.FileTypeComboBox.Bounds.Top,
            $"File type label {fileTypeLabel} overlaps the file type box {dialog.FileTypeComboBox.Bounds}.");
        Assert.True(dialog.FileNameEdit.Bounds.Bottom <= fileTypeLabel.Top,
            $"Name box {dialog.FileNameEdit.Bounds} overlaps the file type label {fileTypeLabel}.");

        // The sort box shares the files header row, so the two may not run into each other either.
        Assert.True(dialog.SortComboBox.Bounds.Right <= dialog.FilesList.Bounds.Right);
        Assert.True(dialog.SortComboBox.Bounds.Bottom <= dialog.FilesList.Bounds.Top);
    }

    /// <summary>
    /// The bounds a drawn label was clipped to. The dialog paints its captions rather than
    /// hosting label controls, so this is the only place their geometry shows up.
    /// </summary>
    private static BRect LabelBounds(UiSession session, string text)
    {
        BRenderList renderList = session.RenderFrame();
        BRect clip = BRect.Empty;
        foreach (BRenderCommand command in renderList.Commands)
        {
            if (command is BRenderCommand.PushClip push)
                clip = push.Rect;
            else if (command is BRenderCommand.DrawText draw && draw.Text.Text == text)
                return clip;
        }

        Assert.Fail($"The dialog drew no '{text}' label.");
        return BRect.Empty;
    }

    private static string[] FileNames(StandardFileDialog dialog) =>
        dialog.FilesList.Items.Select(static item => item.Text).ToArray();

    private static int IndexOfSortItem(StandardFileDialog dialog, string text)
    {
        for (int index = 0; index < dialog.SortComboBox.Items.Count; index++)
        {
            if (dialog.SortComboBox.Items[index].Text == text)
                return index;
        }

        Assert.Fail($"The sort box offers no '{text}'.");
        return -1;
    }

    private static void WriteFile(string directory, string name, int length, DateTime lastWriteUtc)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }

    [Fact(Timeout = 600000)]
    public void Standard_File_Dialog_Shows_Places_And_Descriptive_Chrome()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFileDialog();

        Assert.NotEmpty(dialog.PlacesList.Items);

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            Assert.Contains(dialog.PlacesList.Items, item => item.Text == "Home");

        session.AddRoot(dialog);
        BRenderList renderList = session.RenderFrame();

        renderList.Validate();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Places");
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text.StartsWith("Choose a document", StringComparison.Ordinal));
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "File name");
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(640, 360);

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
