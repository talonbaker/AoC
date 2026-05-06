using System;
using SquareClickerPointer.EventArgs;

namespace SquareClickerPointer.EventBuses;

/// <summary>
/// Event bus carrying <see cref="DotReleasedEventArgs"/> from publishers to
/// subscribers using the standard .NET <see cref="EventHandler{TEventArgs}"/>
/// pattern.  Registered as a singleton in the DI container so every consumer
/// shares the same event source.
/// </summary>
public sealed class DotPositionEventBus
{
    public event EventHandler<DotReleasedEventArgs>? DotReleased;

    public void Subscribe(EventHandler<DotReleasedEventArgs> handler)
        => DotReleased += handler;

    public void Unsubscribe(EventHandler<DotReleasedEventArgs> handler)
        => DotReleased -= handler;

    public void Publish(object sender, DotReleasedEventArgs args)
        => DotReleased?.Invoke(sender, args);
}
