using Apps.Sanity.Models.Requests;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public interface IHtmlToJsonConverter
{
    List<JObject> ToJsonPatches(string html, JObject mainContent, string targetLanguage, bool publish,
        Dictionary<string, JObject>? referencedContents = null);
}
