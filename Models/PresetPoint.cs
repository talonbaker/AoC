namespace SquareClickerPointer.Models;

/// <summary>
/// An immutable preset position that the three action buttons can snap to.
/// </summary>
public record PresetPoint(double X, double Y, string Label)
{
    /// <summary>Human-readable coordinate summary shown in button tooltips.</summary>
    public string Description => $"X: {X:F1}  Y: {Y:F1}";
}
