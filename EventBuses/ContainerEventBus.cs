using System;
using SquareClickerPointer.EventArgs;

namespace SquareClickerPointer.EventBuses;

/// <summary>
/// Event bus carrying <see cref="ContainerExpandedEventArgs"/> from publishers
/// to subscribers using the standard .NET
/// <see cref="EventHandler{TEventArgs}"/> pattern.
/// </summary>
public sealed class ContainerEventBus
{
    public event EventHandler<ContainerExpandedEventArgs>? ContainerExpanded;

    public void Subscribe(EventHandler<ContainerExpandedEventArgs> handler)
        => ContainerExpanded += handler;

    public void Unsubscribe(EventHandler<ContainerExpandedEventArgs> handler)
        => ContainerExpanded -= handler;

    public void Publish(object sender, ContainerExpandedEventArgs args)
        => ContainerExpanded?.Invoke(sender, args);
}
