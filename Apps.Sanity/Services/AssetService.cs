﻿using Apps.Sanity.Actions;
using Apps.Sanity.Api;
using Apps.Sanity.Models.Responses;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.Sanity.Services;

public class AssetService(InvocationContext invocationContext)
{
    private readonly ApiClient _apiClient = new(invocationContext.AuthenticationCredentialsProviders);
    
    public async Task<string> GetAssetUrlAsync(string datasetId, string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            throw new ArgumentException("Asset ID cannot be null or empty.", nameof(assetId));
        }

        var asset = await GetContentAsync(datasetId, assetId);
        return asset.Url;
    }
    
    private async Task<AssetResponse> GetContentAsync(string datasetId, string assetId)
    {
        var contentActions = new ContentActions(invocationContext, null!);
        var content = await contentActions.SearchContentAsJObjectAsync(new()
        {
            DatasetId = datasetId,
            GroqQuery =  $"_id == \"{assetId}\""
        });

        if (content.Count == 0)
        {
            throw new PluginMisconfigurationException(
                "No content found for the provided ID. Please verify that the ID is correct and try again.");
        }

        var asset = content.First().ToObject<AssetResponse>();
        return asset!;
    }
}