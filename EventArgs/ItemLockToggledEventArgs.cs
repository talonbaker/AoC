namespace SquareClickerPointer.EventArgs;

/// <summary>
/// Event payload published when the padlock toggle on a list item is flipped.
/// </summary>
public sealed class ItemLockToggledEventArgs : System.EventArgs
{
    public int ItemId { get; }
    public string ContainerId { get; }
    public bool IsLocked { get; }

    public ItemLockToggledEventArgs(int itemId, string containerId, bool isLocked)
    {
        ItemId = itemId;
        ContainerId = containerId;
        IsLocked = isLocked;
    }
}
