using System;
using SquareClickerPointer.EventArgs;

namespace SquareClickerPointer.EventBuses;

/// <summary>
/// Event bus carrying <see cref="ShapeChangedEventArgs"/> from publishers to
/// subscribers using the standard .NET <see cref="EventHandler{TEventArgs}"/>
/// pattern.
/// </summary>
public sealed class ItemShapeEventBus
{
    public event EventHandler<ShapeChangedEventArgs>? ShapeChanged;

    public void Subscribe(EventHandler<ShapeChangedEventArgs> handler)
        => ShapeChanged += handler;

    public void Unsubscribe(EventHandler<ShapeChangedEventArgs> handler)
        => ShapeChanged -= handler;

    public void Publish(object sender, ShapeChangedEventArgs args)
        => ShapeChanged?.Invoke(sender, args);
}
