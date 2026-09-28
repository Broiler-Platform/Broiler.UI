using System.Collections.Concurrent;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The queued dispatcher: posts from any thread wait for the owner thread to
/// drain them, a drain always ends, and the host is woken once per batch of work.
/// </summary>
public sealed class StandardQueuedUiDispatcherTests
{
    [Fact]
    public void A_Post_Waits_For_The_Owner_To_Drain_It_And_Runs_In_Order()
    {
        var dispatcher = new StandardQueuedUiDispatcher();
        var ran = new List<int>();

        dispatcher.Post(() => ran.Add(1));
        dispatcher.Post(() => ran.Add(2));

        // Not even on the owner thread does posting run anything.
        Assert.Empty(ran);
        Assert.True(dispatcher.HasPendingWork);

        Assert.Equal(2, dispatcher.Drain());
        Assert.Equal([1, 2], ran);
        Assert.False(dispatcher.HasPendingWork);
        Assert.Equal(0, dispatcher.Drain());
    }

    [Fact]
    public void Only_The_Owner_Thread_Has_Access_Or_May_Drain()
    {
        var dispatcher = new StandardQueuedUiDispatcher();
        bool otherHasAccess = true;
        Exception? otherDrain = null;

        // A thread of its own is certain not to be this one; a thread-pool task
        // is not, when the test itself runs on the thread pool.
        var other = new Thread(() =>
        {
            otherHasAccess = dispatcher.CheckAccess();
            otherDrain = Record.Exception(() => dispatcher.Drain());
        });
        other.Start();
        other.Join();

        Assert.True(dispatcher.CheckAccess());
        Assert.False(otherHasAccess);
        Assert.IsType<InvalidOperationException>(otherDrain);
    }

    [Fact]
    public void Work_Posted_From_Another_Thread_Runs_On_The_Owner_Thread()
    {
        int owner = Environment.CurrentManagedThreadId;
        var dispatcher = new StandardQueuedUiDispatcher();
        int ranOn = -1;

        // A thread of its own rather than an awaited task: the test has to stay
        // on the owner thread to drain, and an await need not come back to it.
        var poster = new Thread(() => dispatcher.Post(() => ranOn = Environment.CurrentManagedThreadId));
        poster.Start();
        poster.Join();
        Assert.Equal(-1, ranOn);

        dispatcher.Drain();
        Assert.Equal(owner, ranOn);
    }

    [Fact]
    public void The_Host_Is_Woken_Once_Per_Batch_Of_Work()
    {
        int wakes = 0;
        var dispatcher = new StandardQueuedUiDispatcher(() => wakes++);

        dispatcher.Post(() => { });
        dispatcher.Post(() => { });
        dispatcher.Post(() => { });
        Assert.Equal(1, wakes);

        dispatcher.Drain();
        dispatcher.Post(() => { });
        Assert.Equal(2, wakes);

        // A drain with nothing left behind asks for nothing.
        dispatcher.Drain();
        Assert.Equal(2, wakes);
    }

    [Fact]
    public void A_Callback_That_Posts_Again_Runs_In_The_Next_Drain_So_A_Drain_Always_Ends()
    {
        int wakes = 0;
        var dispatcher = new StandardQueuedUiDispatcher(() => wakes++);
        int runs = 0;
        void Again()
        {
            runs++;
            dispatcher.Post(Again);
        }

        dispatcher.Post(Again);
        Assert.Equal(1, dispatcher.Drain());
        Assert.Equal(1, runs);

        // The repost asked for another drain rather than running in this one.
        Assert.Equal(2, wakes);
        Assert.Equal(1, dispatcher.Drain());
        Assert.Equal(2, runs);
    }

    [Fact]
    public void A_Drain_Started_Inside_A_Callback_Leaves_The_Queue_To_The_Outer_Drain()
    {
        var dispatcher = new StandardQueuedUiDispatcher();
        var ran = new List<string>();
        int nested = -1;

        dispatcher.Post(() =>
        {
            ran.Add("first");
            nested = dispatcher.Drain();
        });
        dispatcher.Post(() => ran.Add("second"));

        Assert.Equal(2, dispatcher.Drain());
        Assert.Equal(0, nested);
        Assert.Equal(["first", "second"], ran);
    }

    [Fact]
    public void A_Callback_That_Throws_Leaves_The_Rest_Queued_And_Asks_For_Another_Drain()
    {
        int wakes = 0;
        var dispatcher = new StandardQueuedUiDispatcher(() => wakes++);
        bool secondRan = false;

        dispatcher.Post(() => throw new InvalidOperationException("broken"));
        dispatcher.Post(() => secondRan = true);

        Assert.Throws<InvalidOperationException>(() => dispatcher.Drain());
        Assert.False(secondRan);
        Assert.True(dispatcher.HasPendingWork);
        Assert.Equal(2, wakes);

        Assert.Equal(1, dispatcher.Drain());
        Assert.True(secondRan);
    }

    [Fact]
    public void Posts_From_Many_Threads_All_Run_Once_And_In_Each_Threads_Order()
    {
        const int Threads = 8;
        const int PostsPerThread = 2_000;
        var dispatcher = new StandardQueuedUiDispatcher();
        var seen = new ConcurrentBag<(int Thread, int Index)>();
        var lastIndex = new int[Threads];
        Array.Fill(lastIndex, -1);
        bool outOfOrder = false;

        using var start = new ManualResetEventSlim();
        Task[] posters = Enumerable.Range(0, Threads).Select(thread => Task.Run(() =>
        {
            start.Wait();
            for (int index = 0; index < PostsPerThread; index++)
            {
                int captured = index;
                dispatcher.Post(() =>
                {
                    outOfOrder |= captured != lastIndex[thread] + 1;
                    lastIndex[thread] = captured;
                    seen.Add((thread, captured));
                });
            }
        })).ToArray();

        // Drained while the posters are still going, as a UI thread would, and
        // never awaited: only this thread may drain.
        start.Set();
        Task posted = Task.WhenAll(posters);
        while (!posted.IsCompleted)
            dispatcher.Drain();

        dispatcher.Drain();

        Assert.Null(posted.Exception);
        Assert.Equal(Threads * PostsPerThread, seen.Count);
        Assert.Equal(Threads * PostsPerThread, seen.Distinct().Count());
        Assert.False(outOfOrder);
    }

    [Fact]
    public void A_Null_Callback_Is_Refused()
    {
        Assert.Throws<ArgumentNullException>(() => new StandardQueuedUiDispatcher().Post(null!));
    }

    [Fact]
    public void A_Session_Can_Be_Built_On_It()
    {
        var dispatcher = new StandardQueuedUiDispatcher();

        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(dispatcher).Build(new NullHost());

        Assert.Same(dispatcher, session.Dispatcher);
        Assert.True(session.Dispatcher.CheckAccess());
    }

    private sealed class NullHost : IUiHost
    {
        public Broiler.Graphics.Geometry.BSize ViewportSize { get; } = new(100, 100);

        public double Scale => 1;

        public Broiler.Graphics.RenderList.BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(Broiler.Graphics.RenderList.BRenderList renderList)
        {
        }
    }
}
