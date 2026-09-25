using Avalonia;
using Avalonia.Controls;
using OgmaLibrary.App.ViewModels.Reader;

namespace OgmaLibrary.App.Views.Reader;

/// <summary>
/// Attached behaviour that reports the hosting window's render scaling to the
/// <see cref="ReaderViewModel"/> so pages are rasterised at device resolution, and
/// re-reports it when the window moves to a monitor with different scaling
/// (Sept-23 Kaizen K33, T04.6).
/// </summary>
public static class ReaderRenderScaling
{
    /// <summary>Enables render-scaling tracking on a control inside the reader view.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(ReaderRenderScaling));

    private static readonly AttachedProperty<TopLevel?> TrackedTopLevelProperty =
        AvaloniaProperty.RegisterAttached<Control, TopLevel?>("TrackedTopLevel", typeof(ReaderRenderScaling));

    private static readonly AttachedProperty<EventHandler?> ScalingHandlerProperty =
        AvaloniaProperty.RegisterAttached<Control, EventHandler?>("ScalingHandler", typeof(ReaderRenderScaling));

    static ReaderRenderScaling()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    /// <summary>Gets whether tracking is enabled.</summary>
    /// <param name="control">The control.</param>
    /// <returns><see langword="true"/> when enabled.</returns>
    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    /// <summary>Sets whether tracking is enabled.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">Whether to track render scaling.</param>
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            control.AttachedToVisualTree += OnAttached;
            control.DetachedFromVisualTree += OnDetached;
            control.DataContextChanged += OnDataContextChanged;
            Attach(control);
        }
        else
        {
            control.AttachedToVisualTree -= OnAttached;
            control.DetachedFromVisualTree -= OnDetached;
            control.DataContextChanged -= OnDataContextChanged;
            Detach(control);
        }
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            Attach(control);
        }
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            Detach(control);
        }
    }

    private static void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
        {
            Report(control, control.GetValue(TrackedTopLevelProperty));
        }
    }

    private static void Attach(Control control)
    {
        Detach(control);
        if (TopLevel.GetTopLevel(control) is not { } topLevel)
        {
            return;
        }

        control.SetValue(TrackedTopLevelProperty, topLevel);
        topLevel.ScalingChanged += OnScalingChanged;
        Report(control, topLevel);

        void OnScalingChanged(object? sender, EventArgs args) => Report(control, topLevel);

        // Keep the handler reachable for Detach without a closure dictionary.
        control.SetValue(ScalingHandlerProperty, OnScalingChanged);
    }

    private static void Detach(Control control)
    {
        if (control.GetValue(TrackedTopLevelProperty) is { } topLevel &&
            control.GetValue(ScalingHandlerProperty) is { } handler)
        {
            topLevel.ScalingChanged -= handler;
        }

        control.SetValue(TrackedTopLevelProperty, null);
        control.SetValue(ScalingHandlerProperty, null);
    }

    private static void Report(Control control, TopLevel? topLevel)
    {
        if (topLevel is not null && control.DataContext is ReaderViewModel viewModel)
        {
            viewModel.UpdateRenderScaling(topLevel.RenderScaling);
        }
    }
}
