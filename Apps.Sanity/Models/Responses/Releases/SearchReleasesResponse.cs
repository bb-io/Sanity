using Blackbird.Applications.Sdk.Common;

namespace Apps.Sanity.Models.Responses.Releases;

public class SearchReleasesResponse(List<ReleaseResponse> items) : BaseSearchResponse<ReleaseResponse>(items)
{
    [Display("Releases")]
    public override List<ReleaseResponse> Items { get; set; } = items;
}
