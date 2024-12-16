using Apps.Sanity.Constants;
using Apps.Sanity.Webhooks.Models;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Common.Webhooks;
using Blackbird.Applications.Sdk.Utils.Extensions.Sdk;
using RestSharp;

namespace Apps.Sanity.Webhooks.Handlers;

public abstract class BaseWebhookHandler(InvocationContext invocationContext)
    : BaseInvocable(invocationContext), IWebhookEventHandler
{
    protected abstract string Event { get; }

    private const string AppName = "sanity";

    public async Task SubscribeAsync(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var projectId = InvocationContext.AuthenticationCredentialsProviders.Get(CredsNames.ProjectId).Value;
        var payloadUrl = values["payloadUrl"];
        var bridgeClient =
            new RestClient($"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/webhooks/{AppName}");

        var bridgeSubscribeRequest = new RestRequest($"/{projectId}/{Event}", Method.Post)
            .AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken)
            .AddBody(payloadUrl);

        await bridgeClient.ExecuteAsync(bridgeSubscribeRequest);
    }

    public async Task UnsubscribeAsync(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        Dictionary<string, string> values)
    {
        var projectId = InvocationContext.AuthenticationCredentialsProviders.Get(CredsNames.ProjectId).Value;
        var payloadUrl = values["payloadUrl"];
        var bridgeClient =
            new RestClient($"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/webhooks/{AppName}");

        var getWebhooksRequest = new RestRequest($"/{projectId}/{Event}")
            .AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken);

        var webhooks = await bridgeClient.GetAsync<List<BridgeGetResponse>>(getWebhooksRequest);
        var webhook = webhooks!.FirstOrDefault(w => w.Value == payloadUrl);

        if (webhook != null)
        {
            var deleteWebhookRequest = new RestRequest($"/{projectId}/{Event}/{webhook.Id}", Method.Delete)
                .AddHeader("Blackbird-Token", ApplicationConstants.BlackbirdToken);
            await bridgeClient.ExecuteAsync(deleteWebhookRequest);
        }
    }
}