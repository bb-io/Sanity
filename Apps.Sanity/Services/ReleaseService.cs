using Apps.Sanity.Api;
using Apps.Sanity.Models.Dtos;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses;
using Apps.Sanity.Models.Responses.Content;
using Apps.Sanity.Models.Responses.Releases;
using Apps.Sanity.Utils;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Sanity.Services;

public class ReleaseService
{
    private static readonly HashSet<string> SelectableStates =
    [
        "active",
        "scheduled",
        "scheduling",
        "unscheduling"
    ];

    private readonly ApiClient _client;
    private readonly IEnumerable<AuthenticationCredentialsProvider> _creds;

    public ReleaseService(ApiClient client, IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        _client = client;
        _creds = creds;
    }

    public async Task<List<ReleaseResponse>> SearchReleasesAsync(SearchReleasesRequest request)
    {
        var apiRequest = CreateQueryRequest(request.GetDatasetIdOrDefault(), request.BuildGroqQuery());
        var response = await _client.ExecuteWithErrorHandling<BaseSearchDto<ReleaseResponse>>(apiRequest);
        return response.Result;
    }

    public async Task<ReleaseResponse> GetReleaseAsync(string datasetId, string releaseName)
    {
        var query = "*[_type == \"system.release\" && name == $releaseName][0]";
        var request = CreateQueryRequest(datasetId, query)
            .AddQueryParameter("$releaseName", JsonConvert.SerializeObject(releaseName));

        var response = await _client.ExecuteWithErrorHandling<SearchResponse<ReleaseResponse>>(request);
        if (response.Result == null)
        {
            throw new PluginMisconfigurationException(
                $"Release '{releaseName}' was not found. Please verify the release name and try again.");
        }

        return response.Result;
    }

    public async Task<List<SelectableReleaseDto>> GetSelectableReleasesAsync(string datasetId, string? searchString)
    {
        var releases = await SearchReleasesAsync(new SearchReleasesRequest
        {
            DatasetId = datasetId
        });

        return releases
            .Where(x => SelectableStates.Contains(x.State))
            .Where(x => string.IsNullOrWhiteSpace(searchString)
                || x.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Title)
                    && x.Title.Contains(searchString, StringComparison.OrdinalIgnoreCase)))
            .Select(x => new SelectableReleaseDto(x.Name, x.Title, x.State))
            .ToList();
    }

    public async Task<List<ContentResponse>> GetReleaseDocumentsAsync(string datasetId, ReleaseResponse release)
    {
        if (release.State.Equals("published", StringComparison.OrdinalIgnoreCase))
        {
            var publishedIds = release.FinalDocumentStates?
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList() ?? new List<string>();

            return await GetDocumentsByIdsAsync(datasetId, publishedIds);
        }

        var request = CreateQueryRequest(datasetId, "*[sanity::partOfRelease($releaseName)]{_id,_type,_createdAt,_updatedAt}")
            .AddQueryParameter("$releaseName", JsonConvert.SerializeObject(release.Name));

        var response = await _client.ExecuteWithErrorHandling<BaseSearchDto<ContentResponse>>(request);
        return response.Result;
    }

    public async Task CreateReleaseAsync(string datasetId, string releaseName, string? title, string? description, string? releaseType)
    {
        var metadata = new JObject();

        if (!string.IsNullOrWhiteSpace(title))
        {
            metadata["title"] = title;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            metadata["description"] = description;
        }

        if (!string.IsNullOrWhiteSpace(releaseType))
        {
            metadata["releaseType"] = releaseType;
        }

        var action = new JObject
        {
            ["actionType"] = "sanity.action.release.create",
            ["releaseId"] = releaseName
        };

        if (metadata.HasValues)
        {
            action["metadata"] = metadata;
        }

        await DispatchActionsAsync(datasetId, action);
    }

    public async Task DeleteReleaseAsync(string datasetId, string releaseName)
    {
        var action = new JObject
        {
            ["actionType"] = "sanity.action.release.delete",
            ["releaseId"] = releaseName
        };

        await DispatchActionsAsync(datasetId, action);
    }

    public async Task CreateOrReplaceReleaseVersionAsync(string datasetId, string releaseName, JObject content)
    {
        await CreateOrReplaceReleaseVersionsAsync(datasetId, releaseName, [content]);
    }

    public async Task CreateOrReplaceReleaseVersionsAsync(string datasetId, string releaseName, IEnumerable<JObject> contents)
    {
        var releaseDocuments = contents
            .Select(PrepareReleaseVersionDocument(releaseName))
            .ToList();

        if (!releaseDocuments.Any())
        {
            return;
        }

        var existingDocumentIds = await GetExistingDocumentIdsAsync(
            datasetId,
            releaseDocuments
                .Select(document => document["_id"]?.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id))!);

        var actions = releaseDocuments
            .Select(document => CreateReleaseVersionAction(document, existingDocumentIds))
            .ToList();

        await DispatchActionsAsync(datasetId, actions);
    }

    private async Task<HashSet<string>> GetExistingDocumentIdsAsync(string datasetId, IEnumerable<string> ids)
    {
        var documentIds = ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (documentIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var query = string.Join(" || ", documentIds.Select(id => $"_id == {JsonConvert.SerializeObject(id)}"));
        var request = CreateQueryRequest(datasetId, $"*[{query}]{{_id}}");
        var response = await _client.ExecuteWithErrorHandling<BaseSearchDto<ContentResponse>>(request);

        return response.Result
            .Select(x => x.ContentId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static JObject CreateReleaseVersionAction(JObject document, HashSet<string> existingDocumentIds)
    {
        var versionId = document["_id"]?.ToString();
        var publishedId = ReleaseContentHelper.GetPublishedId(versionId ?? string.Empty);
        var actionType = versionId != null && existingDocumentIds.Contains(versionId)
            ? "sanity.action.document.version.replace"
            : "sanity.action.document.version.create";

        return new JObject
        {
            ["actionType"] = actionType,
            ["publishedId"] = publishedId,
            ["document"] = document
        };
    }

    private async Task DispatchActionsAsync(string datasetId, IEnumerable<JObject> actions)
    {
        var actionList = actions.ToList();
        if (!actionList.Any())
        {
            return;
        }

        var request = new ApiRequest($"/data/actions/{datasetId}", Method.Post, _creds)
            .AddStringBody(new JObject
            {
                ["actions"] = new JArray(actionList)
            }.ToString(), ContentType.Json);

        await _client.ExecuteWithErrorHandling<TransactionResponse>(request);
    }

    private async Task<List<ContentResponse>> GetDocumentsByIdsAsync(string datasetId, IEnumerable<string> ids)
    {
        var documentIds = ids
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (documentIds.Count == 0)
        {
            return new List<ContentResponse>();
        }

        var query = string.Join(" || ", documentIds.Select(id => $"_id == {JsonConvert.SerializeObject(id)}"));
        var request = CreateQueryRequest(datasetId, $"*[{query}]{{_id,_type,_createdAt,_updatedAt}}");
        var response = await _client.ExecuteWithErrorHandling<BaseSearchDto<ContentResponse>>(request);
        return response.Result;
    }

    private async Task DispatchActionsAsync(string datasetId, JObject action)
    {
        await DispatchActionsAsync(datasetId, [action]);
    }

    private RestRequest CreateQueryRequest(string datasetId, string query)
    {
        return new ApiRequest($"/data/query/{datasetId}", Method.Get, _creds)
            .AddQueryParameter("query", query)
            .AddQueryParameter("perspective", "raw");
    }

    private static Func<JObject, JObject> PrepareReleaseVersionDocument(string releaseName)
    {
        return content =>
        {
            var releaseDocument = (JObject)content.DeepClone();
            var publishedId = ReleaseContentHelper.GetPublishedId(releaseDocument["_id"]?.ToString() ?? string.Empty);

            releaseDocument.Remove("_rev");
            releaseDocument.Remove("_createdAt");
            releaseDocument.Remove("_updatedAt");
            releaseDocument["_id"] = ReleaseContentHelper.BuildVersionId(releaseName, publishedId);

            return releaseDocument;
        };
    }

    public record SelectableReleaseDto(string Name, string? Title, string State);
}
