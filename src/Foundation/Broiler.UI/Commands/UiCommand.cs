using System;

namespace Broiler.UI;

/// <summary>
/// Reusable standard command implementation for binding actions to UI buttons, menus, and shortcuts.
/// </summary>
public class UiCommand : IUiCommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private string _label;
    private string? _acceleratorText;
    private string? _tooltipText;

    public string Label
    {
        get => _label;
        set => _label = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string? AcceleratorText
    {
        get => _acceleratorText;
        set => _acceleratorText = value;
    }

    public string? TooltipText
    {
        get => _tooltipText;
        set => _tooltipText = value;
    }

    public event EventHandler? CanExecuteChanged;

    public UiCommand(
        string label,
        Action<object?> execute,
        Func<object?, bool>? canExecute = null,
        string? acceleratorText = null,
        string? tooltipText = null)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _acceleratorText = acceleratorText;
        _tooltipText = tooltipText;
    }

    public UiCommand(
        string label,
        Action execute,
        Func<bool>? canExecute = null,
        string? acceleratorText = null,
        string? tooltipText = null)
        : this(label, _ => execute(), canExecute is null ? null : _ => canExecute(), acceleratorText, tooltipText)
    {
    }

    public bool CanExecute(object? parameter = null) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter = null)
    {
        if (CanExecute(parameter))
        {
            _execute(parameter);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
