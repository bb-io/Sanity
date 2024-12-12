using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Responses;

public class BaseSearchResponse<T>
{
    [Display("Total count")]
    public double TotalCount { get; set; }
    
    public virtual List<T> Items { get; set; }

    public BaseSearchResponse(List<T> items)
    {
        Items = items;
        TotalCount = items.Count;
    }
}