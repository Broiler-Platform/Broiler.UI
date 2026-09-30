using System;
using Broiler.Documents.FormatCodes;

namespace Broiler.UI.FormatCodeView;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class FormatCodeViewSelectionChangedEventArgs : EventArgs
{
    public FormatCodeViewSelectionChangedEventArgs(int anchor, int focus)
    {
        Anchor = anchor;
        Focus = focus;
    }

    public int Anchor { get; }

    public int Focus { get; }
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class FormatCodeNavigationRequestedEventArgs : EventArgs
{
    public FormatCodeNavigationRequestedEventArgs(
        FormatCodeMappedPosition mapping,
        FormatCodeToken? token)
    {
        Mapping = mapping;
        Token = token;
    }

    public FormatCodeMappedPosition Mapping { get; }

    public FormatCodeToken? Token { get; }
}

// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: constructing the arguments with a null intent stores null instead of throwing ArgumentNullException
// Broiler-Human:        PENDING
public sealed class FormatCodeEditRequestedEventArgs : EventArgs
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a null intent is stored instead of throwing ArgumentNullException
    // Broiler-Human:        PENDING
    public FormatCodeEditRequestedEventArgs(
        FormatCodeEditIntent intent,
        FormatCodeToken? token = null)
    {
        Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        Token = token;
    }

    public FormatCodeEditIntent Intent { get; }

    public FormatCodeToken? Token { get; }
}
