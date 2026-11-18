using System;
using System.Globalization;
using System.Linq;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Paging;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.Dialog.Standard;

/// <summary>Page geometry in points, including the distances from the sheet edges to running content.</summary>
public sealed record PageSetupOptions(BSize PaperSize, BPageMargins Margins, double HeaderDistance,
    double FooterDistance, int ColumnCount, double ColumnGap);

/// <summary>Edits and validates page geometry for a document or section.</summary>
public static class StandardPageSetupDialog
{
    /// <param name="apply">Applies the geometry or returns a message to display in the dialog.</param>
    public static void Show(UiWindow owner, BSize viewport, string title, PageSetupOptions initial,
        Func<PageSetupOptions, string?> apply, bool canClose = true)
    {
        var dialog = new StandardDialog { Title = title, PreferredSize = new BSize(560, 445), CanClose = canClose };
        var message = new StandardLabel { Text = "Dimensions and margins use points." };
        var size = Input("Paper width and height", Points(initial.PaperSize.Width, initial.PaperSize.Height));
        var orientation = Input("Orientation (portrait or landscape)",
            initial.PaperSize.Width > initial.PaperSize.Height ? "landscape" : "portrait");
        var margins = Input("Margins (left, right, top, bottom)",
            Points(initial.Margins.Left, initial.Margins.Right, initial.Margins.Top, initial.Margins.Bottom));
        var distances = Input("Header and footer distances from the edge", Points(initial.HeaderDistance, initial.FooterDistance));
        var columns = Input("Columns and gap", Points(initial.ColumnCount, initial.ColumnGap));

        var applyButton = new StandardButton { Text = "Apply" };
        var cancelButton = new StandardButton { Text = "Cancel" };
        UiElement[] rows = [message, new StandardLabel { Text = size.AccessibleName }, size,
            new StandardLabel { Text = orientation.AccessibleName }, orientation,
            new StandardLabel { Text = margins.AccessibleName }, margins,
            new StandardLabel { Text = distances.AccessibleName }, distances,
            new StandardLabel { Text = columns.AccessibleName }, columns];
        applyButton.Clicked += (_, _) =>
        {
            if (!TryRead(size.Text, orientation.Text, margins.Text, distances.Text, columns.Text, out var options, out var error))
            {
                message.Text = error;
                return;
            }
            string? applyError = apply(options);
            if (applyError is null)
                dialog.Accept();
            else
                message.Text = applyError;
        };
        cancelButton.Clicked += (_, _) => dialog.Cancel();
        dialog.AddChild(new StandardDialogFields(rows, [applyButton, cancelButton]));
        dialog.ShowModal(owner, StandardDialogPlacement.Center(viewport, dialog.PreferredSize));
        size.Session?.SetFocus(size);
    }

    private static StandardEdit Input(string label, string value) => new() { AccessibleName = label, Text = value };

    private static bool TryRead(string paperText, string orientationText, string marginText, string distanceText,
        string columnText, out PageSetupOptions options, out string error)
    {
        options = null!;
        error = "Check the paper size, margins, distances and columns.";
        if (!Numbers(paperText, 2, out var paper) || paper.Any(value => value < 36) ||
            !Numbers(marginText, 4, out var margins) || !Numbers(distanceText, 2, out var distances) ||
            !Numbers(columnText, 2, out var columns) || columns[0] is < 1 or > 16 || columns[0] != Math.Truncate(columns[0]))
            return false;

        string orientation = orientationText.Trim().ToLowerInvariant();
        error = "Choose portrait or landscape.";
        if (orientation is not ("portrait" or "landscape"))
            return false;

        bool landscape = orientation == "landscape";
        double width = landscape ? Math.Max(paper[0], paper[1]) : Math.Min(paper[0], paper[1]);
        double height = landscape ? Math.Min(paper[0], paper[1]) : Math.Max(paper[0], paper[1]);
        int count = (int)columns[0];
        double gap = columns[1];
        error = "Leave room for the text, columns, and running content.";
        if (margins[0] + margins[1] + gap * (count - 1) >= width || margins[2] + margins[3] >= height ||
            distances[0] > margins[2] || distances[1] > margins[3])
            return false;

        options = new PageSetupOptions(new BSize(width, height),
            new BPageMargins(margins[0], margins[2], margins[1], margins[3]), distances[0], distances[1], count, gap);
        return true;
    }

    private static string Points(params double[] numbers) =>
        string.Join(", ", numbers.Select(number => number.ToString("0.###", CultureInfo.InvariantCulture)));

    private static bool Numbers(string text, int count, out double[] values)
    {
        string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
        values = new double[count];
        if (parts.Length != count)
            return false;
        for (int index = 0; index < count; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]) ||
                !double.IsFinite(values[index]) || values[index] is < 0 or > 20000)
                return false;
        }
        return true;
    }
}
