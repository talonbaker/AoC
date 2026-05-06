using Avalonia.Controls;
using CommunityToolkit.Mvvm.DependencyInjection;
using SquareClickerPointer.ViewModels;

namespace SquareClickerPointer.Views;

// ═══════════════════════════════════════════════════════════════════════════════
//  TriangleAlphaDataView  —  code-behind
// ═══════════════════════════════════════════════════════════════════════════════
//
//  HOW SMALL SHOULD CODE-BEHIND BE?
//  ──────────────────────────────────
//  Ideally, as close to zero as possible.
//
//  For this control there is exactly one thing that cannot be expressed in XAML:
//  wiring the DataContext to the ViewModel from the DI container.  Everything else
//  — layout, bindings, visibility toggling — lives in the .axaml file.
//
//  If you find yourself adding logic here (event handlers, calculations, state),
//  ask: "Should this be a ViewModel property or command instead?"  In almost all
//  cases the answer is yes.
//
//  WHY Ioc.Default.GetRequiredService<T>() (not 'new TriangleAlphaDataViewModel()'):
//
//   1. TriangleAlphaDataViewModel requires a DotPositionEventBus in its constructor.
//      If we wrote 'new TriangleAlphaDataViewModel()' the compiler would error —
//      there is no parameterless constructor (by design — see that file for why).
//
//   2. More importantly: the DI container registered DotPositionEventBus as a singleton.
//      GetRequiredService resolves the same instance that PointControlViewModel
//      already received.  That shared bus is what carries DotReleased events from
//      one ViewModel to the other.
//
//      If we somehow constructed a TriangleAlphaDataViewModel with a different
//      DotPositionEventBus than PointControlViewModel uses, the subscription would
//      exist on a different bus — events would never arrive.  Singletons in the
//      container are the structural guarantee that prevents this bug.

/// <summary>
/// Code-behind for the TriangleAlphaData user control.
/// Retrieves its ViewModel from the DI container and sets it as DataContext.
/// All display logic lives in TriangleAlphaDataView.axaml.
/// </summary>
public partial class TriangleAlphaDataView : UserControl
{
    public TriangleAlphaDataView()
    {
        // Resolve the singleton ViewModel.  The container injects the shared
        // DotPositionEventBus so this VM is on the same bus as PointControlViewModel.
        DataContext = Ioc.Default.GetRequiredService<TriangleAlphaDataViewModel>();
        InitializeComponent();
    }
}
