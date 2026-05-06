using System;
using SquareClickerPointer.EventArgs;

namespace SquareClickerPointer.EventBuses;

/// <summary>
/// Event bus carrying <see cref="ColorChangedEventArgs"/> from publishers to
/// subscribers using the standard .NET <see cref="EventHandler{TEventArgs}"/>
/// pattern.
/// </summary>
public sealed class ItemColorEventBus
{
    public event EventHandler<ColorChangedEventArgs>? ColorChanged;

    public void Subscribe(EventHandler<ColorChangedEventArgs> handler)
        => ColorChanged += handler;

    public void Unsubscribe(EventHandler<ColorChangedEventArgs> handler)
        => ColorChanged -= handler;

    public void Publish(object sender, ColorChangedEventArgs args)
        => ColorChanged?.Invoke(sender, args);
}
