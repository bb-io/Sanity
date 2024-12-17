using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Responses.Content;

public class SearchContentResponse(List<ContentResponse> items) : BaseSearchResponse<ContentResponse>(items)
{
    [Display("Content")] 
    public override List<ContentResponse> Items { get; set; } = items;
}