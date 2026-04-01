using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Services;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Sanity.DataSourceHandlers;

public class ReleaseDataHandler(InvocationContext invocationContext, [ActionParameter] SearchReleasesRequest request)
    : AppInvocable(invocationContext), IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var releaseService = new ReleaseService(new Api.ApiClient(InvocationContext.AuthenticationCredentialsProviders),
            InvocationContext.AuthenticationCredentialsProviders);

        var releases = await releaseService.GetSelectableReleasesAsync(request.GetDatasetIdOrDefault(), context.SearchString);
        return releases.Select(x => new DataSourceItem(x.Name, GetDisplayName(x.Name, x.Title, x.State)));
    }

    private static string GetDisplayName(string name, string? title, string state)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return $"{name} ({state})";
        }

        return $"{title} [{name}] ({state})";
    }
}
