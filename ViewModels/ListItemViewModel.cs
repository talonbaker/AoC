using System;
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
//  This ViewModel wraps one ListItemModel and exposes everything the
//  ListItemView.axaml row needs:
//
//    • Read-only display values (Id, Name, Shape, ColorBrush)
//    • Four boolean shape-selector helpers (IsStar, IsHexagon, …) so the View
//      can control Path visibility without a value converter
//    • IsLocked — the only mutable piece of state; toggled by the lock button
//
//  WHY NOT put this logic directly in ListItemModel?
//  ──────────────────────────────────────────────────
//  ListItemModel is a plain data record — no UI types, no INotifyPropertyChanged,
//  no messaging.  It can be created and tested anywhere without Avalonia being
//  present.  ListItemViewModel is the bridge from that clean domain data to the
//  INotifyPropertyChanged world that XAML bindings require.
//
//  CREATION
//  ─────────
//  ListItemViewModel is NOT registered in the DI container because it requires
//  a per-instance ListItemModel — DI cannot know which model to inject.
//  Instead, ExpandableContainerViewModel creates these manually:
//
//      new ListItemViewModel(model, containerId, _messenger)
//
//  The shared IMessenger singleton flows down from the DI container through
//  ExpandableContainerViewModel into each item.  This is called "parameter
//  forwarding" — DI injects shared services at the top; leaf objects receive
//  them through constructor arguments from their parents.

/// <summary>
/// ViewModel for one row in the expandable list.
/// Created by <see cref="ExpandableContainerViewModel.SetItems"/> with the raw
/// model data.  Exposes display-ready properties and publishes lock-change events.
/// </summary>
public partial class ListItemViewModel : ViewModelBase
{
    // ── Injected dependencies ─────────────────────────────────────────────────
    //
    // IMessenger: the shared message bus — same singleton used by every other VM.
    // Stored so we can publish ItemLockToggledMessage when the padlock is toggled.
    //
    // _containerId: forwarded from the parent ExpandableContainerViewModel so the
    // message includes enough context for subscribers to identify the source.
    private readonly IMessenger _messenger;
    private readonly string     _containerId;

    // ── Read-only display properties ──────────────────────────────────────────
    //
    // These come directly from the ListItemModel record and never change at
    // runtime.  No INotifyPropertyChanged needed — plain auto-properties are fine.

    /// <summary>Unique identifier shown to the right of the item name.</summary>
    public int Id { get; }

    /// <summary>Display label shown in the row.</summary>
    public string Name { get; }

    /// <summary>Which geometric shape to render alongside the name.</summary>
    public ShapeType Shape { get; }

    /// <summary>
    /// Ready-to-bind brush for both the shape icon and the status square.
    /// Converted from ListItemModel.ItemColor (a raw Color struct) once, here,
    /// so the View never has to deal with the conversion.
    /// </summary>
    public IBrush ColorBrush { get; }

    // ── Shape selector helpers ────────────────────────────────────────────────
    //
    // The View cannot write a compiled binding like:
    //
    //     IsVisible="{Binding Shape == ShapeType.Star}"   ← NOT valid XAML
    //
    // Instead we expose one bool property per shape.  ListItemView.axaml uses:
    //
    //     <Path ... IsVisible="{Binding IsStar}"/>
    //     <Path ... IsVisible="{Binding IsHexagon}"/>
    //     ...
    //
    // These are computed once at construction; Shape never changes so they never
    // need to raise PropertyChanged.  They are just regular read-only properties.
    //
    // WHY NOT a value converter?
    //   A converter would be shared code that ListItemView has to reach out to.
    //   These properties are right here in the ViewModel, fully visible in one
    //   place, zero configuration in XAML.  Prefer simple properties over converters
    //   whenever the computation is trivial and one-directional.

    /// <summary>True when this item's shape is a star — controls Path visibility.</summary>
    public bool IsStar     => Shape == ShapeType.Star;

    /// <summary>True when this item's shape is a hexagon — controls Path visibility.</summary>
    public bool IsHexagon  => Shape == ShapeType.Hexagon;

    /// <summary>True when this item's shape is a triangle — controls Path visibility.</summary>
    public bool IsTriangle => Shape == ShapeType.Triangle;

    /// <summary>True when this item's shape is a square — controls Path visibility.</summary>
    public bool IsSquare   => Shape == ShapeType.Square;

    // ── Mutable state ─────────────────────────────────────────────────────────
    //
    // IsLocked is the only property that changes at runtime (when the user
    // clicks the padlock button).  [ObservableProperty] generates the full
    // INotifyPropertyChanged plumbing automatically — the XAML toggle button
    // binding updates the moment the value changes.
    //
    // [NotifyPropertyChangedFor(nameof(LockIcon))] tells the source generator to
    // also raise PropertyChanged("LockIcon") when IsLocked changes, so the
    // icon swap in the View updates in sync.

    /// <summary>
    /// True when the padlock is in the locked state.
    /// The View binds this to the ToggleButton.IsChecked with Mode=TwoWay —
    /// the user clicking the button directly writes back to this property.
    /// OnIsLockedChanged (below) fires automatically after each write.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockIcon))]
    private bool _isLocked;

    /// <summary>
    /// UI glyph for the lock button — changes automatically when IsLocked changes
    /// because of the [NotifyPropertyChangedFor] attribute above.
    /// </summary>
    public string LockIcon => IsLocked ? "🔒" : "🔓";

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ExpandableContainerViewModel.SetItems() for each model record.
    /// </summary>
    /// <param name="model">Immutable source data from the external caller.</param>
    /// <param name="containerId">
    /// ID of the owning container, forwarded into lock-change messages so
    /// subscribers can identify the source container without a direct reference.
    /// </param>
    /// <param name="messenger">
    /// Shared IMessenger singleton (injected into the parent and forwarded here).
    /// Used to publish ItemLockToggledMessage when the lock state changes.
    /// </param>
    public ListItemViewModel(ListItemModel model, string containerId, IMessenger messenger)
    {
        Id           = model.Id;
        Name         = model.Name;
        Shape        = model.Shape;
        ColorBrush   = new SolidColorBrush(model.ItemColor);
        _isLocked    = model.IsLocked;
        _containerId = containerId;
        _messenger   = messenger;
    }

    // ── Property-change hook — auto-called by the source generator ────────────
    //
    // CommunityToolkit.Mvvm generates a call to OnIsLockedChanged(value) inside
    // the generated IsLocked property setter, AFTER the backing field is updated
    // and PropertyChanged is raised.  We use this hook to publish the message.
    //
    // WHY a hook instead of a command?
    //   The ToggleButton binds IsChecked ↔ IsLocked with Mode=TwoWay.
    //   When the user clicks, the button writes IsLocked directly — no command
    //   is involved in that path.  The hook fires on every write regardless of
    //   who did the writing (button, code, test), making it the right place
    //   for side-effects that should always accompany the state change.

    /// <summary>
    /// Fires automatically (via source generator) whenever IsLocked changes.
    /// Publishes a message so any interested subscriber elsewhere in the app
    /// can react — without this ViewModel knowing who those subscribers are.
    /// </summary>
    partial void OnIsLockedChanged(bool value)
    {
        _messenger.Send(new ItemLockToggledMessage(Id, _containerId, value));
    }

    // ── Search / filter helpers ───────────────────────────────────────────────
    //
    // These are called by ItemListViewModel.ApplyFilter() — NOT by the View.
    // Keeping the matching logic here means:
    //   (a) The item knows what fields are searchable (its own concern).
    //   (b) Tests can verify matching without touching the list or the container.

    /// <summary>
    /// Returns true if this item should appear for the given search text.
    /// Searches Id (as a string) and Name, case-insensitively.
    /// Empty or whitespace search text matches everything.
    /// </summary>
    public bool MatchesSearch(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        return Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || Id.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if this item passes the active shape filter.
    /// A null filter means "show all shapes."
    /// </summary>
    public bool MatchesFilter(ShapeType? filterShape)
        => filterShape is null || Shape == filterShape;
}
