using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using MomentTrace.Presentation;

namespace MomentTrace.Desktop.Controls;

public partial class FocusRhythmControl : UserControl
{
    private DispatcherTimer? _scrollAnimationTimer;
    private double _scrollAnimationFrom;
    private double _scrollAnimationTo;
    private readonly Stopwatch _scrollAnimationClock = new();
    private Point _dragStartPoint;
    private Vector _dragStartOffset;
    private bool _isDragging;

    public static readonly StyledProperty<IEnumerable<FocusBlockViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<FocusRhythmControl, IEnumerable<FocusBlockViewModel>?>(nameof(Items));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FocusRhythmControl, string?>(nameof(Title), "Focus rhythm");

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<FocusRhythmControl, string?>(nameof(Subtitle), "Your activity flow throughout the day");

    public static readonly StyledProperty<ICommand?> ItemCommandProperty =
        AvaloniaProperty.Register<FocusRhythmControl, ICommand?>(nameof(ItemCommand));

    public FocusRhythmControl()
    {
        InitializeComponent();
    }

    public IEnumerable<FocusBlockViewModel>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public ICommand? ItemCommand
    {
        get => GetValue(ItemCommandProperty);
        set => SetValue(ItemCommandProperty, value);
    }

    public void ResetScrollPosition()
    {
        _scrollAnimationTimer?.Stop();
        FocusScrollViewer.Offset = new Vector(0, FocusScrollViewer.Offset.Y);
    }

    private void ScrollLeftButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollBy(-GetScrollStep());
    }

    private void ScrollRightButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollBy(GetScrollStep());
    }

    private double GetScrollStep()
    {
        return FocusScrollViewer.Bounds.Width > 0
            ? Math.Max(220d, FocusScrollViewer.Bounds.Width * 0.78d)
            : 320d;
    }

    private void ScrollBy(double delta)
    {
        var maxOffset = Math.Max(0d, FocusScrollViewer.Extent.Width - FocusScrollViewer.Viewport.Width);
        var nextX = Math.Clamp(FocusScrollViewer.Offset.X + delta, 0d, maxOffset);
        AnimateScrollTo(nextX);
    }

    private void FocusScrollViewer_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(FocusScrollViewer).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        _dragStartPoint = e.GetPosition(FocusScrollViewer);
        _dragStartOffset = FocusScrollViewer.Offset;
        _isDragging = false;
    }

    private void FocusScrollViewer_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(FocusScrollViewer).Properties.IsLeftButtonPressed is false)
        {
            ReleaseDrag(e);
            return;
        }

        var delta = e.GetPosition(FocusScrollViewer) - _dragStartPoint;
        if (!_isDragging && Math.Abs(delta.X) < 4)
        {
            return;
        }

        if (!_isDragging)
        {
            _isDragging = true;
            e.Pointer.Capture(FocusScrollViewer);
        }

        var maxOffset = Math.Max(0, FocusScrollViewer.Extent.Width - FocusScrollViewer.Viewport.Width);
        var nextX = Math.Clamp(_dragStartOffset.X - delta.X, 0, maxOffset);
        FocusScrollViewer.Offset = new Vector(nextX, FocusScrollViewer.Offset.Y);
        e.Handled = true;
    }

    private void FocusScrollViewer_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ReleaseDrag(e);
    }

    private void ReleaseDrag(PointerEventArgs e)
    {
        e.Pointer.Capture(null);
        _isDragging = false;
    }

    private void AnimateScrollTo(double targetX)
    {
        _scrollAnimationFrom = FocusScrollViewer.Offset.X;
        _scrollAnimationTo = targetX;

        if (Math.Abs(_scrollAnimationTo - _scrollAnimationFrom) < 1d)
        {
            FocusScrollViewer.Offset = new Vector(_scrollAnimationTo, FocusScrollViewer.Offset.Y);
            return;
        }

        _scrollAnimationTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _scrollAnimationTimer.Tick -= ScrollAnimationTimer_Tick;
        _scrollAnimationTimer.Tick += ScrollAnimationTimer_Tick;

        _scrollAnimationClock.Restart();
        _scrollAnimationTimer.Start();
    }

    private void ScrollAnimationTimer_Tick(object? sender, EventArgs e)
    {
        const double durationMs = 220d;

        var progress = Math.Clamp(_scrollAnimationClock.Elapsed.TotalMilliseconds / durationMs, 0d, 1d);
        var eased = 1d - Math.Pow(1d - progress, 3d);
        var nextX = _scrollAnimationFrom + ((_scrollAnimationTo - _scrollAnimationFrom) * eased);
        FocusScrollViewer.Offset = new Vector(nextX, FocusScrollViewer.Offset.Y);

        if (progress >= 1d)
        {
            _scrollAnimationTimer?.Stop();
            FocusScrollViewer.Offset = new Vector(_scrollAnimationTo, FocusScrollViewer.Offset.Y);
        }
    }
}
