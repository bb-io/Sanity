using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses.Releases;
using Apps.Sanity.Services;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Sanity.Actions;

[ActionList("Releases")]
public class ReleaseActions(InvocationContext invocationContext) : AppInvocable(invocationContext)
{
    private readonly ReleaseService _releaseService = new(new Api.ApiClient(invocationContext.AuthenticationCredentialsProviders),
        invocationContext.AuthenticationCredentialsProviders);

    [Action("Search releases", Description = "Search for releases within a specific dataset.")]
    public async Task<SearchReleasesResponse> SearchReleasesAsync([ActionParameter] SearchReleasesRequest request)
    {
        var releases = await _releaseService.SearchReleasesAsync(request);
        return new SearchReleasesResponse(releases);
    }

    [Action("Get release", Description = "Retrieve a release by its name.")]
    public Task<ReleaseResponse> GetReleaseAsync([ActionParameter] ReleaseIdentifier request)
    {
        return _releaseService.GetReleaseAsync(request.GetDatasetIdOrDefault(), request.ReleaseName);
    }

    [Action("Create release", Description = "Create a new content release.")]
    public async Task<ReleaseResponse> CreateReleaseAsync([ActionParameter] CreateReleaseRequest request)
    {
        await _releaseService.CreateReleaseAsync(
            request.GetDatasetIdOrDefault(),
            request.ReleaseName,
            request.Title,
            request.Description,
            request.ReleaseType);

        return await _releaseService.GetReleaseAsync(request.GetDatasetIdOrDefault(), request.ReleaseName);
    }

    [Action("Delete release", Description = "Delete a published or archived release.")]
    public Task DeleteReleaseAsync([ActionParameter] ReleaseIdentifier request)
    {
        return _releaseService.DeleteReleaseAsync(request.GetDatasetIdOrDefault(), request.ReleaseName);
    }

    [Action("Search release documents", Description = "Retrieve documents that belong to a release. Published releases are resolved from final document states.")]
    public async Task<GetReleaseDocumentsResponse> GetReleaseDocumentsAsync([ActionParameter] ReleaseIdentifier request)
    {
        var release = await _releaseService.GetReleaseAsync(request.GetDatasetIdOrDefault(), request.ReleaseName);
        var documents = await _releaseService.GetReleaseDocumentsAsync(request.GetDatasetIdOrDefault(), release);
        var ids = documents.Select(x => x.ContentId).ToList();

        return new GetReleaseDocumentsResponse(documents, ids);
    }
}
