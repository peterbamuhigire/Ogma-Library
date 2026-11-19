using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.App.Infrastructure;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>Editable presentation wrapper for one pending metadata proposal.</summary>
public sealed class MetadataProposalItemViewModel : INotifyPropertyChanged
{
    private string _editableValue;
    private readonly string? _fieldLabel;
    private readonly string? _acceptAccessibleName;
    private readonly string? _rejectAccessibleName;

    /// <summary>Initializes a proposal card for the review panel.</summary>
    public MetadataProposalItemViewModel(
        MetadataProposalDescriptor proposal,
        string acceptLabel,
        string rejectLabel,
        string proposedValueLabel,
        string currentValueLabel,
        string sourceLabel,
        string confidenceLabel)
    {
        Proposal = proposal ?? throw new ArgumentNullException(nameof(proposal));
        _editableValue = proposal.ProposedValue ?? string.Empty;
        AcceptLabel = acceptLabel;
        RejectLabel = rejectLabel;
        ProposedValueLabel = proposedValueLabel;
        CurrentValueLabel = currentValueLabel;
        SourceLabel = sourceLabel;
        ConfidenceLabel = confidenceLabel;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The durable proposal identity and concurrency version.</summary>
    public MetadataProposalDescriptor Proposal { get; }

    /// <summary>Stable database proposal identifier.</summary>
    public long ProposalId => Proposal.Id;

    /// <summary>Canonical metadata field name.</summary>
    public string FieldName => Proposal.FieldName;

    /// <summary>Provider that produced the proposal.</summary>
    public string Source => Proposal.Source;

    /// <summary>Proposal confidence formatted for the current locale.</summary>
    public string Confidence => $"{Proposal.Confidence:P0}";

    /// <summary>Current catalogue value shown for comparison.</summary>
    public string CurrentValue => Proposal.CurrentValue ?? "—";

    /// <summary>Localized accept action label.</summary>
    public string AcceptLabel { get; }

    /// <summary>Localized reject action label.</summary>
    public string RejectLabel { get; }

    /// <summary>Localized proposed-value label.</summary>
    public string ProposedValueLabel { get; }

    /// <summary>Localized current-value label.</summary>
    public string CurrentValueLabel { get; }

    /// <summary>Localized source label.</summary>
    public string SourceLabel { get; }

    /// <summary>Localized confidence label.</summary>
    public string ConfidenceLabel { get; }

    /// <summary>Localised field label, for example "Title"; defaults to the canonical field name.</summary>
    public string FieldLabel
    {
        get => _fieldLabel ?? FieldName;
        init => _fieldLabel = value;
    }

    /// <summary>Accessible name of the Accept button, naming the field.</summary>
    public string AcceptAccessibleName
    {
        get => _acceptAccessibleName ?? AcceptLabel;
        init => _acceptAccessibleName = value;
    }

    /// <summary>Accessible name of the Reject button, naming the field.</summary>
    public string RejectAccessibleName
    {
        get => _rejectAccessibleName ?? RejectLabel;
        init => _rejectAccessibleName = value;
    }

    /// <summary>Localised "current → proposed" summary.</summary>
    public string ChangeText { get; init; } = string.Empty;

    /// <summary>Stable automation id of the Accept button.</summary>
    public string AcceptAutomationId => "Detail.Review.Accept." + FieldName;

    /// <summary>Stable automation id of the Reject button.</summary>
    public string RejectAutomationId => "Detail.Review.Reject." + FieldName;

    /// <summary>Value to apply when the proposal is accepted.</summary>
    public string EditableValue
    {
        get => _editableValue;
        set
        {
            if (_editableValue != value)
            {
                _editableValue = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        UiThreadGuard.Verify(this, name, PropertyChanged);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
