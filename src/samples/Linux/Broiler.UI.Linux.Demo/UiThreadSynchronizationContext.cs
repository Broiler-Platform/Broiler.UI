using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Broiler.UI.Standard;

namespace Broiler.UI.Linux.Demo;

/// <summary>
/// Keeps an async loop on the thread that starts it: whatever the loop awaits, it
/// resumes here rather than on the thread pool.
/// </summary>
/// <remarks>
/// <para>
/// The session's <see cref="StandardQueuedUiDispatcher"/> belongs to the thread that
/// created it, and only that thread may drain it. The render loop awaits its frame
/// timer and its input devices, and with no synchronization context each await
/// would resume on whichever pool thread finished the awaited work - still one
/// step at a time, but no longer on the dispatcher's thread. This context queues
/// every continuation back onto the starting thread, which runs them in order until
/// the loop completes.
/// </para>
/// <para>
/// The renderer does not need it: the Linux OpenGL surfaces bind their EGL context
/// around each operation, for exactly the thread-hopping this removes.
/// </para>
/// </remarks>
internal sealed class UiThreadSynchronizationContext : SynchronizationContext
{
    private readonly BlockingCollection<Action> _work;
    private readonly int _threadId = Environment.CurrentManagedThreadId;

    private UiThreadSynchronizationContext(BlockingCollection<Action> work) => _work = work;

    /// <summary>Runs <paramref name="loop"/> to completion on the calling thread.</summary>
    public static void Run(Func<Task> loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        using var work = new BlockingCollection<Action>();
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(new UiThreadSynchronizationContext(work));
        try
        {
            Task task = loop();
            _ = task.ContinueWith(
                static (_, queue) => ((BlockingCollection<Action>)queue!).CompleteAdding(),
                work,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            foreach (Action continuation in work.GetConsumingEnumerable())
                continuation();

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        try
        {
            _work.Add(() => d(state));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            // The loop has finished, so there is nothing left to resume.
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Environment.CurrentManagedThreadId != _threadId)
            throw new NotSupportedException("The loop's thread only runs work posted to it.");

        d(state);
    }

    public override SynchronizationContext CreateCopy() => this;
}
