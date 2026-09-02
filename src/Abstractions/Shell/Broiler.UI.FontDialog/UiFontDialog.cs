using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.FontDialog;

public abstract class UiFontDialog : UiDialog
{
    /// <summary>
    /// The generic families the renderer resolves itself, and the well-known names to fall back on
    /// when the host will not say what it has.
    /// </summary>
    /// <remarks>
    /// The generic three are not installed fonts and never appear in a host's list, but they are
    /// what <see cref="BFontStyle.Default"/> names and what a document that came from CSS asks for,
    /// so a picker that dropped them could not show the font the caret is actually in. The named
    /// faces after them are a last resort for a host with no font source at all — a browser page —
    /// where offering something plausible beats offering nothing.
    /// </remarks>
    private static readonly string[] BuiltInFamilies =
    [
        "sans-serif",
        "serif",
        "monospace",
        "Segoe UI",
        "Arial",
        "Calibri",
        "Times New Roman",
        "Georgia",
        "Verdana",
        "Consolas",
        "Courier New",
        "Noto Sans",
        "Noto Serif",
        "DejaVu Sans",
        "DejaVu Serif",
        "Liberation Sans",
        "Liberation Serif",
    ];

    /// <summary>The generic families, kept at the head of the list ahead of the installed ones.</summary>
    private static readonly string[] GenericFamilies = ["sans-serif", "serif", "monospace"];

    private string[] _fontFamilies = ResolveHostFamilies();
    private BFontStyle _selectedFont = BFontStyle.Default;
    private string _sampleText = "The quick brown fox jumps over the lazy dog";
    private bool _underline;
    private bool _strikethrough;

    protected UiFontDialog()
    {
        // A font list is as long as the host's font set, and a preview is worth more the more of
        // the sample it can show. Both are reasons to let this one be stretched, which is the
        // exception UiDialog's fixed-size default leaves room for.
        CanResize = true;
    }

    public event EventHandler? SelectedFontChanged;

    /// <summary>Raised when <see cref="Underline"/> or <see cref="Strikethrough"/> changes.</summary>
    public event EventHandler? DecorationsChanged;

    public IReadOnlyList<string> FontFamilies => _fontFamilies;

    public BFontStyle SelectedFont
    {
        get => _selectedFont;
        set
        {
            ThrowIfDisposed();
            BFontStyle normalized = NormalizeFont(value);
            if (_selectedFont == normalized)
                return;

            _selectedFont = normalized;
            bool familiesChanged = EnsureSelectedFamilyIsListed();
            if (familiesChanged)
                OnFontFamiliesChanged();
            OnSelectedFontChanged();
            SelectedFontChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public string SampleText
    {
        get => _sampleText;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_sampleText, value))
                return;

            _sampleText = value;
            OnSampleTextChanged();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>Whether the chosen text is underlined.</summary>
    /// <remarks>
    /// Underline and strike-through are not part of <see cref="BFontStyle"/>: a font has a family,
    /// a size, a weight and a slant, while a rule drawn under or through a run is decoration the
    /// renderer adds afterwards. They live beside the font here for the same reason they sit beside
    /// it in a document's inline style — the user thinks of all six as "how this text looks", and a
    /// font dialog that could not turn on an underline would send them to a second one.
    /// </remarks>
    public bool Underline
    {
        get => _underline;
        set
        {
            ThrowIfDisposed();
            if (_underline == value)
                return;

            _underline = value;
            RaiseDecorationsChanged();
        }
    }

    /// <summary>Whether the chosen text has a line drawn through it.</summary>
    public bool Strikethrough
    {
        get => _strikethrough;
        set
        {
            ThrowIfDisposed();
            if (_strikethrough == value)
                return;

            _strikethrough = value;
            RaiseDecorationsChanged();
        }
    }

    public void SetFontFamilies(IEnumerable<string>? families)
    {
        ThrowIfDisposed();
        string[] normalized = NormalizeFamilies(families).ToArray();
        if (normalized.Length == 0)
            normalized = BuiltInFamilies;

        _fontFamilies = normalized;
        EnsureSelectedFamilyIsListed();
        OnFontFamiliesChanged();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool AcceptSelection() => Accept(FormatFontValue(SelectedFont, Underline, Strikethrough));

    public Task<UiDialogResult> ShowFontModal(UiWindow owner, BRect placement = default) =>
        ShowModal(owner, placement);

    public Task<UiDialogResult> ShowFontModeless(UiWindow owner, BRect placement = default) =>
        ShowModeless(owner, placement);

    public static string FormatFontValue(BFontStyle font) => FormatFontValue(font, underline: false, strikethrough: false);

    /// <summary>
    /// The dialog's result value: the font, then the two decorations. The decorations are appended
    /// rather than woven in, so a value written before they existed still parses.
    /// </summary>
    public static string FormatFontValue(BFontStyle font, bool underline, bool strikethrough)
    {
        font = NormalizeFont(font);
        return string.Join(
            "|",
            Escape(font.FamilyName),
            font.SizeInPixels.ToString("0.###", CultureInfo.InvariantCulture),
            ((int)font.Weight).ToString(CultureInfo.InvariantCulture),
            font.Slant.ToString(),
            underline ? "underline" : "none",
            strikethrough ? "strike" : "none");
    }

    public static bool TryParseFontValue(string? value, out BFontStyle font) =>
        TryParseFontValue(value, out font, out _, out _);

    public static bool TryParseFontValue(string? value, out BFontStyle font, out bool underline, out bool strikethrough)
    {
        font = BFontStyle.Default;
        underline = false;
        strikethrough = false;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] parts = SplitEscaped(value).ToArray();
        if (parts.Length is not (4 or 6) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double size) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) ||
            !Enum.TryParse(parts[3], ignoreCase: true, out BFontSlant slant))
        {
            return false;
        }

        if (parts.Length == 6)
        {
            underline = StringComparer.OrdinalIgnoreCase.Equals(parts[4], "underline");
            strikethrough = StringComparer.OrdinalIgnoreCase.Equals(parts[5], "strike");
        }

        font = NormalizeFont(new BFontStyle(Unescape(parts[0]), size, (BFontWeight)weight, slant));
        return true;
    }

    protected virtual void OnFontFamiliesChanged()
    {
    }

    protected virtual void OnSelectedFontChanged()
    {
    }

    protected virtual void OnSampleTextChanged()
    {
    }

    protected virtual void OnDecorationsChanged()
    {
    }

    private void RaiseDecorationsChanged()
    {
        OnDecorationsChanged();
        DecorationsChanged?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>
    /// The families the dialog opens with: the generic three, then whatever the host reports
    /// installed. A host with no font source — a browser page, a machine whose font directories are
    /// unreadable — leaves <see cref="BSystemFonts"/> empty, and the built-in names stand in.
    /// </summary>
    private static string[] ResolveHostFamilies()
    {
        IReadOnlyList<string> installed = BSystemFonts.GetFamilies();
        return installed.Count == 0
            ? BuiltInFamilies
            : NormalizeFamilies(GenericFamilies.Concat(installed)).ToArray();
    }

    private bool EnsureSelectedFamilyIsListed()
    {
        if (_fontFamilies.Any(family => string.Equals(family, _selectedFont.FamilyName, StringComparison.OrdinalIgnoreCase)))
            return false;

        _fontFamilies = NormalizeFamilies(_fontFamilies.Append(_selectedFont.FamilyName)).ToArray();
        return true;
    }

    private static BFontStyle NormalizeFont(BFontStyle? font)
    {
        font ??= BFontStyle.Default;
        string family = string.IsNullOrWhiteSpace(font.FamilyName)
            ? BFontStyle.Default.FamilyName
            : font.FamilyName.Trim();
        double size = double.IsNaN(font.SizeInPixels) || double.IsInfinity(font.SizeInPixels) || font.SizeInPixels <= 0
            ? BFontStyle.Default.SizeInPixels
            : Math.Clamp(font.SizeInPixels, 1.0, 512.0);
        BFontSlant slant = Enum.IsDefined(font.Slant) ? font.Slant : BFontSlant.Normal;
        return new BFontStyle(family, size, font.Weight, slant);
    }

    private static IEnumerable<string> NormalizeFamilies(IEnumerable<string>? families)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? family in families ?? BuiltInFamilies)
        {
            string normalized = family?.Trim() ?? string.Empty;
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;

            yield return normalized;
        }
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);

    private static string Unescape(string value)
    {
        if (!value.Contains('\\', StringComparison.Ordinal))
            return value;

        var chars = new List<char>(value.Length);
        bool escaping = false;
        foreach (char character in value)
        {
            if (escaping)
            {
                chars.Add(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            chars.Add(character);
        }

        if (escaping)
            chars.Add('\\');
        return new string([.. chars]);
    }

    private static IEnumerable<string> SplitEscaped(string value)
    {
        var chars = new List<char>(value.Length);
        bool escaping = false;
        foreach (char character in value)
        {
            if (escaping)
            {
                chars.Add('\\');
                chars.Add(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            if (character == '|')
            {
                yield return new string([.. chars]);
                chars.Clear();
                continue;
            }

            chars.Add(character);
        }

        if (escaping)
            chars.Add('\\');
        yield return new string([.. chars]);
    }
}
