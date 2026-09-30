namespace Broiler.UI;

/// <summary>
/// Neutral contract for elements that can receive keyboard focus and participate in tab navigation.
/// </summary>
public interface IUiFocusable
{
    /// <summary>Whether this element is eligible to receive focus in principle.</summary>
    bool Focusable { get; }

    /// <summary>Whether this element can currently receive focus (visible, attached, and enabled).</summary>
    bool CanFocus { get; }

    /// <summary>Whether this element participates in sequential Tab key navigation.</summary>
    bool IsTabStop { get; }

    /// <summary>Relative tab navigation ordering index (default 0).</summary>
    int TabIndex { get; }
}
