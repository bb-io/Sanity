using Apps.Sanity.DataSourceHandlers;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Requests;

public class SearchReleasesRequest : DatasetIdentifier
{
    [Display("Release name", Description = "Filters releases by exact name when provided.")]
    public string? Name { get; set; }

    [Display("State"), StaticDataSource(typeof(ReleaseStateDataSource))]
    public string? State { get; set; }

    public string BuildGroqQuery()
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(Name))
        {
            filters.Add($"name == {SerializeString(Name)}");
        }

        if (!string.IsNullOrWhiteSpace(State))
        {
            filters.Add($"state == {SerializeString(State)}");
        }

        var query = "releases::all()";
        if (filters.Count > 0)
        {
            query += $"[{string.Join(" && ", filters)}]";
        }

        return $"{query} | order(_createdAt desc)";
    }

    private static string SerializeString(string value)
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }
}
