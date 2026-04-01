using Blackbird.Applications.Sdk.Common;
using Newtonsoft.Json;

namespace Apps.Sanity.Models.Responses.Releases;

public class ReleaseResponse
{
    [Display("Release document ID"), JsonProperty("_id")]
    public string Id { get; set; } = string.Empty;

    [Display("Release name"), JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [Display("State"), JsonProperty("state")]
    public string State { get; set; } = string.Empty;

    [Display("Created at"), JsonProperty("_createdAt")]
    public DateTime CreatedAt { get; set; }

    [Display("Updated at"), JsonProperty("_updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [Display("Publish at"), JsonProperty("publishAt")]
    public DateTime? PublishAt { get; set; }

    [Display("Published at"), JsonProperty("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [Display("User ID"), JsonProperty("userId")]
    public string? UserId { get; set; }

    [Display("Metadata"), JsonProperty("metadata")]
    public ReleaseMetadataResponse? Metadata { get; set; }

    [Display("Final document states"), JsonProperty("finalDocumentStates")]
    public List<ReleaseFinalDocumentStateResponse>? FinalDocumentStates { get; set; }

    [Display("Title")]
    public string? Title => Metadata?.Title;

    [Display("Description")]
    public string? Description => Metadata?.Description;

    [Display("Release type")]
    public string? ReleaseType => Metadata?.ReleaseType;
}
