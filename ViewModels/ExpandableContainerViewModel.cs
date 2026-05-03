using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SquareClickerPointer.Messages;
using SquareClickerPointer.Models;

namespace SquareClickerPointer.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
//  ExpandableContainerViewModel  —  the top-level VM for one collapsible panel
// ═══════════════════════════════════════════════════════════════════════════════
//
//  ROLE IN THE SYSTEM
//  ──────────────────
//  This ViewModel is the public API for external code that wants to populate a
//  container.  External code (e.g. MainWindow.axaml.cs) does:
//
//      container.ViewModel.Title    = "My Data";
//      container.ViewModel.SetItems(listOfModels);
//
//  Internally, this VM owns three concerns:
//
//    1. HEADER STATE — IsExpanded + Title.  The expand/collapse toggle button
//       in the header row is driven entirely by these two properties.
//
//    2. SEARCH + FILTER STATE — SearchText + ActiveFilter + IsFilterOpen.
//       When these change, the VM calls ItemList.ApplyFilter() to rebuild the
//       visible list.  The search TextBox and filter dropdown bind to these.
//
//    3. ACCORDION BEHAVIOR (pub/sub) — publishes ContainerExpandedMessage when
//       expanding; subscribes to it to collapse when a sibling opens.
//
//  WHY is this registered as TRANSIENT in the DI container?
//  ─────────────────────────────────────────────────────────
//  There can be multiple containers in the window (Container 1, 2, 3…).  Each
//  must have its own independent state — its own title, its own item list, its
//  own IsExpanded flag.  Transient = "create a new instance for every caller."
//  Each ExpandableContainerView code-behind calls GetRequiredService<…>() and
//  gets a brand-new, unshared ViewModel.
//
//  Compare to PointControlViewModel which is Singleton because there is exactly
//  one of it in the window and sharing state across two imaginary copies would
//  make no sense.
//
//  CHILD VIEWMODEL — ItemListViewModel
//  ─────────────────────────────────────
//  This VM owns an ItemListViewModel instance, injected via the DI container.
//  Because ItemListViewModel is also Transient, DI creates a fresh one for
//  each ExpandableContainerViewModel, automatically.  You do not call 'new'
//  manually — the container wires it up for you.
//
//  DATA FLOW OVERVIEW
//  ──────────────────
//
//   External code
//       │  SetItems(models[])
//       ▼
//   ExpandableContainerViewModel  ──publishes──▶  ContainerExpandedMessage
//       │  ItemList.ApplyFilter()               (when expanding)
//       │
//       ▼
//   ItemListViewModel.FilteredItems
//       │
//       ▼  (ItemsControl binds to this)
//   ListItemView rows  ──(TwoWay IsChecked)──▶  ListItemViewModel.IsLocked
//                                                         │
//                                                         ▼ publishes
//                                               ItemLockToggledMessage

/// <summary>
/// ViewModel for one expandable container panel.
/// Set <see cref="Title"/> and call <see cref="SetItems"/> to populate it;
/// the container handles all search, filter, and accordion behavior internally.
/// </summary>
public partial class ExpandableContainerViewModel : ViewModelBase
{
    private readonly IMessenger      _messenger;
    private readonly ItemListViewModel _itemList;

    // ── Unique identity ───────────────────────────────────────────────────────
    //
    // A new Guid is assigned at construction — each container instance is
    // guaranteed a unique ID even if you create many at once.
    // This ID travels inside ContainerExpandedMessage so siblings can identify
    // "was this message sent by ME or by someone else?"

    /// <summary>
    /// Unique identifier for this container instance.
    /// Generated once at construction; immutable for the lifetime of the VM.
    /// </summary>
    public string ContainerId { get; } = Guid.NewGuid().ToString();

    // ── Header state ──────────────────────────────────────────────────────────

    /// <summary>Text displayed in the header row next to the chevron arrow.</summary>
    [ObservableProperty] private string _title = "Container";

    /// <summary>
    /// True when the container body (search bar + list) is visible.
    /// The header button's ToggleExpandCommand flips this.
    /// </summary>
    [ObservableProperty] private bool _isExpanded;

    // ── Search + filter state ─────────────────────────────────────────────────
    //
    // [ObservableProperty] generates a property setter that:
    //   1. Sets the backing field.
    //   2. Raises PropertyChanged so XAML bindings update the UI.
    //   3. Calls the partial method OnXxxChanged(value) if it exists.
    //
    // We implement the partial methods below to trigger ApplyFilter() any time
    // either the search text or the shape filter changes.

    /// <summary>
    /// Text typed in the search box.  Bound two-way to the TextBox in the View.
    /// Changing this triggers OnSearchTextChanged which rebuilds the filtered list.
    /// </summary>
    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>
    /// Controls whether the filter dropdown panel is visible below the search row.
    /// </summary>
    [ObservableProperty] private bool _isFilterOpen;

    /// <summary>
    /// The currently active shape filter.  Null = "all shapes."
    /// Set by the filter panel buttons; triggers OnActiveFilterChanged.
    /// </summary>
    [ObservableProperty] private ShapeType? _activeFilter;

    // ── Child ViewModel ───────────────────────────────────────────────────────
    //
    // ItemList is a read-only property: external code never replaces it, only calls
    // methods on it (SetItems, ApplyFilter).
    //
    // WHY expose it at all?
    //   ExpandableContainerView.axaml needs to set the DataContext of the nested
    //   ItemListView to this object.  The XAML binding is:
    //
    //       <views:ItemListView DataContext="{Binding ItemList}"/>
    //
    //   That binding resolves at runtime to this exact object — the one DI created
    //   and injected into our constructor.

    /// <summary>
    /// Child ViewModel managing the filtered item list.
    /// ItemListView.axaml binds its DataContext to this property.
    /// </summary>
    public ItemListViewModel ItemList => _itemList;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// DI constructs this by injecting the shared IMessenger singleton and a fresh
    /// Transient ItemListViewModel.  You never call this constructor manually.
    /// </summary>
    /// <param name="messenger">
    /// Shared message bus — same instance used by all other ViewModels in the app.
    /// Used here to publish ContainerExpandedMessage and to subscribe to it.
    /// </param>
    /// <param name="itemList">
    /// A fresh ItemListViewModel, injected by the DI container (Transient).
    /// Each ExpandableContainerViewModel gets its own, isolated list.
    /// </param>
    public ExpandableContainerViewModel(IMessenger messenger, ItemListViewModel itemList)
    {
        _messenger = messenger;
        _itemList  = itemList;

        // ── Subscribe: accordion collapse ─────────────────────────────────────
        //
        // When ANY container publishes ContainerExpandedMessage, we check:
        //   "Is this message from ME (I just expanded)?  Ignore it."
        //   "Is this message from a SIBLING?  Collapse myself."
        //
        // WHY the (recipient, message) pattern (not a closure capture):
        //   See the detailed explanation in TriangleAlphaDataViewModel — the short
        //   answer is that closures capture 'this' strongly, defeating the
        //   WeakReferenceMessenger's ability to let this VM be garbage-collected.
        //   The (recipient, message) overload stores only a weak reference to
        //   'this'; the lambda receives it as a typed parameter instead.
        _messenger.Register<ExpandableContainerViewModel, ContainerExpandedMessage>(
            this,
            (recipient, message) =>
            {
                // Only collapse if a DIFFERENT container expanded.
                if (message.ContainerId != recipient.ContainerId)
                    recipient.IsExpanded = false;
            });
    }

    // ── Property-change hooks (partial methods, called by source generator) ───

    /// <summary>
    /// Called automatically after SearchText changes.
    /// Re-runs the filter so the list instantly reflects what the user typed.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        // Delegate to the child VM — it knows how to iterate and filter _allItems.
        _itemList.ApplyFilter(value, ActiveFilter);
    }

    /// <summary>
    /// Called automatically after ActiveFilter changes.
    /// Re-runs the filter using the existing search text + the new shape filter.
    /// </summary>
    partial void OnActiveFilterChanged(ShapeType? value)
    {
        _itemList.ApplyFilter(SearchText, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    //
    // [RelayCommand] generates a public ICommand property named {Method}Command.
    // The View binds: Command="{Binding ToggleExpandCommand}"

    /// <summary>
    /// Called when the user clicks anywhere on the header row.
    /// Expands or collapses the container body.
    /// Publishes ContainerExpandedMessage when expanding so siblings can collapse.
    /// </summary>
    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;

        if (IsExpanded)
        {
            // Notify siblings via the message bus — they will set IsExpanded = false
            // in their registered handler above.
            //
            // We only publish when EXPANDING.  Setting IsExpanded = false in another
            // container's handler is a direct property assignment — it does NOT call
            // ToggleExpand() on that container, so no further messages are sent.
            // No infinite loop.
            _messenger.Send(new ContainerExpandedMessage(ContainerId));
        }
        else
        {
            // Close the filter dropdown when collapsing so it is not open
            // the next time this container is expanded.
            IsFilterOpen = false;
        }
    }

    /// <summary>
    /// Toggles the shape-filter dropdown panel open or closed.
    /// </summary>
    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterOpen = !IsFilterOpen;
    }

    // ── Filter-selection commands ─────────────────────────────────────────────
    //
    // One command per filter option.  This is intentionally verbose (not a
    // parametrised command) because:
    //   • Each button in the filter panel is bound to a different named command.
    //   • Named commands are the most readable and discoverable pattern for a
    //     JR developer working through the XAML.
    //   • Parametrised ICommand requires boxing/unboxing of the enum value and
    //     adds a converter or a XAML extension — extra indirection for no gain.

    /// <summary>Removes the active shape filter — shows all shapes.</summary>
    [RelayCommand]
    private void FilterAll()       => ApplyShapeFilter(null);

    /// <summary>Restricts the list to Star items only.</summary>
    [RelayCommand]
    private void FilterStar()      => ApplyShapeFilter(ShapeType.Star);

    /// <summary>Restricts the list to Hexagon items only.</summary>
    [RelayCommand]
    private void FilterHexagon()   => ApplyShapeFilter(ShapeType.Hexagon);

    /// <summary>Restricts the list to Triangle items only.</summary>
    [RelayCommand]
    private void FilterTriangle()  => ApplyShapeFilter(ShapeType.Triangle);

    /// <summary>Restricts the list to Square items only.</summary>
    [RelayCommand]
    private void FilterSquare()    => ApplyShapeFilter(ShapeType.Square);

    // Shared logic called by every filter command.
    private void ApplyShapeFilter(ShapeType? shape)
    {
        ActiveFilter = shape;    // raises PropertyChanged + triggers OnActiveFilterChanged
        IsFilterOpen = false;    // close the dropdown after selecting
        // Note: _itemList.ApplyFilter is called by OnActiveFilterChanged — no need to call it here.
    }

    // ── External population API ───────────────────────────────────────────────
    //
    // These are the methods external code calls after obtaining a reference to
    // this ViewModel via ExpandableContainerView.ViewModel.
    //
    // WHY are these here and not on ItemListViewModel?
    //   External code knows about the container (it placed the view in XAML).
    //   It should not need to reach through to a child VM.  The container is the
    //   public boundary; ItemListViewModel is an internal implementation detail.

    /// <summary>
    /// Replaces the entire item collection and shows all items (clears active filters).
    /// </summary>
    /// <param name="models">Raw data records built by the external caller.</param>
    public void SetItems(IEnumerable<ListItemModel> models)
    {
        var viewModels = models.Select(m => new ListItemViewModel(m, ContainerId, _messenger));
        _itemList.SetItems(viewModels);
    }

    /// <summary>
    /// Appends a single item to the list.
    /// The item is immediately visible (no filter re-evaluation — call
    /// SetItems instead if you need a clean filtered view after adding).
    /// </summary>
    public void AddItem(ListItemModel model)
    {
        _itemList.AddItem(new ListItemViewModel(model, ContainerId, _messenger));
    }
}
