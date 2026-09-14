using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Broiler.UI.Dialog;

namespace Broiler.UI.AboutDialog;

/// <summary>Product information and a snapshot of component versions.</summary>
public abstract class UiAboutDialog : UiDialog
{
    private string _productName;
    private string _productVersion;
    private IReadOnlyDictionary<string, string> _componentVersions;

    protected UiAboutDialog()
    {
        Title = "About";
        Assembly product = Assembly.GetEntryAssembly() ?? typeof(UiAboutDialog).Assembly;
        _productName = GetProductName(product);
        _productVersion = GetVersion(product);
        _componentVersions = ReadComponentVersions(GetLoadedComponents());
    }

    /// <summary>The application name, initially read from the entry assembly.</summary>
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

    protected virtual void OnContentChanged() { }

    private void NotifyContentChanged()
    {
        OnContentChanged();
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private static IEnumerable<Assembly> GetLoadedComponents()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && assembly.GetName().Name is string name &&
                (name == "Broiler" || name.StartsWith("Broiler.", StringComparison.Ordinal)))
                yield return assembly;
        }
    }

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

    private static string GetProductName(Assembly assembly)
    {
        string? name = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        return string.IsNullOrWhiteSpace(name) ? assembly.GetName().Name ?? "Application" : name;
    }

    // Preserve release labels but omit build metadata, commonly a long SDK-generated commit hash.
    private static string GetVersion(Assembly assembly)
    {
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(version))
            return version.Split('+')[0];
        version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        return !string.IsNullOrWhiteSpace(version) ? version : assembly.GetName().Version?.ToString() ?? "Unknown";
    }
}
