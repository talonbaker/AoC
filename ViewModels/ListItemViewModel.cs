using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using SquareClickerPointer.Messages;
using SquareClickerPointer.Models;

namespace SquareClickerPointer.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
//  ListItemViewModel  —  state and behaviour for a single row in the list
// ═══════════════════════════════════════════════════════════════════════════════
//
//  ROLE IN THE SYSTEM
//  ──────────────────
//  Wraps one ListItemModel and exposes everything ListItemView.axaml needs:
//
//    • Static display (Id, Name) — never change; plain auto-properties.
//    • Observable shape (Shape, IsStar/IsHexagon/IsTriangle/IsSquare) — can be
//      changed by the shape picker; XAML bindings update automatically.
//    • Observable colour (ColorBrush) — can be changed by the colour picker.
//    • Observable lock state (IsLocked) — toggled by the padlock button.
//    • Picker state (IsColorPickerOpen, IsShapePickerOpen) — drives Popup.IsOpen.
//    • Palette items (ColorPaletteItems, ShapePaletteItems) — static lists of
//      swatch ViewModels pre-wired with their pick commands.
//
//  PICKER ARCHITECTURE
//  ────────────────────
//  Both pickers use the same pattern:
//
//    1. A ToggleButton in the row binds IsChecked ↔ IsColorPickerOpen (TwoWay).
//       Clicking opens the picker; clicking again (or light-dismiss) closes it.
//    2. A Popup.IsOpen also binds TwoWay to IsColorPickerOpen.
//    3. Inside the Popup, an ItemsControl renders ColorPaletteItems.
//    4. Each ColorSwatchViewModel.PickCommand calls SelectColor(c) here.
//    5. SelectColor() updates ColorBrush, sets IsColorPickerOpen = false
//       (closing the Popup via binding), and publishes ColorChangedMessage.
//
//  WHY "command per swatch" instead of a parametrised RelayCommand<Color>?
//    Passing a Color struct as CommandParameter through XAML requires boxing and
//    a converter.  Each ColorSwatchViewModel pre-captures its Color in a closure
//    so the XAML binds Command={Binding PickCommand} with zero parameters.
//    See ColorSwatchViewModel.cs for the full explanation.

/// <summary>
/// ViewModel for one row in the expandable list.
/// Exposes mutable shape, colour, and lock state; manages picker open/close state.
/// </summary>
public partial class ListItemViewModel : ViewModelBase
{
    // ── Injected / forwarded dependencies ─────────────────────────────────────

    private readonly IMessenger _messenger;
    private readonly string     _containerId;

    // ── Colour palette shared by all ListItemViewModels ───────────────────────
    //
    // Static: every item uses the same set of 12 colours.  Defined once at the
    // class level, not in each instance constructor, because the colours never
    // change and instantiating 12 SolidColorBrush objects per list item (when
    // only ONE item's picker is open at a time) would be wasteful.
    //
    // Each entry becomes one tile in the 4-column × 3-row colour picker grid.
    // The colours are chosen to match the visual palette shown in the design spec.
    private static readonly Color[] _paletteColors =
    {
        Color.FromRgb(130,  10,  30), // Dark Red / Crimson
        Color.FromRgb(  0, 175, 215), // Sky Blue
        Color.FromRgb( 50,  50, 180), // Royal Blue
        Color.FromRgb(215, 100, 215), // Orchid / Pink
        Color.FromRgb(168, 210,  20), // Chartreuse / Lime
        Color.FromRgb(162, 122,  82), // Brown / Tan
        Color.FromRgb(205,  30,  30), // Bright Red
        Color.FromRgb(235, 130,   0), // Orange
        Color.FromRgb(145,  50, 185), // Purple
        Color.FromRgb(145, 140, 200), // Lavender
        Color.FromRgb(130, 130, 130), // Gray
        Color.FromRgb(100, 225, 230), // Cyan
    };

    // ── Read-only display properties ──────────────────────────────────────────

    /// <summary>Unique identifier shown to the right of the item name.</summary>
    public int Id { get; }

    /// <summary>Display label shown in the row.</summary>
    public string Name { get; }

    // ── Observable shape ──────────────────────────────────────────────────────
    //
    // Shape is now mutable so the user can change it via the shape picker.
    //
    // [ObservableProperty] generates the public Shape property with a setter that
    // raises PropertyChanged("Shape") and also raises PropertyChanged for the
    // four computed bools listed in [NotifyPropertyChangedFor] attributes.
    //
    // WHY [NotifyPropertyChangedFor] on the bool helpers?
    //   IsStar is computed as "Shape == ShapeType.Star".  When Shape changes, we
    //   need the XAML IsVisible binding on each Path to re-evaluate.  The binding
    //   system re-evaluates when PropertyChanged("IsStar") fires.
    //   Without [NotifyPropertyChangedFor], changing Shape would update the
    //   property but the View would never know to refresh its shape Paths.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStar))]
    [NotifyPropertyChangedFor(nameof(IsHexagon))]
    [NotifyPropertyChangedFor(nameof(IsTriangle))]
    [NotifyPropertyChangedFor(nameof(IsSquare))]
    private ShapeType _shape;

    /// <summary>True when Shape is Star — drives Path visibility in the row and picker.</summary>
    public bool IsStar     => Shape == ShapeType.Star;

    /// <summary>True when Shape is Hexagon — drives Path visibility in the row and picker.</summary>
    public bool IsHexagon  => Shape == ShapeType.Hexagon;

    /// <summary>True when Shape is Triangle — drives Path visibility in the row and picker.</summary>
    public bool IsTriangle => Shape == ShapeType.Triangle;

    /// <summary>True when Shape is Square — drives Path visibility in the row and picker.</summary>
    public bool IsSquare   => Shape == ShapeType.Square;

    // ── Observable colour ─────────────────────────────────────────────────────
    //
    // ColorBrush is now mutable (changed when the user picks a colour).
    // [ObservableProperty] generates the property and raises PropertyChanged
    // automatically — XAML bindings on both the shape icon and the status square
    // update the moment ColorBrush is replaced.

    [ObservableProperty]
    private IBrush _colorBrush = null!; // initialised in constructor

    // ── Lock state ────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockIcon))]
    private bool _isLocked;

    /// <summary>Emoji glyph for the padlock button — updates when IsLocked changes.</summary>
    public string LockIcon => IsLocked ? "🔒" : "🔓";

    // ── Picker open/close state ───────────────────────────────────────────────
    //
    // These booleans drive Popup.IsOpen (TwoWay) and ToggleButton.IsChecked (TwoWay)
    // in ListItemView.axaml.  Setting one to false from code (inside SelectColor /
    // SelectShape) closes the popup via the binding — no code-behind needed.

    /// <summary>True while the colour picker popup is open.</summary>
    [ObservableProperty] private bool _isColorPickerOpen;

    /// <summary>True while the shape picker popup is open.</summary>
    [ObservableProperty] private bool _isShapePickerOpen;

    // ── Picker palette collections ────────────────────────────────────────────
    //
    // These are populated once in the constructor and never change.
    // IReadOnlyList signals to callers (and code reviewers) that this is a
    // fixed data set — not a live filtered collection like ItemListViewModel.FilteredItems.

    /// <summary>
    /// The 12 colour tiles shown in the colour picker grid.
    /// Each tile's PickCommand is pre-wired to call SelectColor() on this ViewModel.
    /// </summary>
    public IReadOnlyList<ColorSwatchViewModel> ColorPaletteItems { get; }

    /// <summary>
    /// The 4 shape tiles (Star, Hexagon, Triangle, Square) shown in the shape picker.
    /// Each tile's PickCommand is pre-wired to call SelectShape() on this ViewModel.
    /// </summary>
    public IReadOnlyList<ShapeSwatchViewModel> ShapePaletteItems { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ExpandableContainerViewModel.SetItems() for each ListItemModel.
    /// </summary>
    public ListItemViewModel(ListItemModel model, string containerId, IMessenger messenger)
    {
        Id           = model.Id;
        Name         = model.Name;
        _shape       = model.Shape;
        _colorBrush  = new SolidColorBrush(model.ItemColor);
        _isLocked    = model.IsLocked;
        _containerId = containerId;
        _messenger   = messenger;

        // Build colour palette items — one per entry in _paletteColors.
        // The lambda captures 'c' (a Color value-type copy) and 'this' (the VM).
        // When the swatch's PickCommand fires, it calls SelectColor(c) on THIS VM.
        //
        // WHY is capturing 'this' acceptable here (unlike in messenger registration)?
        //   ColorSwatchViewModel is OWNED by this ListItemViewModel — they have
        //   the same lifetime.  There is no risk of the palette items outliving
        //   this VM and holding it alive (they go out of scope together).
        //   The WeakReference concern only applies when an EXTERNAL object (the
        //   messenger) holds a reference that might prevent GC of this VM.
        ColorPaletteItems = _paletteColors
            .Select(c => new ColorSwatchViewModel(c, () => SelectColor(c)))
            .ToArray();

        // Build shape palette items — one per ShapeType enum value.
        ShapePaletteItems = Enum.GetValues<ShapeType>()
            .Select(s => new ShapeSwatchViewModel(s, () => SelectShape(s)))
            .ToArray();
    }

    // ── Picker selection handlers ─────────────────────────────────────────────
    //
    // Private methods (not RelayCommand) because they are called by the swatch
    // ViewModels via closure, not directly from XAML commands.
    //
    // Each method:
    //   1. Updates the observable property (triggers XAML refresh automatically).
    //   2. Closes its picker by setting the open-flag to false (Popup/ToggleButton
    //      bindings update via TwoWay).
    //   3. Publishes a message so any interested subscriber elsewhere in the app
    //      can react without knowing about this ViewModel.

    private void SelectColor(Color color)
    {
        ColorBrush         = new SolidColorBrush(color);
        IsColorPickerOpen  = false;
        _messenger.Send(new ColorChangedMessage(Id, _containerId, color));
    }

    private void SelectShape(ShapeType shape)
    {
        Shape              = shape;   // [NotifyPropertyChangedFor] refreshes IsStar etc.
        IsShapePickerOpen  = false;
        _messenger.Send(new ShapeChangedMessage(Id, _containerId, shape));
    }

    // ── Property-change hook — lock state ─────────────────────────────────────

    partial void OnIsLockedChanged(bool value)
    {
        _messenger.Send(new ItemLockToggledMessage(Id, _containerId, value));
    }

    // ── Search / filter helpers (called by ItemListViewModel) ─────────────────

    /// <summary>
    /// True if this item passes the given search text.
    /// Checks Id (as string) and Name case-insensitively.
    /// </summary>
    public bool MatchesSearch(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        return Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || Id.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True if this item passes the active shape filter.
    /// Null filter = all shapes shown.
    /// </summary>
    public bool MatchesFilter(ShapeType? filterShape)
        => filterShape is null || Shape == filterShape;
}
