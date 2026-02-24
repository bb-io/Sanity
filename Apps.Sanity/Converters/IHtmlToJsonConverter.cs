using Apps.Sanity.Models;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public interface IHtmlToJsonConverter
{
    DocumentMutationResult ToJsonPatches(string html, JObject mainContent, string targetLanguage, bool publish,
        Dictionary<string, JObject>? referencedContents = null);
}
