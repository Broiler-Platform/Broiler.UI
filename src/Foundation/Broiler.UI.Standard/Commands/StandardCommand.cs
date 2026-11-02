using System;

namespace Broiler.UI.Standard;

public sealed class StandardCommand : IUiCommand
{
    public StandardCommand(
        string name,
        Action execute,
        Func<bool>? canExecute = null,
        string? acceleratorText = null,
        string? tooltipText = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command names must be non-empty.", nameof(name));

        Name = name;
        ExecuteCore = execute ?? throw new ArgumentNullException(nameof(execute));
        CanExecuteCore = canExecute;
        AcceleratorText = acceleratorText;
        TooltipText = tooltipText;
    }

    public string Name { get; }

    public string Label => Name;

    public string? AcceleratorText { get; }

    public string? TooltipText { get; }

    public bool CanExecute(object? parameter = null) => CanExecuteCore?.Invoke() ?? true;

    public void Execute(object? parameter = null) => ExecuteCore();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private Action ExecuteCore { get; }

    private Func<bool>? CanExecuteCore { get; }

    public bool TryExecute()
    {
        if (!CanExecute())
            return false;

        ExecuteCore();
        return true;
    }
}
