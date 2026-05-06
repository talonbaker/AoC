using System;
using SquareClickerPointer.EventArgs;

namespace SquareClickerPointer.EventBuses;

/// <summary>
/// Event bus carrying <see cref="ItemLockToggledEventArgs"/> from publishers
/// to subscribers using the standard .NET
/// <see cref="EventHandler{TEventArgs}"/> pattern.
/// </summary>
public sealed class ItemLockEventBus
{
    public event EventHandler<ItemLockToggledEventArgs>? ItemLockToggled;

    public void Subscribe(EventHandler<ItemLockToggledEventArgs> handler)
        => ItemLockToggled += handler;

    public void Unsubscribe(EventHandler<ItemLockToggledEventArgs> handler)
        => ItemLockToggled -= handler;

    public void Publish(object sender, ItemLockToggledEventArgs args)
        => ItemLockToggled?.Invoke(sender, args);
}
