using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.UI.Standard;

public sealed class StandardAnimationScheduler : IDisposable
{
    private readonly UiSession _session;
    private readonly Action<bool>? _onRunningChanged;
    private readonly List<AnimationRegistration> _registrations = [];
    private bool? _reducedMotionOverride;
    private bool _isDisposed;

    public StandardAnimationScheduler(UiSession session, Action<bool>? onRunningChanged = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _onRunningChanged = onRunningChanged;
    }

    public event EventHandler<bool>? RunningChanged;

    public bool IsDisposed => _isDisposed;

    public int Count => _registrations.Count(static registration => !registration.IsDisposed);

    public bool IsRunning => Count > 0;

    public bool ReducedMotion
    {
        get => _reducedMotionOverride ?? ResolveReducedMotion();
        set => _reducedMotionOverride = value;
    }

    public IDisposable Register(TimeSpan interval, Action<UiTimestamp> callback)
    {
        ThrowIfDisposed();
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));
        ArgumentNullException.ThrowIfNull(callback);

        bool wasRunning = IsRunning;
        var registration = new AnimationRegistration(this, interval, _session.Clock.Now, callback);
        _registrations.Add(registration);

        if (!wasRunning)
            NotifyRunningChanged(true);

        return registration;
    }

    /// <summary>
    /// Starts a timed transition over <paramref name="duration"/> that calls <paramref name="onProgress"/>
    /// with normalized progress [0.0, 1.0]. If <see cref="ReducedMotion"/> is enabled, the transition
    /// finishes synchronously on the current frame without requesting animation ticks.
    /// </summary>
    public IDisposable StartTransition(TimeSpan duration, Action<double> onProgress, Action? onCompleted = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(onProgress);

        if (ReducedMotion || duration <= TimeSpan.Zero)
        {
            onProgress(1.0);
            onCompleted?.Invoke();
            return EmptyDisposable.Instance;
        }

        UiTimestamp start = _session.Clock.Now;
        IDisposable? registrationHandle = null;
        bool completed = false;

        registrationHandle = Register(TimeSpan.FromMilliseconds(16), now =>
        {
            double elapsedMs = (now.Elapsed - start.Elapsed).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / duration.TotalMilliseconds, 0.0, 1.0);
            onProgress(progress);

            if (progress >= 1.0 && !completed)
            {
                completed = true;
                registrationHandle?.Dispose();
                onCompleted?.Invoke();
            }
        });

        return new TransitionToken(registrationHandle, () => completed);
    }

    public int Tick()
    {
        if (_isDisposed)
            return 0;

        UiTimestamp now = _session.Clock.Now;
        int invoked = 0;
        bool wasRunning = IsRunning;

        foreach (AnimationRegistration registration in _registrations.ToArray())
        {
            if (registration.IsDisposed)
                continue;

            if (now.Elapsed - registration.LastTick.Elapsed >= registration.Interval)
            {
                registration.LastTick = now;
                registration.Callback(now);
                invoked++;
            }
        }

        _registrations.RemoveAll(static registration => registration.IsDisposed);

        if (wasRunning && !IsRunning)
            NotifyRunningChanged(false);

        return invoked;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        bool wasRunning = IsRunning;

        foreach (AnimationRegistration registration in _registrations.ToArray())
            registration.IsDisposed = true;

        _registrations.Clear();

        if (wasRunning)
            NotifyRunningChanged(false);
    }

    private void Remove(AnimationRegistration registration)
    {
        if (registration.IsDisposed)
            return;

        bool wasRunning = IsRunning;
        registration.IsDisposed = true;
        _registrations.Remove(registration);

        if (wasRunning && !IsRunning)
            NotifyRunningChanged(false);
    }

    private void NotifyRunningChanged(bool running)
    {
        if (running)
            (_session.Host as IUiAnimationHost)?.StartAnimation();
        else
            (_session.Host as IUiAnimationHost)?.StopAnimation();

        _onRunningChanged?.Invoke(running);
        RunningChanged?.Invoke(this, running);
    }

    private bool ResolveReducedMotion()
    {
        if (_session.Host is IUiSystemSettingsHost settingsHost && settingsHost.Settings.ReducedMotion)
            return true;
        return StandardControlPaint.GetTheme(_session).ReducedMotion;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private sealed class AnimationRegistration : IDisposable
    {
        private readonly StandardAnimationScheduler _owner;

        public AnimationRegistration(
            StandardAnimationScheduler owner,
            TimeSpan interval,
            UiTimestamp lastTick,
            Action<UiTimestamp> callback)
        {
            _owner = owner;
            Interval = interval;
            LastTick = lastTick;
            Callback = callback;
        }

        public TimeSpan Interval { get; }

        public UiTimestamp LastTick { get; set; }

        public Action<UiTimestamp> Callback { get; }

        public bool IsDisposed { get; set; }

        public void Dispose() => _owner.Remove(this);
    }

    private sealed class TransitionToken(IDisposable? registration, Func<bool> isCompleted) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            if (!isCompleted())
                registration?.Dispose();
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }
}
