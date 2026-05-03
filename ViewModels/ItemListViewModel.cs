using System.Collections.Generic;
using System.Collections.ObjectModel;
using SquareClickerPointer.Models;

namespace SquareClickerPointer.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
//  ItemListViewModel  —  manages the filtered collection of list items
// ═══════════════════════════════════════════════════════════════════════════════
//
//  ROLE IN THE SYSTEM
//  ──────────────────
//  This ViewModel sits INSIDE ExpandableContainerViewModel — it is a child VM
//  owned and controlled by the container.
//
//  Its single job: maintain two parallel collections.
//
//    _allItems        — the complete, unfiltered source of truth.
//    FilteredItems    — the subset the View actually displays.
//
//  When search text or a filter changes, ExpandableContainerViewModel calls
//  ApplyFilter() which rebuilds FilteredItems from _allItems.
//
//  WHY a separate ViewModel instead of putting this in ExpandableContainerViewModel?
//  ──────────────────────────────────────────────────────────────────────────────────
//  Single Responsibility Principle: ExpandableContainerViewModel owns expand/collapse,
//  search text, and filter state — that is already a lot.  Delegating the actual
//  collection management to a dedicated child keeps each class focused.
//
//  It also makes the list independently usable: if you ever want a list that lives
//  outside an expandable container, ItemListViewModel can be used directly.
//
//  WHY is ItemListViewModel registered as Transient in the DI container?
//  ────────────────────────────────────────────────────────────────────────
//  Each ExpandableContainerView needs its OWN list with its OWN items.
//  Transient means: "create a new instance every time someone asks."
//  When DI creates an ExpandableContainerViewModel, it creates a fresh
//  ItemListViewModel for it automatically (constructor injection).
//  Container 1's list never shares state with Container 2's list.
//
//  NOTE: This class does NOT take IMessenger because it never sends or receives
//  messages directly.  It is pure state management.  Messages are the concern of
//  its parent (ExpandableContainerViewModel) and of ListItemViewModel itself.

/// <summary>
/// Child ViewModel owned by <see cref="ExpandableContainerViewModel"/>.
/// Maintains the full item collection and the filtered subset the View displays.
/// </summary>
public class ItemListViewModel : ViewModelBase
{
    // ── Source of truth ───────────────────────────────────────────────────────
    //
    // _allItems holds every item added to this list, regardless of current filters.
    // It is private: only ItemListViewModel decides how to populate and clear it.
    // ApplyFilter() iterates this list to rebuild FilteredItems each time.
    private readonly ObservableCollection<ListItemViewModel> _allItems = new();

    // ── View-facing collection ────────────────────────────────────────────────
    //
    // FilteredItems is what ItemListView.axaml's ItemsControl binds to.
    // It is rebuilt (Clear + re-add) whenever the search text or filter changes.
    //
    // WHY ObservableCollection instead of IEnumerable or List?
    //   ItemsControl watches this collection for CollectionChanged events.
    //   ObservableCollection fires those events when items are added or removed.
    //   An IEnumerable is a snapshot — the UI would never know it changed.
    //   A List has no events at all.
    //
    // WHY not just filter _allItems with LINQ and return a new collection?
    //   LINQ returns a new list every call.  If we assigned that new list to a
    //   property, the ItemsControl would unmount every existing item and re-mount
    //   all new ones — visually jarring and expensive.  Mutating the same
    //   ObservableCollection with targeted Add/Remove gives the ItemsControl
    //   exactly the minimal set of changes it needs.

    /// <summary>
    /// The subset of items passing the current search + filter criteria.
    /// ItemListView.axaml's ItemsControl binds to this collection.
    /// Updated by <see cref="ApplyFilter"/> — do not mutate externally.
    /// </summary>
    public ObservableCollection<ListItemViewModel> FilteredItems { get; } = new();

    // ── Population API ────────────────────────────────────────────────────────
    //
    // These are called by ExpandableContainerViewModel, which in turn is called
    // by external code (e.g. MainWindow.axaml.cs).  External code never touches
    // ItemListViewModel directly — it always goes through the container.

    /// <summary>
    /// Replaces the entire item list and shows all items (no active filter).
    /// Called by <see cref="ExpandableContainerViewModel.SetItems"/>.
    /// </summary>
    public void SetItems(IEnumerable<ListItemViewModel> items)
    {
        _allItems.Clear();
        foreach (var item in items)
            _allItems.Add(item);

        // Show everything; no filter applied yet.
        ApplyFilter(string.Empty, null);
    }

    /// <summary>
    /// Appends a single item and makes it immediately visible (assumes it passes
    /// any currently active filter — call ApplyFilter afterwards if needed).
    /// </summary>
    public void AddItem(ListItemViewModel item)
    {
        _allItems.Add(item);
        FilteredItems.Add(item);
    }

    // ── Filtering ─────────────────────────────────────────────────────────────
    //
    // Called by ExpandableContainerViewModel.OnSearchTextChanged and
    // OnActiveFilterChanged (both generated hooks from [ObservableProperty]).
    //
    // WHY does filtering live here rather than in ExpandableContainerViewModel?
    //   The container knows WHAT to filter by (SearchText, ActiveFilter).
    //   The list knows HOW to filter (iterating _allItems, calling item.MatchesSearch).
    //   Each class owns its own domain.  If filtering logic gets more complex
    //   (score-based search, multi-select filters), it grows here, not in the container.

    /// <summary>
    /// Rebuilds <see cref="FilteredItems"/> to contain only items that pass both
    /// the search text check and the shape-type filter check.
    /// </summary>
    /// <param name="searchText">
    /// Text the user typed in the search box.  Null or empty = no text filter.
    /// </param>
    /// <param name="filterShape">
    /// Shape to restrict visibility to.  Null = all shapes are shown.
    /// </param>
    public void ApplyFilter(string searchText, ShapeType? filterShape)
    {
        FilteredItems.Clear();
        foreach (var item in _allItems)
        {
            if (item.MatchesSearch(searchText) && item.MatchesFilter(filterShape))
                FilteredItems.Add(item);
        }
    }
}
