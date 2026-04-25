using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using SquareClickerPointer.ViewModels;

namespace SquareClickerPointer.Views;

public partial class PointControlView : UserControl
{
    // ── Public event ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Raised whenever the dot position is committed from any input source
    /// (canvas drag, slider, text box, or preset button).
    /// Subscribe from any other control: <c>myControl.DotMoved += (x, y) => { … };</c>
    /// </summary>
    public event Action<double, double>? DotMoved;

    // ── ViewModel reference (kept for clean event wiring / teardown) ─────────────
    private PointControlViewModel? _vm;

    public PointControlView()
    {
        DataContext = new PointControlViewModel();
        InitializeComponent();
        BuildCheckerPattern();
    }

    // ── Checker pattern on the canvas ─────────────────────────────────────────────
    // Builds 41 lighter squares into PadCanvas at z=0 to create the checker effect.
    // The canvas Background property provides the darker base colour (#242424).
    private void BuildCheckerPattern()
    {
        if (this.FindControl<Canvas>("PadCanvas") is not { } canvas) return;

        const int tileSize = 24;
        const int cols     = 9;   // 9 × 24 = 216

        var lightBrush = new SolidColorBrush(Color.Parse("#2C2C2C"));

        for (int row = 0; row < cols; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if ((row + col) % 2 != 0) continue;

                var rect = new Rectangle
                {
                    Width  = tileSize,
                    Height = tileSize,
                    Fill   = lightBrush,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(rect, col * tileSize);
                Canvas.SetTop(rect,  row * tileSize);
                canvas.Children.Insert(0, rect);   // below crosshairs and dot
            }
        }
    }

    // ── Wire / unwire the ViewModel event whenever DataContext changes ────────────
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null)
            _vm.DotMoved -= OnVmDotMoved;

        _vm = DataContext as PointControlViewModel;

        if (_vm is not null)
            _vm.DotMoved += OnVmDotMoved;
    }

    private void OnVmDotMoved(double x, double y) => DotMoved?.Invoke(x, y);

    // ── Canvas pointer handling ───────────────────────────────────────────────────

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Canvas canvas || _vm is null) return;
        var pos = e.GetPosition(canvas);
        _vm.SetFromCanvasPoint(pos.X, pos.Y);
        e.Pointer.Capture(canvas);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Canvas canvas || _vm is null) return;
        if (!e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;
        var pos = e.GetPosition(canvas);
        _vm.SetFromCanvasPoint(pos.X, pos.Y);
        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    // ── Value box Enter-key commit ────────────────────────────────────────────────
    // LostFocus triggers the binding update; pressing Enter clears focus to trigger it.
    private void OnValueBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
