using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Identifiers;

public class ReleaseIdentifier : DatasetIdentifier
{
    [Display("Release name", Description = "Unique release name or ID"), DataSource(typeof(ReleaseDataHandler))]
    public string ReleaseName { get; set; } = string.Empty;
}
