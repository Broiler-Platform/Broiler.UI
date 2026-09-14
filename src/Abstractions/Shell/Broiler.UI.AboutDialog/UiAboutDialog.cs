using System;
using System.Collections.Generic;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.AboutDialog;

/// <summary>
/// Abstract base for an About dialog. Shows product information and component versions.
/// </summary>
public abstract class UiAboutDialog : UiDialog
{
    /// <summary>
    /// The name of the product/application.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// The version string of the product/application.
    /// </summary>
    public string ProductVersion { get; set; } = string.Empty;

    /// <summary>
    /// Mapping of component name to its version. May be empty.
    /// </summary>
    public IReadOnlyDictionary<string, string> ComponentVersions { get; set; }
        = (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

    protected UiAboutDialog()
    {
        Title = "About";
        CanResize = false;
    }
}
