using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using SquareClickerPointer.ViewModels;

namespace SquareClickerPointer.Views;

// ═══════════════════════════════════════════════════════════════════════════════
//  PointControlView  —  code-behind for the SquareClickerPointer user control
// ═══════════════════════════════════════════════════════════════════════════════
//
//  WHAT BELONGS IN CODE-BEHIND?
//  ─────────────────────────────
//  Code-behind is the right place for things that:
//    1. Cannot be expressed in XAML (procedural loops, complex imperative logic).
//    2. Are pure VIEW concerns — they have no meaning outside the UI context.
//
//  What you will find here:
//    • Retrieving the ViewModel from the DI container (one-time setup).
//    • Building the checker-board tile pattern (visual-only, no business logic).
//    • Translating raw Avalonia pointer events into plain method calls on the VM.
//
//  What you will NOT find here:
//    • Domain calculations  (those are in PointControlViewModel).
//    • Message publishing   (CommitPosition() delegates that to the ViewModel).
//    • References to other Views or other ViewModels.
//    • Any event-bus usage (the View has no reason to touch the buses directly).
//
//  CHANGE FROM PREVIOUS VERSION
//  ─────────────────────────────
//  The old code-behind had:
//
//      public event Action<double, double>? DotMoved;
//      private void OnVmDotMoved(double x, double y) => DotMoved?.Invoke(x, y);
//
//  This "relay event" forced any consumer (e.g. a hypothetical MainWindow) to:
//    (a) Hold a typed reference to PointControlView.
//    (b) Explicitly subscribe/unsubscribe in code.
//    (c) Know the View's event signature.
//
//  All three are removed.  Cross-component communication now flows entirely through
//  DotPositionEventBus (PointControlViewModel → DotReleased event →
//  TriangleAlphaDataViewModel).  This View is no longer part of that channel at all.

/// <summary>
/// Code-behind for the SquareClickerPointer (2-D pad + sliders) user control.
/// All business logic lives in <see cref="PointControlViewModel"/>.
/// </summary>
public partial class PointControlView : UserControl
{
    // ── Typed ViewModel reference ─────────────────────────────────────────────────
    //
    // WHY we keep a typed field instead of casting DataContext every time:
    //   The pointer handlers call ViewModel methods on every mouse-move event
    //   (potentially 60 fps).  Casting DataContext on every call would add overhead
    //   and a null-check per event.  Storing a typed reference once is cleaner.
    //
    // WHY it is readonly:
    //   The ViewModel is a singleton retrieved from Ioc.Default in the constructor.
    //   It will never change for the lifetime of this View.  readonly makes that
    //   contract explicit and prevents accidental reassignment.
    //
    private readonly PointControlViewModel _vm;

    public PointControlView()
    {
        // ── Step 1: Retrieve the ViewModel from the DI container ──────────────────
        //
        // Ioc.Default is CommunityToolkit.Mvvm's process-wide IoC container.
        // GetRequiredService<T>() looks up the registered singleton and returns it.
        //
        // WHY NOT new PointControlViewModel() here:
        //   PointControlViewModel requires a DotPositionEventBus argument.  If we
        //   constructed it manually we would have to source the bus ourselves —
        //   which means this View would need to know where the bus lives, pulling
        //   it into a concern it should not have.  The container owns that wiring.
        //
        //   More critically: constructing a NEW ViewModel here would give it a
        //   DIFFERENT DotPositionEventBus instance than TriangleAlphaDataViewModel
        //   is listening on.  The event would be raised on a bus with no
        //   subscribers — TriangleAlphaData would never update.  Singletons in the
        //   container prevent that class of bug.
        //
        _vm = Ioc.Default.GetRequiredService<PointControlViewModel>();

        // Setting DataContext before InitializeComponent() means the XAML compiled
        // bindings resolve against the correct type on first render — no flicker or
        // "binding not found" warnings in the output window.
        DataContext = _vm;

        InitializeComponent();
        BuildCheckerPattern();
    }

    // ── Checker pattern ───────────────────────────────────────────────────────────
    //
    // This is pure VIEW logic — the checker is decorative and has no domain meaning.
    // It lives here in code-behind (not in the ViewModel) because:
    //   • It requires Avalonia types (Rectangle, Canvas, SolidColorBrush).
    //   • The ViewModel must not reference the Avalonia.Controls namespace.
    //   • A double-nested for-loop in XAML would require an ItemsControl + custom panel
    //     + item source just for static decoration — far more complex than 12 lines here.
    //
    private void BuildCheckerPattern()
    {
        if (this.FindControl<Canvas>("PadCanvas") is not { } canvas) return;

        const int tileSize = 24;
        const int cols     = 9;   // 9 × 24 = 216 px = PointControlViewModel.CanvasSize

        var lightBrush = new SolidColorBrush(Color.Parse("#2C2C2C"));

        for (int row = 0; row < cols; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if ((row + col) % 2 != 0) continue;    // every other cell = checker pattern

                var rect = new Rectangle
                {
                    Width            = tileSize,
                    Height           = tileSize,
                    Fill             = lightBrush,
                    IsHitTestVisible = false,   // clicks pass through to the Canvas below
                };
                Canvas.SetLeft(rect, col * tileSize);
                Canvas.SetTop(rect,  row * tileSize);
                canvas.Children.Insert(0, rect);    // z-index 0: renders behind crosshairs and dot
            }
        }
    }

    // ── Canvas pointer handling ───────────────────────────────────────────────────
    //
    // WHY pointer handling lives HERE (View) not in the ViewModel:
    //
    //   PointerPressedEventArgs, PointerEventArgs, etc. are Avalonia-specific types.
    //   Referencing them in the ViewModel would couple it to the UI framework, making
    //   it impossible to unit-test without an Avalonia runtime.
    //
    //   Instead, the View acts as a thin TRANSLATOR:
    //     Raw event (Avalonia types)  →  simple method call (plain doubles)
    //
    //   The ViewModel only ever sees primitive doubles — framework-agnostic.

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        var pos = e.GetPosition(canvas);

        // Immediately update the dot so the visual responds on first click.
        _vm.SetFromCanvasPoint(pos.X, pos.Y);

        // Capture the pointer: even if the cursor leaves the canvas boundary during
        // a fast drag, we keep receiving PointerMoved events until Capture(null).
        e.Pointer.Capture(canvas);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        if (!e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(canvas);
        // Live preview — updates X/Y (and therefore the crosshair and dot) but does
        // NOT raise the DotReleased event.  See PointControlViewModel.SetFromCanvasPoint
        // for the full explanation of why publishing is deferred to release.
        _vm.SetFromCanvasPoint(pos.X, pos.Y);
        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Release pointer capture — PointerMoved events stop arriving.
        e.Pointer.Capture(null);

        // ── THE INTEGRATION POINT ─────────────────────────────────────────────────
        //
        // The user has finished their drag gesture.  We notify the ViewModel, which
        // is then responsible for deciding what "pointer released" means in domain
        // terms — in this case, raising the DotReleased event on DotPositionEventBus.
        //
        // WHY _vm.CommitPosition() and not _dotBus.Publish() directly here:
        //
        //   Raising an event directly from the View would mean the View knows about
        //   the event bus and DotReleasedEventArgs — domain/application types.  The
        //   View is a UI concern; it should not need to know about the buses or the
        //   event-args contracts.  The ViewModel owns that knowledge.  The View's
        //   job is just to say "the user released the pointer", and the ViewModel
        //   decides what to do.
        //
        //   This maintains the MVVM boundary cleanly.
        //
        _vm.CommitPosition();

        e.Handled = true;
    }

    // ── Value box Enter-key commit ────────────────────────────────────────────────
    //
    // The text boxes use UpdateSourceTrigger=LostFocus (via ReflectionBinding).
    // Pressing Enter clears focus → LostFocus fires → binding commits to ViewModel.
    // This gives a "press Enter to confirm" experience without any extra ViewModel code.
    //
    private void OnValueBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
