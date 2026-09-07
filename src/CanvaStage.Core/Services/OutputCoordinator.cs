using CanvaStage.Core.Models;

namespace CanvaStage.Core.Services;

public sealed class OutputCoordinator : IOutputCoordinator
{
    private PresentationDocument _document;
    private int _selectedPage;
    private bool _isLive;
    private bool _isBlanked;
    private OutputMode _mode;
    private MonitorLayout _layout;
    private NextLineOption _nextLineOption;
    private MonitorAppearance _appearance;

    public OutputCoordinator(PresentationDocument document, AppPreferences preferences)
    {
        _document = document;
        _selectedPage = document.FindPage(preferences.SelectedPage) is null ? document.Pages.First().Number : preferences.SelectedPage;
        _mode = preferences.OutputMode;
        _layout = preferences.MonitorLayout;
        _nextLineOption = preferences.NextLineOption;
        _appearance = preferences.Appearance;
    }

    public event EventHandler<OutputSnapshot>? SnapshotChanged;

    public OutputSnapshot Snapshot => CreateSnapshot();

    public void StartOutput()
    {
        _isLive = true;
        _isBlanked = false;
        Publish();
    }

    public void StopOutput()
    {
        _isLive = false;
        _isBlanked = false;
        Publish();
    }

    public void SetBlanked(bool isBlanked)
    {
        if (!_isLive || _isBlanked == isBlanked)
        {
            return;
        }

        _isBlanked = isBlanked;
        Publish();
    }

    public void SelectPage(int pageNumber)
    {
        if (_document.FindPage(pageNumber) is null || _selectedPage == pageNumber)
        {
            return;
        }

        _selectedPage = pageNumber;
        Publish();
    }

    public void GoPrevious() => SelectPage(_selectedPage - 1);

    public void GoNext() => SelectPage(_selectedPage + 1);

    public void SetOutputMode(OutputMode mode)
    {
        _mode = mode;
        Publish();
    }

    public void SetLayout(MonitorLayout layout)
    {
        _layout = layout;
        Publish();
    }

    public void SetNextLineOption(NextLineOption option)
    {
        _nextLineOption = option;
        Publish();
    }

    public void SetAppearance(MonitorAppearance appearance)
    {
        _appearance = appearance;
        Publish();
    }

    public void SetDocument(PresentationDocument document, int? preferredPageNumber = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Pages.Count == 0)
        {
            throw new ArgumentException("A presentation must have at least one page.", nameof(document));
        }

        _document = document;
        int requestedPage = preferredPageNumber ?? _selectedPage;
        _selectedPage = document.FindPage(requestedPage)?.Number ?? document.Pages.First().Number;
        Publish();
    }

    private OutputSnapshot CreateSnapshot()
    {
        PresentationPage? page = _isLive ? _document.FindPage(_selectedPage) : null;
        int index = _document.Pages.ToList().FindIndex(candidate => candidate.Number == _selectedPage);
        PresentationPage? nextPage = index >= 0 && index + 1 < _document.Pages.Count ? _document.Pages[index + 1] : null;
        string? nextLine = ResolveNextLine(nextPage);

        return new OutputSnapshot(
            _isLive,
            _isBlanked,
            _document,
            _selectedPage,
            page,
            nextLine,
            _mode,
            _layout,
            _nextLineOption,
            _appearance,
            index > 0,
            index >= 0 && index < _document.Pages.Count - 1);
    }

    private string? ResolveNextLine(PresentationPage? nextPage)
    {
        if (_nextLineOption == NextLineOption.Hidden || nextPage is null)
        {
            return null;
        }

        return _nextLineOption == NextLineOption.TitleOnly
            ? nextPage.Title
            : nextPage.PrimaryText;
    }

    private void Publish() => SnapshotChanged?.Invoke(this, CreateSnapshot());
}
