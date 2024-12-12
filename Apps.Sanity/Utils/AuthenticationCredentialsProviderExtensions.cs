using Apps.Sanity.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;

namespace Apps.Sanity.Utils;

public static class AuthenticationCredentialsProviderExtensions
{
    public static Uri BuildUri(this IEnumerable<AuthenticationCredentialsProvider> credentials)
    {
        const string apiVersion = "v2022-03-07";
        
        var projectId = credentials.Get(CredsNames.ProjectId).Value;
        return new($"https://{projectId}.api.sanity.io/{apiVersion}");
    }
}