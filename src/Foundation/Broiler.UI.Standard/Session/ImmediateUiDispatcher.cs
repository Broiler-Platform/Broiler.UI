using System;

namespace Broiler.UI.Standard;

/// <summary>
/// A dispatcher that runs a posted callback at once, on the thread that posted it.
/// </summary>
/// <remarks>
/// Right for a host that never leaves its UI thread, and for tests. It does not
/// marshal: a callback posted from another thread runs on that thread and would
/// change the element tree from it. A host whose work can finish on another
/// thread uses <see cref="StandardQueuedUiDispatcher"/> instead.
/// </remarks>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        callback();
    }
}
