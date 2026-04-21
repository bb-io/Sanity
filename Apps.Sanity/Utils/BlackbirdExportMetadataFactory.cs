using Apps.Sanity.Models;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Utils;

public static class BlackbirdExportMetadataFactory
{
    private const string SystemName = "Sanity";
    private const string SystemRef = "https://www.sanity.io/";

    public static BlackbirdExportMetadata Create(JObject content, string fallbackContentId, string fallbackLanguage,
        string? studioBaseUrl)
    {
        return Create(content, fallbackContentId, fallbackLanguage, studioBaseUrl, null);
    }

    public static BlackbirdExportMetadata Create(JObject content, string fallbackContentId, string fallbackLanguage,
        string? studioBaseUrl, string? ucidOverride)
    {
        var contentId = content["_id"]?.ToString() ?? fallbackContentId;
        var language = content["language"]?.ToString() ?? fallbackLanguage;
        var contentType = content["_type"]?.ToString();

        return new BlackbirdExportMetadata
        {
            HtmlLanguage = language,
            Ucid = string.IsNullOrWhiteSpace(ucidOverride) ? contentId : ucidOverride,
            ContentName = TryGetContentName(content, language),
            AdminUrl = BuildAdminUrl(studioBaseUrl, contentType, contentId),
            SystemName = SystemName,
            SystemRef = SystemRef
        };
    }

    private static string? TryGetContentName(JObject content, string language)
    {
        foreach (var fieldName in new[] { "name", "title" })
        {
            if (!content.TryGetValue(fieldName, StringComparison.InvariantCultureIgnoreCase, out var token))
            {
                continue;
            }

            var value = TryExtractStringValue(token, language);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryExtractStringValue(JToken token, string language)
    {
        if (token.Type == JTokenType.String)
        {
            return token.ToString();
        }

        if (token is JObject obj)
        {
            if (obj.TryGetValue("value", out var valueToken) && valueToken.Type == JTokenType.String)
            {
                return valueToken.ToString();
            }

            return null;
        }

        if (token is not JArray array)
        {
            return null;
        }

        var localizedEntry = array
            .OfType<JObject>()
            .FirstOrDefault(x => string.Equals(x["_key"]?.ToString(), language, StringComparison.OrdinalIgnoreCase));

        if (localizedEntry?["value"]?.Type == JTokenType.String)
        {
            return localizedEntry["value"]!.ToString();
        }

        return null;
    }

    private static string? BuildAdminUrl(string? studioBaseUrl, string? contentType, string contentId)
    {
        if (string.IsNullOrWhiteSpace(studioBaseUrl) || string.IsNullOrWhiteSpace(contentType) ||
            string.IsNullOrWhiteSpace(contentId))
        {
            return null;
        }

        if (!Uri.TryCreate(studioBaseUrl.TrimEnd('/'), UriKind.Absolute, out var parsedBaseUrl))
        {
            return null;
        }

        return $"{parsedBaseUrl.AbsoluteUri.TrimEnd('/')}/structure/{Uri.EscapeDataString(contentType)};{Uri.EscapeDataString(contentId)}";
    }
}
