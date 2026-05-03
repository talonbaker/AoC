using System;
using System.Globalization;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SquareClickerPointer.Messages;
using SquareClickerPointer.Models;

namespace SquareClickerPointer.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
//  PointControlViewModel  —  the brain of the SquareClickerPointer control
// ═══════════════════════════════════════════════════════════════════════════════
//
//  ROLE IN MVVM
//  ────────────
//  The ViewModel is the middleman between the data (Models) and the display (View).
//  Its two responsibilities:
//    1. Hold and transform STATE so the View can display it via data-binding.
//    2. Handle COMMANDS and interactions forwarded from the View.
//
//  It must NOT reference any Avalonia UI controls directly (Canvas, Button, etc.).
//  Keeping that boundary clean means this class can be tested in a console project
//  with no window system at all — you just call methods and assert properties.
//
//  DATA FLOW OVERVIEW
//  ──────────────────
//
//  ┌─────────────────────────────────────────────────────────────────────────┐
//  │                         PointControlViewModel                           │
//  │                                                                         │
//  │  INPUT (how state gets in):                                             │
//  │    • SetFromCanvasPoint(pixelX, pixelY)  ← called by View on drag      │
//  │    • CommitPosition()                    ← called by View on release   │
//  │    • XText / YText setters               ← bound to text boxes         │
//  │    • X / Y directly                      ← bound to sliders            │
//  │    • ApplyPresetCommand                  ← bound to preset buttons     │
//  │    • GoToCenterCommand / GoToMaxCommand  ← bound to icon buttons       │
//  │                                                                         │
//  │  OUTPUT (how state gets out):                                           │
//  │    • [ObservableProperty] X, Y           → binds to crosshair & dot    │
//  │    • CanvasDotX/Y, Line endpoints        → derived pixel positions     │
//  │    • XText / YText                       → formatted display strings   │
//  │    • DotReleasedMessage via IMessenger   → any subscriber, zero-cost   │
//  └─────────────────────────────────────────────────────────────────────────┘
//
//  WHY IMessenger INSTEAD OF THE OLD DotMoved EVENT
//  ─────────────────────────────────────────────────
//  The previous version had:
//      public event Action<double, double>? DotMoved;
//
//  That event required an outside caller (MainWindow, or another View) to:
//    a) Have a reference to a PointControlView/ViewModel object.
//    b) Explicitly subscribe/unsubscribe.
//    c) Know the method signature.
//
//  With IMessenger:
//    • This class publishes one line: _messenger.Send(new DotReleasedMessage(X, Y))
//    • It does not know who (if anyone) is listening.
//    • TriangleAlphaDataViewModel can subscribe without either class having
//      a reference to the other.
//    • Adding a third subscriber (logging, analytics, another panel) costs zero
//      changes here.

/// <summary>
/// ViewModel for the SquareClickerPointer (PointControlView) user control.
/// Manages the 2-D dot position in the 0–10 domain and publishes a
/// <see cref="DotReleasedMessage"/> when the user commits a canvas gesture.
/// </summary>
public partial class PointControlViewModel : ViewModelBase
{
    // ── Canvas layout constants ───────────────────────────────────────────────────
    //
    // These are PUBLIC so that PointControlView.cs can reference them when it needs
    // to know canvas dimensions — avoids duplicating the magic number 216 in the View.
    // They are const so the compiler inlines them (zero runtime cost).
    //
    public const double CanvasSize  = 216.0;
    public const double DotDiameter = 16.0;
    private const double DotRadius  = DotDiameter / 2.0;   // used to center the ellipse
    private const double MaxValue   = 10.0;                 // domain upper bound

    // ── Suppress flags ────────────────────────────────────────────────────────────
    //
    // WHY these exist — the "cascade prevention" problem:
    //
    //   When IsLocked is true, changing X must immediately sync Y to match, and
    //   vice versa.  The sync runs inside OnXChanged / OnYChanged.  Without guards,
    //   the cycle would be:
    //
    //     set X = 7.0
    //       → OnXChanged fires
    //         → set Y = 7.0  (because IsLocked)
    //           → OnYChanged fires
    //             → set X = 7.0  (already 7.0, but assignment still calls OnXChanged)
    //               → OnXChanged fires … infinite loop → StackOverflowException
    //
    //   _suppressLockSync: raised to true at the top of the sync block so the
    //   responding handler sees it and skips the reverse-sync.
    //
    //   _suppressFire: raised to true when a caller sets BOTH X and Y in a row
    //   (e.g. SetFromCanvasPoint, GoToCenter) so that neither OnXChanged nor
    //   OnYChanged publishes DotReleasedMessage independently; the caller publishes
    //   exactly once via CommitPosition() at the end.
    //
    private bool _suppressLockSync;
    private bool _suppressFire;

    // ── IMessenger — injected via the DI container ────────────────────────────────
    //
    // WHY the field type is IMessenger (interface) not WeakReferenceMessenger (concrete):
    //
    //   Coding against an interface is the Dependency Inversion Principle (the 'D' in
    //   SOLID).  It means:
    //     • Unit tests can pass a fake/no-op IMessenger that just records what was sent,
    //       without spinning up the real global messenger.
    //     • If we ever want to swap WeakReferenceMessenger for StrongReferenceMessenger
    //       (or a custom implementation), we change ONE line in App.axaml.cs — not here.
    //
    private readonly IMessenger _messenger;

    // ── Constructor — receives all dependencies from the DI container ─────────────
    //
    // WHY a constructor parameter instead of using WeakReferenceMessenger.Default directly:
    //
    //   Constructor injection makes dependencies VISIBLE.  Anyone reading this class
    //   can immediately see "this class needs an IMessenger to function."  There are no
    //   hidden global references buried inside methods.  The DI container (App.axaml.cs)
    //   is the single place that decides what IMessenger implementation to use and
    //   provides it here.
    //
    //   NEVER call `new PointControlViewModel()` yourself — the constructor requires
    //   an IMessenger argument.  Always let Ioc.Default.GetRequiredService<PointControlViewModel>()
    //   create the instance so the container wires up the dependency.
    //
    public PointControlViewModel(IMessenger messenger)
    {
        _messenger = messenger;
    }

    // ── Core position properties ──────────────────────────────────────────────────
    //
    // [ObservableProperty] is a CommunityToolkit.Mvvm source-generator attribute.
    //
    //   WHAT IT GENERATES (roughly):
    //     public double X
    //     {
    //         get => _x;
    //         set { if (_x == value) return; _x = value; OnPropertyChanged(nameof(X)); OnXChanged(value); }
    //     }
    //
    //   WHAT [NotifyPropertyChangedFor(nameof(...))] DOES:
    //   Adds an additional OnPropertyChanged("CanvasDotX") call inside the generated
    //   setter, so XAML bindings on CanvasDotX automatically refresh when X changes.
    //   Without it, moving the slider would change X but the dot on the canvas would
    //   not move until something else triggered a re-render.
    //
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasDotX))]
    [NotifyPropertyChangedFor(nameof(VerticalLineStart))]
    [NotifyPropertyChangedFor(nameof(VerticalLineEnd))]
    [NotifyPropertyChangedFor(nameof(XText))]
    private double _x = 5.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanvasDotY))]
    [NotifyPropertyChangedFor(nameof(HorizontalLineStart))]
    [NotifyPropertyChangedFor(nameof(HorizontalLineEnd))]
    [NotifyPropertyChangedFor(nameof(YText))]
    private double _y = 5.0;

    [ObservableProperty]
    private bool _isLocked;

    // ── Editable text-box strings ─────────────────────────────────────────────────
    //
    // WHY string properties on the ViewModel (rather than value converters in XAML):
    //
    //   The ViewModel owns how data is *formatted for display*.  A converter lives
    //   in the View layer and is harder to unit-test.  Putting format logic here
    //   keeps it in one place and fully testable.
    //
    // WHY ReflectionBinding in the AXAML (not the default compiled binding):
    //
    //   Avalonia's compiled bindings ({Binding}) do not support
    //   UpdateSourceTrigger=LostFocus on string-typed properties.  ReflectionBinding
    //   is slightly slower at startup but supports all binding modes.  We use it only
    //   on these two text boxes; everything else uses compiled bindings.
    //
    public string XText
    {
        get => X.ToString("F2", CultureInfo.InvariantCulture);
        set
        {
            if (!double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double v)) return;

            // _suppressFire: we are about to set X which would trigger OnXChanged.
            // We do NOT want OnXChanged to publish a message here — text-box edits
            // are not "canvas releases" and should not drive TriangleAlphaData.
            _suppressFire = true;
            X = Math.Clamp(v, 0, MaxValue);
            _suppressFire = false;
            // NOTE: intentionally no CommitPosition() call here.
            // Only the canvas pointer-release gesture commits to TriangleAlphaData.
        }
    }

    public string YText
    {
        get => Y.ToString("F2", CultureInfo.InvariantCulture);
        set
        {
            if (!double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double v)) return;
            _suppressFire = true;
            Y = Math.Clamp(v, 0, MaxValue);
            _suppressFire = false;
        }
    }

    // ── Canvas pixel positions (derived / computed properties) ────────────────────
    //
    // These properties have NO backing field — they are 100% calculated from X and Y
    // every time they are read.  The [NotifyPropertyChangedFor] attributes on _x/_y
    // ensure the View re-reads them when X or Y changes.
    //
    // WHY pixel math lives in the ViewModel (not the View):
    //   The mapping "domain value 0-10 → pixel 0-216" is a business/domain rule.
    //   It is testable, reusable, and has nothing to do with rendering.  If you moved
    //   it into a XAML converter it would be buried in the View and harder to change.
    //
    // Canvas.Left / Canvas.Top position the TOP-LEFT corner of the ellipse, so we
    // subtract DotRadius to visually center the dot on the logical coordinate.
    public double CanvasDotX => X / MaxValue * CanvasSize - DotRadius;

    // Y is FLIPPED: in screen space Y=0 is the top of the canvas, but in our domain
    // Y=0 is the bottom and Y=10 is the top.  (1.0 - Y/MaxValue) performs the flip.
    public double CanvasDotY => (1.0 - Y / MaxValue) * CanvasSize - DotRadius;

    // Crosshair Line endpoints.  Avalonia.Point is a lightweight struct — fine to
    // return from a property.  Consumed via ReflectionBinding in PointControlView.axaml.
    public Point VerticalLineStart   => new(X / MaxValue * CanvasSize, 0);
    public Point VerticalLineEnd     => new(X / MaxValue * CanvasSize, CanvasSize);
    public Point HorizontalLineStart => new(0,          (1.0 - Y / MaxValue) * CanvasSize);
    public Point HorizontalLineEnd   => new(CanvasSize, (1.0 - Y / MaxValue) * CanvasSize);

    // ── Lock synchronisation ──────────────────────────────────────────────────────
    //
    // partial void OnXChanged / OnYChanged are generated hooks provided by
    // CommunityToolkit.Mvvm.  The source generator calls them at the END of the
    // generated X and Y setters.  We use them to run side-effects when a property
    // changes without overriding the entire generated property.
    //
    partial void OnXChanged(double value)
    {
        // If locked AND we are not already inside a sync round, mirror X into Y.
        if (IsLocked && !_suppressLockSync)
        {
            _suppressLockSync = true;

            // Save and restore _suppressFire so that only the initiating call site
            // (SetFromCanvasPoint, a slider, etc.) decides whether to publish.
            bool prevFire = _suppressFire;
            _suppressFire = true;   // stop OnYChanged from also publishing
            Y = value;
            _suppressFire = prevFire;

            _suppressLockSync = false;
        }
        // _suppressFire guard: if a caller batched X+Y together and set _suppressFire=true
        // before us, we do nothing here.  The caller will call CommitPosition() once.
    }

    partial void OnYChanged(double value)
    {
        if (IsLocked && !_suppressLockSync)
        {
            _suppressLockSync = true;
            bool prevFire = _suppressFire;
            _suppressFire = true;
            X = value;
            _suppressFire = prevFire;
            _suppressLockSync = false;
        }
    }

    // ── Canvas drag entry points ──────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="Views.PointControlView"/> on every pointer-press and
    /// pointer-move while the left button is held.
    ///
    /// Converts canvas pixel coordinates (0..CanvasSize) into the 0–10 domain and
    /// updates X and Y, which immediately refreshes the crosshair and dot in the UI
    /// through data binding.
    ///
    /// WHY this does NOT call CommitPosition():
    ///   During a drag the position changes at ~60 fps.  Publishing a message on
    ///   every frame would flood TriangleAlphaDataViewModel with recalculations that
    ///   are immediately overwritten.  We only publish once the user lifts the mouse
    ///   (see CommitPosition below).
    /// </summary>
    public void SetFromCanvasPoint(double pixelX, double pixelY)
    {
        // Suppress both flags so:
        //   (a) The lock sync does not kick in mid-update — we are setting X and Y
        //       together atomically and the lock-mirror logic is not needed.
        //   (b) OnXChanged / OnYChanged do not independently try to publish.
        _suppressLockSync = true;
        _suppressFire = true;
        X = Math.Clamp(pixelX / CanvasSize * MaxValue, 0, MaxValue);
        Y = Math.Clamp((1.0 - pixelY / CanvasSize) * MaxValue, 0, MaxValue);
        _suppressLockSync = false;
        _suppressFire = false;
        // Do NOT call CommitPosition() here — pointer-move is a live preview, not a commit.
    }

    /// <summary>
    /// Called by <see cref="Views.PointControlView"/> when the pointer is released
    /// from the canvas.  Publishes a <see cref="DotReleasedMessage"/> to the message
    /// bus so that any subscriber can react to the final committed position.
    ///
    /// WHY this is a method on the ViewModel (not fired directly from the View):
    ///
    ///   The decision "a pointer-release on the canvas means: publish a message" is
    ///   a DOMAIN rule, not a UI rule.  The View's job is to translate a raw pointer
    ///   event into a meaningful action — it calls CommitPosition() and lets this
    ///   class decide what that means.  If we put _messenger.Send() in the View's
    ///   code-behind, the View would need to know about IMessenger and message types,
    ///   which are business-layer concerns.
    ///
    ///   This is the Publish side of the Publisher/Subscriber pattern:
    ///   _messenger.Send() routes DotReleasedMessage to every registered recipient
    ///   without this class knowing who they are or how many there are.
    /// </summary>
    public void CommitPosition()
    {
        _messenger.Send(new DotReleasedMessage(X, Y));
    }

    // ── Presets ───────────────────────────────────────────────────────────────────
    //
    // PresetPoint is a record in Models/PresetPoint.cs — an immutable data snapshot.
    // The View binds Button.CommandParameter to Preset1/Preset2 and passes the object
    // back to ApplyPresetCommand.  The ViewModel never touches a UI element directly.
    //
    public PresetPoint Preset1 { get; } = new(2.0, 8.0, "A");
    public PresetPoint Preset2 { get; } = new(8.0, 2.0, "B");

    [RelayCommand]
    private void ApplyPreset(PresetPoint? preset)
    {
        if (preset is null) return;
        _suppressLockSync = true;
        _suppressFire = true;
        X = preset.X;
        Y = preset.Y;
        _suppressLockSync = false;
        _suppressFire = false;
        // Presets do not call CommitPosition() — only canvas drags do.
        // If you want preset clicks to also update TriangleAlphaData, add CommitPosition() here.
    }

    // ── Utility commands ──────────────────────────────────────────────────────────
    //
    // [RelayCommand] generates GoToCenterCommand, GoToMaxCommand, etc. as ICommand
    // properties.  XAML Button.Command binds to ICommand — the View never needs a
    // typed reference to this ViewModel to invoke these.  That keeps the View/ViewModel
    // boundary clean and makes all commands unit-testable without a UI.
    //
    [RelayCommand]
    private void GoToCenter()
    {
        _suppressLockSync = true;
        _suppressFire = true;
        X = MaxValue / 2.0;
        Y = MaxValue / 2.0;
        _suppressLockSync = false;
        _suppressFire = false;
    }

    [RelayCommand]
    private void GoToMax()
    {
        _suppressLockSync = true;
        _suppressFire = true;
        X = MaxValue;
        Y = MaxValue;
        _suppressLockSync = false;
        _suppressFire = false;
    }

    [RelayCommand]
    private void GoToMaxX()
    {
        _suppressFire = true;
        X = MaxValue;
        _suppressFire = false;
    }

    [RelayCommand]
    private void GoToMaxY()
    {
        _suppressFire = true;
        Y = MaxValue;
        _suppressFire = false;
    }
}
