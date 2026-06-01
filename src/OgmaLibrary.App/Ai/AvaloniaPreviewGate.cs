using Avalonia.Controls;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.App.Views.Ai;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.App.Ai;

/// <summary>Bridges the gateway preview gate to the Avalonia payload-preview dialog.</summary>
public sealed class AvaloniaPreviewGate : IAiPreviewGate
{
    private readonly ILocalizationService _localization;
    private readonly Func<Window?> _ownerProvider;
    private readonly Func<PayloadPreviewDialog> _dialogFactory;

    /// <summary>Initializes a new instance of <see cref="AvaloniaPreviewGate"/>.</summary>
    public AvaloniaPreviewGate(
        ILocalizationService localization,
        Func<Window?> ownerProvider,
        Func<PayloadPreviewDialog>? dialogFactory = null)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(ownerProvider);
        _localization = localization;
        _ownerProvider = ownerProvider;
        _dialogFactory = dialogFactory ?? (() => new PayloadPreviewDialog());
    }

    /// <inheritdoc />
    public async Task<AiPreviewDecision> ShowAsync(AiPayloadPreview preview, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        Window owner = _ownerProvider()
            ?? throw new InvalidOperationException("Payload preview requires an active owner window.");
        PayloadPreviewDialog dialog = _dialogFactory();
        dialog.DataContext = new PayloadPreviewViewModel(preview, _localization);
        AiPreviewDecision? decision = await dialog
            .ShowDialog<AiPreviewDecision?>(owner)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return decision ?? AiPreviewDecision.Cancel;
    }
}
