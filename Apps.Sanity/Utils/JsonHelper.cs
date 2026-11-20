using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class JsonHelper
{
    public static List<string> GetLocales(JObject document, IEnumerable<string>? excludeLocales = null)
    {
        return document.Descendants()
            .OfType<JObject>()
            .Where(item => item.Parent is JArray && IsInternationalizedValue(item))
            .Select(item => (item["language"] as JValue)?.Value as string
                ?? (item["_key"] as JValue)?.Value as string)
            .OfType<string>()
            .Where(locale => !string.IsNullOrWhiteSpace(locale))
            .Except(excludeLocales ?? [], StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static bool IsInternationalizedValue(JObject obj)
    {
        if (obj.TryGetValue("_type", out var typeToken))
        {
            string typeStr = typeToken.ToString();
            return typeStr.Contains("internationalizedArray", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public static bool TranslationForSpecificLanguageExist(string json, string translationLanguage)
    {
        var keyToFind = $"\"_key\": \"{translationLanguage}\"";
        return json.Contains(keyToFind);
    }
}
