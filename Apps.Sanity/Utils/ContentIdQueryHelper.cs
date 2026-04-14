using System.Text.RegularExpressions;

namespace Apps.Sanity.Utils;

public static class ContentIdQueryHelper
{
    private static readonly Regex ExactIdConditionRegex = new(@"_id\s*==\s*""([^""]+)""", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractExactContentIds(string? groqQuery)
    {
        if (string.IsNullOrWhiteSpace(groqQuery))
        {
            return [];
        }

        return ExactIdConditionRegex.Matches(groqQuery)
            .Select(match => match.Groups[1].Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool RequiresRawPerspective(string? groqQuery, bool returnDrafts)
    {
        if (returnDrafts)
        {
            return true;
        }

        return ExtractExactContentIds(groqQuery)
            .Any(id => id.StartsWith("drafts.", StringComparison.Ordinal)
                || ReleaseContentHelper.IsVersionId(id));
    }
}
