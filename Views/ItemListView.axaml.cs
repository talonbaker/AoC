using Avalonia.Controls;

namespace SquareClickerPointer.Views;

// ═══════════════════════════════════════════════════════════════════════════════
//  ItemListView code-behind  —  intentionally almost empty
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY IS THIS FILE EMPTY?
//  ────────────────────────
//  The entire responsibility of ItemListView is expressed in ItemListView.axaml:
//  bind to FilteredItems, wrap each item in a DataTemplate, scroll if needed.
//  None of that requires C# code.
//
//  DATACONTEXT — set by the parent, NOT here
//  ──────────────────────────────────────────
//  ExpandableContainerView.axaml places this view as:
//
//      <views:ItemListView DataContext="{Binding ItemList}"/>
//
//  That binding provides the ItemListViewModel BEFORE InitializeComponent()
//  completes (Avalonia processes parent bindings as part of layout).
//
//  If we set DataContext here in the constructor, we would:
//    1. Override the ItemListViewModel the parent just provided.
//    2. Break every binding in ItemListView.axaml because the wrong (or null)
//       DataContext would be in place.
//
//  The rule of thumb: code-behind sets DataContext ONLY when this view is a
//  top-level, self-contained module resolved from the DI container
//  (like ExpandableContainerView or PointControlView).  Nested views that receive
//  their DataContext from a parent leave code-behind empty.

public partial class ItemListView : UserControl
{
    public ItemListView()
    {
        InitializeComponent();
        // DataContext flows in from ExpandableContainerView via binding — do not override it.
    }
}
