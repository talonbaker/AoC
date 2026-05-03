namespace SquareClickerPointer.Models;

// ═══════════════════════════════════════════════════════════════════════════════
//  ShapeType  —  the set of shapes that can be assigned to a list item
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY an enum (not a string)?
//  ────────────────────────────
//  A string like "Star" compiles whether it is spelled right or not.  ShapeType.Typo
//  is a build error — caught in seconds, not discovered at 2 a.m. when a switch
//  statement falls through to default.  Enums are the compiler enforcing your contract.
//
//  WHY is this in Models/ and not in ViewModels/?
//  ────────────────────────────────────────────────
//  This enum describes DOMAIN DATA — what category/type an item belongs to.
//  The Model layer owns the definition; the ViewModel only reads and exposes it.
//  If you need to test ListItemViewModel in isolation, you import just Models.ShapeType
//  without pulling in any UI, ViewModel, or DI assemblies.

/// <summary>
/// Identifies which geometric shape is rendered alongside a list item.
/// Determines both the icon drawn in the row and which filter bucket it belongs to.
/// </summary>
public enum ShapeType
{
    Star,
    Hexagon,
    Triangle,
    Square
}
