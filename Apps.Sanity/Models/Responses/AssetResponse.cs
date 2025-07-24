using Newtonsoft.Json;

namespace Apps.Sanity.Models.Responses;

public class AssetResponse
{
    [JsonProperty("_id")]
    public string AssetId { get; set; } = string.Empty;
    
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;
}