using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.App.ViewModels.About;

namespace OgmaLibrary.App.Views.About;

/// <summary>
/// The About Ogma Library dialog. It draws its own frame, closes on Esc or Close, moves when
/// its hero band is dragged, and rises into place when the OS allows animations.
/// </summary>
public sealed partial class AboutWindow : Window
{
    private readonly Border? _root;
    private readonly Button? _closeButton;

    /// <summary>Initializes a new instance of the <see cref="AboutWindow"/> class.</summary>
    public AboutWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _root = this.FindControl<Border>("Root");
        _closeButton = this.FindControl<Button>("CloseButton");
        Opened += OnOpened;
        KeyDown += OnKeyDown;
    }

    /// <summary>Whether the entrance animation runs (false when the OS asks for reduced motion).</summary>
    public bool IsMotionAllowed { get; init; } = AboutMotion.IsMotionAllowed();

    private AboutViewModel? ViewModel => DataContext as AboutViewModel;

    private void OnOpened(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => _closeButton?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
        if (IsMotionAllowed && _root is not null)
        {
            UiActions.Run(() => RiseAsync(_root), "about.entrance");
        }
    }

    private Task RiseAsync(Border root)
    {
        double rise = this.TryFindResource("About.Motion.Rise", ActualThemeVariant, out object? value) && value is double d ? d : 12;
        double milliseconds = this.TryFindResource("Motion.Duration.Base", ActualThemeVariant, out object? ms) && ms is double m ? m : 200;
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, 0d), new Setter(TranslateTransform.YProperty, rise) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, 1d), new Setter(TranslateTransform.YProperty, 0d) },
                },
            },
        };
        return animation.RunAsync(root);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Hero_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Link_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AboutLink link } && ViewModel is { } viewModel)
        {
            e.Handled = true;
            UiActions.Run(() => viewModel.OpenAsync(link), "about.link.open");
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }
}

/// <summary>
/// Whether decorative motion may run: off when <c>OGMA_REDUCED_MOTION=1</c> or, on Windows,
/// when "Show animations in Windows" is off. (Phase 09 introduces a shared MotionPreference;
/// this dialog-local check keeps the About lane independent until it lands.)
/// </summary>
internal static class AboutMotion
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>Whether decorative motion is allowed.</summary>
    /// <returns><see langword="true"/> when the entrance may animate.</returns>
    public static bool IsMotionAllowed()
    {
        string? reduced = Environment.GetEnvironmentVariable("OGMA_REDUCED_MOTION");
        if (string.Equals(reduced, "1", StringComparison.Ordinal) ||
            string.Equals(reduced, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out int enabled, 0) || enabled != 0;
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return true;
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);
}
