using System.Text;
using System.Text.RegularExpressions;

namespace CanvaStage.Core.Utilities;

/// <summary>Uses independent slide text as casing evidence, never as replacement wording.</summary>
public static partial class SlideCapitalizationMatcher
{
    // Punctuation and whitespace always come from the native text. Numbers participate
    // in matching so a similar verse, date or numbered line cannot supply evidence.
    [GeneratedRegex(@"[\p{L}\p{M}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();

    public static bool HasCandidate(string text)
    {
        int consecutive = 0;
        foreach (Match word in Words().Matches(text))
        {
            consecutive = IsUpper(word.Value) ? consecutive + 1 : 0;
            if (consecutive >= 3) return true;
        }
        return false;
    }

    public static string Match(string original, IEnumerable<string> referenceTexts)
    {
        Match[] source = Words().Matches(original).Cast<Match>().ToArray();
        if (!HasCandidate(original) || source.Length < 4 || source.Sum(word => word.Length) < 20) return original;

        Match[][] references = referenceTexts.Select(text => Words().Matches(text).Cast<Match>().ToArray()).ToArray();
        (bool found, string[]? agreed) = Find(source, references);
        if (found) return agreed is null ? original : Apply(original, source, agreed);

        // OCR may misread an unrelated word elsewhere in a long paragraph. In that
        // case accept only a complete uppercase run plus four unchanged context words
        // on either side, and change casing inside that run alone.
        StringBuilder result = new(original);
        for (int end = source.Length; end > 0;)
        {
            int last = --end;
            if (!IsUpper(source[last].Value)) continue;
            while (end > 0 && IsUpper(source[end - 1].Value)) end--;
            int count = last - end + 1;
            if (count < 3) continue;
            int firstContext = Math.Max(0, end - 4);
            int lastContext = Math.Min(source.Length, last + 5);
            Match[] window = source[firstContext..lastContext];
            if (window.Length < 8 || window.Sum(word => word.Length) < 32) continue;
            (_, string[]? casing) = Find(window, references);
            if (casing is null) continue;
            for (int index = last; index >= end; index--)
            {
                result.Remove(source[index].Index, source[index].Length);
                result.Insert(source[index].Index, casing[index - firstContext]);
            }
        }
        return result.ToString();
    }

    private static bool IsUpper(string word) => word.Count(char.IsLetter) >= 2
        && word.Any(char.IsUpper) && !word.Any(char.IsLower);

    private static (bool Found, string[]? Casing) Find(Match[] source, Match[][] references)
    {
        string[]? agreed = null;
        bool found = false;
        foreach (Match[] evidence in references)
        {
            for (int start = 0; start <= evidence.Length - source.Length; start++)
            {
                bool matches = true;
                for (int index = 0; index < source.Length; index++)
                    if (source[index].Length != evidence[start + index].Length
                        || !string.Equals(source[index].Value, evidence[start + index].Value, StringComparison.OrdinalIgnoreCase))
                    { matches = false; break; }
                if (!matches) continue;

                string[] result = evidence.Skip(start).Take(source.Length).Select(word => word.Value).ToArray();
                // Conflicting repeated text is ambiguous, including a match which
                // agrees with the original. Do not choose the first occurrence.
                if (found && !result.SequenceEqual(agreed!)) return (true, null);
                found = true;
                agreed = result;
            }
        }
        return (found, agreed);
    }

    private static string Apply(string original, Match[] source, string[] casing)
    {
        StringBuilder result = new(original);
        for (int end = source.Length; end > 0;)
        {
            int last = --end;
            if (!IsUpper(source[last].Value)) continue;
            while (end > 0 && IsUpper(source[end - 1].Value)) end--;
            if (last - end + 1 < 3) continue;
            for (int index = last; index >= end; index--)
            {
                result.Remove(source[index].Index, source[index].Length);
                result.Insert(source[index].Index, casing[index]);
            }
        }
        return result.ToString();
    }
}
