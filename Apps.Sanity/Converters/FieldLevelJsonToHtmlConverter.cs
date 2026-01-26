using Apps.Sanity.Models;
using Apps.Sanity.Services;
using Apps.Sanity.Utils;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public class FieldLevelJsonToHtmlConverter : IJsonToHtmlConverter
{
    public string ToHtml(JObject jObject, string contentId, string sourceLanguage, AssetService assetService,
        string datasetId, Dictionary<string, JObject>? referencedEntries = null,
        IEnumerable<string>? orderOfFields = null, List<FieldSizeRestriction>? fieldRestrictions = null,
        IEnumerable<string>? excludedFields = null)
    {
        // Use existing JsonToHtmlConverter logic
        return JsonToHtmlConverter.ToHtml(jObject, contentId, sourceLanguage, assetService, datasetId,
            referencedEntries, orderOfFields, fieldRestrictions);
    }
}
