namespace CanvaStage.Windows.ViewModels;

public sealed partial class MainViewModel
{
    private bool _matchSlideCapitalization;
    public bool MatchSlideCapitalization
    {
        get => _matchSlideCapitalization;
        set
        {
            if (!SetProperty(ref _matchSlideCapitalization, value)) return;
            _coordinator.SetDocument(Snapshot.Document.WithSlideCapitalization(value));
            OnPropertyChanged(nameof(SlideCapitalizationStatus));
        }
    }

    public string SlideCapitalizationStatus => !MatchSlideCapitalization
        ? "Off. Monitor uses the original exported text."
        : !Snapshot.Document.WasSlideCapitalizationChecked
            ? "On. Apply or Re-download the deck, or run Simple Setup, to check long uppercase sections against the slide images. Existing text is unchanged until then."
            : $"{Snapshot.Document.CapitalizationAdjustedPages} page(s) adjusted; {Snapshot.Document.CapitalizationUnchangedPages} candidate page(s) kept as exported; {Snapshot.Document.CapitalizationUnavailablePages} could not be checked. Review Monitor against Canva. Uncertain text stays unchanged; turning this off restores the original text.";
}
