using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application.Metadata;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>Editable presentation wrapper for one pending metadata proposal.</summary>
public sealed class MetadataProposalItemViewModel : INotifyPropertyChanged
{
    private string _editableValue;

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

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
