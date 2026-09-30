using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.FontDialog;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=7; Fingerprint=TBF
// Broiler-Falsified-If: a font assigned to SelectedFont or read back by TryParseFontValue keeps a size outside 1 to 512 or a weight outside 1 to 1000
// Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: sans-serif, serif and monospace are not the first three entries, so a host with no font source hides the generic families
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an entry is not one of the three generic family names at the head of BuiltInFamilies
    // Broiler-Human:        PENDING
    private static readonly string[] GenericFamilies = ["sans-serif", "serif", "monospace"];

    private string[] _fontFamilies = ResolveHostFamilies();
    private BFontStyle _selectedFont = BFontStyle.Default;
    private string _sampleText = "The quick brown fox jumps over the lazy dog";
    private bool _underline;
    private bool _strikethrough;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=7; Fingerprint=TBF
    // Broiler-Falsified-If: a newly constructed dialog reports CanResize false or an empty FontFamilies list
    // Broiler-Human:        PENDING
    protected UiFontDialog()
    {
        // A font list is as long as the host's font set, and a preview is worth more the more of
        // the sample it can show. Both are reasons to let this one be stretched, which is the
        // exception UiDialog's fixed-size default leaves room for.
        CanResize = true;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? SelectedFontChanged;

    /// <summary>Raised when <see cref="Underline"/> or <see cref="Strikethrough"/> changes.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? DecorationsChanged;

    public IReadOnlyList<string> FontFamilies => _fontFamilies;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: assigning a font whose family is missing from FontFamilies leaves that family out of FontFamilies after the set returns
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null makes SampleText return null instead of an empty string
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting Underline to the value it already has raises DecorationsChanged
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting Strikethrough to the value it already has raises DecorationsChanged
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an empty or all-blank family list leaves FontFamilies empty instead of the built-in names
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the value handed to Accept drops the Underline or Strikethrough state the dialog currently holds
    // Broiler-Human:        PENDING
    public bool AcceptSelection() => Accept(FormatFontValue(SelectedFont, Underline, Strikethrough));

    public Task<UiDialogResult> ShowFontModal(UiWindow owner, BRect placement = default) =>
        ShowModal(owner, placement);

    public Task<UiDialogResult> ShowFontModeless(UiWindow owner, BRect placement = default) =>
        ShowModeless(owner, placement);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the value written for a font differs from the six-field value the three-argument overload writes for it with both decorations off
    // Broiler-Human:        PENDING
    public static string FormatFontValue(BFontStyle font) => FormatFontValue(font, underline: false, strikethrough: false);

    /// <summary>
    /// The dialog's result value: the font, then the two decorations. The decorations are appended
    /// rather than woven in, so a value written before they existed still parses.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a family name containing '|' or '\' is written so that TryParseFontValue reads back a different family name
    // Broiler-Human:        PENDING
    public static string FormatFontValue(BFontStyle font, bool underline, bool strikethrough)
    {
        font = NormalizeFont(font);
        return string.Join(
            "|",
            Escape(font.FamilyName),
            font.Size.ToString("0.###", CultureInfo.InvariantCulture),
            ((int)font.Weight).ToString(CultureInfo.InvariantCulture),
            font.Slant.ToString(),
            underline ? "underline" : "none",
            strikethrough ? "strike" : "none");
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a six-field value carrying underline and strike is rejected here although the four-out overload accepts it
    // Broiler-Human:        PENDING
    public static bool TryParseFontValue(string? value, out BFontStyle font) =>
        TryParseFontValue(value, out font, out _, out _);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a value with five '|'-separated fields, or with a size field that is not a number, is reported as parsed
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnFontFamiliesChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnSelectedFontChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnSampleTextChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnDecorationsChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: DecorationsChanged reaches its handlers before OnDecorationsChanged has run
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=7; Fingerprint=TBF
    // Broiler-Falsified-If: a host for which BSystemFonts reports no families gets an empty list instead of BuiltInFamilies
    // Broiler-Human:        PENDING
    private static string[] ResolveHostFamilies()
    {
        IReadOnlyList<string> installed = BSystemFonts.GetFamilies();
        return installed.Count == 0
            ? BuiltInFamilies
            : NormalizeFamilies(GenericFamilies.Concat(installed)).ToArray();
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a selected family that differs from a listed one only in letter case is appended a second time
    // Broiler-Human:        PENDING
    private bool EnsureSelectedFamilyIsListed()
    {
        if (_fontFamilies.Any(family => string.Equals(family, _selectedFont.FamilyName, StringComparison.OrdinalIgnoreCase)))
            return false;

        _fontFamilies = NormalizeFamilies(_fontFamilies.Append(_selectedFont.FamilyName)).ToArray();
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a font with weight 0, -5 or 5000 comes out of NormalizeFont with that weight unchanged
    // Broiler-Human:        PENDING
    private static BFontStyle NormalizeFont(BFontStyle? font)
    {
        font ??= BFontStyle.Default;
        string family = string.IsNullOrWhiteSpace(font.FamilyName)
            ? BFontStyle.Default.FamilyName
            : font.FamilyName.Trim();
        double size = double.IsNaN(font.Size) || double.IsInfinity(font.Size) || font.Size <= 0
            ? BFontStyle.Default.Size
            : Math.Clamp(font.Size, 1.0, 512.0);
        BFontSlant slant = Enum.IsDefined(font.Slant) ? font.Slant : BFontSlant.Normal;
        return new BFontStyle(family, size, font.Weight, slant);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a list holding ' Arial ' and 'arial' yields two entries instead of one
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a '|' in the input comes out without a backslash before it, so SplitEscaped cuts the family at that point
    // Broiler-Human:        PENDING
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an escaped pair such as \| or \\ in the family field is not reduced to the single character after the backslash
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an escaped '|' inside the family field splits the value into an extra field
    // Broiler-Human:        PENDING
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
