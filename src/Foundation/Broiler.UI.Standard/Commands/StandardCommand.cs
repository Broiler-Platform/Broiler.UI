using System;

namespace Broiler.UI.Standard;

public sealed class StandardCommand : IUiCommand
{
    public StandardCommand(string name, Action execute, Func<bool>? canExecute = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command names must be non-empty.", nameof(name));

        Name = name;
        ExecuteCore = execute ?? throw new ArgumentNullException(nameof(execute));
        CanExecuteCore = canExecute;
    }

    public string Name { get; }

    public string Label => Name;

    public string? AcceleratorText => null;

    public string? TooltipText => null;

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

