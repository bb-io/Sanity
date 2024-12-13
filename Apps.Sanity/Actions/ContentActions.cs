using System.Text;
using Apps.Sanity.Api;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Dtos;
using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses;
using Apps.Sanity.Models.Responses.Content;
using Apps.Sanity.Utils;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Sanity.Actions;

[ActionList]
public class ContentActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : AppInvocable(invocationContext)
{
    [Action("Search content",
        Description =
            "Search for content within a specific dataset. If no dataset is specified, the production dataset is used by default.")]
    public async Task<SearchContentResponse> SearchContentAsync([ActionParameter] SearchContentRequest request)
    {
        var result = await SearchContentInternalAsync<ContentResponse>(request);
        result = result.Where(x => !x.Type.Contains("system")).ToList();
        return new(result);
    }

    [Action("Get content",
        Description = "Retrieve a content object from a specific dataset using a content identifier.")]
    public async Task<ContentResponse> GetContentAsync([ActionParameter] ContentIdentifier identifier)
    {
        var groqQuery = $"_id == \"{identifier.ContentId}\"";
        var content = await SearchContentAsync(new()
        {
            DatasetId = identifier.DatasetId,
            GroqQuery = groqQuery
        });

        if (content.TotalCount == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        return content.Items.First();
    }

    [Action("Get content as HTML", Description = "Get localizable content fields as HTML file")]
    public async Task<GetContentAsHtmlResponse> GetContentAsHtmlAsync([ActionParameter] GetContentAsHtmlRequest getContentAsHtmlRequest)
    {
        var groqQuery = $"_id == \"{getContentAsHtmlRequest.ContentId}\"";
        var jObjects = await SearchContentAsJObjectAsync(new()
        {
            DatasetId = getContentAsHtmlRequest.DatasetId,
            GroqQuery = groqQuery
        });

        if (jObjects.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        var content = jObjects.First();
        var html = content.ToHtml(getContentAsHtmlRequest.ContentId, getContentAsHtmlRequest.SourceLanguage);
        var memoryStream =  new MemoryStream(Encoding.UTF8.GetBytes(html));
        memoryStream.Position = 0;

        var fileReference = await fileManagementClient.UploadAsync(memoryStream, "text/html", $"{getContentAsHtmlRequest.ContentId}.html");
        return new()
        {
            File = fileReference
        };
    }

    [Action("Create content", Description = "Create a content object based on his type and other parameters")]
    public async Task<ContentResponse> CreateContentAsync([ActionParameter] CreateContentRequest request)
    {
        if (request.PropertyValues != null && request.Properties != null)
        {
            if (request.Properties.Count() != request.PropertyValues.Count())
            {
                throw new PluginMisconfigurationException(
                    "The number of properties must match the number of property values.");
            }
        }

        var keyValuePairs = request.Properties?
               .Zip(request.PropertyValues!)
               .Select(x => new KeyValuePair<string, string>(x.First, x.Second)).ToList()
                ?? new List<KeyValuePair<string, string>>();

        var createMutation = new Dictionary<string, object>
        {
            { "_type", request.Type }
        };

        keyValuePairs.ForEach(pair => createMutation.Add(pair.Key, pair.Value));

        var apiRequest = new ApiRequest($"/data/mutate/{request}", Method.Post, Creds)
            .WithJsonBody(new
            {
                mutations = new object[]
                {
                    new
                    {
                        create = createMutation
                    }
                }
            });

        var transaction = await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        if (string.IsNullOrEmpty(transaction.TransactionId))
        {
            throw new PluginApplicationException(
                "An unexpected error occurred while creating the content. Please contact support for further assistance.");
        }

        var groqQuery = $"_rev == \"{transaction.TransactionId}\"";
        var content = await SearchContentAsync(new()
        {
            DatasetId = request.DatasetId,
            GroqQuery = groqQuery
        });

        return content.Items.First();
    }

    [Action("Delete content",
        Description = "Remove a content object from a specific dataset using a content identifier.")]
    public async Task DeleteContentAsync([ActionParameter] ContentIdentifier identifier)
    {
        await GetContentAsync(identifier);

        var request = new ApiRequest($"/data/mutate/{identifier}", Method.Post, Creds)
            .WithJsonBody(new
            {
                mutations = new object[]
                {
                    new
                    {
                        delete = new
                        {
                            id = identifier.ContentId,
                            purge = false
                        }
                    }
                }
            });

        await Client.ExecuteWithErrorHandling(request);
    }

    public async Task<List<JObject>> SearchContentAsJObjectAsync(SearchContentRequest identifier)
    {
        var result = await SearchContentInternalAsync<JObject>(identifier);
        result = result.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();
        return result;
    }

    private async Task<List<T>> SearchContentInternalAsync<T>(SearchContentRequest identifier)
    {
        var endpoint = $"/data/query/{identifier}{identifier.BuildGroqQuery()} | order(_createdAt desc)";
        var request = new ApiRequest(endpoint, Method.Get, Creds);
        var content = await Client.ExecuteWithErrorHandling<BaseSearchDto<T>>(request);
        return content.Result;
    }
}