using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace SquareClickerPointer.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════════
//  ColorSwatchViewModel  —  one selectable colour tile in the colour picker
// ═══════════════════════════════════════════════════════════════════════════════
//
//  DESIGN DECISION: why not a RelayCommand<Color> on ListItemViewModel?
//  ────────────────────────────────────────────────────────────────────
//  Passing a struct value (Color) as a CommandParameter through XAML requires:
//    • Boxing the Color to object (CommandParameter is typed as object?)
//    • A converter or {x:Static} reference to pass the exact Color value
//    • Special handling on the receiving end to unbox safely
//
//  Instead, each ColorSwatchViewModel OWNS its command and pre-captures the
//  Color value in a closure.  The XAML binds to {Binding PickCommand} with
//  no parameter — no boxing, no converter, no XAML magic.
//
//  This pattern is called the "Command per item" pattern.  It trades a few
//  extra objects (12 RelayCommand instances) for dramatically simpler XAML.
//
//  DOES NOT INHERIT ObservableObject
//  ────────────────────────────────────
//  None of its properties change after construction — Brush is set once, PickCommand
//  is set once.  Inheriting ObservableObject would add INotifyPropertyChanged
//  overhead for no benefit.  This is a plain sealed class.

/// <summary>
/// Represents one tile in the colour picker grid.
/// Created by <see cref="ListItemViewModel"/> — one instance per palette entry.
/// </summary>
public sealed class ColorSwatchViewModel
{
    /// <summary>
    /// The colour of this swatch tile, as a ready-to-bind brush.
    /// Bound to the tile button's Background in the picker XAML.
    /// </summary>
    public IBrush Brush { get; }

    /// <summary>
    /// Fires when the user clicks this swatch tile.
    /// Captured closure calls ListItemViewModel.SelectColor(thisColor),
    /// which updates ColorBrush, closes the picker, and publishes the message.
    /// </summary>
    public ICommand PickCommand { get; }

    /// <param name="color">The colour this tile represents.</param>
    /// <param name="onPicked">
    /// Callback into the parent ListItemViewModel.
    /// Called with no parameters — the Color is captured in the closure at
    /// construction time, so the command delegate needs no arguments.
    /// </param>
    public ColorSwatchViewModel(Color color, Action onPicked)
    {
        Brush      = new SolidColorBrush(color);
        PickCommand = new RelayCommand(onPicked);
    }
}
