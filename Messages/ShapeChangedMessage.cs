using SquareClickerPointer.Models;

namespace SquareClickerPointer.Messages;

// ═══════════════════════════════════════════════════════════════════════════════
//  ShapeChangedMessage  —  published when the user picks a new shape for an item
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Same reasoning as ColorChangedMessage: the shape change is local to the row
//  but may be relevant to other parts of the app (filter recalculation, persistence,
//  etc.).  Publishing it costs nothing when nobody is subscribed.

/// <summary>
/// Published by <see cref="ViewModels.ListItemViewModel"/> when the user selects
/// a shape from the shape picker flyout.
/// </summary>
/// <param name="ItemId">ID of the item whose shape changed.</param>
/// <param name="ContainerId">ID of the owning container.</param>
/// <param name="NewShape">The newly selected shape type.</param>
public record ShapeChangedMessage(int ItemId, string ContainerId, ShapeType NewShape);
