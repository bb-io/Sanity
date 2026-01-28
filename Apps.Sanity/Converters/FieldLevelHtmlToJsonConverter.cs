using Apps.Sanity.Models.Requests;
using Apps.Sanity.Utils;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public class FieldLevelHtmlToJsonConverter : IHtmlToJsonConverter
{
    public List<JObject> ToJsonPatches(string html, JObject mainContent, string targetLanguage, bool publish,
        Dictionary<string, JObject>? referencedContents = null)
    {
        // Use existing HtmlToJsonConvertor logic
        return HtmlToJsonConvertor.ToJsonPatches(html, mainContent, targetLanguage, publish, referencedContents);
    }
}
