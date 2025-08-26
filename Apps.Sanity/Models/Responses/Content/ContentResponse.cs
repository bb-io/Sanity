using Blackbird.Applications.SDK.Blueprints.Interfaces.CMS;
using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.Sanity.Models.Responses.Content;

public class ContentResponse : IDownloadContentInput
{
    [Display("Content ID"), JsonProperty("_id")]
    public string ContentId { get; set; } = string.Empty;

    [Display("Content type"), JsonProperty("_type")]
    public string Type { get; set; } = string.Empty;

    [Display("Created at"), JsonProperty("_createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [Display("Updated at"), JsonProperty("_updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [Display("Custom header value"), JsonIgnore]
    public string CustomHeaderValue { get; set; } = string.Empty;
}