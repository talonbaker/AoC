namespace SquareClickerPointer.EventArgs;

/// <summary>
/// Event payload broadcast when a container transitions from collapsed to
/// expanded.  Sibling containers use <see cref="ContainerId"/> to ignore
/// their own expansion event and collapse only when a different sibling opens.
/// </summary>
public sealed class ContainerExpandedEventArgs : System.EventArgs
{
    public string ContainerId { get; }

    public ContainerExpandedEventArgs(string containerId)
    {
        ContainerId = containerId;
    }
}
