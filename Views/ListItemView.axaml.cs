using Avalonia.Controls;

namespace SquareClickerPointer.Views;

// ═══════════════════════════════════════════════════════════════════════════════
//  ListItemView code-behind  —  intentionally almost empty
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY IS THIS FILE SO SMALL?
//  ───────────────────────────
//  Everything this view needs is expressed as data-binding in ListItemView.axaml.
//
//  DATACONTEXT — who sets it?
//  ───────────────────────────
//  ItemListView.axaml contains an ItemsControl whose ItemTemplate is a DataTemplate
//  that wraps <views:ListItemView/>.  Avalonia's ItemsControl automatically sets
//  each ListItemView's DataContext to the corresponding ListItemViewModel from
//  the FilteredItems collection.
//
//  We do NOT call DataContext = ... here.  If we did, we would override the
//  DataContext that ItemsControl just set, and all bindings would break.
//  The rule: only the TOP-LEVEL owner of a view (the one that creates and
//  places it) is responsible for setting its DataContext.
//  In a DataTemplate, Avalonia is that owner — not us.

public partial class ListItemView : UserControl
{
    public ListItemView()
    {
        InitializeComponent();
        // DataContext is set externally by ItemsControl — do not set it here.
    }
}
