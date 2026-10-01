namespace Broiler.UI;

/// <summary>
/// An optional host interface that is notified when animation timers should start and stop.
/// When no animations are registered, the host can stop its timer and sleep completely idle.
/// </summary>
public interface IUiAnimationHost
{
    void StartAnimation();
    void StopAnimation();
}
