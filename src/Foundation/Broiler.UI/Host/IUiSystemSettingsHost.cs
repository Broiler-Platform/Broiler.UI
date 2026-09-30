using System;

namespace Broiler.UI;

/// <summary>
/// Event arguments for changes to the host platform's system settings (theme, contrast, motion, text scale, density).
/// </summary>
public class UiSystemSettingsChangedEventArgs : EventArgs
{
    public UiSystemSettings Settings { get; }

    public UiSystemSettingsChangedEventArgs(UiSystemSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }
}

public interface IUiSystemSettingsHost
{
    UiSystemSettings Settings { get; }

    event EventHandler<UiSystemSettingsChangedEventArgs>? SettingsChanged
    {
        add { }
        remove { }
    }
}
