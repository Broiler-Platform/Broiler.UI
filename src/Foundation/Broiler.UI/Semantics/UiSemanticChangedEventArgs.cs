using System;

namespace Broiler.UI;

/// <summary>
/// Event arguments providing incremental semantic-change notifications for host accessibility providers.
/// </summary>
public class UiSemanticChangedEventArgs : EventArgs
{
    public UiElement Element { get; }
    public UiSemanticChangeKind Change { get; }
    public long SemanticId { get; }
    public string? Message { get; }

    public UiSemanticChangedEventArgs(UiElement element, UiSemanticChangeKind change, long semanticId, string? message = null)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        Change = change;
        SemanticId = semanticId;
        Message = message;
    }
}
