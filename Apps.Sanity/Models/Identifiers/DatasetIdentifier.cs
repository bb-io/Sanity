using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Identifiers;

public class DatasetIdentifier
{
    [Display("Dataset ID", Description = "Unique dataset id (name)"), DataSource(typeof(DatasetDataHandler))]
    public string? DatasetId { get; set; }

    public override string ToString()
    {
        return DatasetId ?? "production";
    }
    
    public string GetDatasetIdOrDefault()
    {
        return DatasetId ?? "production";
    }
}