namespace SquareClickerPointer.EventArgs;

/// <summary>
/// Event payload for the dot-released event.  Carries the X/Y coordinates the
/// user committed in the 0–10 domain coordinate space.
/// </summary>
public sealed class DotReleasedEventArgs : System.EventArgs
{
    public double X { get; }
    public double Y { get; }

    public DotReleasedEventArgs(double x, double y)
    {
        X = x;
        Y = y;
    }
}
