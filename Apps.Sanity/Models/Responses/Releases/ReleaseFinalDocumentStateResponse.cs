using Newtonsoft.Json;

namespace Apps.Sanity.Models.Responses.Releases;

public class ReleaseFinalDocumentStateResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}
