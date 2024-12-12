using Apps.Sanity.Api;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Dtos;
using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Models.Responses.Content;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Invocation;
using RestSharp;

namespace Apps.Sanity.Actions;

[ActionList]
public class ContentActions(InvocationContext invocationContext) : AppInvocable(invocationContext)
{
    [Action("Search content",
        Description = "Search content within specific dataset. By default dataset will be as production")]
    public async Task<SearchContentResponse> SearchContentAsync([ActionParameter] DatasetIdentifier identifier)
    {
        var endpoint = $"/data/query/{identifier}?query=*[]";
        var request = new ApiRequest(endpoint, Method.Get, Creds);
        var content = await Client.ExecuteWithErrorHandling<BaseSearchDto<ContentResponse>>(request);
        return new(content.Result);
    }
}