using Broiler.UI.Window;

namespace Broiler.UI.Dialog;

// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed record UiDialogResult(
    UiDialogResultKind Kind,
    string? Value = null,
    UiWindowCloseReason? CloseReason = null)
{
    public static UiDialogResult None { get; } = new(UiDialogResultKind.None);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Accepted(value) returns a result whose Kind is not Accepted
    // Broiler-Human:        PENDING
    public static UiDialogResult Accepted(string? value = null) => new(UiDialogResultKind.Accepted, value);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Rejected(value) returns a result whose Kind is not Rejected
    // Broiler-Human:        PENDING
    public static UiDialogResult Rejected(string? value = null) => new(UiDialogResultKind.Rejected, value);

    public static UiDialogResult Cancelled { get; } = new(UiDialogResultKind.Cancelled);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Closed(reason) returns a result whose CloseReason is not the given reason
    // Broiler-Human:        PENDING
    public static UiDialogResult Closed(UiWindowCloseReason reason) => new(UiDialogResultKind.Closed, CloseReason: reason);
}
