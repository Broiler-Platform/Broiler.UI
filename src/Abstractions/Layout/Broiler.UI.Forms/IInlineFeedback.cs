namespace Broiler.UI.Forms;

/// <summary>
/// Defines the neutral contract for inline form status and feedback presentation.
/// </summary>
public interface IInlineFeedback
{
    FeedbackKind Kind { get; }
    void Set(string? message, FeedbackKind kind = FeedbackKind.Information);
}
