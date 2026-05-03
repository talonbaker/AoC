namespace SquareClickerPointer.Messages;

// ═══════════════════════════════════════════════════════════════════════════════
//  ItemLockToggledMessage  —  published when a list item's lock state changes
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY publish this over the message bus instead of only updating local state?
//  ────────────────────────────────────────────────────────────────────────────
//  The lock icon in the list row IS local UI — the row updates itself automatically
//  via INotifyPropertyChanged.  But "is this item editable?" is a domain concern
//  that other parts of the application may also care about:
//
//   • A detail panel in another view might want to hide its input fields when
//     the selected item is locked.
//   • A toolbar might want to enable/disable a "lock all" button based on how
//     many items are currently locked.
//   • A save service might want to skip saving locked items.
//
//  By publishing the state change, all of those react without ListItemViewModel
//  ever knowing they exist.  This is the Open/Closed Principle in action:
//
//   "Open for extension (add new subscribers), closed for modification (don't
//    touch ListItemViewModel to add new reactions)."
//
//  If today nobody subscribes to this message, it costs nothing — the messenger
//  simply routes it to an empty subscriber list and moves on.  The infrastructure
//  is already there for the day someone needs it.

/// <summary>
/// Published by <see cref="ViewModels.ListItemViewModel"/> (via the
/// <c>OnIsLockedChanged</c> property-change hook) whenever the padlock toggle is
/// flipped by the user.
/// </summary>
/// <param name="ItemId">Identifier of the item whose lock state changed.</param>
/// <param name="ContainerId">
/// ID of the container that owns the item — useful when the same ItemId could
/// exist in multiple containers.
/// </param>
/// <param name="IsLocked">New state after the toggle: true = locked, false = unlocked.</param>
public record ItemLockToggledMessage(int ItemId, string ContainerId, bool IsLocked);
