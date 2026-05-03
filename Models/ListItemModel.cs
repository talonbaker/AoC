using Avalonia.Media;

namespace SquareClickerPointer.Models;

// ═══════════════════════════════════════════════════════════════════════════════
//  ListItemModel  —  raw data record for one row in the expandable list
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY a record?
//  ──────────────
//  This is an immutable snapshot delivered from the outside world.  Once constructed
//  by the caller (e.g. App startup or a data-import layer) nothing should change its
//  fields — immutability prevents "spooky action at a distance" where one piece of
//  code mutates the model and other consumers silently see stale data.
//
//  C# records give you: init-only properties, structural equality, deconstruct,
//  and a compact primary-constructor syntax — all for free.
//
//  WHY does the Model use Avalonia.Media.Color (a UI namespace type)?
//  ───────────────────────────────────────────────────────────────────
//  Avalonia.Media.Color is a plain value struct (four bytes: A, R, G, B).
//  It carries no UI dependencies — no lifetime, no thread affinity, no XAML.
//  Using it here lets callers specify colours naturally (Colors.Red, Color.FromRgb)
//  without forcing them to pre-build a SolidColorBrush.
//  The ViewModel converts the raw Color to an IBrush for data-binding.
//
//  EXTERNAL POPULATION
//  ─────────────────────
//  This type is the "input format" for external code that wants to populate a
//  container.  The caller builds an array of ListItemModel records and passes them
//  to ExpandableContainerView.ViewModel.SetItems().  The container has no hardcoded
//  content — it is a reusable, data-agnostic shell.

/// <summary>
/// Immutable data snapshot for one row in an ExpandableContainerView list.
/// Build these in your startup or data-import code and hand them to the container.
/// </summary>
/// <param name="Id">Unique integer shown to the right of the item name.</param>
/// <param name="Name">Display label shown in the middle of the row.</param>
/// <param name="Shape">Determines which geometric icon is rendered.</param>
/// <param name="ItemColor">
/// Used for both the shape icon fill and the matching status square on the right.
/// </param>
/// <param name="IsLocked">Initial lock state; the user can toggle it at runtime.</param>
public record ListItemModel(
    int      Id,
    string   Name,
    ShapeType Shape,
    Color    ItemColor,
    bool     IsLocked = false
);
