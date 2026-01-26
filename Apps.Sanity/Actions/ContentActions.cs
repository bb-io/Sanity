using System.Text;
using Apps.Sanity.Api;
using Apps.Sanity.Converters;
using Apps.Sanity.Invocables;
using Apps.Sanity.Models;
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

[ActionList("Content")]
public class ContentActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : AppInvocable(invocationContext)
{
    private readonly DraftContentHelper _draftHelper = new(new ApiClient(invocationContext.AuthenticationCredentialsProviders), invocationContext.AuthenticationCredentialsProviders);
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
        var result = await _draftHelper.GetContentWithDraftFallbackAsync(
            identifier.ContentId,
            identifier.GetDatasetIdOrDefault());

        if (result.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        return result.First().ToObject<ContentResponse>()!;
    }

    [Action("Download content", Description = "Get localizable content fields as HTML file")]
    [BlueprintActionDefinition(BlueprintAction.DownloadContent)]
    public async Task<GetContentAsHtmlResponse> GetContentAsHtmlAsync([ActionParameter] GetContentAsHtmlRequest getContentAsHtmlRequest)
    {
        if (string.IsNullOrEmpty(getContentAsHtmlRequest.LocalizationStrategy))
        {
            throw new PluginMisconfigurationException("Localization strategy must be specified.");
        }
        
        var jObjects = await _draftHelper.GetContentWithDraftFallbackAsync(
            getContentAsHtmlRequest.ContentId,
            getContentAsHtmlRequest.GetDatasetIdOrDefault());

        if (jObjects.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        var content = jObjects.First();
        var referencedEntries = new Dictionary<string, JObject>();
        if (getContentAsHtmlRequest.IncludeReferenceEntries == true || getContentAsHtmlRequest.IncludeRichTextReferenceEntries == true)
        {
            var referenceFieldNames = getContentAsHtmlRequest.ReferenceFieldNames?.ToList() ?? new List<string>();
            await CollectReferencesRecursivelyAsync(
                content,
                getContentAsHtmlRequest.DatasetId,
                getContentAsHtmlRequest.IncludeReferenceEntries == true,
                getContentAsHtmlRequest.IncludeRichTextReferenceEntries == true,
                referencedEntries,
                referenceFieldNames
            );
        }

        var assetService = new AssetService(InvocationContext);
        
        List<FieldSizeRestriction>? fieldRestrictions = null;
        if (getContentAsHtmlRequest.FieldNames != null && getContentAsHtmlRequest.FieldMaxLength != null)
        {
            var fieldNames = getContentAsHtmlRequest.FieldNames.ToList();
            var fieldLengths = getContentAsHtmlRequest.FieldMaxLength.ToList();
            
            if (fieldNames.Count != fieldLengths.Count)
            {
                throw new PluginMisconfigurationException(
                    "The number of field names must match the number of field max length values.");
            }
            
            fieldRestrictions = fieldNames
                .Zip(fieldLengths, (name, length) => new FieldSizeRestriction
                {
                    FieldName = name,
                    Restrictions = new Blackbird.Filters.Shared.SizeRestrictions
                    {
                        MaximumSize = length
                    }
                })
                .ToList();
        }
        
        var strategy = Enum.Parse<LocalizationStrategy>(getContentAsHtmlRequest.LocalizationStrategy);
        var converter = ConverterFactory.CreateJsonToHtmlConverter(strategy);
        
        var html = converter.ToHtml(
            content, 
            getContentAsHtmlRequest.ContentId, 
            getContentAsHtmlRequest.SourceLanguage, 
            assetService, 
            getContentAsHtmlRequest.ToString(), 
            referencedEntries, 
            getContentAsHtmlRequest.OrderOfFields, 
            fieldRestrictions,
            getContentAsHtmlRequest.ExcludedFields);
        
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
        var localizationStrategy = HtmlHelper.ExtractLocalizationStrategy(html);

        if (localizationStrategy == LocalizationStrategy.DocumentLevel)
        {
            await UpdateContentDocumentLevelAsync(request, html, contentId);
        }
        else
        {
            await UpdateContentFieldLevelAsync(request, html, contentId);
        }
    }

    private async Task UpdateContentFieldLevelAsync(UpdateContentFromHtmlRequest request, string html, string contentId)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var publish = request.Publish ?? false;
        var isDraftId = contentId.StartsWith("drafts.");

        JObject mainContent;
        if (!publish && isDraftId)
        {
            mainContent = await _draftHelper.EnsureDraftExistsForUpdateAsync(
                contentId,
                request);
        }
        else
        {
            var jObjects = await _draftHelper.GetContentWithDraftFallbackAsync(
                contentId,
                request.GetDatasetIdOrDefault());

            if (jObjects.Count == 0)
            {
                throw new PluginMisconfigurationException(
                    "No content found for the provided ID. Please verify that the ID is correct and try again.");
            }

            mainContent = jObjects.First();
        }
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

        var converter = ConverterFactory.CreateHtmlToJsonConverter(LocalizationStrategy.FieldLevel);
        var allPatches = converter.ToJsonPatches(html, mainContent, request.Locale, publish, referencedContents);
        
        var apiRequest = new ApiRequest($"/data/mutate/{request}", Method.Post, Creds)
            .WithJsonBody(new
            {
                mutations = allPatches
            });
        
        if(publish == false)
        {
            var publishedId = DraftContentHelper.GetPublishedId(contentId);
            await EnsureDraftExistsAsync(request, publishedId, publishedContent: mainContent);
            foreach (var referencedContent in referencedContents)
            {
                var refPublishedId = DraftContentHelper.GetPublishedId(referencedContent.Key);
                await EnsureDraftExistsAsync(request, refPublishedId, publishedContent: referencedContent.Value);
            }
        }

        var transaction = await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        if (string.IsNullOrEmpty(transaction.TransactionId))
        {
            throw new PluginApplicationException(
                "An unexpected error occurred while updating the content. Please contact support for further assistance.");
        }
    }

    private async Task UpdateContentDocumentLevelAsync(UpdateContentFromHtmlRequest request, string html, string baseDocumentId)
    {
        var translationService = new TranslationMetadataService(Client, Creds);
        
        var baseDocumentObjects = await _draftHelper.GetContentWithDraftFallbackAsync(
            baseDocumentId,
            request.GetDatasetIdOrDefault());

        if (baseDocumentObjects.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "Base document not found. Please verify that the content ID is correct.");
        }

        var baseDocument = baseDocumentObjects.First();
        var baseLanguage = baseDocument["language"]?.ToString();
        
        if (string.IsNullOrEmpty(baseLanguage))
        {
            throw new PluginMisconfigurationException(
                "Base document does not have a language field. Document level localization requires a 'language' field.");
        }

        var existingTranslations = await translationService.GetTranslationsAsync(baseDocumentId, request.GetDatasetIdOrDefault());
        
        string translatedDocumentId;
        var converter = ConverterFactory.CreateHtmlToJsonConverter(LocalizationStrategy.DocumentLevel);
        var translatedContentList = converter.ToJsonPatches(html, baseDocument, request.Locale, request.Publish ?? false, null);

        if (translatedContentList.Count == 0)
        {
            throw new PluginApplicationException("No translated content could be extracted from the HTML file.");
        }

        var translatedContent = translatedContentList[0];

        if (existingTranslations.TryGetValue(request.Locale, out var existingTranslatedDocId))
        {
            translatedDocumentId = existingTranslatedDocId;
            
            var mutation = new JObject
            {
                ["mutations"] = new JArray
                {
                    new JObject
                    {
                        ["patch"] = new JObject
                        {
                            ["id"] = translatedDocumentId,
                            ["set"] = translatedContent
                        }
                    }
                }
            };

            var updateRequest = new ApiRequest($"/data/mutate/{request}", Method.Post, Creds)
                .WithJsonBody(mutation);

            await Client.ExecuteWithErrorHandling<TransactionResponse>(updateRequest);
        }
        else
        {
            translatedContent["_type"] = baseDocument["_type"];
            
            try
            {
                translatedDocumentId = await translationService.CreateTranslatedDocumentAsync(
                    request.GetDatasetIdOrDefault(), 
                    translatedContent);
            }
            catch (Exception ex)
            {
                throw new PluginApplicationException($"Failed to create translated document. Error: {ex.Message}", ex);
            }

            try
            {
                await translationService.CreateOrUpdateTranslationMetadataAsync(
                    baseDocumentId, 
                    translatedDocumentId, 
                    baseLanguage, 
                    request.Locale, 
                    request.GetDatasetIdOrDefault());
            }
            catch (Exception ex)
            {
                throw new PluginApplicationException(
                    $"Translated document was created successfully (ID: {translatedDocumentId}), but failed to link it with the base document. Error: {ex.Message}", 
                    ex);
            }
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

    [Action("Add reference to content",
        Description = "Add a reference to another content object within a specific dataset.")]
    public async Task AddReferenceToContentAsync([ActionParameter] AddReferenceRequest request)
    {
        var shouldUpdateAsDraft = request.ShouldUpdateAsDraft();
        var isDraftId = request.ContentId.StartsWith("drafts.");
        
        JObject content;
        if (shouldUpdateAsDraft && isDraftId)
        {
            // Ensure draft exists before updating
            content = await _draftHelper.EnsureDraftExistsForUpdateAsync(
                request.ContentId,
                request);
        }
        else
        {
            var jObjects = await _draftHelper.GetContentWithDraftFallbackAsync(
                request.ContentId,
                request.GetDatasetIdOrDefault());

            if (jObjects.Count == 0)
            {
                throw new PluginMisconfigurationException(
                    "No content found for the provided ID. Please verify that the ID is correct and try again.");
            }

            content = jObjects.First();
        }

        var contentId = shouldUpdateAsDraft 
            ? (isDraftId ? request.ContentId : $"drafts.{request.ContentId}")
            : request.ContentId;

        if (shouldUpdateAsDraft && !isDraftId)
        {
            await EnsureDraftExistsAsync(request, request.ContentId, publishedContent: content);
        }

        var referenceField = content[request.ReferenceFieldName];
        JObject mutation;

        if (referenceField is JArray)
        {
            mutation = new JObject
            {
                ["patch"] = new JObject
                {
                    ["id"] = contentId,
                    ["insert"] = new JObject
                    {
                        ["after"] = $"{request.ReferenceFieldName}[-1]",
                        ["items"] = new JArray
                        {
                            new JObject
                            {
                                ["_type"] = "reference",
                                ["_ref"] = request.ReferenceContentId,
                                ["_key"] = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 12)
                            }
                        }
                    }
                }
            };
        }
        else if (referenceField is JObject || referenceField == null)
        {
            mutation = new JObject
            {
                ["patch"] = new JObject
                {
                    ["id"] = contentId,
                    ["set"] = new JObject
                    {
                        [request.ReferenceFieldName] = new JObject
                        {
                            ["_type"] = "reference",
                            ["_ref"] = request.ReferenceContentId
                        }
                    }
                }
            };
        }
        else
        {
            throw new PluginMisconfigurationException(
                $"Field '{request.ReferenceFieldName}' is not a valid reference field.");
        }

        var apiRequest = new ApiRequest($"/data/mutate/{request}", Method.Post, Creds)
            .WithJsonBody(new
            {
                mutations = new[] { mutation }
            });

        var transaction = await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        if (string.IsNullOrEmpty(transaction.TransactionId))
        {
            throw new PluginApplicationException(
                "An unexpected error occurred while adding the reference. Please contact support for further assistance.");
        }
    }

    [Action("Remove reference from content",
        Description = "Remove a reference to another content object within a specific dataset.")]
    public async Task RemoveReferenceFromContentAsync([ActionParameter] RemoveReferenceRequest request)
    {
        var shouldUpdateAsDraft = request.ShouldUpdateAsDraft();
        var isDraftId = request.ContentId.StartsWith("drafts.");
        
        JObject content;
        if (shouldUpdateAsDraft && isDraftId)
        {
            // Ensure draft exists before updating
            content = await _draftHelper.EnsureDraftExistsForUpdateAsync(
                request.ContentId,
                request);
        }
        else
        {
            var jObjects = await _draftHelper.GetContentWithDraftFallbackAsync(
                request.ContentId,
                request.GetDatasetIdOrDefault());

            if (jObjects.Count == 0)
            {
                throw new PluginMisconfigurationException(
                    "No content found for the provided ID. Please verify that the ID is correct and try again.");
            }

            content = jObjects.First();
        }

        var contentId = shouldUpdateAsDraft 
            ? (isDraftId ? request.ContentId : $"drafts.{request.ContentId}")
            : request.ContentId;

        if (shouldUpdateAsDraft && !isDraftId)
        {
            await EnsureDraftExistsAsync(request, request.ContentId, publishedContent: content);
        }

        var referenceField = content[request.ReferenceFieldName];
        JObject mutation;

        if (referenceField is JArray referenceArray)
        {
            var indexToRemove = -1;
            for (int i = 0; i < referenceArray.Count; i++)
            {
                var item = referenceArray[i] as JObject;
                if (item?["_ref"]?.ToString() == request.ReferenceContentId)
                {
                    indexToRemove = i;
                    break;
                }
            }

            if (indexToRemove == -1)
            {
                throw new PluginMisconfigurationException(
                    $"Reference to content '{request.ReferenceContentId}' not found in field '{request.ReferenceFieldName}'.");
            }

            mutation = new JObject
            {
                ["patch"] = new JObject
                {
                    ["id"] = contentId,
                    ["unset"] = new JArray
                    {
                        $"{request.ReferenceFieldName}[{indexToRemove}]"
                    }
                }
            };
        }
        else if (referenceField is JObject referenceObject)
        {
            if (referenceObject["_ref"]?.ToString() != request.ReferenceContentId)
            {
                throw new PluginMisconfigurationException(
                    $"Field '{request.ReferenceFieldName}' does not reference content '{request.ReferenceContentId}'.");
            }

            mutation = new JObject
            {
                ["patch"] = new JObject
                {
                    ["id"] = contentId,
                    ["unset"] = new JArray
                    {
                        request.ReferenceFieldName
                    }
                }
            };
        }
        else
        {
            throw new PluginMisconfigurationException(
                $"Field '{request.ReferenceFieldName}' not found or is not a reference field.");
        }

        var apiRequest = new ApiRequest($"/data/mutate/{request}", Method.Post, Creds)
            .WithJsonBody(new
            {
                mutations = new[] { mutation }
            });

        var transaction = await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        if (string.IsNullOrEmpty(transaction.TransactionId))
        {
            throw new PluginApplicationException(
                "An unexpected error occurred while removing the reference. Please contact support for further assistance.");
        }
    }

    public async Task<List<JObject>> SearchContentAsJObjectAsync(SearchContentRequest identifier)
    {
        var result = await SearchContentInternalAsync<JObject>(identifier);
        result = result.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();
        
        // If searching for a single draft ID and nothing found, try the published version
        if (result.Count == 0 && identifier.GroqQuery != null && identifier.GroqQuery.Contains("_id =="))
        {
            var idMatch = System.Text.RegularExpressions.Regex.Match(identifier.GroqQuery, @"_id == \""(drafts\.[^\""]+)\""");
            if (idMatch.Success)
            {
                var draftId = idMatch.Groups[1].Value;
                var publishedId = DraftContentHelper.GetPublishedId(draftId);
                var newRequest = new SearchContentRequest
                {
                    DatasetId = identifier.GetDatasetIdOrDefault(),
                    GroqQuery = $"_id == \"{publishedId}\"",
                    ReturnDrafts = false
                };
                result = await SearchContentInternalAsync<JObject>(newRequest);
                result = result.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();
            }
        }
        
        return result;
    }
    
    private async Task EnsureDraftExistsAsync(DatasetIdentifier datasetIdentifier, string contentId, JObject publishedContent)
    {
        var groqQuery = $"_id == \"drafts.{contentId}\"";
        var draftContent = await SearchContentAsync(new()
        {
            DatasetId = datasetIdentifier.GetDatasetIdOrDefault(),
            GroqQuery = groqQuery,
            ReturnDrafts = true
        });

        if (draftContent.TotalCount == 0)
        {
            var createMutation = new Dictionary<string, object>
            {
                { "_id", $"drafts.{contentId}" }
            };

            foreach (var property in publishedContent.Properties())
            {
                if (property.Name != "_id")
                {
                    createMutation.Add(property.Name, property.Value);
                }
            }

            var apiRequest = new ApiRequest($"/data/mutate/{datasetIdentifier}", Method.Post, Creds)
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

            await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        }
    }
    
    private async Task<List<T>> SearchContentInternalAsync<T>(SearchContentRequest identifier)
    {
        var endpoint = $"/data/query/{identifier}{identifier.BuildGroqQuery()} | order(_createdAt desc)";
        var request = new ApiRequest(endpoint, Method.Get, Creds);
        if(identifier.ReturnDrafts == true)
        {
            request.AddParameter("perspective", "raw");
        }
        
        var content = await Client.ExecuteWithErrorHandling<BaseSearchDto<T>>(request);
        return content.Result;
    }

    private async Task CollectReferencesRecursivelyAsync(
        JObject content,
        string? datasetId,
        bool includeReferenceEntries,
        bool includeRichTextReferenceEntries,
        Dictionary<string, JObject> referencedEntries,
        IEnumerable<string> referenceFieldNames)
    {
        var referenceIds = new List<string>();
        CollectReferenceIds(content, includeReferenceEntries, includeRichTextReferenceEntries, referenceIds, referenceFieldNames: referenceFieldNames);
        referenceIds = referenceIds.Where(id => !referencedEntries.ContainsKey(id)).ToList();

        if (!referenceIds.Any())
            return;

        var idConditions = string.Join(" || ", referenceIds.Select(id => $"_id == \"{id}\""));
        var referencedObjects = await SearchContentAsJObjectAsync(new()
        {
            DatasetId = datasetId,
            GroqQuery = idConditions
        });

        var objectsById = referencedObjects
            .Where(entry => entry["_id"] != null)
            .ToDictionary(entry => entry["_id"]!.ToString());

        foreach (var id in referenceIds)
        {
            if (objectsById.TryGetValue(id, out var entry))
            {
                referencedEntries[id] = entry;
                await CollectReferencesRecursivelyAsync(
                    entry,
                    datasetId,
                    includeReferenceEntries,
                    includeRichTextReferenceEntries,
                    referencedEntries,
                    referenceFieldNames);
            }
        }
    }

    private void CollectReferenceIds(
        JToken token,
        bool includeReferenceEntries,
        bool includeRichTextReferenceEntries,
        List<string> referenceIds,
        IEnumerable<string> referenceFieldNames,
        string? parentPropertyName = null)
    {
        if (token is JObject obj)
        {
            var isReferenceField = parentPropertyName != null && referenceFieldNames.Contains(obj["_type"]?.ToString());
            if ((obj["_type"]?.ToString() == "reference" || isReferenceField) && obj["_ref"] != null)
            {
                if(obj["_ref"]?.ToString().Contains("image") == true)
                {
                    return;
                }
                
                var refId = obj["_ref"]!.ToString();
                bool isRichTextReference = parentPropertyName == "value";

                if ((isRichTextReference && includeRichTextReferenceEntries) ||
                    (!isRichTextReference && includeReferenceEntries))
                {
                    if (!referenceIds.Contains(refId))
                    {
                        referenceIds.Add(refId);
                    }
                }
            }

            foreach (var prop in obj.Properties())
            {
                CollectReferenceIds(prop.Value, includeReferenceEntries, includeRichTextReferenceEntries,
                                   referenceIds, referenceFieldNames, prop.Name);
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                CollectReferenceIds(item, includeReferenceEntries, includeRichTextReferenceEntries,
                                   referenceIds, referenceFieldNames, parentPropertyName);
            }
        }
    }
}