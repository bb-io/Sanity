using Apps.Sanity.Api;
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

    public async Task<Dictionary<string, string>> GetTranslationsAsync(string baseDocumentId, string datasetId)
    {
        var query = $"*[_type == \"translation.metadata\" && references($id)][0]{{\"translations\": translations[].value->{{_id,language}}}}";
        
        var request = new RestRequest($"/data/query/{datasetId}")
            .AddQueryParameter("query", query)
            .AddQueryParameter("$id", baseDocumentId);

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

        var request = new RestRequest($"/data/mutate/{datasetId}")
            .AddQueryParameter("returnIds", "true")
            .AddJsonBody(mutation.ToString());

        var response = await _apiClient.ExecuteWithErrorHandling<MutateResponse>(request);
        
        if (response?.Results == null || response.Results.Count == 0)
        {
            throw new PluginApplicationException("Failed to create translated document. No results returned from the API.");
        }

        return response.Results[0].Id;
    }

    public async Task CreateOrUpdateTranslationMetadataAsync(string baseDocumentId, string translatedDocumentId, 
        string baseLanguage, string targetLanguage, string datasetId)
    {
        var existingMetadata = await GetTranslationMetadataAsync(baseDocumentId, datasetId);

        if (existingMetadata != null)
        {
            await UpdateTranslationMetadataAsync(existingMetadata, translatedDocumentId, targetLanguage, datasetId);
        }
        else
        {
            await CreateTranslationMetadataAsync(baseDocumentId, translatedDocumentId, baseLanguage, targetLanguage, datasetId);
        }
    }

    private async Task<JObject?> GetTranslationMetadataAsync(string baseDocumentId, string datasetId)
    {
        var query = $"*[_type == \"translation.metadata\" && references($id)][0]";
        
        var request = new RestRequest($"/data/query/{datasetId}")
            .AddQueryParameter("query", query)
            .AddQueryParameter("$id", baseDocumentId);

        var response = await _apiClient.ExecuteWithErrorHandling<SearchResponse<JObject>>(request);
        
        return response?.Result;
    }

    private async Task CreateTranslationMetadataAsync(string baseDocumentId, string translatedDocumentId,
        string baseLanguage, string targetLanguage, string datasetId)
    {
        var metadata = new JObject
        {
            ["_type"] = "translation.metadata",
            ["translations"] = new JArray
            {
                new JObject
                {
                    ["_key"] = baseLanguage,
                    ["value"] = new JObject
                    {
                        ["_type"] = "reference",
                        ["_ref"] = baseDocumentId
                    }
                },
                new JObject
                {
                    ["_key"] = targetLanguage,
                    ["value"] = new JObject
                    {
                        ["_type"] = "reference",
                        ["_ref"] = translatedDocumentId
                    }
                }
            }
        };

        var mutation = new JObject
        {
            ["mutations"] = new JArray
            {
                new JObject
                {
                    ["create"] = metadata
                }
            }
        };

        var request = new RestRequest($"/data/mutate/{datasetId}")
            .AddJsonBody(mutation.ToString());

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
        string targetLanguage, string datasetId)
    {
        var metadataId = existingMetadata["_id"]?.ToString();
        if (string.IsNullOrEmpty(metadataId))
        {
            throw new PluginApplicationException("Translation metadata ID is missing.");
        }

        var translations = existingMetadata["translations"] as JArray ?? new JArray();
        
        var existingTranslation = translations
            .OfType<JObject>()
            .FirstOrDefault(t => t["_key"]?.ToString() == targetLanguage);

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
            translations.Add(new JObject
            {
                ["_key"] = targetLanguage,
                ["value"] = new JObject
                {
                    ["_type"] = "reference",
                    ["_ref"] = translatedDocumentId
                }
            });
        }

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
                            ["translations"] = translations
                        }
                    }
                }
            }
        };

        var request = new RestRequest($"/data/mutate/{datasetId}")
            .AddJsonBody(mutation.ToString());

        try
        {
            await _apiClient.ExecuteWithErrorHandling(request);
        }
        catch (Exception ex)
        {
            throw new PluginApplicationException($"Failed to update translation metadata. Error: {ex.Message}", ex);
        }
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
