namespace Apps.Sanity.Models.Dtos;

public class BaseSearchDto<T>
{
    public string Query { get; set; } = string.Empty;

    public List<T> Result { get; set; } = new();

    public List<string> SyncTags { get; set; } = new();

    public string Ms { get; set; } = string.Empty;
}