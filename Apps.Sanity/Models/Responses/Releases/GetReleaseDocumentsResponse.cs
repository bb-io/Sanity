using Apps.Sanity.Models.Responses.Content;
using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Responses.Releases;

public class GetReleaseDocumentsResponse(List<ContentResponse> items, List<string> ids)
    : BaseSearchResponse<ContentResponse>(items)
{
    [Display("Documents")]
    public override List<ContentResponse> Items { get; set; } = items;

    [Display("Document IDs")]
    public List<string> Ids { get; set; } = ids;
}
