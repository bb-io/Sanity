using Apps.Sanity.Api;
using Apps.Sanity.Models.Dtos;
using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Models.Responses;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Sanity.Utils;

public class DraftContentHelper
{
    private readonly ApiClient _client;
    private readonly IEnumerable<AuthenticationCredentialsProvider> _creds;

    public DraftContentHelper(ApiClient client, IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        _client = client;
        _creds = creds;
    }
    
    public async Task<List<JObject>> GetContentWithDraftFallbackAsync(
        string contentId,
        string? datasetId)
    {
        var isDraftId = contentId.StartsWith("drafts.");
        var groqQuery = $"_id == \"{contentId}\"";
        
        var result = await SearchContentInternalAsync<JObject>(datasetId, groqQuery, isDraftId);
        result = result.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();
        if (result.Count == 0 && isDraftId)
        {
            var publishedId = contentId.Substring("drafts.".Length);
            groqQuery = $"_id == \"{publishedId}\"";
            
            result = await SearchContentInternalAsync<JObject>(datasetId, groqQuery, false);
            result = result.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();
            foreach (var item in result)
            {
                item["_id"] = contentId;
            }
        }

        return result;
    }
    
    public async Task<JObject> EnsureDraftExistsForUpdateAsync(
        string contentId,
        DatasetIdentifier datasetIdentifier)
    {
        var isDraftId = contentId.StartsWith("drafts.");
        var publishedId = isDraftId ? contentId.Substring("drafts.".Length) : contentId;
        var draftId = isDraftId ? contentId : $"drafts.{contentId}";

        var groqQuery = $"_id == \"{draftId}\"";
        var draftResult = await SearchContentInternalAsync<JObject>(
            datasetIdentifier.ToString(), 
            groqQuery, 
            true);
        draftResult = draftResult.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();

        if (draftResult.Count > 0)
        {
            return draftResult.First();
        }

        groqQuery = $"_id == \"{publishedId}\"";
        var publishedResult = await SearchContentInternalAsync<JObject>(
            datasetIdentifier.ToString(), 
            groqQuery, 
            false);
        publishedResult = publishedResult.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();

        if (publishedResult.Count == 0)
        {
            throw new PluginMisconfigurationException(
                $"No content found for ID '{publishedId}'. Cannot create draft version.");
        }

        var publishedContent = publishedResult.First();

        await CreateDraftFromPublishedAsync(datasetIdentifier, publishedId, publishedContent);

        draftResult = await SearchContentInternalAsync<JObject>(
            datasetIdentifier.ToString(), 
            $"_id == \"{draftId}\"", 
            true);
        draftResult = draftResult.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();

        return draftResult.First();
    }
    
    private async Task<List<T>> SearchContentInternalAsync<T>(string? datasetId, string groqQuery, bool returnDrafts)
    {
        var escapedQuery = groqQuery.Replace("&", "%26");
        var endpoint = $"/data/query/{datasetId}?query=*[{escapedQuery}] | order(_createdAt desc)";
        var request = new ApiRequest(endpoint, Method.Get, _creds);
        
        if(returnDrafts)
        {
            request.AddParameter("perspective", "raw");
        }
        
        var content = await _client.ExecuteWithErrorHandling<BaseSearchDto<T>>(request);
        return content.Result;
    }

    private async Task CreateDraftFromPublishedAsync(
        DatasetIdentifier datasetIdentifier,
        string publishedId,
        JObject publishedContent)
    {
        var createMutation = new Dictionary<string, object>
        {
            { "_id", $"drafts.{publishedId}" }
        };

        foreach (var property in publishedContent.Properties())
        {
            if (property.Name != "_id")
            {
                createMutation.Add(property.Name, property.Value);
            }
        }

        var apiRequest = new ApiRequest($"/data/mutate/{datasetIdentifier}", Method.Post, _creds)
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

        await _client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
    }

    public static string GetPublishedId(string contentId)
    {
        return contentId.StartsWith("drafts.") 
            ? contentId.Substring("drafts.".Length) 
            : contentId;
    }

    public static string GetDraftId(string contentId)
    {
        return contentId.StartsWith("drafts.") 
            ? contentId 
            : $"drafts.{contentId}";
    }
}
