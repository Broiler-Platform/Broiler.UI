using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Broiler.UI.Dialog;

namespace Broiler.UI.AboutDialog;

/// <summary>Product information and a snapshot of component versions.</summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: a component version is shown with the + build metadata suffix of its informational version, or the default list names an assembly outside Broiler and Broiler.*
// Broiler-Human:        PENDING
public abstract class UiAboutDialog : UiDialog
{
    private string _productName;
    private string _productVersion;
    private IReadOnlyDictionary<string, string> _componentVersions;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: constructing the dialog in a process with no entry assembly throws instead of falling back to the product name and version of the UI assembly
    // Broiler-Human:        PENDING
    protected UiAboutDialog()
    {
        Title = "About";
        Assembly product = Assembly.GetEntryAssembly() ?? typeof(UiAboutDialog).Assembly;
        _productName = GetProductName(product);
        _productVersion = GetVersion(product);
        _componentVersions = ReadComponentVersions(GetLoadedComponents());
    }

    /// <summary>The application name, initially read from the entry assembly.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a null assignment is stored as null rather than as an empty string
    // Broiler-Human:        PENDING
    public string ProductName
    {
        get => _productName;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (_productName == value)
                return;
            _productName = value;
            NotifyContentChanged();
        }
    }

    /// <summary>The application version, initially read from the entry assembly.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning the version already shown raises OnContentChanged and a full invalidation again
    // Broiler-Human:        PENDING
    public string ProductVersion
    {
        get => _productVersion;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (_productVersion == value)
                return;
            _productVersion = value;
            NotifyContentChanged();
        }
    }

    /// <summary>
    /// Component names and versions. Defaults to loaded Broiler assemblies. Assigning takes a
    /// sorted, read-only snapshot; assign again to display changes to the source dictionary.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a later change to the assigned source dictionary shows up in ComponentVersions without a new assignment
    // Broiler-Human:        PENDING
    public IReadOnlyDictionary<string, string> ComponentVersions
    {
        get => _componentVersions;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            var snapshot = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in value)
                snapshot[component.Key] = component.Value;
            _componentVersions = new ReadOnlyDictionary<string, string>(snapshot);
            NotifyContentChanged();
        }
    }

    /// <summary>
    /// Refreshes metadata from the supplied assemblies. Defaults to the entry assembly and
    /// currently loaded Broiler components; it does not load unused dependencies. Supply an
    /// explicit component list to include third-party libraries or plugins.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an explicit component list is replaced by the loaded Broiler assemblies, or a dynamic assembly in it is listed
    // Broiler-Human:        PENDING
    public void PopulateFromAssemblies(Assembly? productAssembly = null, IEnumerable<Assembly>? componentAssemblies = null)
    {
        ThrowIfDisposed();
        Assembly product = productAssembly ?? Assembly.GetEntryAssembly() ?? typeof(UiAboutDialog).Assembly;
        var components = ReadComponentVersions(componentAssemblies ?? GetLoadedComponents());
        _productName = GetProductName(product);
        _productVersion = GetVersion(product);
        _componentVersions = components;
        NotifyContentChanged();
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnContentChanged() { }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a content change calls OnContentChanged without invalidating Measure, so a longer product name is clipped to the old size
    // Broiler-Human:        PENDING
    private void NotifyContentChanged()
    {
        OnContentChanged();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an assembly named BroilerX, or a dynamic Broiler assembly, is included in the default component list
    // Broiler-Human:        PENDING
    private static IEnumerable<Assembly> GetLoadedComponents()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && assembly.GetName().Name is string name &&
                (name == "Broiler" || name.StartsWith("Broiler.", StringComparison.Ordinal)))
                yield return assembly;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a dynamic assembly is listed, or the returned dictionary can still be changed after it is handed out
    // Broiler-Human:        PENDING
    private static IReadOnlyDictionary<string, string> ReadComponentVersions(IEnumerable<Assembly> assemblies)
    {
        var versions = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Assembly assembly in assemblies)
        {
            if (!assembly.IsDynamic && assembly.GetName().Name is string name)
                versions[name] = GetVersion(assembly);
        }
        return new ReadOnlyDictionary<string, string>(versions);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an assembly whose AssemblyProduct is blank or whitespace is shown by that blank value instead of its assembly name
    // Broiler-Human:        PENDING
    private static string GetProductName(Assembly assembly)
    {
        string? name = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        return string.IsNullOrWhiteSpace(name) ? assembly.GetName().Name ?? "Application" : name;
    }

    // Preserve release labels but omit build metadata, commonly a long SDK-generated commit hash.
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an informational version such as 1.2.3-beta+abc123 is shown with its +abc123 suffix or without its -beta label
    // Broiler-Human:        PENDING
    private static string GetVersion(Assembly assembly)
    {
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(version))
            return version.Split('+')[0];
        version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        return !string.IsNullOrWhiteSpace(version) ? version : assembly.GetName().Version?.ToString() ?? "Unknown";
    }
}
