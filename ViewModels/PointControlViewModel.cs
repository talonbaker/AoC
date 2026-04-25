using System;
using System.Globalization;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SquareClickerPointer.Models;

namespace SquareClickerPointer.ViewModels;

public partial class PointControlViewModel : ViewModelBase
{
    // ── Canvas layout constants ───────────────────────────────────────────────────
    public const double CanvasSize  = 216.0;
    public const double DotDiameter = 16.0;
    private const double DotRadius  = DotDiameter / 2.0;
    private const double MaxValue   = 10.0;

    // ── Suppress flags ────────────────────────────────────────────────────────────
    // _suppressLockSync stops the lock callback from bouncing X↔Y indefinitely.
    // _suppressFire stops OnXChanged / OnYChanged from each firing DotMoved when
    // the caller (SetFromCanvasPoint, ApplyPreset, text-box commit) wants to fire
    // exactly once at the end of the whole operation.
    private bool _suppressLockSync;
    private bool _suppressFire;

    // ── DotMoved event ────────────────────────────────────────────────────────────
    /// <summary>
    /// Raised once per logical user gesture whenever the dot position is committed.
    /// Subscribe from any other control or view model that needs to react to movement.
    /// </summary>
    public event Action<double, double>? DotMoved;

    private void FireDotMoved() => DotMoved?.Invoke(X, Y);

    // ── Core position properties ──────────────────────────────────────────────────
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
    // Bound to the value boxes on both tabs.  Uses ReflectionBinding in the AXAML
    // so UpdateSourceTrigger=LostFocus works (compiled bindings don't support it).
    public string XText
    {
        get => X.ToString("F2", CultureInfo.InvariantCulture);
        set
        {
            if (!double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double v)) return;
            _suppressFire = true;
            X = Math.Clamp(v, 0, MaxValue);
            _suppressFire = false;
            FireDotMoved();
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
            FireDotMoved();
        }
    }

    // ── Canvas pixel positions ────────────────────────────────────────────────────
    public double CanvasDotX => X / MaxValue * CanvasSize - DotRadius;
    public double CanvasDotY => (1.0 - Y / MaxValue) * CanvasSize - DotRadius;

    // Crosshair line endpoints — Avalonia.Point, consumed by Shapes.Line via ReflectionBinding
    public Point VerticalLineStart   => new(X / MaxValue * CanvasSize, 0);
    public Point VerticalLineEnd     => new(X / MaxValue * CanvasSize, CanvasSize);
    public Point HorizontalLineStart => new(0, (1.0 - Y / MaxValue) * CanvasSize);
    public Point HorizontalLineEnd   => new(CanvasSize, (1.0 - Y / MaxValue) * CanvasSize);

    // ── Lock synchronisation ──────────────────────────────────────────────────────
    // When one slider moves while locked, the other tracks it.
    // _suppressFire is set while syncing so that only the *initiating* handler fires DotMoved.
    partial void OnXChanged(double value)
    {
        if (IsLocked && !_suppressLockSync)
        {
            _suppressLockSync = true;
            bool prevFire = _suppressFire;
            _suppressFire = true;   // prevent OnYChanged from also firing
            Y = value;
            _suppressFire = prevFire;
            _suppressLockSync = false;
        }

        if (!_suppressFire)
            FireDotMoved();
    }

    partial void OnYChanged(double value)
    {
        if (IsLocked && !_suppressLockSync)
        {
            _suppressLockSync = true;
            bool prevFire = _suppressFire;
            _suppressFire = true;   // prevent OnXChanged from also firing
            X = value;
            _suppressFire = prevFire;
            _suppressLockSync = false;
        }

        if (!_suppressFire)
            FireDotMoved();
    }

    // ── Canvas drag entry point ───────────────────────────────────────────────────
    /// <summary>
    /// Called by the canvas code-behind on pointer press/move.
    /// Converts pixel coordinates (0..CanvasSize) into the 0-10 domain and
    /// fires DotMoved exactly once after both axes are updated.
    /// </summary>
    public void SetFromCanvasPoint(double pixelX, double pixelY)
    {
        _suppressLockSync = true;
        _suppressFire = true;
        X = Math.Clamp(pixelX / CanvasSize * MaxValue, 0, MaxValue);
        Y = Math.Clamp((1.0 - pixelY / CanvasSize) * MaxValue, 0, MaxValue);
        _suppressLockSync = false;
        _suppressFire = false;
        FireDotMoved();
    }

    // ── Presets ───────────────────────────────────────────────────────────────────
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
        FireDotMoved();
    }

    // ── Utility commands ──────────────────────────────────────────────────────────
    [RelayCommand]
    private void GoToCenter()
    {
        _suppressLockSync = true;
        _suppressFire = true;
        X = MaxValue / 2.0;
        Y = MaxValue / 2.0;
        _suppressLockSync = false;
        _suppressFire = false;
        FireDotMoved();
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
        FireDotMoved();
    }

    [RelayCommand]
    private void GoToMaxX()
    {
        _suppressFire = true;
        X = MaxValue;
        _suppressFire = false;
        FireDotMoved();
    }

    [RelayCommand]
    private void GoToMaxY()
    {
        _suppressFire = true;
        Y = MaxValue;
        _suppressFire = false;
        FireDotMoved();
    }
}
