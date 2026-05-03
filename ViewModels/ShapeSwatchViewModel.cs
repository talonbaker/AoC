using System;
using CommunityToolkit.Mvvm.Input;
using SquareClickerPointer.Models;
using System.Windows.Input;

namespace SquareClickerPointer.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
//  ShapeSwatchViewModel  —  one selectable shape tile in the shape picker
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Same design rationale as ColorSwatchViewModel — "command per item" pattern
//  to avoid passing ShapeType as a CommandParameter through XAML.
//
//  SHAPE DISPLAY HELPERS
//  ──────────────────────
//  The picker XAML renders each shape swatch using the same four-Path Canvas
//  approach used in ListItemView — four overlapping Paths, only one visible.
//  The IsStar / IsHexagon / IsTriangle / IsSquare bool properties drive
//  IsVisible on each Path, exactly as in ListItemViewModel.
//
//  Because Shape never changes after construction, these computed properties
//  never need to raise PropertyChanged.

/// <summary>
/// Represents one tile in the shape picker grid.
/// Created by <see cref="ListItemViewModel"/> — one instance per ShapeType value.
/// </summary>
public sealed class ShapeSwatchViewModel
{
    /// <summary>The shape this tile represents.</summary>
    public ShapeType Shape { get; }

    /// <summary>True when Shape is Star — drives Path visibility in the picker XAML.</summary>
    public bool IsStar     => Shape == ShapeType.Star;

    /// <summary>True when Shape is Hexagon — drives Path visibility in the picker XAML.</summary>
    public bool IsHexagon  => Shape == ShapeType.Hexagon;

    /// <summary>True when Shape is Triangle — drives Path visibility in the picker XAML.</summary>
    public bool IsTriangle => Shape == ShapeType.Triangle;

    /// <summary>True when Shape is Square — drives Path visibility in the picker XAML.</summary>
    public bool IsSquare   => Shape == ShapeType.Square;

    /// <summary>
    /// Fires when the user clicks this shape tile.
    /// Calls ListItemViewModel.SelectShape(thisShape) via captured closure.
    /// </summary>
    public ICommand PickCommand { get; }

    /// <param name="shape">The shape this tile represents.</param>
    /// <param name="onPicked">Callback into the parent ListItemViewModel.</param>
    public ShapeSwatchViewModel(ShapeType shape, Action onPicked)
    {
        Shape      = shape;
        PickCommand = new RelayCommand(onPicked);
    }
}
