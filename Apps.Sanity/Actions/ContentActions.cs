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
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Blackbird.Filters.Transformations;
using Blackbird.Filters.Xliff.Xliff2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using HtmlAgilityPack;

namespace Apps.Sanity.Actions;

[ActionList("Content")]
public class ContentActions(InvocationContext invocationContext, IFileManagementClient fileManagementClient) : AppInvocable(invocationContext)
{
    private readonly DraftContentHelper _draftHelper = new(new ApiClient(invocationContext.AuthenticationCredentialsProviders), invocationContext.AuthenticationCredentialsProviders);
    private readonly ReleaseService _releaseService = new(new ApiClient(invocationContext.AuthenticationCredentialsProviders), invocationContext.AuthenticationCredentialsProviders);

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

        var content = await GetContentOrThrowAsync(
            getContentAsHtmlRequest.ContentId,
            getContentAsHtmlRequest.GetDatasetIdOrDefault());
        var referencedEntries = await GetReferencedEntriesAsync(getContentAsHtmlRequest, content);
        var fieldRestrictions = GetFieldRestrictions(getContentAsHtmlRequest.FieldNames, getContentAsHtmlRequest.FieldMaxLength);
        var html = await BuildContentHtmlAsync(getContentAsHtmlRequest, content, referencedEntries, fieldRestrictions);

        return await UploadHtmlOutputAsync($"{getContentAsHtmlRequest.ContentId}.html", html);
    }

    [Action("Upload content", Description = "Update localizable content fields from HTML file")]
    [BlueprintActionDefinition(BlueprintAction.UploadContent)]
    public async Task<UploadContentResponse> UpdateContentFromHtmlAsync([ActionParameter] UpdateContentFromHtmlRequest request)
    {
        ValidateUploadRequest(request);

        var (html, transformation) = await ReadUploadInputAsync(request.Content);
        var contentId = request.ContentId ?? HtmlHelper.ExtractContentId(html);
        var localizationStrategy = HtmlHelper.ExtractLocalizationStrategy(html);
        var translationMetadataSchema = ResolveTranslationMetadataSchema(request, localizationStrategy);
        request.ReleaseName ??= ReleaseContentHelper.GetReleaseName(contentId);
        var result = await ExecuteUploadAsync(request, html, contentId, localizationStrategy, translationMetadataSchema);

        return await CreateUploadOutputAsync(request, html, contentId, localizationStrategy, transformation, result);
    }

    private async Task<UploadContentResult> UpdateContentFieldLevelAsync(UpdateContentFromHtmlRequest request, string html, string contentId)
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
        var mutationResult = converter.ToJsonPatches(html, mainContent, request.Locale, publish, referencedContents);
        
        // Extract field-level patches from the result
        List<JObject> allPatches = new List<JObject>();
        if (mutationResult.Mutations.Any())
        {
            var mainMutation = mutationResult.Mutations.First();
            if (mainMutation.Content["fieldLevelPatches"] is JArray patchArray)
            {
                allPatches = patchArray.OfType<JObject>().ToList();
            }
        }
        
        var apiRequest = new ApiRequest($"/data/mutate/{request.GetDatasetIdOrDefault()}", Method.Post, Creds)
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

        var targetContentId = publish
            ? DraftContentHelper.GetPublishedId(contentId)
            : DraftContentHelper.GetDraftId(DraftContentHelper.GetPublishedId(contentId));
        var targetContent = await GetContentForOutputAsync(targetContentId, request.GetDatasetIdOrDefault());
        var referenceIdMapping = referencedContents.Keys.ToDictionary(
            key => key,
            key => publish
                ? DraftContentHelper.GetPublishedId(key)
                : DraftContentHelper.GetDraftId(DraftContentHelper.GetPublishedId(key)),
            StringComparer.Ordinal);

        return new UploadContentResult(
            targetContentId,
            targetContent,
            referenceIdMapping,
            new Dictionary<string, JObject>());
    }

    private async Task<UploadContentResult> UpdateContentFieldLevelInReleaseAsync(
        UpdateContentFromHtmlRequest request,
        string html,
        string contentId)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var datasetId = request.GetDatasetIdOrDefault();
        var releaseName = request.ReleaseName!;
        var publish = request.Publish ?? false;
        var mainContent = await GetReleaseAwareContentAsync(contentId, datasetId);

        var referencedContentIds = HtmlHelper.ExtractReferencedContentIds(doc)
            .Distinct()
            .ToList();

        var referencedContents = await GetReleaseAwareContentsByIdAsync(datasetId, referencedContentIds);

        var releaseSeedDocuments = new[] { mainContent }.Concat(referencedContents.Values).ToList();

        if (!publish)
        {
            releaseSeedDocuments = releaseSeedDocuments
                .Select(x =>
                {
                    var clone = (JObject)x.DeepClone();
                    var id = clone["_id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith("drafts.", StringComparison.Ordinal))
                    {
                        clone["_id"] = DraftContentHelper.GetDraftId(id);
                    }

                    return clone;
                })
                .ToList();
        }

        await _releaseService.CreateOrReplaceReleaseVersionsAsync(
            datasetId,
            releaseName,
            releaseSeedDocuments);

        var converter = ConverterFactory.CreateHtmlToJsonConverter(LocalizationStrategy.FieldLevel);
        var mutationResult = converter.ToJsonPatches(html, mainContent, request.Locale, true, referencedContents);

        var releasePatches = new List<JObject>();
        if (mutationResult.Mutations.Count != 0
            && mutationResult.Mutations.First().Content["fieldLevelPatches"] is JArray patchArray)
        {
            releasePatches = patchArray
                .OfType<JObject>()
                .Select(x =>
                {
                    var patched = RetargetPatchToRelease(x, releaseName);

                    if (!publish)
                    {
                        var id = patched["patch"]?["id"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith("drafts.", StringComparison.Ordinal))
                            patched["patch"]!["id"] = DraftContentHelper.GetDraftId(id);
                    }

                    return patched;
                })
                .ToList();
        }

        if (!releasePatches.Any())
        {
            var releaseContentId = ReleaseContentHelper.BuildVersionId(releaseName, contentId);
            if (!publish)
                releaseContentId = DraftContentHelper.GetDraftId(releaseContentId);

            var releaseContent = await GetReleaseAwareContentAsync(releaseContentId, datasetId);
            var emptyReferenceMapping = referencedContentIds.ToDictionary(
                key => key,
                key =>
                {
                    var releaseRefId = ReleaseContentHelper.BuildVersionId(releaseName, key);
                    return publish ? releaseRefId : DraftContentHelper.GetDraftId(releaseRefId);
                },
                StringComparer.Ordinal);

            return new UploadContentResult(
                releaseContentId,
                releaseContent,
                emptyReferenceMapping,
                new Dictionary<string, JObject>());
        }

        var apiRequest = new ApiRequest($"/data/mutate/{datasetId}", Method.Post, Creds)
            .AddStringBody(new JObject
            {
                ["mutations"] = new JArray(releasePatches)
            }.ToString(), ContentType.Json);

        var transaction = await Client.ExecuteWithErrorHandling<TransactionResponse>(apiRequest);
        if (string.IsNullOrEmpty(transaction.TransactionId))
        {
            throw new PluginApplicationException(
                "An unexpected error occurred while updating the release content. Please contact support for further assistance.");
        }

        var targetContentId = ReleaseContentHelper.BuildVersionId(releaseName, contentId);
        if (!publish)
            targetContentId = DraftContentHelper.GetDraftId(targetContentId);

        var targetContent = await GetReleaseAwareContentAsync(targetContentId, datasetId);
        var referenceIdMapping = referencedContentIds.ToDictionary(
            key => key,
            key =>
            {
                var releaseRefId = ReleaseContentHelper.BuildVersionId(releaseName, key);
                return publish ? releaseRefId : DraftContentHelper.GetDraftId(releaseRefId);
            },
            StringComparer.Ordinal);

        return new UploadContentResult(
            targetContentId,
            targetContent,
            referenceIdMapping,
            new Dictionary<string, JObject>());
    }

    private async Task<UploadContentResult> UpdateContentDocumentLevelInReleaseAsync(UpdateContentFromHtmlRequest request, string html, string baseDocumentId, TranslationMetadataSchema translationMetadataSchema)
    {
        var datasetId = request.GetDatasetIdOrDefault();
        var releaseName = request.ReleaseName!;
        var publish = request.Publish ?? false;
        var translationService = new TranslationMetadataService(Client, Creds);

        var basePublishedDocumentId = ReleaseContentHelper.GetPublishedId(baseDocumentId);
        var baseDocument = await GetReleaseAwareContentAsync(baseDocumentId, datasetId);
        var baseLanguage = baseDocument["language"]?.ToString();

        if (string.IsNullOrEmpty(baseLanguage))
        {
            throw new PluginMisconfigurationException(
                "Base document does not have a language field. Document level localization requires a 'language' field.");
        }

        var existingTranslations = await translationService.GetTranslationsAsync(basePublishedDocumentId, datasetId);
        if (!existingTranslations.ContainsKey(request.Locale))
        {
            var releaseDocs = await SearchContentAsJObjectAsync(new SearchContentRequest
            {
                DatasetId = datasetId,
                ReturnDrafts = true,
                GroqQuery =
                    $"_type == {JsonConvert.SerializeObject(baseDocument["_type"]?.ToString())} " +
                    $"&& language == {JsonConvert.SerializeObject(request.Locale)} " +
                    $"&& (_id match {JsonConvert.SerializeObject($"versions.{releaseName}.*")} " +
                    $" || _id match {JsonConvert.SerializeObject($"drafts.versions.{releaseName}.*")})"
            });

            if (releaseDocs.Count > 0)
            {
                var existingReleaseDocId = releaseDocs.First()["_id"]!.ToString();
                existingTranslations[request.Locale] = ReleaseContentHelper.GetPublishedId(DraftContentHelper.GetPublishedId(existingReleaseDocId));
            }
        }
        
        var converter = ConverterFactory.CreateHtmlToJsonConverter(LocalizationStrategy.DocumentLevel);
        var mutationResult = converter.ToJsonPatches(html, baseDocument, request.Locale, true, null);

        if (mutationResult.Mutations.Count == 0)
        {
            throw new PluginApplicationException("No translated content could be extracted from the HTML file.");
        }

        var mainMutation = mutationResult.Mutations.FirstOrDefault(m => m.IsMainDocument);
        if (mainMutation == null)
        {
            throw new PluginApplicationException("Main document mutation not found in the result.");
        }

        var idMapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var outputReferenceIdMapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var releaseDocuments = new List<JObject>();

        foreach (var refMutation in mutationResult.Mutations.Where(m => !m.IsMainDocument))
        {
            var originalReferenceId = ReleaseContentHelper.GetPublishedId(refMutation.OriginalDocumentId);
            var baseReferenceDocumentId = await translationService.GetBaseDocumentIdAsync(originalReferenceId, datasetId)
                ?? originalReferenceId;

            var existingReferenceTranslations = await translationService.GetTranslationsAsync(baseReferenceDocumentId, datasetId);
            var localizedReferenceId = existingReferenceTranslations.TryGetValue(request.Locale, out var existingLocalizedRefId)
                ? existingLocalizedRefId
                : ReleaseContentHelper.GetPublishedId(refMutation.TargetDocumentId ?? GenerateLocalizedDocumentId());

            var referenceLookupId = ReleaseContentHelper.IsVersionId(refMutation.OriginalDocumentId)
                ? refMutation.OriginalDocumentId
                : baseReferenceDocumentId;
            var baseReferenceDocument = await GetReleaseAwareContentAsync(referenceLookupId, datasetId);
            var localizedReferenceContent = PrepareLocalizedDocumentForRelease(
                refMutation.Content,
                localizedReferenceId,
                request.Locale,
                baseReferenceDocument["_type"]?.ToString());

            releaseDocuments.Add(localizedReferenceContent);

            var baseReferenceLanguage = baseReferenceDocument["language"]?.ToString() ?? baseLanguage;
            var referenceMetadataContent = await translationService.BuildTranslationMetadataContentAsync(
                baseReferenceDocumentId,
                localizedReferenceId,
                baseReferenceLanguage,
                request.Locale,
                datasetId,
                translationMetadataSchema);

            releaseDocuments.Add(referenceMetadataContent);

            foreach (var mapping in refMutation.ReferenceMapping)
            {
                idMapping[mapping.Key] = localizedReferenceId;
                outputReferenceIdMapping[mapping.Key] = ReleaseContentHelper.BuildVersionId(releaseName, localizedReferenceId);
            }
        }

        UpdateReferences(mainMutation.Content, idMapping);

        var translatedDocumentId = existingTranslations.TryGetValue(request.Locale, out var existingTranslatedDocId)
            ? existingTranslatedDocId
            : GenerateLocalizedDocumentId();
        
        var targetMainReleaseId = ReleaseContentHelper.BuildVersionId(releaseName, translatedDocumentId);
        if (!publish)
            targetMainReleaseId = DraftContentHelper.GetDraftId(targetMainReleaseId);

        var localizedMainContent = PrepareLocalizedDocumentForRelease(
            mainMutation.Content,
            targetMainReleaseId,
            request.Locale,
            baseDocument["_type"]?.ToString());
        localizedMainContent["_id"] = targetMainReleaseId;

        releaseDocuments.Add(localizedMainContent);

        var mainMetadataContent = await translationService.BuildTranslationMetadataContentAsync(
            basePublishedDocumentId,
            translatedDocumentId,
            baseLanguage,
            request.Locale,
            datasetId,
            translationMetadataSchema);

        releaseDocuments.Add(mainMetadataContent);

        await _releaseService.CreateOrReplaceReleaseVersionsAsync(datasetId, releaseName, releaseDocuments);

        var targetContentId = ReleaseContentHelper.BuildVersionId(releaseName, translatedDocumentId);
        var targetContent = await GetReleaseAwareContentAsync(targetContentId, datasetId);
        var referencedContents = await GetReleaseAwareContentsByIdAsync(datasetId, idMapping.Values);

        return new UploadContentResult(
            targetContentId,
            targetContent,
            outputReferenceIdMapping,
            referencedContents);
    }

    private async Task<UploadContentResult> UpdateContentDocumentLevelAsync(UpdateContentFromHtmlRequest request, string html, string baseDocumentId, TranslationMetadataSchema translationMetadataSchema)
    {
        var publish = request.Publish ?? false;
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
        
        var converter = ConverterFactory.CreateHtmlToJsonConverter(LocalizationStrategy.DocumentLevel);
        var mutationResult = converter.ToJsonPatches(html, baseDocument, request.Locale, publish, null);

        if (mutationResult.Mutations.Count == 0)
        {
            throw new PluginApplicationException("No translated content could be extracted from the HTML file.");
        }

        // Find main document mutation
        var mainMutation = mutationResult.Mutations.FirstOrDefault(m => m.IsMainDocument);
        if (mainMutation == null)
        {
            throw new PluginApplicationException("Main document mutation not found in the result.");
        }

        // Build ID mapping for referenced documents
        var idMapping = new Dictionary<string, string>(StringComparer.Ordinal);
        
        // Process referenced documents first
        foreach (var refMutation in mutationResult.Mutations.Where(m => !m.IsMainDocument))
        {
            string localizedRefId;
            
            // Find the base document ID (in case the referenced document is already a translation)
            var baseRefDocId = await translationService.GetBaseDocumentIdAsync(
                refMutation.OriginalDocumentId, 
                request.GetDatasetIdOrDefault()) ?? refMutation.OriginalDocumentId;
            
            // Check if this referenced document already has a translated version
            var existingRefTranslations = await translationService.GetTranslationsAsync(
                baseRefDocId, 
                request.GetDatasetIdOrDefault());

            if (existingRefTranslations.TryGetValue(request.Locale, out var existingLocalizedRefId))
            {
                // Update existing translated reference document
                localizedRefId = existingLocalizedRefId;
                var targetPatchId = publish ? localizedRefId : DraftContentHelper.GetDraftId(localizedRefId);

                if (!publish)
                {
                    var publishedContentList = await _draftHelper.GetContentWithDraftFallbackAsync(
                        localizedRefId, 
                        request.GetDatasetIdOrDefault());
                    
                    if (publishedContentList.Count > 0)
                        await EnsureDraftExistsAsync(request, localizedRefId, publishedContentList.First());
                }
                
                var patchContent = (JObject)refMutation.Content.DeepClone();
                patchContent.Remove("_id");
                patchContent.Remove("_type");
                patchContent.Remove("_rev");
                patchContent.Remove("_createdAt");
                patchContent.Remove("_updatedAt");
                
                // Ensure language field is set
                patchContent["language"] = request.Locale;
                
                var patchMutation = new JObject
                {
                    ["mutations"] = new JArray
                    {
                        new JObject
                        {
                            ["patch"] = new JObject
                            {
                                ["id"] = targetPatchId,
                                ["set"] = patchContent
                            }
                        }
                    }
                };

                var patchRequest = new ApiRequest($"/data/mutate/{request.GetDatasetIdOrDefault()}", Method.Post, Creds)
                    .WithJsonBody(patchMutation);

                await Client.ExecuteWithErrorHandling<TransactionResponse>(patchRequest);
            }
            else
            {
                // Get base referenced document for type and language
                var baseRefDoc = await _draftHelper.GetContentWithDraftFallbackAsync(
                    baseRefDocId, 
                    request.GetDatasetIdOrDefault());
                
                if (baseRefDoc.Count == 0)
                {
                    throw new PluginApplicationException($"Base referenced document with ID '{baseRefDocId}' not found.");
                }
                
                var baseRefLanguage = baseRefDoc.First()["language"]?.ToString() ?? baseLanguage;
                var baseRefType = baseRefDoc.First()["_type"]?.ToString();
                
                localizedRefId = GenerateLocalizedDocumentId();
                var targetCreateId = publish ? localizedRefId : DraftContentHelper.GetDraftId(localizedRefId);

                var createContent = (JObject)refMutation.Content.DeepClone();
                createContent.Remove("_id");
                createContent.Remove("_rev");
                createContent.Remove("_createdAt");
                createContent.Remove("_updatedAt");
                createContent["_id"] = targetCreateId;
                createContent["language"] = request.Locale;
                if (!string.IsNullOrEmpty(baseRefType))
                {
                    createContent["_type"] = baseRefType;
                }
                
                var createMutation = new JObject
                {
                    ["mutations"] = new JArray
                    {
                        new JObject
                        {
                            ["createIfNotExists"] = createContent
                        }
                    }
                };

                var createMutationRequest =
                    new ApiRequest($"/data/mutate/{request.GetDatasetIdOrDefault()}", Method.Post, Creds)
                        .WithJsonBody(createMutation);
                
                await Client.ExecuteWithErrorHandling<TransactionResponse>(createMutationRequest);
                
                // Link the translated reference with its base
                try
                {
                    var metadataTranslatedRefId = publish
                        ? localizedRefId
                        : DraftContentHelper.GetDraftId(localizedRefId);

                    await translationService.CreateOrUpdateTranslationMetadataAsync(
                        baseRefDocId,
                        metadataTranslatedRefId,
                        baseRefLanguage,
                        request.Locale,
                        request.GetDatasetIdOrDefault(),
                        translationMetadataSchema);
                }
                catch (Exception ex)
                {
                    throw new PluginApplicationException(
                        $"Translated referenced document was created (ID: {localizedRefId}), but failed to link it. Error: {ex.Message}", 
                        ex);
                }
            }

            // Add to ID mapping
            foreach (var mapping in refMutation.ReferenceMapping)
            {
                idMapping[mapping.Key] = localizedRefId;
            }
        }

        // Update references in main document content
        UpdateReferences(mainMutation.Content, idMapping);
        
        string translatedDocumentId;
        if (existingTranslations.TryGetValue(request.Locale, out var existingTranslatedDocId))
        {
            translatedDocumentId = existingTranslatedDocId;
            var targetPatchId = publish ? translatedDocumentId : DraftContentHelper.GetDraftId(translatedDocumentId);

            if (!publish)
            {
                var publishedContentList = await _draftHelper.GetContentWithDraftFallbackAsync(
                    translatedDocumentId, 
                    request.GetDatasetIdOrDefault());
                
                if (publishedContentList.Count > 0)
                    await EnsureDraftExistsAsync(request, translatedDocumentId, publishedContentList.First());
            }

            var patchContent = (JObject)mainMutation.Content.DeepClone();
            patchContent.Remove("_id");
            patchContent.Remove("_type");
            patchContent.Remove("_rev");
            patchContent.Remove("_createdAt");
            patchContent.Remove("_updatedAt");
            
            // Ensure language field is set
            patchContent["language"] = request.Locale;
            
            var mutation = new JObject
            {
                ["mutations"] = new JArray
                {
                    new JObject
                    {
                        ["patch"] = new JObject
                        {
                            ["id"] = targetPatchId,
                            ["set"] = patchContent
                        }
                    }
                }
            };

            var updateRequest = new ApiRequest($"/data/mutate/{request.GetDatasetIdOrDefault()}", Method.Post, Creds)
                .WithJsonBody(mutation);

            await Client.ExecuteWithErrorHandling<TransactionResponse>(updateRequest);
        }
        else
        {
            translatedDocumentId = GenerateLocalizedDocumentId();
            var targetCreateId = publish ? translatedDocumentId : DraftContentHelper.GetDraftId(translatedDocumentId);

            var createContent = (JObject)mainMutation.Content.DeepClone();
            createContent.Remove("_id");
            createContent.Remove("_rev");
            createContent.Remove("_createdAt");
            createContent.Remove("_updatedAt");
            createContent["_id"] = targetCreateId;
            createContent["_type"] = baseDocument["_type"];
            createContent["language"] = request.Locale;
            
            var createMutation = new JObject
            {
                ["mutations"] = new JArray
                {
                    new JObject
                    {
                        ["createIfNotExists"] = createContent
                    }
                }
            };

            var createMutationRequest =
                new ApiRequest($"/data/mutate/{request.GetDatasetIdOrDefault()}", Method.Post, Creds).WithJsonBody(
                    createMutation);
            
            await Client.ExecuteWithErrorHandling<TransactionResponse>(createMutationRequest);
            
            try
            {
                var metadataTranslatedDocumentId = publish
                    ? translatedDocumentId
                    : DraftContentHelper.GetDraftId(translatedDocumentId);

                await translationService.CreateOrUpdateTranslationMetadataAsync(
                    baseDocumentId,
                    metadataTranslatedDocumentId,
                    baseLanguage,
                    request.Locale,
                    request.GetDatasetIdOrDefault(),
                    translationMetadataSchema);
            }
            catch (Exception ex)
            {
                throw new PluginApplicationException(
                    $"Translated document was created successfully (ID: {translatedDocumentId}), but failed to link it with the base document. Error: {ex.Message}", 
                    ex);
            }
        }

        var finalTargetContentId = publish ? translatedDocumentId : DraftContentHelper.GetDraftId(translatedDocumentId);
        var targetContent = await GetContentForOutputAsync(finalTargetContentId, request.GetDatasetIdOrDefault());
        var referencedContents = await GetContentsByExactIdsAsync(request.GetDatasetIdOrDefault(), idMapping.Values);

        return new UploadContentResult(
            finalTargetContentId,
            targetContent,
            idMapping,
            referencedContents);
    }

    private void UpdateReferences(JObject content, Dictionary<string, string> idMapping)
    {
        if (idMapping.Count == 0)
            return;

        UpdateReferencesRecursive(content, idMapping);
    }

    private void UpdateReferencesRecursive(JToken token, Dictionary<string, string> idMapping)
    {
        if (token is JObject obj)
        {
            // Check if this is a reference object
            if (obj["_type"]?.ToString() == "reference" && obj["_ref"] != null)
            {
                var currentRefId = obj["_ref"]!.ToString();
                
                // Update reference if it exists in mapping
                if (idMapping.TryGetValue(currentRefId, out var newRefId))
                {
                    obj["_ref"] = newRefId;
                }
            }
            else
            {
                // Recursively process all properties
                foreach (var property in obj.Properties().ToList())
                {
                    UpdateReferencesRecursive(property.Value, idMapping);
                }
            }
        }
        else if (token is JArray arr)
        {
            // Recursively process array items
            foreach (var item in arr)
            {
                UpdateReferencesRecursive(item, idMapping);
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
            var exactIds = ContentIdQueryHelper.ExtractExactContentIds(identifier.GroqQuery);
            if (exactIds.Count == 1 && exactIds[0].StartsWith("drafts.", StringComparison.Ordinal))
            {
                var draftId = exactIds[0];
                var publishedId = DraftContentHelper.GetPublishedId(draftId);
                var newRequest = new SearchContentRequest
                {
                    DatasetId = identifier.GetDatasetIdOrDefault(),
                    GroqQuery = $"_id == {JsonConvert.SerializeObject(publishedId)}",
                    ReturnDrafts = false
                };
                result = await SearchContentInternalAsync<JObject>(newRequest);
                result = result.Where(x => !x["_type"]!.ToString().Contains("system")).ToList();
            }
        }
        
        return result;
    }

    private static void ValidateUploadRequest(UpdateContentFromHtmlRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ReleaseName) && request.Publish == true)
        {
            throw new PluginMisconfigurationException(
                "Publish after update cannot be used together with Release name. Choose either direct publishing or saving the translation into a release.");
        }
    }

    private async Task<JObject> GetContentOrThrowAsync(string contentId, string datasetId)
    {
        var contentObjects = await _draftHelper.GetContentWithDraftFallbackAsync(contentId, datasetId);
        if (contentObjects.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        return contentObjects.First();
    }

    private async Task<Dictionary<string, JObject>> GetReferencedEntriesAsync(GetContentAsHtmlRequest request, JObject content)
    {
        var referencedEntries = new Dictionary<string, JObject>();
        if (request.IncludeReferenceEntries != true && request.IncludeRichTextReferenceEntries != true)
        {
            return referencedEntries;
        }

        var referenceFieldNames = request.ReferenceFieldNames?.ToList() ?? new List<string>();
        await CollectReferencesRecursivelyAsync(
            content,
            request.DatasetId,
            request.IncludeReferenceEntries == true,
            request.IncludeRichTextReferenceEntries == true,
            referencedEntries,
            referenceFieldNames);

        return referencedEntries;
    }

    private static List<FieldSizeRestriction>? GetFieldRestrictions(IEnumerable<string>? fieldNamesInput,
        IEnumerable<int>? fieldLengthsInput)
    {
        if (fieldNamesInput == null || fieldLengthsInput == null)
        {
            return null;
        }

        var fieldNames = fieldNamesInput.ToList();
        var fieldLengths = fieldLengthsInput.ToList();

        if (fieldNames.Count != fieldLengths.Count)
        {
            throw new PluginMisconfigurationException(
                "The number of field names must match the number of field max length values.");
        }

        return fieldNames
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

    private async Task<string> BuildContentHtmlAsync(GetContentAsHtmlRequest request, JObject content,
        Dictionary<string, JObject> referencedEntries, List<FieldSizeRestriction>? fieldRestrictions)
    {
        var strategy = Enum.Parse<LocalizationStrategy>(request.LocalizationStrategy);
        var converter = ConverterFactory.CreateJsonToHtmlConverter(strategy);
        var sourceLanguage = content["language"]?.ToString() ?? request.SourceLanguage;
        var exportMetadata = BlackbirdExportMetadataFactory.Create(content, request.ContentId, sourceLanguage);

        return await converter.ToHtmlAsync(
            content,
            request.ContentId,
            sourceLanguage,
            new AssetService(InvocationContext),
            request.ToString(),
            referencedEntries,
            request.OrderOfFields,
            fieldRestrictions,
            request.ExcludedFields,
            exportMetadata);
    }

    private async Task<GetContentAsHtmlResponse> UploadHtmlOutputAsync(string fileName, string html)
    {
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        memoryStream.Position = 0;

        var fileReference = await fileManagementClient.UploadAsync(memoryStream, "text/html", fileName);
        return new GetContentAsHtmlResponse
        {
            Content = fileReference
        };
    }

    private async Task<(string Html, Transformation? Transformation)> ReadUploadInputAsync(FileReference content)
    {
        var file = await fileManagementClient.DownloadAsync(content);
        var bytes = await file.GetByteData();
        var fileContent = Encoding.Default.GetString(bytes);

        if (!Xliff2Serializer.IsXliff2(fileContent))
        {
            return (fileContent, null);
        }

        var transformation = Transformation.Parse(fileContent, content.Name);
        var html = transformation.Target().Serialize();
        if (html == null)
        {
            throw new PluginMisconfigurationException("XLIFF did not contain any files");
        }

        return (html, transformation);
    }

    private async Task<UploadContentResult> ExecuteUploadAsync(UpdateContentFromHtmlRequest request, string html,
        string contentId, LocalizationStrategy localizationStrategy, TranslationMetadataSchema translationMetadataSchema)
    {
        if (!string.IsNullOrWhiteSpace(request.ReleaseName))
        {
            return localizationStrategy == LocalizationStrategy.DocumentLevel
                ? await UpdateContentDocumentLevelInReleaseAsync(request, html, contentId, translationMetadataSchema)
                : await UpdateContentFieldLevelInReleaseAsync(request, html, contentId);
        }

        return localizationStrategy == LocalizationStrategy.DocumentLevel
            ? await UpdateContentDocumentLevelAsync(request, html, contentId, translationMetadataSchema)
            : await UpdateContentFieldLevelAsync(request, html, contentId);
    }

    private static TranslationMetadataSchema ResolveTranslationMetadataSchema(
        UpdateContentFromHtmlRequest request, LocalizationStrategy localizationStrategy)
    {
        if (string.IsNullOrWhiteSpace(request.TranslationMetadataSchema))
        {
            return TranslationMetadataSchema.Default;
        }

        if (localizationStrategy != LocalizationStrategy.DocumentLevel)
        {
            throw new PluginMisconfigurationException(
                "Translation metadata schema can only be specified for document level localization. Remove the value or upload content exported with document level localization.");
        }

        if (!Enum.TryParse<TranslationMetadataSchema>(request.TranslationMetadataSchema, ignoreCase: true, out var parsed))
        {
            throw new PluginMisconfigurationException(
                $"Unknown translation metadata schema '{request.TranslationMetadataSchema}'. Use one of: {string.Join(", ", Enum.GetNames<TranslationMetadataSchema>())}.");
        }

        return parsed;
    }

    private async Task<UploadContentResponse> CreateUploadOutputAsync(UpdateContentFromHtmlRequest request, string html,
        string sourceContentId, LocalizationStrategy localizationStrategy, Transformation? transformation, UploadContentResult result)
    {
        var sourceUcid = await ResolveUploadArtifactSourceUcidAsync(
            sourceContentId,
            request.GetDatasetIdOrDefault());
        var exportMetadata = BlackbirdExportMetadataFactory.Create(
            result.Content,
            result.ContentId,
            request.Locale,
            sourceUcid);
        var outputContent = transformation != null
            ? UploadContentArtifactBuilder.BuildTransformation(transformation, request.Locale, exportMetadata)
            : UploadContentArtifactBuilder.BuildHtml(
                html,
                request.Locale,
                result.ContentId,
                exportMetadata,
                localizationStrategy == LocalizationStrategy.DocumentLevel ? result.Content : null,
                result.ReferenceIdMapping,
                result.ReferencedContents);

        return await UploadOutputFileAsync(request, result.ContentId, transformation != null, outputContent);
    }

    private async Task<string> ResolveUploadArtifactSourceUcidAsync(string sourceContentId, string datasetId)
    {
        if (!ReleaseContentHelper.IsVersionId(sourceContentId))
        {
            return ReleaseContentHelper.GetUploadArtifactSourceUcid(
                sourceContentId,
                publishedDocumentExists: false);
        }

        var publishedId = ReleaseContentHelper.GetPublishedId(sourceContentId);
        var publishedEntries = await GetContentsByExactIdsAsync(datasetId, [publishedId]);
        var publishedDocumentExists = publishedEntries.ContainsKey(publishedId);

        return ReleaseContentHelper.GetUploadArtifactSourceUcid(sourceContentId, publishedDocumentExists);
    }

    private async Task<UploadContentResponse> UploadOutputFileAsync(UpdateContentFromHtmlRequest request, string contentId,
        bool isTransformationOutput, string outputContent)
    {
        using var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(outputContent));
        outputStream.Position = 0;

        var outputFile = await fileManagementClient.UploadAsync(
            outputStream,
            isTransformationOutput ? request.Content.ContentType ?? "application/xliff+xml" : "text/html",
            request.Content.Name ?? $"{contentId}.html");

        return new UploadContentResponse
        {
            Content = outputFile
        };
    }

    private async Task<JObject> GetContentForOutputAsync(string contentId, string datasetId)
    {
        var content = await _draftHelper.GetContentWithDraftFallbackAsync(contentId, datasetId);
        if (content.Count == 0)
        {
            throw new PluginApplicationException(
                $"Content '{contentId}' was updated but could not be reloaded for output generation.");
        }

        return content.First();
    }

    private async Task<JObject> GetReleaseAwareContentAsync(string contentId, string datasetId)
    {
        var entries = await GetReleaseAwareContentsByIdAsync(datasetId, [contentId]);
        if (!entries.TryGetValue(contentId, out var entry))
        {
            var publishedId = ReleaseContentHelper.GetPublishedId(contentId);
            throw new PluginMisconfigurationException(
                $"No content found for ID '{contentId}' or its published counterpart '{publishedId}'. Please verify that the content exists before adding it to a release.");
        }

        return entry;
    }

    private async Task<Dictionary<string, JObject>> GetReleaseAwareContentsByIdAsync(string datasetId, IEnumerable<string> contentIds)
    {
        var requestedIds = contentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (!requestedIds.Any())
        {
            return new Dictionary<string, JObject>();
        }

        var resolvedEntries = await GetContentsByExactIdsAsync(datasetId, requestedIds);

        var fallbackPublishedIds = requestedIds
            .Where(id => !resolvedEntries.ContainsKey(id))
            .Select(id => new
            {
                RequestedId = id,
                PublishedId = ReleaseContentHelper.GetPublishedId(id)
            })
            .Where(x => !string.Equals(x.RequestedId, x.PublishedId, StringComparison.Ordinal))
            .ToList();

        if (!fallbackPublishedIds.Any())
        {
            return resolvedEntries;
        }

        var fallbackEntries = await GetContentsByExactIdsAsync(datasetId, fallbackPublishedIds.Select(x => x.PublishedId));
        foreach (var fallback in fallbackPublishedIds)
        {
            if (fallbackEntries.TryGetValue(fallback.PublishedId, out var entry))
            {
                resolvedEntries[fallback.RequestedId] = entry;
            }
        }

        return resolvedEntries;
    }

    private async Task<Dictionary<string, JObject>> GetContentsByExactIdsAsync(string datasetId, IEnumerable<string> contentIds)
    {
        var ids = contentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (!ids.Any())
        {
            return new Dictionary<string, JObject>();
        }

        var idConditions = string.Join(" || ", ids.Select(id => $"_id == {JsonConvert.SerializeObject(id)}"));
        var entries = await SearchContentAsJObjectAsync(new SearchContentRequest
        {
            DatasetId = datasetId,
            GroqQuery = idConditions
        });

        return entries
            .Where(entry => entry["_id"] != null)
            .ToDictionary(entry => entry["_id"]!.ToString(), StringComparer.Ordinal);
    }

    private static JObject RetargetPatchToRelease(JObject patch, string releaseName)
    {
        var releasePatch = (JObject)patch.DeepClone();
        var patchId = releasePatch["patch"]?["id"]?.ToString();
        if (!string.IsNullOrWhiteSpace(patchId))
        {
            releasePatch["patch"]!["id"] = ReleaseContentHelper.BuildVersionId(releaseName, patchId);
        }

        return releasePatch;
    }

    private static JObject PrepareLocalizedDocumentForRelease(JObject content, string publishedDocumentId,
        string targetLanguage, string? documentType)
    {
        var releaseContent = (JObject)content.DeepClone();
        releaseContent["_id"] = ReleaseContentHelper.GetPublishedId(publishedDocumentId);
        releaseContent["language"] = targetLanguage;

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            releaseContent["_type"] = documentType;
        }

        releaseContent.Remove("_rev");
        releaseContent.Remove("_createdAt");
        releaseContent.Remove("_updatedAt");

        return releaseContent;
    }

    private static string GenerateLocalizedDocumentId()
    {
        return Guid.NewGuid().ToString("N");
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
        if (ContentIdQueryHelper.RequiresRawPerspective(identifier.GroqQuery, identifier.ReturnDrafts == true))
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
                if (entry["language"] != null)
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
