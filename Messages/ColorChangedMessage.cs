using Avalonia.Media;

namespace SquareClickerPointer.Messages;

// ═══════════════════════════════════════════════════════════════════════════════
//  ColorChangedMessage  —  published when the user picks a new colour for an item
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY publish this?
//  ──────────────────
//  The colour square is local to the list row, but the selected colour might
//  matter elsewhere — a preview panel, a legend, a save service that tracks
//  which items changed.  Publishing here makes all of those possible without
//  touching ListItemViewModel.

/// <summary>
/// Published by <see cref="ViewModels.ListItemViewModel"/> when the user selects
/// a colour from the colour picker flyout.
/// </summary>
/// <param name="ItemId">ID of the item whose colour changed.</param>
/// <param name="ContainerId">ID of the owning container.</param>
/// <param name="NewColor">The newly selected Avalonia Color value.</param>
public record ColorChangedMessage(int ItemId, string ContainerId, Color NewColor);
