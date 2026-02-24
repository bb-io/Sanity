using Apps.Sanity.Models;
using Apps.Sanity.Utils;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public class FieldLevelHtmlToJsonConverter : IHtmlToJsonConverter
{
    public DocumentMutationResult ToJsonPatches(string html, JObject mainContent, string targetLanguage, bool publish,
        Dictionary<string, JObject>? referencedContents = null)
    {
        var patches = HtmlToJsonConvertor.ToJsonPatches(html, mainContent, targetLanguage, publish, referencedContents);
        var mainContentId = HtmlHelper.ExtractContentId(html);
        
        var result = new DocumentMutationResult
        {
            Mutations = new List<DocumentMutation>(),
            MainDocumentId = mainContentId
        };
        
        if (patches.Any())
        {
            result.Mutations.Add(new DocumentMutation
            {
                OriginalDocumentId = mainContentId,
                TargetDocumentId = mainContentId,
                Content = new JObject
                {
                    ["fieldLevelPatches"] = new JArray(patches)
                },
                IsMainDocument = true,
                ReferenceMapping = new Dictionary<string, string>()
            });
        }
        
        return result;
    }
}
