using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace MomentTrace.Desktop;

public sealed class FirstRunOnboardingWindow : Window
{
    private readonly DesktopWindowPreferences _preferences;
    private readonly DesktopStartupRegistration _startupRegistration;
    private readonly CheckBox _adultFilterCheckBox;
    private readonly CheckBox _startWithWindowsCheckBox;

    public FirstRunOnboardingWindow(DesktopWindowPreferences preferences)
    {
        _preferences = preferences;
        _startupRegistration = new DesktopStartupRegistration("MomentTrace.Desktop", Environment.ProcessPath ?? AppContext.BaseDirectory);
        var palette = DesktopAppThemePalette.Find(preferences.Theme);
        palette.ApplyTo(this);

        Title = "Welcome to MomentTrace";
        Width = 560;
        Height = 520;
        MinWidth = 520;
        MinHeight = 500;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        Background = Brushes.Transparent;

        _adultFilterCheckBox = BuildCheckBox("Exclude adult websites before saving history", preferences.ExcludeAdultWebsites, palette);
        _startWithWindowsCheckBox = BuildCheckBox("Start MomentTrace automatically when you sign in", _startupRegistration.IsEnabled(), palette);

        Content = new Border
        {
            Background = new SolidColorBrush(palette.Base),
            BorderBrush = new SolidColorBrush(palette.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24),
            Child = BuildContent(palette)
        };
    }

    private StackPanel BuildContent(DesktopAppThemePalette palette)
    {
        var continueButton = new Button
        {
            Content = "Start using MomentTrace",
            Background = new SolidColorBrush(palette.Accent),
            BorderBrush = new SolidColorBrush(palette.Accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Foreground = new SolidColorBrush(palette.AccentText),
            Padding = new Thickness(18, 10),
            MinWidth = 176,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        continueButton.Click += (_, _) => Complete();

        return new StackPanel
        {
            Spacing = 18,
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Welcome to MomentTrace",
                            Foreground = new SolidColorBrush(palette.Text),
                            FontSize = 26,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = "A private memory timeline for what happened on this device.",
                            Foreground = new SolidColorBrush(palette.MutedText),
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                },
                BuildInfoPanel(
                    "Local-first by default",
                    "Your activity history, screenshots, settings, privacy filters, icons, and cached previews stay under your local MomentTrace app data folder.",
                    palette),
                BuildInfoPanel(
                    "You stay in control",
                    "Settings includes export, activity deletion, and full local-data cleanup when you want to remove MomentTrace data from this device.",
                    palette),
                new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        _adultFilterCheckBox,
                        _startWithWindowsCheckBox
                    }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { continueButton }
                }
            }
        };
    }

    private static Border BuildInfoPanel(string title, string body, DesktopAppThemePalette palette)
    {
        return new Border
        {
            Background = new SolidColorBrush(palette.Card),
            BorderBrush = new SolidColorBrush(palette.Stroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = new SolidColorBrush(palette.Text),
                        FontSize = 14,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = body,
                        Foreground = new SolidColorBrush(palette.MutedText),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 18
                    }
                }
            }
        };
    }

    private static CheckBox BuildCheckBox(string text, bool isChecked, DesktopAppThemePalette palette)
    {
        return new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            Foreground = new SolidColorBrush(palette.Text),
            FontSize = 13
        };
    }

    private void Complete()
    {
        _preferences.ExcludeAdultWebsites = _adultFilterCheckBox.IsChecked == true;
        _preferences.HasCompletedFirstRunOnboarding = true;
        _preferences.Save();
        _startupRegistration.SetEnabled(_startWithWindowsCheckBox.IsChecked == true);
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Complete();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
