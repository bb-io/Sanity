using Apps.Sanity.Models;
using Apps.Sanity.Services;
using Newtonsoft.Json.Linq;

namespace Apps.Sanity.Converters;

public interface IJsonToHtmlConverter
{
    Task<string> ToHtmlAsync(JObject jObject, string contentId, string sourceLanguage, AssetService assetService,
        string datasetId, Dictionary<string, JObject>? referencedEntries = null,
        IEnumerable<string>? orderOfFields = null, List<FieldSizeRestriction>? fieldRestrictions = null,
        IEnumerable<string>? excludedFields = null);
}
