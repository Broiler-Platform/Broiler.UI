using System;

namespace Broiler.UI;

/// <summary>
/// A reusable platform-neutral command abstraction that couples a label, enablement check,
/// keyboard accelerator, tooltip hint, and invocation logic.
/// </summary>
public interface IUiCommand
{
    /// <summary>Display label for menus, buttons, or toolbars.</summary>
    string Label { get; }

    /// <summary>Whether the command can currently execute with the given parameter.</summary>
    bool CanExecute(object? parameter = null);

    /// <summary>Executes the command with the given parameter.</summary>
    void Execute(object? parameter = null);

    /// <summary>Optional keyboard accelerator description (e.g. "Ctrl+S", "F5").</summary>
    string? AcceleratorText { get; }

    /// <summary>Optional tooltip or hint describing the action.</summary>
    string? TooltipText { get; }

    /// <summary>Raised when the command's execution state may have changed.</summary>
    event EventHandler? CanExecuteChanged;
}
