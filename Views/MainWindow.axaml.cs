using Avalonia.Controls;
using Avalonia.Media;
using SquareClickerPointer.Models;

namespace SquareClickerPointer.Views;

// ═══════════════════════════════════════════════════════════════════════════════
//  MainWindow code-behind  —  seeds data into the expandable containers
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHY IS DATA SEEDED HERE?
//  ─────────────────────────
//  ExpandableContainerView is a reusable, data-agnostic shell.  It has no
//  hardcoded content — the external caller provides the title and items.
//  In this application, "external caller" means MainWindow, which is the
//  composition point where the views are assembled.
//
//  In a real application, the data would come from a service, a database, or
//  a network call.  MainWindow (or its ViewModel) would still be responsible
//  for acquiring that data and passing it to the containers.
//
//  WHY code-behind instead of a binding from MainWindowViewModel?
//  ──────────────────────────────────────────────────────────────
//  Each container resolves its OWN ViewModel from the DI container
//  (Transient registration).  MainWindowViewModel does not own or create those
//  ViewModels.  To give MainWindowViewModel a list of containers it could
//  manage, you would need to refactor to a parent-VM-creates-child-VMs pattern
//  (sometimes called a "Screen conductor").  For this teaching example, direct
//  population in code-behind is the clearest demonstration of how external
//  population works without adding that abstraction.
//
//  ORDER OF OPERATIONS:
//   1. InitializeComponent() → XAML parsed → ExpandableContainerView instances
//      created → each calls its constructor → each resolves a Transient
//      ExpandableContainerViewModel from DI and sets its DataContext.
//   2. SeedContainers() → we use the x:Name references to reach each view's
//      ViewModel and call SetItems() / set Title.
//   3. User sees fully-populated, collapsed containers.

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Populate after XAML is built (controls exist after InitializeComponent).
        SeedContainers();
    }

    private void SeedContainers()
    {
        // ── Container 1 ───────────────────────────────────────────────────────
        //
        // Container1View is the ExpandableContainerView with x:Name="Container1View"
        // declared in MainWindow.axaml.  The generated InitializeComponent() code
        // assigns the matching control instance to this field automatically.
        Container1View.ViewModel.Title = "Container 1";
        Container1View.ViewModel.SetItems(
        [
            new ListItemModel(101, "Alpha Node",    ShapeType.Star,     Color.FromRgb(220,  60,  60)),
            new ListItemModel(102, "Beta Node",     ShapeType.Hexagon,  Color.FromRgb( 60, 180,  60)),
            new ListItemModel(103, "Gamma Node",    ShapeType.Triangle, Color.FromRgb( 60, 140, 220)),
        ]);

        // ── Container 2 ───────────────────────────────────────────────────────
        //
        // Mirrors the four-item layout shown in the design mockup.
        // Colors match the red/green/blue/purple scheme in the image.
        // IsLocked=true on Item 1 demonstrates the padlock in locked state.
        Container2View.ViewModel.Title = "Container 2";
        Container2View.ViewModel.SetItems(
        [
            new ListItemModel(201, "Item 1", ShapeType.Star,     Color.FromRgb(220,  44,  44), IsLocked: true),
            new ListItemModel(202, "Item 2", ShapeType.Hexagon,  Color.FromRgb( 34, 170,  34)),
            new ListItemModel(203, "Item 3", ShapeType.Triangle, Color.FromRgb( 30, 144, 255)),
            new ListItemModel(204, "Item 4", ShapeType.Square,   Color.FromRgb(150,  60, 200)),
        ]);

        // ── Container 3 ───────────────────────────────────────────────────────
        //
        // A different data set to show that containers are truly independent:
        // different shapes, different colors, different item count.
        Container3View.ViewModel.Title = "Container 3";
        Container3View.ViewModel.SetItems(
        [
            new ListItemModel(301, "Delta",   ShapeType.Square,   Color.FromRgb(255, 165,   0)),
            new ListItemModel(302, "Epsilon", ShapeType.Star,     Color.FromRgb(  0, 200, 180)),
            new ListItemModel(303, "Zeta",    ShapeType.Triangle, Color.FromRgb(200, 100, 200)),
            new ListItemModel(304, "Eta",     ShapeType.Hexagon,  Color.FromRgb(255, 220,  50)),
            new ListItemModel(305, "Theta",   ShapeType.Square,   Color.FromRgb( 80, 160, 255)),
        ]);
    }
}
