using SquareClickerPointer.Models;

namespace SquareClickerPointer.EventArgs;

/// <summary>
/// Event payload published when the user picks a new shape for a list item.
/// </summary>
public sealed class ShapeChangedEventArgs : System.EventArgs
{
    public int ItemId { get; }
    public string ContainerId { get; }
    public ShapeType NewShape { get; }

    public ShapeChangedEventArgs(int itemId, string containerId, ShapeType newShape)
    {
        ItemId = itemId;
        ContainerId = containerId;
        NewShape = newShape;
    }
}
