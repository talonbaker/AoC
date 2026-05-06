using Avalonia.Media;

namespace SquareClickerPointer.EventArgs;

/// <summary>
/// Event payload published when the user picks a new colour for a list item.
/// </summary>
public sealed class ColorChangedEventArgs : System.EventArgs
{
    public int ItemId { get; }
    public string ContainerId { get; }
    public Color NewColor { get; }

    public ColorChangedEventArgs(int itemId, string containerId, Color newColor)
    {
        ItemId = itemId;
        ContainerId = containerId;
        NewColor = newColor;
    }
}
