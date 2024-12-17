using Apps.Sanity.Utils;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Utils.RestSharp;
using RestSharp;

namespace Apps.Sanity.Api;

public class ApiClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    : BlackBirdRestClient(new()
    {
        BaseUrl = authenticationCredentialsProviders.BuildUri(),
        ThrowOnAnyError = false
    })
{
    protected override Exception ConfigureErrorException(RestResponse response)
    {
        throw new Exception(response.Content!);
    }
}