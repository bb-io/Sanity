namespace Apps.Sanity.Models.Responses;

public class SearchResponse<T>
{
    public string Query { get; set; } = string.Empty;
    public T? Result { get; set; }
}
