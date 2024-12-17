namespace Apps.Sanity.Utils;

public static class JsonHelper
{
    public static bool TranslationForSpecificLanguageExist(string json, string translationLanguage)
    {
        var keyToFind = $"\"_key\": \"{translationLanguage}\"";
        return json.Contains(keyToFind);
    }
}