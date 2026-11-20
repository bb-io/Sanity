using Apps.Sanity.Models;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class BlackbirdExportMetadataFactory
{
    private const string SystemName = "Sanity";
    private const string SystemRef = "https://www.sanity.io/";

    public static BlackbirdExportMetadata Create(JObject content, string fallbackContentId, string fallbackLanguage)
    {
        return Create(content, fallbackContentId, fallbackLanguage, null);
    }

    public static BlackbirdExportMetadata Create(JObject content, string fallbackContentId, string fallbackLanguage, string? ucidOverride)
    {
        var contentId = content["_id"]?.ToString() ?? fallbackContentId;
        var language = content["language"]?.ToString() ?? fallbackLanguage;
        var contentType = content["_type"]?.ToString();

        return new BlackbirdExportMetadata
        {
            HtmlLanguage = language,
            Ucid = string.IsNullOrWhiteSpace(ucidOverride) ? contentId : ucidOverride,
            ContentName = TryGetContentName(content, language, useDefaultFields: true),
            SystemName = SystemName,
            SystemRef = SystemRef
        };
    }

    public static string? TryGetContentName(JObject content, string? locale, string? titleField = null, bool useDefaultFields = false)
    {
        if (string.IsNullOrWhiteSpace(titleField))
        {
            if (!useDefaultFields)
            {
                return null;
            }

            titleField = null;
        }

        string[] fieldNames = titleField is null ? ["name", "title"] : [titleField];
        var candidates = fieldNames
            .Select(fieldName => content.Property(fieldName, StringComparison.OrdinalIgnoreCase))
            .OfType<JProperty>()
            .Where(IsTitleField);

        foreach (var candidate in candidates)
        {
            var value = candidate.Value switch
            {
                JArray array when !string.IsNullOrWhiteSpace(locale) => FindLocalizedValue(array, locale),
                _ => candidate.Value
            };
			
			if (value is not JValue { Type: JTokenType.String, Value: string name })
				continue;

            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return null;
    }

    public static IEnumerable<string> GetTitleFields(JObject content) =>
        content.Properties()
            .Where(IsTitleField)
            .Select(property => property.Name);

    private static bool IsTitleField(JProperty property)
    {
        if (property.Name.StartsWith('_'))
            return false;

        if (property.Value.Type == JTokenType.String)
            return true;

        if (property.Value is not JArray array)
            return false;

        var hasStringValue = false;
        foreach (var entry in array)
        {
            if (entry is not JObject item || !JsonHelper.IsInternationalizedValue(item))
                return false;

            var valueType = item["value"]?.Type;
            if (valueType is not (null or JTokenType.Null or JTokenType.String))
                return false;

            hasStringValue = hasStringValue || valueType == JTokenType.String;
        }

        return hasStringValue;
    }
	
    private static JToken? FindLocalizedValue(JArray array, string locale) =>
        array.OfType<JObject>()
            .FirstOrDefault(entry =>
            {
                var language = (entry["language"] as JValue)?.Value as string;
                var key = (entry["_key"] as JValue)?.Value as string;
                var entryLocale = string.IsNullOrWhiteSpace(language) ? key : language;
                return string.Equals(entryLocale, locale, StringComparison.OrdinalIgnoreCase);
            })?["value"];
}
