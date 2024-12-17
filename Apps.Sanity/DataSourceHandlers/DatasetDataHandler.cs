using Apps.Sanity.Api;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Responses;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.Sanity.DataSourceHandlers;

public class DatasetDataHandler(InvocationContext invocationContext)
    : AppInvocable(invocationContext), IAsyncDataSourceItemHandler
{
    public async Task<IEnumerable<DataSourceItem>> GetDataAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var request = new ApiRequest("/datasets", Method.Get, Creds);
        var response = await Client.ExecuteWithErrorHandling<List<DatasetResponse>>(request);
        
        return response
            .Where(x => context.SearchString == null || x.Name.Contains(context.SearchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => new DataSourceItem(x.Name, x.Name));
    }
}