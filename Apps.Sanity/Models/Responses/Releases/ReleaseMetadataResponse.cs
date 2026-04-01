using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.Sanity.Models.Responses.Releases;

public class ReleaseMetadataResponse
{
    [Display("Title"), JsonProperty("title")]
    public string? Title { get; set; }

    [Display("Description"), JsonProperty("description")]
    public string? Description { get; set; }

    [Display("Release type"), JsonProperty("releaseType")]
    public string? ReleaseType { get; set; }

    [Display("Intended publish at"), JsonProperty("intendedPublishAt")]
    public DateTime? IntendedPublishAt { get; set; }
}
