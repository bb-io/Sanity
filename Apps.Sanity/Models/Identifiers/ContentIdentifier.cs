using Apps.Sanity.DataSourceHandlers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Identifiers;

public class ContentIdentifier : DatasetIdentifier
{
    [Display("Content ID", Description = "Unique identifier of content object"), DataSource(typeof(ContentDataHandler))]
    public string ContentId { get; set; } = string.Empty;
}