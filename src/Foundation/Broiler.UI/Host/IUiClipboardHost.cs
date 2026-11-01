namespace Broiler.UI;

public interface IUiClipboardHost
{
    /// <summary>
    /// Whether TryGetText is safe for a menu availability probe. Browser hosts
    /// return false: a read can request permission and replay an asynchronous paste.
    /// When false, controls defer clipboard validation until Paste is invoked.
    /// </summary>
    bool CanQueryTextSynchronously => true;

    bool TryGetText(out string text);

    void SetText(string text);
}
