using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;

namespace Apps.Sanity.Models.Requests;

public class SearchContentRequest : DatasetIdentifier
{
    [Display("Content types")] 
    public IEnumerable<string>? Types { get; set; }

    [Display("Updated after")] 
    public DateTime? UpdatedAfter { get; set; }

    [Display("Created after")] 
    public DateTime? CreatedAfter { get; set; }

    [Display("GROQ query",
        Description =
            "Overrides all other optional parameters and allows you to manually specify filter parameters in GROQ format. You should provide a query like this: `_type==\"event\" && Name in [\"First name\"]`. For more information, see: https://www.sanity.io/docs/query-cheat-sheet.")]
    public string? GroqQuery { get; set; }

    [Display("Return drafts")]
    public bool? ReturnDrafts { get; set; }

    public string BuildGroqQuery()
    {
        var queryParameter = "?query=*[GROQ]";
        if (!string.IsNullOrEmpty(GroqQuery))
        {
            return queryParameter.Replace("GROQ", GroqQuery.Replace("&", "%26"));
        }

        var groq = string.Empty;
        
        if (Types != null)
        {
            var wrappedTypes = Types.Select(x => $"'{x}'").ToList();
            var joinedString = string.Join(",", wrappedTypes);
            var parameter = $"_type in [{joinedString}]";
            groq = GroqQueryBuilder.AddParameter(groq, parameter);
        }

        if (UpdatedAfter.HasValue)
        {
            var parameter = $"dateTime(_updatedAt) > dateTime('{UpdatedAfter.Value:yyyy-MM-ddTHH:mm:ssZ}')";
            groq = GroqQueryBuilder.AddParameter(groq, parameter);
        }
        
        if (CreatedAfter.HasValue)
        {
            var parameter = $"dateTime(_createdAt) > dateTime('{CreatedAfter.Value:yyyy-MM-ddTHH:mm:ssZ}')";
            groq = GroqQueryBuilder.AddParameter(groq, parameter);
        }
        
        return queryParameter.Replace("&", "%26").Replace("GROQ", groq);
    }
}
