using Apps.Sanity.Api;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;
using RestSharp;

namespace Apps.Sanity.Connections;

public class ConnectionValidator : IConnectionValidator
{
    public async ValueTask<ConnectionValidationResponse> ValidateConnection(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        CancellationToken cancellationToken)
    {
        var credentialsProviders = authenticationCredentialsProviders as AuthenticationCredentialsProvider[] ??
                                   authenticationCredentialsProviders.ToArray();

        var apiClient = new ApiClient(credentialsProviders);
        
        const string dataset = "production";
        var request = new ApiRequest($"/data/query/{dataset}?query=*%5B%5D", Method.Get,
            credentialsProviders);

        var response = await apiClient.ExecuteAsync(request, cancellationToken);
        return new()
        {
            IsValid = response.IsSuccessful,
            Message = response.Content
        };
    }
}