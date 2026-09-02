using CanvaStage.Core.Models;

namespace CanvaStage.Core.Services;

public sealed class DemoPresentationProvider : IPresentationProvider
{
    public Task<PresentationDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Create());
    }

    public static PresentationDocument Create()
    {
        PresentationPage[] pages =
        [
            new(1, "Welcome", ["Welcome to Sunday service", "We are glad you are here."], "#14344D", "#75E6C8"),
            new(2, "Opening", ["Great is Your faithfulness", "Morning by morning, new mercies I see."], "#34224D", "#D5B8FF"),
            new(3, "Reading", ["Psalm 100", "Make a joyful noise unto the Lord, all ye lands."], "#4C2F20", "#FFC27A"),
            new(4, "Prayer", ["Let us pray together."], "#263A2B", "#A7E7A4"),
            new(5, "Closing", ["Go in peace.", "Love God. Love people."], "#302A38", "#FFB4D0")
        ];

        return new PresentationDocument("demo-sunday", "Sunday Demo Deck", pages, DateTimeOffset.UtcNow);
    }
}
