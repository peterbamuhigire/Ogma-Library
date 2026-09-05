using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>Presentation wrapper for one explicit relocation-review decision.</summary>
public sealed class ReconciliationReviewItemViewModel : INotifyPropertyChanged
{
    private string? _selectedPath;

    /// <summary>Initializes a review item without pre-selecting a path.</summary>
    public ReconciliationReviewItemViewModel(ReconciliationReviewDescriptor review)
    {
        Review = review ?? throw new ArgumentNullException(nameof(review));
        CandidatePaths = review.CandidatePaths;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The immutable service descriptor.</summary>
    public ReconciliationReviewDescriptor Review { get; }

    /// <summary>The database review identity.</summary>
    public long ReviewId => Review.ReviewId;

    /// <summary>The occurrence identity shown to the operator.</summary>
    public string FileOccurrenceId => Review.FileOccurrenceId;

    /// <summary>The stable reason code.</summary>
    public string ReasonCode => Review.ReasonCode;

    /// <summary>Safe root-relative paths retained by reconciliation.</summary>
    public IReadOnlyList<string> CandidatePaths { get; }

    /// <summary>The operator-selected candidate, if any.</summary>
    public string? SelectedPath
    {
        get => _selectedPath;
        set
        {
            if (!string.Equals(_selectedPath, value, StringComparison.Ordinal))
            {
                _selectedPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAccept));
            }
        }
    }

    /// <summary>True when the operator has chosen a candidate.</summary>
    public bool CanAccept => !string.IsNullOrWhiteSpace(SelectedPath);

    /// <summary>The review creation timestamp.</summary>
    public DateTimeOffset CreatedUtc => Review.CreatedUtc;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
