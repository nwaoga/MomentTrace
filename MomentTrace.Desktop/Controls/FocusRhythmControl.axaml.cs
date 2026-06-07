using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MomentTrace.Presentation;

namespace MomentTrace.Desktop.Controls;

public partial class FocusRhythmControl : UserControl
{
    public static readonly StyledProperty<IEnumerable<FocusBlockViewModel>?> ItemsProperty =
        AvaloniaProperty.Register<FocusRhythmControl, IEnumerable<FocusBlockViewModel>?>(nameof(Items));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FocusRhythmControl, string?>(nameof(Title), "Focus rhythm");

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

    public ICommand? ItemCommand
    {
        get => GetValue(ItemCommandProperty);
        set => SetValue(ItemCommandProperty, value);
    }
}
