using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Text;

namespace Broiler.UI.Label;

// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: for the text '&&&x' DisplayText shows something other than '&x' or EffectiveAccessKey reports a key other than 'X'
// Broiler-Human:        PENDING
public abstract class UiLabel : UiElement
{
    private string _text = string.Empty;
    private BFontStyle _font = BFontStyle.Default;
    private BColor _foreground = BColor.Black;
    private UiTextWrapping _wrapping;
    private UiTextTrimming _trimming;
    private UiTextDirection _direction;
    private char? _accessKey;
    private UiElement? _target;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a change to Text that alters the displayed string raises no Measure invalidation, so the label keeps its previous desired size
    // Broiler-Human:        PENDING
    public string Text
    {
        get => _text;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_text, value))
                return;

            _text = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public string DisplayText => StripAccessMarkers(Text);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public BFontStyle Font
    {
        get => _font;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_font == value)
                return;

            _font = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public BColor Foreground
    {
        get => _foreground;
        set
        {
            ThrowIfDisposed();
            if (_foreground == value)
                return;

            _foreground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiTextWrapping Wrapping
    {
        get => _wrapping;
        set
        {
            ThrowIfDisposed();
            if (_wrapping == value)
                return;

            _wrapping = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiTextTrimming Trimming
    {
        get => _trimming;
        set
        {
            ThrowIfDisposed();
            if (_trimming == value)
                return;

            _trimming = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiTextDirection Direction
    {
        get => _direction;
        set
        {
            ThrowIfDisposed();
            if (_direction == value)
                return;

            _direction = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public char? AccessKey
    {
        get => _accessKey;
        set
        {
            ThrowIfDisposed();
            if (_accessKey == value)
                return;

            _accessKey = value;
            Invalidate(UiInvalidationKind.Semantic | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an explicit AccessKey is ignored in favour of an '&' marker found in Text
    // Broiler-Human:        PENDING
    public char? EffectiveAccessKey => AccessKey ?? FindAccessMarker(Text);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a target assigned while the label or the target is detached stays set after the two attach to different UI sessions, with no exception
    // Broiler-Human:        PENDING
    public UiElement? Target
    {
        get => _target;
        set
        {
            ThrowIfDisposed();
            if (value is not null && Session is not null && value.Session is not null && value.Session != Session)
                throw new InvalidOperationException("A label target must belong to the same UI session.");
            if (ReferenceEquals(_target, value))
                return;

            _target = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the semantic name for the text 'Save &As' is anything other than 'Save As'
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Label,
            DisplayText,
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: '&&' is not collapsed to a single '&', or a single '&' before a character remains in the returned text
    // Broiler-Human:        PENDING
    protected static string StripAccessMarkers(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains('&', StringComparison.Ordinal))
            return text;

        var characters = new List<char>(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '&')
            {
                if (index + 1 < text.Length && text[index + 1] == '&')
                {
                    characters.Add('&');
                    index++;
                }

                continue;
            }

            characters.Add(current);
        }

        return new string([.. characters]);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        state |= UiSemanticState.ReadOnly;
        return state;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: for the text '&&x&y' the returned key is anything other than 'Y'
    // Broiler-Human:        PENDING
    private static char? FindAccessMarker(string text)
    {
        for (int index = 0; index < text.Length - 1; index++)
        {
            if (text[index] == '&' && text[index + 1] != '&')
                return char.ToUpperInvariant(text[index + 1]);

            if (text[index] == '&' && text[index + 1] == '&')
                index++;
        }

        return null;
    }
}
