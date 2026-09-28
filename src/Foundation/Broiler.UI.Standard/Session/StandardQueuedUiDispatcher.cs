using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Broiler.UI.Standard;

/// <summary>
/// A dispatcher for a host with one UI thread: a callback posted from any thread
/// waits in a queue until that thread drains it, at a point the host chooses.
/// </summary>
/// <remarks>
/// <para>
/// This is the dispatcher that work finishing on another thread can post back
/// through. <see cref="ImmediateUiDispatcher"/> runs a posted callback wherever it
/// was posted, which is right for a host that never leaves its UI thread and
/// wrong for one that does: a folder listed on the thread pool would come back
/// and change the element tree from the thread pool.
/// </para>
/// <para>
/// The thread that creates the dispatcher owns it. <see cref="CheckAccess"/> is
/// true only there, and only there does <see cref="Drain"/> run the queue.
/// Posting never runs anything, not even on the owner thread, so a callback runs
/// at a safe point of the host's choosing - between window messages, before a
/// frame is rendered - rather than inside whatever posted it (ADR 0005).
/// </para>
/// <para>
/// The host hears about new work through the wake callback, which runs on the
/// posting thread when the first callback arrives after a drain. It should make
/// <see cref="Drain"/> happen soon - post a window message, signal the render
/// loop - and must be safe to call from any thread. A host should also drain at
/// a point it reaches regularly, such as before each frame, which also covers a
/// wake it could not deliver.
/// </para>
/// </remarks>
public sealed class StandardQueuedUiDispatcher : IUiDispatcher
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly Action? _wake;
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;

    /// <summary>1 once a wake has been asked for since the last drain started.</summary>
    private int _wakeRequested;
    private bool _draining;

    /// <summary>A dispatcher owned by the calling thread.</summary>
    /// <param name="wake">
    /// Called on the posting thread when work arrives after a drain, so the host can
    /// schedule the next one. Null for a host that drains on a timer regardless.
    /// </param>
    public StandardQueuedUiDispatcher(Action? wake = null) => _wake = wake;

    /// <summary>True while callbacks are queued that no drain has run yet.</summary>
    public bool HasPendingWork => !_queue.IsEmpty;

    /// <summary>True on the thread that created the dispatcher.</summary>
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThreadId;

    /// <summary>
    /// Queues <paramref name="callback"/> to run on the owner thread at the next
    /// <see cref="Drain"/>. Safe to call from any thread.
    /// </summary>
    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _queue.Enqueue(callback);
        RequestWake();
    }

    /// <summary>
    /// Runs the callbacks that were queued when the drain started, in the order
    /// they were posted.
    /// </summary>
    /// <returns>How many callbacks ran.</returns>
    /// <remarks>
    /// A callback posted while the queue drains - including one a callback posts -
    /// waits for the next drain and asks for it, so a drain always ends. A drain
    /// started from inside a callback, as a nested message loop might start one,
    /// runs nothing and leaves the queue to the drain already under way. When a
    /// callback throws, the exception propagates, the callbacks after it stay
    /// queued, and another drain is asked for.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Called on a thread other than the owner.</exception>
    public int Drain()
    {
        if (!CheckAccess())
            throw new InvalidOperationException("A queued UI dispatcher can only be drained on the thread that created it.");

        if (_draining)
            return 0;

        _draining = true;

        // Cleared before the queue is read, and with a full fence, so a callback
        // posted from here on asks for a drain of its own instead of relying on
        // this one.
        Interlocked.Exchange(ref _wakeRequested, 0);
        int count = _queue.Count;
        int ran = 0;
        try
        {
            while (ran < count && _queue.TryDequeue(out Action? callback))
            {
                ran++;
                callback();
            }
        }
        finally
        {
            _draining = false;

            // What is left - posted during the drain, or stranded behind a callback
            // that threw - still needs a drain. Anything posted during this one has
            // already asked, so this only wakes for what was stranded.
            if (!_queue.IsEmpty)
                RequestWake();
        }

        return ran;
    }

    private void RequestWake()
    {
        if (Interlocked.Exchange(ref _wakeRequested, 1) == 0)
            _wake?.Invoke();
    }
}
