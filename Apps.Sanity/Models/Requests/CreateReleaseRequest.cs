using Apps.Sanity.DataSourceHandlers;
using Apps.Sanity.Models.Identifiers;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dictionaries;
using Blackbird.Applications.Sdk.Common.Dynamic;

namespace Apps.Sanity.Models.Requests;

public class CreateReleaseRequest : ReleaseIdentifier
{
    [Display("Title", Description = "Human-readable release title.")]
    public string? Title { get; set; }

    [Display("Description")]
    public string? Description { get; set; }

    [Display("Release type"), StaticDataSource(typeof(ReleaseTypeDataSource))]
    public string? ReleaseType { get; set; }
}
