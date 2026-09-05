using System.Text.RegularExpressions;

namespace CanvaStage.Core.Utilities;

public static partial class PresenterPageCounterParser
{
    private const int MaximumCounterParts = 3;

    public static bool TryParse(IReadOnlyList<string> accessibleText, out int currentPage, out int totalPages)
    {
        ArgumentNullException.ThrowIfNull(accessibleText);

        for (int startIndex = 0; startIndex < accessibleText.Count; startIndex++)
        {
            string candidate = string.Empty;
            for (int partCount = 1; partCount <= MaximumCounterParts && startIndex + partCount <= accessibleText.Count; partCount++)
            {
                string part = accessibleText[startIndex + partCount - 1].Trim();
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                candidate = string.IsNullOrEmpty(candidate) ? part : $"{candidate} {part}";
                if (TryParseCandidate(candidate, out currentPage, out totalPages))
                {
                    return true;
                }
            }
        }

        currentPage = 0;
        totalPages = 0;
        return false;
    }

    private static bool TryParseCandidate(string candidate, out int currentPage, out int totalPages)
    {
        Match match = LabeledCounterRegex().Match(candidate);
        if (!match.Success)
        {
            match = UnlabeledCounterRegex().Match(candidate);
        }

        if (!match.Success)
        {
            match = SlashCounterRegex().Match(candidate);
        }

        if (match.Success
            && int.TryParse(match.Groups["current"].Value, out currentPage)
            && int.TryParse(match.Groups["total"].Value, out totalPages)
            && currentPage >= 1
            && totalPages >= currentPage)
        {
            return true;
        }

        currentPage = 0;
        totalPages = 0;
        return false;
    }

    [GeneratedRegex(@"\b(?:page|slide)\s*(?<current>\d{1,4})\s*(?:of|/)\s*(?<total>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabeledCounterRegex();

    [GeneratedRegex(@"(?<![\d:])(?<current>\d{1,4})\s+of\s+(?<total>\d{1,4})(?![\d:])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnlabeledCounterRegex();

    [GeneratedRegex(@"(?<![\d:])(?<current>\d{1,4})\s*/\s*(?<total>\d{1,4})(?![\d:])", RegexOptions.CultureInvariant)]
    private static partial Regex SlashCounterRegex();
}
