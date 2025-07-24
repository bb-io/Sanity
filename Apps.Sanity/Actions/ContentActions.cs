using System.Text;
using Apps.Sanity.Api;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models.Dtos;
using Apps.Sanity.Models.Identifiers;
using Apps.Sanity.Models.Requests;
using Apps.Sanity.Models.Responses;
using Apps.Sanity.Models.Responses.Content;
using Apps.Sanity.Services;
using Apps.Sanity.Utils;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Blackbird.Filters.Transformations;
using Blackbird.Filters.Xliff.Xliff2;
using Newtonsoft.Json.Linq;
using RestSharp;
using HtmlAgilityPack;

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

    [Action("Download content", Description = "Get localizable content fields as HTML file")]
    [BlueprintActionDefinition(BlueprintAction.DownloadContent)]
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
        var referencedEntries = new Dictionary<string, JObject>();
        if (getContentAsHtmlRequest.IncludeReferenceEntries == true || getContentAsHtmlRequest.IncludeRichTextReferenceEntries == true)
        {
            await CollectReferencesRecursivelyAsync(
                content,
                getContentAsHtmlRequest.DatasetId,
                getContentAsHtmlRequest.IncludeReferenceEntries == true,
                getContentAsHtmlRequest.IncludeRichTextReferenceEntries == true,
                referencedEntries
            );
        }

        var assetService = new AssetService(InvocationContext);
        var html = content.ToHtml(getContentAsHtmlRequest.ContentId, getContentAsHtmlRequest.SourceLanguage, assetService, getContentAsHtmlRequest.ToString(), referencedEntries);
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        memoryStream.Position = 0;

        var fileReference = await fileManagementClient.UploadAsync(memoryStream, "text/html", $"{getContentAsHtmlRequest.ContentId}.html");
        return new()
        {
            Content = fileReference
        };
    }

    [Action("Upload content", Description = "Update localizable content fields from HTML file")]
    [BlueprintActionDefinition(BlueprintAction.UploadContent)]
    public async Task UpdateContentFromHtmlAsync([ActionParameter] UpdateContentFromHtmlRequest request)
    {
        var file = await fileManagementClient.DownloadAsync(request.Content);
        var bytes = await file.GetByteData();
        var html = Encoding.Default.GetString(bytes);
        if (Xliff2Serializer.IsXliff2(html))
        {
            html = Transformation.Parse(html, request.Content.Name).Target().Serialize();
            if (html == null)
            {
                throw new PluginMisconfigurationException("XLIFF did not contain any files");
            }
        }

        var contentId = request.ContentId ?? HtmlHelper.ExtractContentId(html);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var jObjects = await SearchContentAsJObjectAsync(new()
        {
            DatasetId = request.DatasetId,
            GroqQuery = $"_id == \"{contentId}\""
        });

        if (jObjects.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        var mainContent = jObjects.First();
        var referencedContentIds = HtmlHelper.ExtractReferencedContentIds(doc);
        var referencedContents = new Dictionary<string, JObject>();

        if (referencedContentIds.Any())
        {
            var idConditions = string.Join(" || ", referencedContentIds.Select(id => $"_id == \"{id}\""));
            var referencedObjects = await SearchContentAsJObjectAsync(new()
            {
                DatasetId = request.DatasetId,
                GroqQuery = idConditions
            });

            foreach (var entry in referencedObjects)
            {
                if (entry["_id"] != null)
                {
                    referencedContents[entry["_id"]!.ToString()] = entry;
                }
            }
        }

        var allPatches = HtmlToJsonConvertor.ToJsonPatches(html, mainContent, request.Locale, referencedContents);
        var apiRequest = new ApiRequest($"/data/mutate/{request}", Method.Post, Creds)
            .WithJsonBody(new
            {
                mutations = allPatches
            });

        var transaction = await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        if (string.IsNullOrEmpty(transaction.TransactionId))
        {
            throw new PluginApplicationException(
                "An unexpected error occurred while updating the content. Please contact support for further assistance.");
        }
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

    private async Task CollectReferencesRecursivelyAsync(
        JObject content,
        string? datasetId,
        bool includeReferenceEntries,
        bool includeRichTextReferenceEntries,
        Dictionary<string, JObject> referencedEntries)
    {
        var referenceIds = new HashSet<string>();
        CollectReferenceIds(content, includeReferenceEntries, includeRichTextReferenceEntries, referenceIds);
        referenceIds.RemoveWhere(id => referencedEntries.ContainsKey(id));

        if (!referenceIds.Any())
            return;

        var idConditions = string.Join(" || ", referenceIds.Select(id => $"_id == \"{id}\""));
        var referencedObjects = await SearchContentAsJObjectAsync(new()
        {
            DatasetId = datasetId,
            GroqQuery = idConditions
        });

        foreach (var entry in referencedObjects)
        {
            if (entry["_id"] != null)
            {
                var id = entry["_id"]!.ToString();
                referencedEntries[id] = entry;
                await CollectReferencesRecursivelyAsync(
                    entry,
                    datasetId,
                    includeReferenceEntries,
                    includeRichTextReferenceEntries,
                    referencedEntries);
            }
        }
    }

    private void CollectReferenceIds(
        JToken token,
        bool includeReferenceEntries,
        bool includeRichTextReferenceEntries,
        HashSet<string> referenceIds,
        string? parentPropertyName = null)
    {
        if (token is JObject obj)
        {
            if (obj["_type"]?.ToString() == "reference" && obj["_ref"] != null)
            {
                var refId = obj["_ref"]!.ToString();
                bool isRichTextReference = parentPropertyName == "value";

                if ((isRichTextReference && includeRichTextReferenceEntries) ||
                    (!isRichTextReference && includeReferenceEntries))
                {
                    referenceIds.Add(refId);
                }
            }

            foreach (var prop in obj.Properties())
            {
                CollectReferenceIds(prop.Value, includeReferenceEntries, includeRichTextReferenceEntries,
                                   referenceIds, prop.Name);
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                CollectReferenceIds(item, includeReferenceEntries, includeRichTextReferenceEntries,
                                   referenceIds, parentPropertyName);
            }
        }
    }
}