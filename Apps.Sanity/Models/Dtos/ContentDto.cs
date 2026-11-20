using Apps.Sanity.Models.Responses.Content;
using Newtonsoft.Json;

namespace Apps.Sanity.Models.Dtos;

public class ContentDto
{
    [JsonProperty("_id")]
    public string ContentId { get; set; } = string.Empty;

    [JsonProperty("_type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("language")]
    public string? Language { get; set; }

    [JsonProperty("_createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("_updatedAt")]
    public DateTime UpdatedAt { get; set; }

    public string CustomHeaderValue { get; set; } = string.Empty;

    public ContentResponse ToResponse() => new()
    {
        ContentId = ContentId,
        Type = Type,
        Language = Language,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        CustomHeaderValue = CustomHeaderValue
    };
}
