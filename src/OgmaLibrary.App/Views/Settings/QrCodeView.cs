using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using QRCoder;

namespace OgmaLibrary.App.Views.Settings;

/// <summary>
/// Renders a QR code as real vector modules (Sept-23 Phase 09, task 9.9) instead of the former
/// 4 px monospace text. Dark modules on a light quiet zone (4 modules, per ISO/IEC 18004) in
/// every theme, with a minimum module size so a phone camera can resolve it from the screen.
/// </summary>
public sealed class QrCodeView : Control
{
    /// <summary>The text encoded in the QR code (for example a join URI).</summary>
    public static readonly StyledProperty<string?> PayloadProperty =
        AvaloniaProperty.Register<QrCodeView, string?>(nameof(Payload));

    /// <summary>The minimum drawn size of one module, in device-independent pixels.</summary>
    public const double MinimumModuleSize = 4;

    /// <summary>The quiet zone width, in modules.</summary>
    public const int QuietZoneModules = 4;

    private bool[,]? _modules;

    static QrCodeView()
    {
        AffectsRender<QrCodeView>(PayloadProperty);
        AffectsMeasure<QrCodeView>(PayloadProperty);
    }

    /// <summary>The encoded text.</summary>
    public string? Payload
    {
        get => GetValue(PayloadProperty);
        set => SetValue(PayloadProperty, value);
    }

    /// <summary>The module matrix side length including the quiet zone (0 when empty).</summary>
    public int ModuleCount => _modules?.GetLength(0) ?? 0;

    /// <summary>Builds the module matrix (including the quiet zone) for a payload.</summary>
    /// <param name="payload">The text to encode.</param>
    /// <returns>The matrix, or null for an empty payload.</returns>
    public static bool[,]? BuildModules(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        int size = data.ModuleMatrix.Count;
        int total = size + (2 * QuietZoneModules);
        var modules = new bool[total, total];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < data.ModuleMatrix[y].Count; x++)
            {
                modules[y + QuietZoneModules, x + QuietZoneModules] = data.ModuleMatrix[y][x];
            }
        }

        return modules;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PayloadProperty)
        {
            _modules = BuildModules(Payload);
            AutomationProperties.SetHelpText(this, Payload);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double side = ModuleCount * MinimumModuleSize;
        return new Size(side, side);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        bool[,]? modules = _modules;
        if (modules is null)
        {
            return;
        }

        int count = modules.GetLength(0);
        double module = Math.Max(MinimumModuleSize, Math.Floor(Math.Min(Bounds.Width, Bounds.Height) / count));
        IBrush light = Resolve("Brush.Qr.Background", Brushes.White);
        IBrush dark = Resolve("Brush.Qr.Module", Brushes.Black);
        context.DrawRectangle(light, null, new Rect(0, 0, count * module, count * module));
        for (int y = 0; y < count; y++)
        {
            for (int x = 0; x < count; x++)
            {
                if (modules[y, x])
                {
                    context.DrawRectangle(dark, null, new Rect(x * module, y * module, module, module));
                }
            }
        }
    }

    private IBrush Resolve(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is IBrush brush ? brush : fallback;
}
