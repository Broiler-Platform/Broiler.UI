using System.Collections.Generic;

namespace Broiler.UI.Menu;

// Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: two separately constructed items share one Children list, so a submenu added under one appears under the other
// Broiler-Human:        PENDING
public sealed class UiMenuItem
{
    public UiMenuItem(string id, string text)
    {
        Id = id;
        Text = text;
    }

    public UiMenuItem(string id, IUiCommand command, object? commandParameter = null)
    {
        Id = id;
        Command = command ?? throw new System.ArgumentNullException(nameof(command));
        CommandParameter = commandParameter;
        Text = command.Label;
        Accelerator = command.AcceleratorText;
        IsEnabled = command.CanExecute(commandParameter);
    }

    public string Id { get; }

    public string Text { get; set; }

    public string? CommandName { get; set; }

    public IUiCommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    public string? Accelerator { get; set; }

    public char? AccessKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public void Invoke()
    {
        if (IsEnabled)
            Command?.Execute(CommandParameter);
    }

    public bool IsSeparator { get; set; }

    public bool IsCheckable { get; set; }

    public bool IsChecked { get; set; }

    public IList<UiMenuItem> Children { get; } = [];
}
