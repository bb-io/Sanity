using Apps.Sanity.Api;
using Apps.Sanity.Models;
using Apps.Sanity.Models.Responses;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Utils.Extensions.Http;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Apps.Sanity.Services;

public class TranslationMetadataService
{
    private readonly ApiClient _apiClient;
    private readonly IEnumerable<AuthenticationCredentialsProvider> _creds;

    public TranslationMetadataService(ApiClient apiClient, IEnumerable<AuthenticationCredentialsProvider> creds)
    {
        _apiClient = apiClient;
        _creds = creds;
    }

    public async Task<string?> GetBaseDocumentIdAsync(string documentId, string datasetId)
    {
        var query = $"*[_type == \"translation.metadata\" && references($id)][0]{{\"baseId\": translations[0].value->._id}}";
        
        var request = new ApiRequest($"/data/query/{datasetId}", Method.Get, _creds)
            .AddQueryParameter("query", query)
            .AddQueryParameter("$id", $"\"{documentId}\"");

        var response = await _apiClient.ExecuteWithErrorHandling<SearchResponse<JObject>>(request);
        
        return response?.Result?["baseId"]?.ToString();
    }

    public async Task<Dictionary<string, string>> GetTranslationsAsync(string baseDocumentId, string datasetId)
    {
        var query = $"*[_type == \"translation.metadata\" && references($id)][0]{{\"translations\": translations[].value->{{_id,language}}}}";
        
        var request = new ApiRequest($"/data/query/{datasetId}", Method.Get, _creds)
            .AddQueryParameter("query", query)
            .AddQueryParameter("$id", $"\"{baseDocumentId}\"");

        var response = await _apiClient.ExecuteWithErrorHandling<SearchResponse<JObject>>(request);
        
        if (response?.Result == null)
        {
            return new Dictionary<string, string>();
        }

        var translations = response.Result["translations"] as JArray;
        if (translations == null)
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>();
        foreach (var translation in translations.OfType<JObject>())
        {
            var id = translation["_id"]?.ToString();
            var language = translation["language"]?.ToString();
            
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(language))
            {
                result[language] = id;
            }
        }

        return result;
    }

    public async Task<string> CreateTranslatedDocumentAsync(string datasetId, JObject translatedContent)
    {
        var mutation = new JObject
        {
            ["mutations"] = new JArray
            {
                new JObject
                {
                    ["create"] = translatedContent
                }
            }
        };

        var request = new ApiRequest($"/data/mutate/{datasetId}", Method.Post, _creds)
            .AddQueryParameter("returnIds", "true")
            .AddStringBody(mutation.ToString(), ContentType.Json);

        var response = await _apiClient.ExecuteWithErrorHandling<MutateResponse>(request);
        
        if (response?.Results == null || response.Results.Count == 0)
        {
            throw new PluginApplicationException("Failed to create translated document. No results returned from the API.");
        }

        return response.Results[0].Id;
    }

    public async Task CreateOrUpdateTranslationMetadataAsync(string baseDocumentId, string translatedDocumentId,
        string baseLanguage, string targetLanguage, string datasetId,
        TranslationMetadataSchema schema = TranslationMetadataSchema.Default)
    {
        var existingMetadata = await GetTranslationMetadataAsync(baseDocumentId, datasetId);

        if (existingMetadata != null)
        {
            await UpdateTranslationMetadataAsync(existingMetadata, translatedDocumentId, targetLanguage, datasetId, schema);
        }
        else
        {
            await CreateTranslationMetadataAsync(baseDocumentId, translatedDocumentId, baseLanguage, targetLanguage, datasetId, schema);
        }
    }

    public async Task<JObject?> GetTranslationMetadataAsync(string baseDocumentId, string datasetId)
    {
        var query = $"*[_type == \"translation.metadata\" && references($id)][0]";
        
        var request = new ApiRequest($"/data/query/{datasetId}", Method.Get, _creds)
            .AddQueryParameter("query", query)
            .AddQueryParameter("$id", $"\"{baseDocumentId}\"");

        var response = await _apiClient.ExecuteWithErrorHandling<SearchResponse<JObject>>(request);
        
        return response?.Result;
    }

    public async Task<JObject> BuildTranslationMetadataContentAsync(string baseDocumentId, string translatedDocumentId,
        string baseLanguage, string targetLanguage, string datasetId,
        TranslationMetadataSchema schema = TranslationMetadataSchema.Default)
    {
        var existingMetadata = await GetTranslationMetadataAsync(baseDocumentId, datasetId);
        return existingMetadata == null
            ? CreateNewTranslationMetadataContent(baseDocumentId, translatedDocumentId, baseLanguage, targetLanguage, schema)
            : UpdateExistingTranslationMetadataContent(existingMetadata, translatedDocumentId, targetLanguage, schema);
    }

    private async Task CreateTranslationMetadataAsync(string baseDocumentId, string translatedDocumentId,
        string baseLanguage, string targetLanguage, string datasetId, TranslationMetadataSchema schema)
    {
        var metadata = CreateNewTranslationMetadataContent(
            baseDocumentId,
            translatedDocumentId,
            baseLanguage,
            targetLanguage,
            schema);

        var mutation = new JObject
        {
            ["mutations"] = new JArray
            {
                new JObject
                {
                    ["createIfNotExists"] = metadata
                }
            }
        };

        var request = new ApiRequest($"/data/mutate/{datasetId}", Method.Post, _creds)
            .AddStringBody(mutation.ToString(), ContentType.Json);

        try
        {
            await _apiClient.ExecuteWithErrorHandling(request);
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException($"Failed to link translated document with base document. Error: {ex.Message}", ex);
        }
    }

    private async Task UpdateTranslationMetadataAsync(JObject existingMetadata, string translatedDocumentId,
        string targetLanguage, string datasetId, TranslationMetadataSchema schema)
    {
        var metadata = UpdateExistingTranslationMetadataContent(existingMetadata, translatedDocumentId, targetLanguage, schema);
        var metadataId = metadata["_id"]?.ToString();

        var mutation = new JObject
        {
            ["mutations"] = new JArray
            {
                new JObject
                {
                    ["patch"] = new JObject
                    {
                        ["id"] = metadataId,
                        ["set"] = new JObject
                        {
                            ["translations"] = metadata["translations"]
                        }
                    }
                }
            }
        };

        var request = new ApiRequest($"/data/mutate/{datasetId}", Method.Post, _creds)
            .AddStringBody(mutation.ToString(), ContentType.Json);

        try
        {
            await _apiClient.ExecuteWithErrorHandling(request);
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException($"Failed to update translation metadata. Error: {ex.Message}", ex);
        }
    }

    private static JObject CreateNewTranslationMetadataContent(string baseDocumentId, string translatedDocumentId,
        string baseLanguage, string targetLanguage, TranslationMetadataSchema schema)
    {
        return new JObject
        {
            ["_id"] = $"translation.metadata.{baseDocumentId}",
            ["_type"] = "translation.metadata",
            ["translations"] = new JArray
            {
                CreateTranslationReference(baseLanguage, baseDocumentId, schema),
                CreateTranslationReference(targetLanguage, translatedDocumentId, schema)
            }
        };
    }

    private static JObject UpdateExistingTranslationMetadataContent(JObject existingMetadata, string translatedDocumentId,
        string targetLanguage, TranslationMetadataSchema schema)
    {
        var metadata = (JObject)existingMetadata.DeepClone();
        var metadataId = metadata["_id"]?.ToString();
        if (string.IsNullOrEmpty(metadataId))
        {
            throw new PluginApplicationException("Translation metadata ID is missing.");
        }

        var translations = metadata["translations"] as JArray ?? new JArray();
        var matchFieldName = schema == TranslationMetadataSchema.Legacy ? "language" : "_key";
        var existingTranslation = translations
            .OfType<JObject>()
            .FirstOrDefault(t => t[matchFieldName]?.ToString() == targetLanguage);

        if (existingTranslation != null)
        {
            existingTranslation["value"] = new JObject
            {
                ["_type"] = "reference",
                ["_ref"] = translatedDocumentId
            };
        }
        else
        {
            translations.Add(CreateTranslationReference(targetLanguage, translatedDocumentId, schema));
        }

        metadata["translations"] = translations;
        return metadata;
    }

    private const string LegacyTranslationEntryType = "translation";

    private static JObject CreateTranslationReference(string language, string documentId, TranslationMetadataSchema schema)
    {
        var value = new JObject
        {
            ["_type"] = "reference",
            ["_ref"] = documentId
        };

        if (schema == TranslationMetadataSchema.Legacy)
        {
            return new JObject
            {
                ["_key"] = Guid.NewGuid().ToString(),
                ["_type"] = LegacyTranslationEntryType,
                ["language"] = language,
                ["value"] = value
            };
        }

        return new JObject
        {
            ["_key"] = language,
            ["value"] = value
        };
    }
}

public class MutateResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public List<MutateResult> Results { get; set; } = new();
}

public class MutateResult
{
    public string Id { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
}
