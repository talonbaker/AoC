using Avalonia.Controls;
using CommunityToolkit.Mvvm.DependencyInjection;
using SquareClickerPointer.ViewModels;

namespace SquareClickerPointer.Views;

// ═══════════════════════════════════════════════════════════════════════════════
//  ExpandableContainerView code-behind
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY IS THE VIEWMODEL EXPOSED AS A PUBLIC PROPERTY?
//  ────────────────────────────────────────────────────
//  This control is populated externally.  The data (title, items) is provided
//  by the code that places the view in the layout — in this project, that is
//  MainWindow.axaml.cs.  To do so, the caller needs access to the ViewModel:
//
//      Container1View.ViewModel.Title = "My Data";
//      Container1View.ViewModel.SetItems(models);
//
//  Exposing it as a typed public property (not a cast of DataContext) is safer
//  and more discoverable.  IntelliSense shows the exact API without guessing.
//
//  WHY Ioc.Default.GetRequiredService<ExpandableContainerViewModel>()?
//  ────────────────────────────────────────────────────────────────────
//  ExpandableContainerViewModel is registered as TRANSIENT in App.axaml.cs.
//  Each call to GetRequiredService<ExpandableContainerViewModel>() creates a NEW
//  instance.  This means Container1, Container2, and Container3 each get their
//  own independent ExpandableContainerViewModel — their own title, their own
//  item list, their own IsExpanded state.  They share only the event-bus
//  singletons (injected into each VM's constructor by the DI container).
//
//  WHY InitializeComponent() AFTER setting DataContext?
//  ──────────────────────────────────────────────────────
//  Avalonia processes XAML bindings as part of InitializeComponent().  If
//  DataContext is null when InitializeComponent() runs, the first binding pass
//  finds nothing.  Setting DataContext first ensures bindings resolve on the
//  first pass, which is more efficient and avoids a second layout cycle.

public partial class ExpandableContainerView : UserControl
{
    // ── Public API for external population ───────────────────────────────────
    //
    // Read-only: only this class sets ViewModel (in the constructor).
    // External code calls methods on it: ViewModel.SetItems(…), ViewModel.Title = …

    /// <summary>
    /// The ViewModel backing this container.
    /// Use this to set the title and populate the item list from outside the control.
    /// </summary>
    public ExpandableContainerViewModel ViewModel { get; }

    public ExpandableContainerView()
    {
        // ── Step 1: Resolve a fresh ViewModel from the DI container ──────────
        //
        // Transient registration means each new ExpandableContainerView gets its
        // own brand-new ExpandableContainerViewModel with its own ItemListViewModel.
        // The DI container injects the event-bus singletons (ContainerEventBus,
        // ItemColorEventBus, ItemLockEventBus, ItemShapeEventBus) and a fresh
        // transient ItemListViewModel into the constructor automatically.
        ViewModel   = Ioc.Default.GetRequiredService<ExpandableContainerViewModel>();

        // ── Step 2: Bind the ViewModel to the View ───────────────────────────
        //
        // DataContext is the object that all {Binding ...} expressions in AXAML
        // resolve against.  Setting it before InitializeComponent() means the
        // first binding pass in AXAML already has a valid DataContext.
        DataContext = ViewModel;

        // ── Step 3: Parse and build the XAML-defined visual tree ────────────
        //
        // InitializeComponent() reads ExpandableContainerView.axaml, creates all
        // the controls declared in it, and wires up the bindings.  By the time
        // this returns, the full visual tree is built and all bindings are live.
        InitializeComponent();
    }
}
