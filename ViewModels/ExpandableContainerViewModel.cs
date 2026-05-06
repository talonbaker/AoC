using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SquareClickerPointer.EventArgs;
using SquareClickerPointer.EventBuses;
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
//    3. ACCORDION BEHAVIOR (pub/sub) — raises ContainerEventBus.ContainerExpanded
//       when expanding; subscribes to it to collapse when a sibling opens.
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
//   ExpandableContainerViewModel  ──raises──▶  ContainerEventBus.ContainerExpanded
//       │  ItemList.ApplyFilter()             (when expanding)
//       │
//       ▼
//   ItemListViewModel.FilteredItems
//       │
//       ▼  (ItemsControl binds to this)
//   ListItemView rows  ──(TwoWay IsChecked)──▶  ListItemViewModel.IsLocked
//                                                         │
//                                                         ▼ raises
//                                            ItemLockEventBus.ItemLockToggled

/// <summary>
/// ViewModel for one expandable container panel.
/// Set <see cref="Title"/> and call <see cref="SetItems"/> to populate it;
/// the container handles all search, filter, and accordion behavior internally.
/// Implements <see cref="IDisposable"/> because it subscribes to
/// <see cref="ContainerEventBus.ContainerExpanded"/> in its constructor.
/// </summary>
public partial class ExpandableContainerViewModel : ViewModelBase, IDisposable
{
    private readonly ContainerEventBus _containerBus;
    private readonly ItemColorEventBus _colorBus;
    private readonly ItemLockEventBus  _lockBus;
    private readonly ItemShapeEventBus _shapeBus;
    private readonly ItemListViewModel _itemList;

    // ── Unique identity ───────────────────────────────────────────────────────
    //
    // A new Guid is assigned at construction — each container instance is
    // guaranteed a unique ID even if you create many at once.
    // This ID travels inside ContainerExpandedEventArgs so siblings can identify
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
    /// DI constructs this by injecting the shared event-bus singletons and a fresh
    /// Transient ItemListViewModel.  You never call this constructor manually.
    /// </summary>
    /// <param name="containerBus">
    /// Shared event bus carrying <see cref="ContainerExpandedEventArgs"/>.
    /// Used here to raise ContainerExpanded when this container opens, and to
    /// subscribe so we collapse when a sibling opens.
    /// </param>
    /// <param name="colorBus">
    /// Shared <see cref="ItemColorEventBus"/>, forwarded to each
    /// <see cref="ListItemViewModel"/> created by this container.
    /// </param>
    /// <param name="lockBus">
    /// Shared <see cref="ItemLockEventBus"/>, forwarded to each
    /// <see cref="ListItemViewModel"/> created by this container.
    /// </param>
    /// <param name="shapeBus">
    /// Shared <see cref="ItemShapeEventBus"/>, forwarded to each
    /// <see cref="ListItemViewModel"/> created by this container.
    /// </param>
    /// <param name="itemList">
    /// A fresh ItemListViewModel, injected by the DI container (Transient).
    /// Each ExpandableContainerViewModel gets its own, isolated list.
    /// </param>
    public ExpandableContainerViewModel(
        ContainerEventBus containerBus,
        ItemColorEventBus colorBus,
        ItemLockEventBus  lockBus,
        ItemShapeEventBus shapeBus,
        ItemListViewModel itemList)
    {
        _containerBus = containerBus;
        _colorBus     = colorBus;
        _lockBus      = lockBus;
        _shapeBus     = shapeBus;
        _itemList     = itemList;

        // ── Subscribe: accordion collapse ─────────────────────────────────────
        //
        // When ANY container raises ContainerExpanded, we check:
        //   "Is this from ME (I just expanded)?  Ignore it."
        //   "Is this from a SIBLING?  Collapse myself."
        //
        // The handler is a method group (OnContainerExpanded) rather than an
        // inline lambda so the same delegate instance can be passed to both
        // Subscribe and Unsubscribe.  Without that, Dispose's Unsubscribe call
        // would silently fail to remove the handler.
        _containerBus.Subscribe(OnContainerExpanded);
    }

    private void OnContainerExpanded(object? sender, ContainerExpandedEventArgs e)
    {
        // Only collapse if a DIFFERENT container expanded.
        if (e.ContainerId != ContainerId)
            IsExpanded = false;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    //
    // The bus holds a strong reference to this ViewModel through the subscription
    // delegate.  Calling Dispose() detaches the handler so the VM can be garbage
    // collected once the owning View is unloaded.

    public void Dispose()
    {
        _containerBus.Unsubscribe(OnContainerExpanded);
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
    /// Raises ContainerEventBus.ContainerExpanded when expanding so siblings can collapse.
    /// </summary>
    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;

        if (IsExpanded)
        {
            // Notify siblings via the event bus — they will set IsExpanded = false
            // in OnContainerExpanded above.
            //
            // We only raise the event when EXPANDING.  Setting IsExpanded = false in
            // another container's handler is a direct property assignment — it does
            // NOT call ToggleExpand() on that container, so no further events fire.
            // No infinite loop.
            _containerBus.Publish(this, new ContainerExpandedEventArgs(ContainerId));
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
        var viewModels = models.Select(m =>
            new ListItemViewModel(m, ContainerId, _colorBus, _lockBus, _shapeBus));
        _itemList.SetItems(viewModels);
    }

    /// <summary>
    /// Appends a single item to the list.
    /// The item is immediately visible (no filter re-evaluation — call
    /// SetItems instead if you need a clean filtered view after adding).
    /// </summary>
    public void AddItem(ListItemModel model)
    {
        _itemList.AddItem(
            new ListItemViewModel(model, ContainerId, _colorBus, _lockBus, _shapeBus));
    }
}
